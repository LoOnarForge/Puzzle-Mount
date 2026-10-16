using System.Collections.Generic;
using UnityEngine;

public static class LineRendererPathBuilder
{
    private const float MinSegmentLength = 0.001f;
    private const float ZigzagDetourStepBase = 1f;
    private const float VoxelCellSize = 0.5f;
    private const int MaxVoxelAxisCells = 200;
    private const float ReferencePathDeviationWeight = 0.35f;
    private const float VerticalStepExtraCost = 8f;
    private const int MaxVoxelPathSearchSteps = 120000;
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

    public const int DefaultCollisionFreeAttempts = 6;

    // Builds a path using preset route variants and optional physics obstacle checks (editor generation).
    public static Vector3[] BuildPoints(
        Vector3 start,
        Vector3 end,
        string deviceName,
        int pivotCount,
        int zigzagAmount,
        LineRendererPathBuildOptions options)
    {
        Vector3[] built = BuildPointsInternal(start, end, deviceName, pivotCount, zigzagAmount, options);
        return built ?? System.Array.Empty<Vector3>();
    }

    // Tries up to maxAttempts route variants; returns false if every attempt still overlaps obstacles.
    public static bool TryBuildCollisionFreePoints(
        Vector3 start,
        Vector3 end,
        string deviceName,
        int pivotCount,
        int zigzagAmount,
        LineRendererPathBuildOptions options,
        int maxAttempts,
        out Vector3[] points,
        out int winningRouteVariantIndex)
    {
        points = null;
        winningRouteVariantIndex = options.routeVariantIndex;
        int attemptCount = Mathf.Max(1, maxAttempts);

        if (!options.avoidObstacles || options.obstacleLayers.value == 0)
        {
            points = BuildPoints(start, end, deviceName, pivotCount, zigzagAmount, options);
            return points.Length >= 2;
        }

        int variantSeed = options.routeVariantIndex;
        LineRendererPathObstacleCollision.ClearVoxelCache();

        for (int attempt = 0; attempt < attemptCount; attempt++)
        {
            float gridMargin = 4f + attempt * 2f;
            List<Vector3> referenceRoute = BuildMinimalOrthogonalRoute(start, end, variantSeed + attempt);
            List<List<Vector3>> gridRoutes = BuildVoxelClearRouteVariants(
                start,
                end,
                referenceRoute,
                options.obstacleLayers,
                gridMargin,
                variantSeed + attempt,
                12);

            if (gridRoutes.Count == 0)
                continue;

            int pick = ModPositive(variantSeed, gridRoutes.Count);
            List<Vector3> gridRoute = gridRoutes[pick];
            if (TryFinalizeClearRoute(
                    gridRoute,
                    deviceName,
                    zigzagAmount,
                    variantSeed,
                    options.obstacleLayers,
                    start,
                    end,
                    out Vector3[] gridPoints))
            {
                points = gridPoints;
                winningRouteVariantIndex = variantSeed;
                return true;
            }

            for (int alt = 0; alt < gridRoutes.Count; alt++)
            {
                if (alt == pick)
                    continue;

                if (TryFinalizeClearRoute(
                        gridRoutes[alt],
                        deviceName,
                        zigzagAmount,
                        variantSeed,
                        options.obstacleLayers,
                        start,
                        end,
                        out gridPoints))
                {
                    points = gridPoints;
                    winningRouteVariantIndex = variantSeed;
                    return true;
                }
            }
        }

        return false;
    }

    private static Vector3[] BuildPointsInternal(
        Vector3 start,
        Vector3 end,
        string deviceName,
        int pivotCount,
        int zigzagAmount,
        LineRendererPathBuildOptions options)
    {
        List<Vector3> route = SelectMainRoute(start, end, options);
        if (route == null || route.Count < 2)
            return null;

        return FinalizeRouteWithZigzag(route, deviceName, zigzagAmount, options.routeVariantIndex);
    }

    private static Vector3[] FinalizeRouteWithZigzag(
        List<Vector3> route,
        string deviceName,
        int zigzagAmount,
        int routeVariantIndex)
    {
        int nameSeed = string.IsNullOrEmpty(deviceName) ? 0 : deviceName.GetHashCode();
        int routeSeed = nameSeed ^ routeVariantIndex;
        var working = SimplifyCollinearRoute(RemoveDuplicateConsecutive(route));

        if (zigzagAmount > 0)
            ApplyManhattanDetours(working, zigzagAmount, routeSeed);

        return RemoveDuplicateConsecutive(working).ToArray();
    }

    private static bool TryFinalizeClearRoute(
        List<Vector3> route,
        string deviceName,
        int zigzagAmount,
        int routeVariantIndex,
        LayerMask layers,
        Vector3 start,
        Vector3 end,
        out Vector3[] clearPoints)
    {
        clearPoints = null;
        if (route == null || route.Count < 2)
            return false;

        int nameSeed = string.IsNullOrEmpty(deviceName) ? 0 : deviceName.GetHashCode();
        int routeSeed = nameSeed ^ routeVariantIndex;
        var working = SimplifyCollinearRoute(RemoveDuplicateConsecutive(route));

        if (LineRendererPathObstacleCollision.RouteOverlapsObstacles(working, layers, start, end))
            return false;

        if (zigzagAmount > 0)
            ApplyClearManhattanDetours(working, zigzagAmount, routeSeed, layers, start, end);

        clearPoints = working.ToArray();
        return clearPoints.Length >= 2;
    }

    private static void ApplyClearManhattanDetours(
        List<Vector3> route,
        int detourCount,
        int routeSeed,
        LayerMask layers,
        Vector3 start,
        Vector3 end)
    {
        for (int detourIndex = 0; detourIndex < detourCount; detourIndex++)
        {
            if (route.Count < 2)
                return;

            int legIndex = PickLongestLegIndex(route, routeSeed + detourIndex);
            if (legIndex < 0)
                return;

            float step = ZigzagDetourStepBase + detourIndex * 0.75f;
            if (!TryInsertClearDetour(route, legIndex, step, routeSeed + detourIndex, layers, start, end))
                TryInsertClearDetour(route, legIndex, step * 0.5f, routeSeed + detourIndex + 17, layers, start, end);
        }
    }

    private static bool TryInsertClearDetour(
        List<Vector3> route,
        int legIndex,
        float step,
        int routeSeed,
        LayerMask layers,
        Vector3 start,
        Vector3 end)
    {
        var before = new List<Vector3>(route);
        if (!TryInsertAxisAlignedDetour(route, legIndex, step, routeSeed))
            return false;

        if (LineRendererPathObstacleCollision.RouteOverlapsObstacles(route, layers, start, end))
        {
            route.Clear();
            route.AddRange(before);
            return false;
        }

        return true;
    }

    private static List<Vector3> SelectMainRoute(Vector3 start, Vector3 end, LineRendererPathBuildOptions options)
    {
        List<List<Vector3>> candidates = CollectRouteCandidates(start, end);
        if (candidates.Count == 0)
            return BuildMinimalOrthogonalRoute(start, end, 0);

        if (options.avoidObstacles && options.obstacleLayers.value != 0)
            return SelectMainRouteWithObstacleAvoidance(start, end, candidates, options);

        int variantPick = ModPositive(options.routeVariantIndex, candidates.Count);
        return new List<Vector3>(candidates[variantPick]);
    }

    private static List<Vector3> SelectMainRouteWithObstacleAvoidance(
        Vector3 start,
        Vector3 end,
        List<List<Vector3>> candidates,
        LineRendererPathBuildOptions options)
    {
        int candidateCount = candidates.Count;
        int variantPick = ModPositive(options.routeVariantIndex, candidateCount);
        List<Vector3> baseRoute = new List<Vector3>(candidates[variantPick]);
        LayerMask layers = options.obstacleLayers;

        LineRendererPathObstacleCollision.ClearVoxelCache();

        if (!LineRendererPathObstacleCollision.RouteOverlapsObstacles(baseRoute, layers, start, end))
            return baseRoute;

        List<Vector3> clearRoute = FindFirstClearRoute(
            EnumerateAvoidanceRouteSearch(start, end, candidates, options.routeVariantIndex, layers, 4f),
            layers,
            start,
            end);

        return clearRoute;
    }

    private static List<Vector3> FindFirstClearRoute(
        IEnumerable<List<Vector3>> routes,
        LayerMask layers,
        Vector3 start,
        Vector3 end)
    {
        foreach (List<Vector3> route in routes)
        {
            if (route == null || route.Count < 2)
                continue;

            if (!LineRendererPathObstacleCollision.RouteOverlapsObstacles(route, layers, start, end))
                return new List<Vector3>(route);
        }

        return null;
    }

    private static IEnumerable<List<Vector3>> EnumerateAvoidanceRouteSearch(
        Vector3 start,
        Vector3 end,
        List<List<Vector3>> candidates,
        int routeVariantIndex,
        LayerMask obstacleLayers,
        float gridMargin)
    {
        int candidateCount = candidates.Count;
        int variantPick = ModPositive(routeVariantIndex, candidateCount);
        List<Vector3> referenceRoute = new List<Vector3>(candidates[variantPick]);
        List<List<Vector3>> gridRoutes = BuildVoxelClearRouteVariants(
            start,
            end,
            referenceRoute,
            obstacleLayers,
            gridMargin,
            routeVariantIndex,
            12);
        for (int g = 0; g < gridRoutes.Count; g++)
            yield return gridRoutes[g];

        int startIndex = ModPositive(routeVariantIndex, candidateCount);
        for (int i = 0; i < candidateCount; i++)
            yield return candidates[(startIndex + i) % candidateCount];

        for (float offset = 0.5f; offset <= 10f; offset += 0.5f)
        {
            yield return BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x, start.y, start.z + offset),
                new Vector3(end.x, start.y, end.z + offset));

            yield return BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x, start.y, start.z - offset),
                new Vector3(end.x, start.y, end.z - offset));

            yield return BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x + offset, start.y, start.z),
                new Vector3(end.x + offset, start.y, end.z));

            yield return BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x - offset, start.y, start.z),
                new Vector3(end.x - offset, start.y, end.z));

            yield return BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x + offset, start.y, start.z + offset),
                new Vector3(end.x, start.y, start.z + offset));

            yield return BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x + offset, start.y, start.z - offset),
                new Vector3(end.x, start.y, end.z - offset));
        }

        if (Mathf.Abs(start.x - end.x) > MinSegmentLength)
        {
            float overshoot = Mathf.Sign(end.x - start.x) * Mathf.Max(Mathf.Abs(end.x - start.x), 2f);
            yield return BuildRouteThroughWaypoints(start, end,
                new Vector3(end.x + overshoot, start.y, start.z),
                new Vector3(end.x + overshoot, start.y, end.z));
        }

        if (Mathf.Abs(start.z - end.z) > MinSegmentLength)
        {
            float overshoot = Mathf.Sign(end.z - start.z) * Mathf.Max(Mathf.Abs(end.z - start.z), 2f);
            yield return BuildRouteThroughWaypoints(start, end,
                new Vector3(start.x, start.y, end.z + overshoot),
                new Vector3(end.x, start.y, end.z + overshoot));
        }
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

    private static List<List<Vector3>> BuildVoxelClearRouteVariants(
        Vector3 start,
        Vector3 end,
        List<Vector3> referenceRoute,
        LayerMask obstacleLayers,
        float boundsMargin,
        int pathVariantSeed,
        int maxPaths)
    {
        var results = new List<List<Vector3>>();
        if (obstacleLayers.value == 0 || maxPaths <= 0 || referenceRoute == null || referenceRoute.Count < 2)
            return results;

        if (!TryBuildVoxelSearchBounds(start, end, referenceRoute, boundsMargin, out Vector3Int minCell, out Vector3Int maxCell))
            return results;

        Vector3Int startCell = WorldToVoxel(start);
        Vector3Int endCell = WorldToVoxel(end);
        if (!IsVoxelInBounds(startCell, minCell, maxCell) || !IsVoxelInBounds(endCell, minCell, maxCell))
            return results;

        HashSet<Vector3Int> referenceVoxels = CollectReferenceVoxels(referenceRoute);
        HashSet<Vector3Int> staticBlocked = PrecomputeBlockedVoxels(minCell, maxCell, obstacleLayers, start, end);
        var extraBlocked = new HashSet<Vector3Int>();

        for (int pathIndex = 0; pathIndex < maxPaths; pathIndex++)
        {
            int neighborBias = ModPositive(pathVariantSeed + pathIndex, VoxelNeighborOffsets.Length);
            List<Vector3Int> cellPath = FindNearestClearVoxelPath(
                startCell,
                endCell,
                minCell,
                maxCell,
                referenceVoxels,
                staticBlocked,
                start,
                end,
                extraBlocked,
                neighborBias);
            if (cellPath == null || cellPath.Count < 2)
                break;

            List<Vector3> route = VoxelPathToOrthogonalRoute(start, end, cellPath);
            if (route != null && route.Count >= 2)
                AddUniqueRoute(results, route);

            if (!TryBlockInteriorVoxelForNextPath(cellPath, extraBlocked))
                break;
        }

        return results;
    }

    private static void AddUniqueRoute(List<List<Vector3>> routes, List<Vector3> route)
    {
        string key = RouteSignature(route);
        for (int i = 0; i < routes.Count; i++)
        {
            if (RouteSignature(routes[i]) == key)
                return;
        }

        routes.Add(route);
    }

    private static bool TryBlockInteriorVoxelForNextPath(List<Vector3Int> cellPath, HashSet<Vector3Int> extraBlocked)
    {
        if (cellPath.Count <= 3)
            return false;

        int mid = cellPath.Count / 2;
        Vector3Int cell = cellPath[mid];
        if (extraBlocked.Contains(cell))
            return false;

        extraBlocked.Add(cell);
        return true;
    }

    private static Vector3Int WorldToVoxel(Vector3 world)
    {
        return new Vector3Int(
            Mathf.RoundToInt(world.x / VoxelCellSize),
            Mathf.RoundToInt(world.y / VoxelCellSize),
            Mathf.RoundToInt(world.z / VoxelCellSize));
    }

    private static Vector3 VoxelToWorld(Vector3Int cell)
    {
        return new Vector3(
            cell.x * VoxelCellSize,
            cell.y * VoxelCellSize,
            cell.z * VoxelCellSize);
    }

    private static bool IsVoxelInBounds(Vector3Int cell, Vector3Int minCell, Vector3Int maxCell)
    {
        return cell.x >= minCell.x && cell.x <= maxCell.x
            && cell.y >= minCell.y && cell.y <= maxCell.y
            && cell.z >= minCell.z && cell.z <= maxCell.z;
    }

    private static bool TryBuildVoxelSearchBounds(
        Vector3 start,
        Vector3 end,
        List<Vector3> referenceRoute,
        float boundsMargin,
        out Vector3Int minCell,
        out Vector3Int maxCell)
    {
        minCell = WorldToVoxel(start);
        maxCell = minCell;

        EncapsulateVoxel(ref minCell, ref maxCell, WorldToVoxel(end));
        for (int i = 0; i < referenceRoute.Count; i++)
            EncapsulateVoxel(ref minCell, ref maxCell, WorldToVoxel(referenceRoute[i]));

        int margin = Mathf.Max(1, Mathf.CeilToInt(boundsMargin / VoxelCellSize));
        minCell -= new Vector3Int(margin, margin, margin);
        maxCell += new Vector3Int(margin, margin, margin);

        int sizeX = maxCell.x - minCell.x + 1;
        int sizeY = maxCell.y - minCell.y + 1;
        int sizeZ = maxCell.z - minCell.z + 1;
        if (sizeX > MaxVoxelAxisCells || sizeY > MaxVoxelAxisCells || sizeZ > MaxVoxelAxisCells)
        {
            minCell = default;
            maxCell = default;
            return false;
        }

        return true;
    }

    private static void EncapsulateVoxel(ref Vector3Int minCell, ref Vector3Int maxCell, Vector3Int cell)
    {
        minCell = new Vector3Int(
            Mathf.Min(minCell.x, cell.x),
            Mathf.Min(minCell.y, cell.y),
            Mathf.Min(minCell.z, cell.z));
        maxCell = new Vector3Int(
            Mathf.Max(maxCell.x, cell.x),
            Mathf.Max(maxCell.y, cell.y),
            Mathf.Max(maxCell.z, cell.z));
    }

    private static HashSet<Vector3Int> CollectReferenceVoxels(List<Vector3> referenceRoute)
    {
        var set = new HashSet<Vector3Int>();
        for (int leg = 0; leg < referenceRoute.Count - 1; leg++)
        {
            Vector3 a = referenceRoute[leg];
            Vector3 b = referenceRoute[leg + 1];
            float length = Vector3.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / VoxelCellSize));
            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                set.Add(WorldToVoxel(Vector3.Lerp(a, b, t)));
            }
        }

        return set;
    }

    private static HashSet<Vector3Int> PrecomputeBlockedVoxels(
        Vector3Int minCell,
        Vector3Int maxCell,
        LayerMask obstacleLayers,
        Vector3 linkStart,
        Vector3 linkEnd)
    {
        var blocked = new HashSet<Vector3Int>();
        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int z = minCell.z; z <= maxCell.z; z++)
                {
                    var cell = new Vector3Int(x, y, z);
                    if (LineRendererPathObstacleCollision.IsVoxelBlockedForPath(
                            VoxelToWorld(cell),
                            obstacleLayers,
                            linkStart,
                            linkEnd))
                        blocked.Add(cell);
                }
            }
        }

        return blocked;
    }

    private static readonly Vector3Int[] VoxelNeighborOffsets =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 0, -1)
    };

    private static List<Vector3Int> FindNearestClearVoxelPath(
        Vector3Int startCell,
        Vector3Int endCell,
        Vector3Int minCell,
        Vector3Int maxCell,
        HashSet<Vector3Int> referenceVoxels,
        HashSet<Vector3Int> staticBlocked,
        Vector3 start,
        Vector3 end,
        HashSet<Vector3Int> extraBlocked,
        int neighborOrderBias)
    {
        var open = new List<Vector3Int> { startCell };
        var openSet = new HashSet<Vector3Int> { startCell };
        var cameFrom = new Dictionary<Vector3Int, Vector3Int>();
        var gScore = new Dictionary<Vector3Int, float> { [startCell] = 0f };
        int steps = 0;

        while (open.Count > 0 && steps < MaxVoxelPathSearchSteps)
        {
            steps++;
            int bestIndex = 0;
            float bestScore = float.MaxValue;
            for (int i = 0; i < open.Count; i++)
            {
                Vector3Int node = open[i];
                float score = gScore[node] + VoxelHeuristic(node, endCell, referenceVoxels);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            Vector3Int current = open[bestIndex];
            open.RemoveAt(bestIndex);
            openSet.Remove(current);

            if (current == endCell)
                return ReconstructVoxelPath(cameFrom, current);

            for (int i = 0; i < VoxelNeighborOffsets.Length; i++)
            {
                Vector3Int offset = VoxelNeighborOffsets[(neighborOrderBias + i) % VoxelNeighborOffsets.Length];
                Vector3Int neighbor = current + offset;
                if (!IsVoxelInBounds(neighbor, minCell, maxCell))
                    continue;

                if (extraBlocked != null && extraBlocked.Contains(neighbor))
                    continue;

                if (neighbor != startCell && neighbor != endCell && staticBlocked.Contains(neighbor))
                    continue;

                float stepCost = VoxelCellSize + ReferencePathDeviationWeight * MinManhattanDistanceToReference(neighbor, referenceVoxels);
                if (offset.y != 0)
                    stepCost += VerticalStepExtraCost;
                float tentativeG = gScore[current] + stepCost;
                if (gScore.TryGetValue(neighbor, out float knownG) && tentativeG >= knownG)
                    continue;

                cameFrom[neighbor] = current;
                gScore[neighbor] = tentativeG;
                if (openSet.Add(neighbor))
                    open.Add(neighbor);
            }
        }

        return null;
    }

    private static float VoxelHeuristic(Vector3Int cell, Vector3Int endCell, HashSet<Vector3Int> referenceVoxels)
    {
        float horizontal = Mathf.Abs(cell.x - endCell.x) + Mathf.Abs(cell.z - endCell.z);
        float vertical = Mathf.Abs(cell.y - endCell.y);
        return (horizontal + vertical) * VoxelCellSize
            + vertical * VerticalStepExtraCost
            + ReferencePathDeviationWeight * MinManhattanDistanceToReference(cell, referenceVoxels);
    }

    private static int ManhattanDistance(Vector3Int a, Vector3Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z);
    }

    private static int MinManhattanDistanceToReference(Vector3Int cell, HashSet<Vector3Int> referenceVoxels)
    {
        if (referenceVoxels.Contains(cell))
            return 0;

        int best = int.MaxValue;
        foreach (Vector3Int reference in referenceVoxels)
        {
            int distance = ManhattanDistance(cell, reference);
            if (distance < best)
                best = distance;
        }

        return best == int.MaxValue ? 0 : best;
    }

    private static List<Vector3Int> ReconstructVoxelPath(Dictionary<Vector3Int, Vector3Int> cameFrom, Vector3Int current)
    {
        var path = new List<Vector3Int> { current };
        while (cameFrom.TryGetValue(current, out Vector3Int previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }

    private static List<Vector3> VoxelPathToOrthogonalRoute(Vector3 start, Vector3 end, List<Vector3Int> cellPath)
    {
        List<Vector3Int> corners = SimplifyCellPath(cellPath);
        var world = new List<Vector3> { start };
        for (int i = 1; i < corners.Count; i++)
            world.Add(VoxelToWorld(corners[i]));

        world.Add(end);

        var route = new List<Vector3> { start };
        Vector3 cursor = start;
        for (int i = 1; i < world.Count; i++)
            MoveAxisAlignedToTarget(route, ref cursor, world[i]);

        return SimplifyCollinearRoute(RemoveDuplicateConsecutive(route));
    }

    private static List<Vector3Int> SimplifyCellPath(List<Vector3Int> cellPath)
    {
        if (cellPath == null || cellPath.Count <= 2)
            return cellPath;

        var simplified = new List<Vector3Int> { cellPath[0] };
        for (int i = 1; i < cellPath.Count - 1; i++)
        {
            Vector3Int previous = simplified[simplified.Count - 1];
            Vector3Int current = cellPath[i];
            Vector3Int next = cellPath[i + 1];
            Vector3Int stepA = current - previous;
            Vector3Int stepB = next - current;
            if (stepA == stepB)
                continue;

            simplified.Add(current);
        }

        simplified.Add(cellPath[cellPath.Count - 1]);
        return simplified;
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

            TryInsertAxisAlignedDetour(route, legIndex, ZigzagDetourStepBase, routeSeed + detourIndex);
        }
    }

    private static bool TryInsertAxisAlignedDetour(List<Vector3> route, int legIndex, float step, int routeSeed)
    {
        Vector3 legStart = route[legIndex];
        Vector3 legEnd = route[legIndex + 1];
        if (!IsAxisAligned(legStart, legEnd))
            return false;

        Vector3 delta = legEnd - legStart;
        float legLength = delta.magnitude;
        if (legLength < step * 2f)
            return false;

        Vector3 direction = delta / legLength;
        List<Vector3> perpendicularChoices = GetPerpendicularAxisChoices(legStart, legEnd);
        if (perpendicularChoices.Count == 0)
            return false;

        int startChoice = ModPositive(routeSeed, perpendicularChoices.Count);
        for (int attempt = 0; attempt < perpendicularChoices.Count; attempt++)
        {
            Vector3 perpendicular = perpendicularChoices[(startChoice + attempt) % perpendicularChoices.Count];
            Vector3 sideStep = perpendicular * step;

            float along0 = legLength * 0.25f;
            float along1 = legLength * 0.75f;

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
            return true;
        }

        return false;
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

    private static List<Vector3> GetPerpendicularAxisChoices(Vector3 legStart, Vector3 legEnd)
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

        return choices;
    }

    private static List<Vector3> SimplifyCollinearRoute(List<Vector3> points)
    {
        if (points == null || points.Count <= 2)
            return points;

        var simplified = new List<Vector3> { points[0] };
        for (int i = 1; i < points.Count - 1; i++)
        {
            Vector3 previous = simplified[simplified.Count - 1];
            Vector3 current = points[i];
            Vector3 next = points[i + 1];

            if (SharesSameAxisDirection(previous, current, current, next))
                continue;

            simplified.Add(current);
        }

        simplified.Add(points[points.Count - 1]);
        return simplified;
    }

    private static bool SharesSameAxisDirection(Vector3 legStartA, Vector3 legEndA, Vector3 legStartB, Vector3 legEndB)
    {
        Vector3 dirA = AxisDirection(legEndA - legStartA);
        Vector3 dirB = AxisDirection(legEndB - legStartB);
        if (dirA.sqrMagnitude <= MinSegmentLength * MinSegmentLength)
            return true;
        if (dirB.sqrMagnitude <= MinSegmentLength * MinSegmentLength)
            return true;

        return dirA == dirB;
    }

    private static Vector3 AxisDirection(Vector3 delta)
    {
        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);
        float absZ = Mathf.Abs(delta.z);

        if (absX >= absY && absX >= absZ && absX > MinSegmentLength)
            return new Vector3(Mathf.Sign(delta.x), 0f, 0f);
        if (absY >= absX && absY >= absZ && absY > MinSegmentLength)
            return new Vector3(0f, Mathf.Sign(delta.y), 0f);
        if (absZ > MinSegmentLength)
            return new Vector3(0f, 0f, Mathf.Sign(delta.z));

        return Vector3.zero;
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
