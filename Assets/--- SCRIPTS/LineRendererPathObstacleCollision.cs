using System.Collections.Generic;
using UnityEngine;

public static class LineRendererPathObstacleCollision
{
    private const float MinSegmentLength = 0.001f;
    private const float ProbeStepMeters = 0.2f;
    private const float EndpointProbeSkipRadius = 0.6f;
    private const float ProbeSizeMeters = 0.2f;
    private static readonly Vector3 ProbeHalfExtents = new Vector3(0.1f, 0.1f, 0.1f);

    private static int _cacheLayerMask;
    private static readonly Dictionary<Vector3Int, bool> VoxelBlockedCache = new Dictionary<Vector3Int, bool>(4096);

    public static void ClearVoxelCache()
    {
        VoxelBlockedCache.Clear();
        _cacheLayerMask = 0;
    }

    public static bool RouteOverlapsObstacles(
        IReadOnlyList<Vector3> route,
        LayerMask obstacleLayers,
        Vector3 linkStart,
        Vector3 linkEnd)
    {
        if (route == null || route.Count < 2)
            return false;

        if (obstacleLayers.value == 0)
            return false;

        for (int leg = 0; leg < route.Count - 1; leg++)
        {
            if (SegmentOverlapsObstacles(route[leg], route[leg + 1], obstacleLayers, linkStart, linkEnd))
                return true;
        }

        return false;
    }

    public static bool IsVoxelBlockedForPath(
        Vector3 voxelCenter,
        LayerMask obstacleLayers,
        Vector3 linkStart,
        Vector3 linkEnd)
    {
        if (obstacleLayers.value == 0)
            return false;

        float skipSq = EndpointProbeSkipRadius * EndpointProbeSkipRadius;
        if ((voxelCenter - linkStart).sqrMagnitude <= skipSq || (voxelCenter - linkEnd).sqrMagnitude <= skipSq)
            return false;

        return IsVoxelBlocked(voxelCenter, obstacleLayers);
    }

    public static bool IsVoxelBlocked(Vector3 voxelCenter, LayerMask obstacleLayers)
    {
        if (obstacleLayers.value == 0)
            return false;

        EnsureCacheMask(obstacleLayers);
        Vector3Int key = WorldToVoxelKey(voxelCenter);
        if (VoxelBlockedCache.TryGetValue(key, out bool cached))
            return cached;

        bool blocked = Physics.CheckBox(
            voxelCenter,
            ProbeHalfExtents,
            Quaternion.identity,
            obstacleLayers.value,
            QueryTriggerInteraction.Ignore);

        VoxelBlockedCache[key] = blocked;
        return blocked;
    }

    private static void EnsureCacheMask(LayerMask obstacleLayers)
    {
        if (_cacheLayerMask != obstacleLayers.value)
        {
            VoxelBlockedCache.Clear();
            _cacheLayerMask = obstacleLayers.value;
        }
    }

    private static Vector3Int WorldToVoxelKey(Vector3 world)
    {
        float q = 1f / ProbeSizeMeters;
        return new Vector3Int(
            Mathf.RoundToInt(world.x * q),
            Mathf.RoundToInt(world.y * q),
            Mathf.RoundToInt(world.z * q));
    }

    private static bool SegmentOverlapsObstacles(
        Vector3 a,
        Vector3 b,
        LayerMask obstacleLayers,
        Vector3 linkStart,
        Vector3 linkEnd)
    {
        Vector3 delta = b - a;
        float length = delta.magnitude;
        if (length <= MinSegmentLength)
            return false;

        int stepCount = Mathf.Max(1, Mathf.CeilToInt(length / ProbeStepMeters));
        float skipSq = EndpointProbeSkipRadius * EndpointProbeSkipRadius;

        for (int step = 0; step <= stepCount; step++)
        {
            float t = step / (float)stepCount;
            Vector3 center = Vector3.Lerp(a, b, t);

            if ((center - linkStart).sqrMagnitude <= skipSq || (center - linkEnd).sqrMagnitude <= skipSq)
                continue;

            if (IsVoxelBlocked(center, obstacleLayers))
                return true;
        }

        return false;
    }
}
