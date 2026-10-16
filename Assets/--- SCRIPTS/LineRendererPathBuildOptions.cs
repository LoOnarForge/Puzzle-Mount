using System;
using UnityEngine;

[Serializable]
public struct LineRendererPathBuildOptions
{
    public int routeVariantIndex;
    public bool avoidObstacles;
    public LayerMask obstacleLayers;
    public float castRadius;

    // Settings used when building editor connection lines (candidates + optional obstacle avoidance).
    public static LineRendererPathBuildOptions ForEditor(int routeVariantIndex, bool avoidObstacles, LayerMask obstacleLayers, float castRadius)
    {
        return new LineRendererPathBuildOptions
        {
            routeVariantIndex = routeVariantIndex,
            avoidObstacles = avoidObstacles,
            obstacleLayers = obstacleLayers,
            castRadius = castRadius
        };
    }
}
