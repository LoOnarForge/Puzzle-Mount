using System.Collections.Generic;
using UnityEngine;

public static class LineRendererPathBuilder
{
    private const float MinSegmentLength = 0.001f;
    private const float ZigzagDetourStep = 0.5f;

    // Corner points only — no points split along straight legs.
    // pivotCount is unused (kept for inspector compatibility).
    // zigzagAmount: 0 = shortest 90° route; higher = extra 90° detour steps (longer path).
    public static Vector3[] BuildPoints(
        Vector3 start,
        Vector3 end,
        string deviceName,
        int pivotCount,
        int zigzagAmount,
        int variationSeed = 0)
    {
        int nameSeed = string.IsNullOrEmpty(deviceName) ? 0 : deviceName.GetHashCode();
        int routeSeed = nameSeed ^ variationSeed;

        List<Vector3> route = BuildMinimalOrthogonalRoute(start, end, routeSeed);
        if (zigzagAmount > 0)
            ApplyManhattanDetours(route, zigzagAmount, routeSeed);

        route = RemoveDuplicateConsecutive(route);
        return route.ToArray();
    }

    private static readonly int[][] AxisStepOrders =
    {
        new[] { 0, 1, 2 },
        new[] { 0, 2, 1 },
        new[] { 1, 0, 2 },
        new[] { 1, 2, 0 },
        new[] { 2, 0, 1 },
        new[] { 2, 1, 0 }
    };

    private static List<Vector3> BuildMinimalOrthogonalRoute(Vector3 start, Vector3 end, int routeSeed)
    {
        var points = new List<Vector3> { start };
        Vector3 cursor = start;

        int orderIndex = Mathf.Abs(routeSeed) % AxisStepOrders.Length;
        int[] axisOrder = AxisStepOrders[orderIndex];

        for (int i = 0; i < axisOrder.Length; i++)
            ApplyAxisStep(points, ref cursor, end, axisOrder[i]);

        TryAddCorner(points, end);
        return points;
    }

    private static void ApplyAxisStep(List<Vector3> points, ref Vector3 cursor, Vector3 end, int axis)
    {
        Vector3 next = cursor;
        switch (axis)
        {
            case 0:
                next.x = end.x;
                break;
            case 1:
                next.y = end.y;
                break;
            default:
                next.z = end.z;
                break;
        }

        TryAddCorner(points, next);
        cursor = next;
    }

    private static void ApplyManhattanDetours(List<Vector3> route, int detourCount, int routeSeed)
    {
        for (int detourIndex = 0; detourIndex < detourCount; detourIndex++)
        {
            if (route.Count < 2)
                return;

            int legIndex = PickLongestLegIndex(route, routeSeed + detourIndex);
            if (legIndex < 0)
                return;

            InsertAxisAlignedDetour(route, legIndex, ZigzagDetourStep, routeSeed + detourIndex);
        }
    }

    private static void InsertAxisAlignedDetour(List<Vector3> route, int legIndex, float step, int routeSeed)
    {
        Vector3 legStart = route[legIndex];
        Vector3 legEnd = route[legIndex + 1];
        if (!IsAxisAligned(legStart, legEnd))
            return;

        Vector3 delta = legEnd - legStart;
        float legLength = delta.magnitude;
        if (legLength < step * 4f)
            return;

        Vector3 direction = delta / legLength;
        Vector3 perpendicular = GetPerpendicularAxisOffset(legStart, legEnd, routeSeed);
        if (perpendicular.sqrMagnitude <= MinSegmentLength * MinSegmentLength)
            return;

        perpendicular.Normalize();
        Vector3 sideStep = perpendicular * step;

        float along0 = legLength * 0.3f;
        float along1 = legLength * 0.7f;

        Vector3 p0 = legStart + direction * along0;
        Vector3 p1 = p0 + sideStep;
        Vector3 p2 = legStart + direction * along1 + sideStep;
        Vector3 p3 = legStart + direction * along1;

        route.RemoveAt(legIndex + 1);
        route.Insert(legIndex + 1, p0);
        route.Insert(legIndex + 2, p1);
        route.Insert(legIndex + 3, p2);
        route.Insert(legIndex + 4, p3);
        route.Insert(legIndex + 5, legEnd);
    }

    private static int PickLongestLegIndex(List<Vector3> route, int routeSeed)
    {
        int bestIndex = -1;
        float bestLength = MinSegmentLength;
        int startLeg = 0;
        int legCount = route.Count - 1;
        if (legCount <= 0)
            return -1;

        startLeg = Mathf.Abs(routeSeed) % legCount;

        for (int i = 0; i < legCount; i++)
        {
            int legIndex = (startLeg + i) % legCount;
            float length = Vector3.Distance(route[legIndex], route[legIndex + 1]);
            if (length > bestLength)
            {
                bestLength = length;
                bestIndex = legIndex;
            }
        }

        return bestIndex;
    }

    private static bool IsAxisAligned(Vector3 a, Vector3 b)
    {
        int axesChanged = 0;
        if (Mathf.Abs(a.x - b.x) > MinSegmentLength)
            axesChanged++;
        if (Mathf.Abs(a.y - b.y) > MinSegmentLength)
            axesChanged++;
        if (Mathf.Abs(a.z - b.z) > MinSegmentLength)
            axesChanged++;

        return axesChanged == 1;
    }

    private static Vector3 GetPerpendicularAxisOffset(Vector3 legStart, Vector3 legEnd, int routeSeed)
    {
        Vector3 delta = legEnd - legStart;
        var choices = new List<Vector3>(4);

        if (Mathf.Abs(delta.x) > MinSegmentLength)
        {
            choices.Add(Vector3.up);
            choices.Add(Vector3.down);
            choices.Add(Vector3.forward);
            choices.Add(Vector3.back);
        }
        else if (Mathf.Abs(delta.y) > MinSegmentLength)
        {
            choices.Add(Vector3.right);
            choices.Add(Vector3.left);
            choices.Add(Vector3.forward);
            choices.Add(Vector3.back);
        }
        else if (Mathf.Abs(delta.z) > MinSegmentLength)
        {
            choices.Add(Vector3.right);
            choices.Add(Vector3.left);
            choices.Add(Vector3.up);
            choices.Add(Vector3.down);
        }

        if (choices.Count == 0)
            return Vector3.zero;

        int pick = Mathf.Abs(routeSeed) % choices.Count;
        return choices[pick];
    }

    private static void TryAddCorner(List<Vector3> points, Vector3 corner)
    {
        if (points.Count == 0)
        {
            points.Add(corner);
            return;
        }

        Vector3 last = points[points.Count - 1];
        if ((corner - last).sqrMagnitude > MinSegmentLength * MinSegmentLength)
            points.Add(corner);
    }

    private static List<Vector3> RemoveDuplicateConsecutive(List<Vector3> points)
    {
        if (points.Count <= 1)
            return points;

        var cleaned = new List<Vector3> { points[0] };
        for (int i = 1; i < points.Count; i++)
        {
            if ((points[i] - cleaned[cleaned.Count - 1]).sqrMagnitude > MinSegmentLength * MinSegmentLength)
                cleaned.Add(points[i]);
        }

        return cleaned;
    }
}
