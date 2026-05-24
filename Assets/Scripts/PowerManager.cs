using System.Collections.Generic;
using UnityEngine;

/// Place one in the scene.
/// Owns the list of all PowerSources and controls when power recalculation happens.
/// Any cube that moves, rotates, or changes connection calls RequestPowerFlowCheck().
/// Recalculation runs once per frame in LateUpdate to avoid mid-frame inconsistencies.
[DefaultExecutionOrder(-40)]
public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance { get; private set; }

    private List<PowerSource> sources = new List<PowerSource>();
    private bool recalculationRequested = false;

    private void Awake()
    {
        Instance = this;
    }

    /// Called by PowerSources on Start to register themselves.
    public void RegisterSource(PowerSource source)
    {
        if (!sources.Contains(source))
            sources.Add(source);
    }

    /// Called by PowerConnectionTrigger on enter/exit, or by cubes on move/rotate.
    public void RequestPowerFlowCheck()
    {
        recalculationRequested = true;
    }

    private void LateUpdate()
    {
        if (!recalculationRequested) return;

        recalculationRequested = false;
        RecalculateAllSources();
    }

    private void RecalculateAllSources()
    {
        // Clear all cube power states globally before any source runs BFS.
        ClearAllCubeStates();

        // Each source independently runs BFS on its own connected graph.
        foreach (PowerSource source in sources)
        {
            source.RunBFS();
        }
    }

    private void ClearAllCubeStates()
    {
        PowerCube[] allCubes = FindObjectsByType<PowerCube>(FindObjectsSortMode.None);
        foreach (PowerCube cube in allCubes)
        {
            cube.ClearPowerState();
        }
    }
}
