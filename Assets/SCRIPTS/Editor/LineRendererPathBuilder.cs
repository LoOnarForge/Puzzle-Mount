using System.Collections.Generic;
using UnityEngine;

public static class LineRendererPathBuilder
{
    private const float MinSegmentLength = 0.001f;
    private const int MinResampledPointCount = 2;
    private const float VerticalRouteClearance = 0.25f;
    private const float ZigzagMetersPerStep = 0.12f;

    // Builds world-space LineRenderer points: orthogonal route, even resampling, then zigzag offsets.
    public static Vector3[] BuildPoints(
        Vector3 start,
        Vector3 end,
        string deviceName,
        int pivotCount,
        int zigzagAmount)
    {
        int routeSeed = string.IsNullOrEmpty(deviceName) ? 0 : deviceName.GetHashCode();
        List<Vector3> route = BuildOrthogonalRoute(start, end, VerticalRouteClearance, routeSeed);
        route = RemoveDuplicateConsecutive(route);

        int targetCount = Mathf.Max(MinResampledPointCount, pivotCount + MinResampledPointCount);
        if (route.Count >= targetCount)
            targetCount = route.Count;

        List<Vector3> resampled = ResamplePolylineByArcLength(route, targetCount);
        ApplyZigzag(resampled, zigzagAmount, ZigzagMetersPerStep, routeSeed);

        return resampled.ToArray();
    }

    private static List<Vector3> BuildOrthogonalRoute(Vector3 start, Vector3 end, float verticalClearance, int routeSeed)
    {
        float routeY = Mathf.Max(start.y, end.y) + verticalClearance;
        var points = new List<Vector3> { start };

        if (Mathf.Abs(start.y - routeY) > MinSegmentLength)
            points.Add(new Vector3(start.x, routeY, start.z));

        bool moveXFirst = (routeSeed & 1) == 0;
        Vector3 beforeEndDrop = new Vector3(end.x, routeY, end.z);

        if (moveXFirst)
        {
            TryAddCorner(points, new Vector3(end.x, routeY, start.z));
            TryAddCorner(points, beforeEndDrop);
        }
        else
        {
            TryAddCorner(points, new Vector3(start.x, routeY, end.z));
            TryAddCorner(points, beforeEndDrop);
        }

        if (Mathf.Abs(end.y - routeY) > MinSegmentLength)
            points.Add(new Vector3(end.x, end.y, end.z));

        TryAddCorner(points, end);
        return points;
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

    private static List<Vector3> ResamplePolylineByArcLength(List<Vector3> polyline, int pointCount)
    {
        if (polyline.Count == 0)
            return new List<Vector3>();

        if (pointCount <= 1)
            return new List<Vector3> { polyline[0] };

        float totalLength = 0f;
        for (int i = 1; i < polyline.Count; i++)
            totalLength += Vector3.Distance(polyline[i - 1], polyline[i]);

        if (totalLength <= MinSegmentLength)
            return new List<Vector3> { polyline[0], polyline[polyline.Count - 1] };

        var result = new List<Vector3>(pointCount);
        float step = totalLength / (pointCount - 1);
        int segmentIndex = 0;
        float segmentStartDistance = 0f;

        for (int sampleIndex = 0; sampleIndex < pointCount; sampleIndex++)
        {
            float targetDistance = step * sampleIndex;
            while (segmentIndex < polyline.Count - 2)
            {
                float segmentLength = Vector3.Distance(polyline[segmentIndex], polyline[segmentIndex + 1]);
                if (segmentStartDistance + segmentLength >= targetDistance)
                    break;

                segmentStartDistance += segmentLength;
                segmentIndex++;
            }

            Vector3 a = polyline[segmentIndex];
            Vector3 b = polyline[Mathf.Min(segmentIndex + 1, polyline.Count - 1)];
            float segLen = Vector3.Distance(a, b);
            float t = segLen <= MinSegmentLength ? 0f : (targetDistance - segmentStartDistance) / segLen;
            result.Add(Vector3.Lerp(a, b, Mathf.Clamp01(t)));
        }

        return result;
    }

    private static void ApplyZigzag(List<Vector3> points, int zigzagAmount, float zigzagMetersPerStep, int routeSeed)
    {
        if (zigzagAmount <= 0 || points.Count < 3)
            return;

        float amplitude = zigzagAmount * zigzagMetersPerStep;
        var random = new System.Random(routeSeed);

        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 tangent = points[i + 1] - points[i - 1];
            if (tangent.sqrMagnitude <= MinSegmentLength * MinSegmentLength)
                continue;

            tangent.Normalize();
            Vector3 offsetDirection = GetZigzagDirection(tangent, random);
            float sign = (i % 2 == 0) ? 1f : -1f;
            points[i] += offsetDirection * (amplitude * sign);
        }
    }

    private static Vector3 GetZigzagDirection(Vector3 tangent, System.Random random)
    {
        if (Mathf.Abs(tangent.y) > 0.9f)
        {
            float pick = (float)random.NextDouble();
            return pick < 0.5f ? Vector3.right : Vector3.forward;
        }

        Vector3 horizontal = new Vector3(tangent.x, 0f, tangent.z);
        if (horizontal.sqrMagnitude <= MinSegmentLength * MinSegmentLength)
            return Vector3.right;

        horizontal.Normalize();
        return new Vector3(-horizontal.z, 0f, horizontal.x);
    }
}
