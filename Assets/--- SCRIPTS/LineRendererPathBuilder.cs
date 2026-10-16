using System.Collections.Generic;
using UnityEngine;

public static class LineRendererPathBuilder
{
    private const float MinSegmentLength = 0.001f;
    private const float ZigzagDetourStep = 0.5f;
    private const float EndpointCastSkip = 0.15f;
    private const float GridCellSize = 0.5f;
    private const float GridBoundsMargin = 2f;
    private const int MaxGridAxisCells = 256;

    private static readonly float[] LaneOffsetMagnitudes = { 0.5f, 1f, 1.5f, 2f, 3f, 4f };

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

    // Builds a path using preset route variants and optional physics obstacle checks (editor generation).
    public static Vector3[] BuildPoints(
        Vector3 start,
        Vector3 end,
        string deviceName,
        int pivotCount,
        int zigzagAmount,
        LineRendererPathBuildOptions options)
    {
        int nameSeed = string.IsNullOrEmpty(deviceName) ? 0 : deviceName.GetHashCode();
        int routeSeed = nameSeed ^ options.routeVariantIndex;

        List<Vector3> route = SelectMainRoute(start, end, options);
        if (zigzagAmount > 0)
            ApplyManhattanDetours(route, zigzagAmount, routeSeed);

        route = RemoveDuplicateConsecutive(route);
        return route.ToArray();
    }

    private static List<Vector3> SelectMainRoute(Vector3 start, Vector3 end, LineRendererPathBuildOptions options)
    {
        List<List<Vector3>> candidates = CollectRouteCandidates(start, end);
        if (candidates.Count == 0)
            return BuildMinimalOrthogonalRoute(start, end, 0);

        var usable = new List<List<Vector3>>();
        if (options.avoidObstacles && options.obstacleLayers.value != 0 && options.castRadius > 0f)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (IsRouteClear(candidates[i], start, end, options.obstacleLayers, options.castRadius))
                    usable.Add(candidates[i]);
            }

            if (usable.Count > 0)
            {
                int pick = ModPositive(options.routeVariantIndex, usable.Count);
                return new List<Vector3>(usable[pick]);
            }

            List<Vector3> gridRoute = BuildGridAvoidanceRoute(start, end, options.obstacleLayers, options.castRadius);
            if (gridRoute != null && gridRoute.Count >= 2)
                return gridRoute;

            Debug.LogWarning(
                "LineRendererPathBuilder: no clear preset route and grid avoidance failed; using first preset.",
                null);
        }

        int variantPick = ModPositive(options.routeVariantIndex, candidates.Count);
        return new List<Vector3>(candidates[variantPick]);
    }

    private static List<List<Vector3>> CollectRouteCandidates(Vector3 start, Vector3 end)
    {
        var candidates = new List<List<Vector3>>();

        for (int orderIndex = 0; orderIndex < AxisStepOrders.Length; orderIndex++)
            AddUniqueCandidate(candidates, BuildMinimalOrthogonalRoute(start, end, orderIndex));

        for (int i = 0; i < LaneOffsetMagnitudes.Length; i++)
        {
            float magnitude = LaneOffsetMagnitudes[i];
            AddLaneOffsetCandidates(candidates, start, end, magnitude);
            AddLaneOffsetCandidates(candidates, start, end, -magnitude);
        }

        AddLongWayCandidates(candidates, start, end);

        candidates.Sort((a, b) => RouteLength(a).CompareTo(RouteLength(b)));
        return candidates;
    }

    private static void AddLongWayCandidates(List<List<Vector3>> candidates, Vector3 start, Vector3 end)
    {
        if (Mathf.Abs(start.x - end.x) > MinSegmentLength)
        {
            float overshoot = Mathf.Sign(end.x - start.x) * Mathf.Max(Mathf.Abs(end.x - start.x), 1f);
            AddUniqueCandidate(candidates, BuildRouteThroughWaypoints(start, end,
                new Vector3(end.x + overshoot, start.y, start.z),
                new Vector3(end.x + overshoot, end.y, end.z)));
        }

        if (Mathf.Abs(start.z - end.z) > MinSegmentLength)
        {
            float overshoot = Mathf.Sign(end.z - start.z) * Mathf.Max(Mathf.Abs(end.z - start.z), 1f);
            AddUniqueCandidate(candidates, BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x, start.y, end.z + overshoot),
                new Vector3(end.x, end.y, end.z + overshoot)));
        }

        if (Mathf.Abs(start.y - end.y) > MinSegmentLength)
        {
            float overshoot = Mathf.Sign(end.y - start.y) * Mathf.Max(Mathf.Abs(end.y - start.y), 1f);
            AddUniqueCandidate(candidates, BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x, end.y + overshoot, start.z),
                new Vector3(end.x, end.y + overshoot, end.z)));
        }
    }

    private static void AddLaneOffsetCandidates(List<List<Vector3>> candidates, Vector3 start, Vector3 end, float offset)
    {
        if (Mathf.Abs(offset) < MinSegmentLength)
            return;

        if (Mathf.Abs(start.z - end.z) > MinSegmentLength || Mathf.Abs(start.x - end.x) > MinSegmentLength)
        {
            AddUniqueCandidate(candidates, BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x, start.y, start.z + offset),
                new Vector3(end.x, start.y, start.z + offset)));

            AddUniqueCandidate(candidates, BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x + offset, start.y, start.z),
                new Vector3(start.x + offset, start.y, end.z)));

            AddUniqueCandidate(candidates, BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x + offset, start.y, start.z + offset),
                new Vector3(end.x, start.y, start.z + offset)));
        }

        if (Mathf.Abs(start.y - end.y) > MinSegmentLength)
        {
            AddUniqueCandidate(candidates, BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x, start.y + offset, start.z),
                new Vector3(end.x, start.y + offset, end.z)));
        }
    }

    private static List<Vector3> BuildRouteThroughWaypoints(Vector3 start, Vector3 end, params Vector3[] waypoints)
    {
        var points = new List<Vector3> { start };
        Vector3 cursor = start;

        for (int i = 0; i < waypoints.Length; i++)
            MoveAxisAlignedToTarget(points, ref cursor, waypoints[i]);

        MoveAxisAlignedToTarget(points, ref cursor, end);
        return RemoveDuplicateConsecutive(points);
    }

    private static void MoveAxisAlignedToTarget(List<Vector3> points, ref Vector3 cursor, Vector3 target)
    {
        ApplyAxisStep(points, ref cursor, target, 0);
        ApplyAxisStep(points, ref cursor, target, 1);
        ApplyAxisStep(points, ref cursor, target, 2);
    }

    private static void AddUniqueCandidate(List<List<Vector3>> candidates, List<Vector3> route)
    {
        if (route == null || route.Count < 2)
            return;

        string key = RouteSignature(route);
        for (int i = 0; i < candidates.Count; i++)
        {
            if (RouteSignature(candidates[i]) == key)
                return;
        }

        candidates.Add(route);
    }

    private static string RouteSignature(List<Vector3> route)
    {
        var parts = new string[route.Count];
        for (int i = 0; i < route.Count; i++)
        {
            Vector3 p = route[i];
            parts[i] = $"{Mathf.Round(p.x * 100f)}_{Mathf.Round(p.y * 100f)}_{Mathf.Round(p.z * 100f)}";
        }

        return string.Join("|", parts);
    }

    private static float RouteLength(List<Vector3> route)
    {
        float length = 0f;
        for (int i = 0; i < route.Count - 1; i++)
            length += Vector3.Distance(route[i], route[i + 1]);
        return length;
    }

    private static bool IsRouteClear(
        List<Vector3> route,
        Vector3 start,
        Vector3 end,
        LayerMask layers,
        float radius)
    {
        for (int i = 0; i < route.Count - 1; i++)
        {
            if (!IsSegmentClear(route[i], route[i + 1], start, end, layers, radius))
                return false;
        }

        return true;
    }

    private static bool IsSegmentClear(
        Vector3 a,
        Vector3 b,
        Vector3 pathStart,
        Vector3 pathEnd,
        LayerMask layers,
        float radius)
    {
        Vector3 delta = b - a;
        float distance = delta.magnitude;
        if (distance <= MinSegmentLength)
            return true;

        Vector3 direction = delta / distance;
        float checkStart = Mathf.Min(EndpointCastSkip, distance * 0.25f);
        float checkEnd = distance - Mathf.Min(EndpointCastSkip, distance * 0.25f);
        if (checkEnd <= checkStart)
            return true;

        Vector3 origin = a + direction * checkStart;
        float checkDistance = checkEnd - checkStart;

        if (Physics.SphereCast(origin, radius, direction, out RaycastHit hit, checkDistance, layers, QueryTriggerInteraction.Ignore))
        {
            if (IsNearEndpoint(hit.point, pathStart, pathEnd, radius * 2f))
                return true;
            return false;
        }

        return true;
    }

    private static bool IsNearEndpoint(Vector3 point, Vector3 pathStart, Vector3 pathEnd, float threshold)
    {
        return (point - pathStart).sqrMagnitude <= threshold * threshold
            || (point - pathEnd).sqrMagnitude <= threshold * threshold;
    }

    private static List<Vector3> BuildGridAvoidanceRoute(
        Vector3 start,
        Vector3 end,
        LayerMask layers,
        float radius)
    {
        float minX = Mathf.Min(start.x, end.x) - GridBoundsMargin;
        float maxX = Mathf.Max(start.x, end.x) + GridBoundsMargin;
        float minZ = Mathf.Min(start.z, end.z) - GridBoundsMargin;
        float maxZ = Mathf.Max(start.z, end.z) + GridBoundsMargin;

        int width = Mathf.CeilToInt((maxX - minX) / GridCellSize) + 1;
        int height = Mathf.CeilToInt((maxZ - minZ) / GridCellSize) + 1;
        if (width > MaxGridAxisCells || height > MaxGridAxisCells)
            return null;

        bool[,] blocked = BuildBlockedGrid(minX, minZ, width, height, start, end, layers, radius);
        Vector2Int startCell = WorldToCell(start, minX, minZ);
        Vector2Int endCell = WorldToCell(end, minX, minZ);

        if (!IsCellInGrid(startCell, width, height) || !IsCellInGrid(endCell, width, height))
            return null;

        blocked[startCell.x, startCell.y] = false;
        blocked[endCell.x, endCell.y] = false;

        List<Vector2Int> cellPath = FindGridPath(blocked, width, height, startCell, endCell);
        if (cellPath == null || cellPath.Count < 2)
            return null;

        return SimplifyGridPathToOrthogonalRoute(start, end, cellPath, minX, minZ);
    }

    private static bool[,] BuildBlockedGrid(
        float minX,
        float minZ,
        int width,
        int height,
        Vector3 start,
        Vector3 end,
        LayerMask layers,
        float radius)
    {
        var blocked = new bool[width, height];
        float midY = (start.y + end.y) * 0.5f;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Vector3 center = new Vector3(minX + x * GridCellSize, midY, minZ + z * GridCellSize);
                if (Physics.CheckSphere(center, radius, layers, QueryTriggerInteraction.Ignore))
                {
                    if (!IsNearEndpoint(center, start, end, radius * 3f))
                        blocked[x, z] = true;
                }
            }
        }

        return blocked;
    }

    private static Vector2Int WorldToCell(Vector3 world, float minX, float minZ)
    {
        int x = Mathf.RoundToInt((world.x - minX) / GridCellSize);
        int z = Mathf.RoundToInt((world.z - minZ) / GridCellSize);
        return new Vector2Int(x, z);
    }

    private static bool IsCellInGrid(Vector2Int cell, int width, int height)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    private static List<Vector2Int> FindGridPath(bool[,] blocked, int width, int height, Vector2Int startCell, Vector2Int endCell)
    {
        var open = new List<Vector2Int> { startCell };
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var gScore = new Dictionary<Vector2Int, float>();
        gScore[startCell] = 0f;

        while (open.Count > 0)
        {
            open.Sort((a, b) =>
            {
                float aScore = gScore[a] + Heuristic(a, endCell);
                float bScore = gScore[b] + Heuristic(b, endCell);
                return aScore.CompareTo(bScore);
            });

            Vector2Int current = open[0];
            open.RemoveAt(0);

            if (current == endCell)
                return ReconstructPath(cameFrom, current);

            TryVisitNeighbor(current, new Vector2Int(current.x + 1, current.y), blocked, width, height, endCell, open, cameFrom, gScore);
            TryVisitNeighbor(current, new Vector2Int(current.x - 1, current.y), blocked, width, height, endCell, open, cameFrom, gScore);
            TryVisitNeighbor(current, new Vector2Int(current.x, current.y + 1), blocked, width, height, endCell, open, cameFrom, gScore);
            TryVisitNeighbor(current, new Vector2Int(current.x, current.y - 1), blocked, width, height, endCell, open, cameFrom, gScore);
        }

        return null;
    }

    private static void TryVisitNeighbor(
        Vector2Int current,
        Vector2Int neighbor,
        bool[,] blocked,
        int width,
        int height,
        Vector2Int endCell,
        List<Vector2Int> open,
        Dictionary<Vector2Int, Vector2Int> cameFrom,
        Dictionary<Vector2Int, float> gScore)
    {
        if (!IsCellInGrid(neighbor, width, height) || blocked[neighbor.x, neighbor.y])
            return;

        float tentative = gScore[current] + 1f;
        if (!gScore.TryGetValue(neighbor, out float existing) || tentative < existing)
        {
            cameFrom[neighbor] = current;
            gScore[neighbor] = tentative;
            if (!open.Contains(neighbor))
                open.Add(neighbor);
        }
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int> { current };
        while (cameFrom.TryGetValue(current, out Vector2Int previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static List<Vector3> SimplifyGridPathToOrthogonalRoute(
        Vector3 start,
        Vector3 end,
        List<Vector2Int> cellPath,
        float minX,
        float minZ)
    {
        var world = new List<Vector3> { start };
        float y = (start.y + end.y) * 0.5f;

        for (int i = 1; i < cellPath.Count; i++)
        {
            Vector2Int cell = cellPath[i];
            world.Add(new Vector3(minX + cell.x * GridCellSize, y, minZ + cell.y * GridCellSize));
        }

        world.Add(end);

        var simplified = new List<Vector3> { start };
        Vector3 cursor = start;
        for (int i = 1; i < world.Count; i++)
            MoveAxisAlignedToTarget(simplified, ref cursor, world[i]);

        return RemoveDuplicateConsecutive(simplified);
    }

    private static int ModPositive(int value, int count)
    {
        if (count <= 0)
            return 0;
        int mod = value % count;
        return mod < 0 ? mod + count : mod;
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
