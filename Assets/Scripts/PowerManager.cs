using System.Collections.Generic;
using UnityEngine;


[DefaultExecutionOrder(-40)]
public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance { get; private set; }

    private List<PowerSource> sources = new List<PowerSource>();
    private List<RunodePower> registeredRunodes = new List<RunodePower>();
    private bool recalculationRequested = false;

    private void Awake()
    {
        Instance = this;
    }

    /// Registers a runode to the tracking list for efficient power clearing.
    public void RegisterRunode(RunodePower runode)
    {
        if (!registeredRunodes.Contains(runode))
            registeredRunodes.Add(runode);
    }

    /// Removes a runode from tracking.
    public void UnregisterRunode(RunodePower runode)
    {
        registeredRunodes.Remove(runode);
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
        // 1. Perform spatial sweeps for all cubes to update their obstruction states
        foreach (RunodePower runode in registeredRunodes)
        {
            if (runode.obstructionController != null)
            {
                runode.obstructionController.PerformSpatialSweep();
            }
        }

        // 2. Clear all cube power states globally before any source runs BFS.
        ClearAllCubeStates();

        // 3. Each source independently runs BFS on its own connected graph.
        foreach (PowerSource source in sources)
        {
            source.RunBFS();
        }
    }

    private void ClearAllCubeStates()
    {
        // Faster lookup: only iterate over registered active runodes.
        foreach (RunodePower runode in registeredRunodes)
        {
            runode.ClearPowerState();
        }
    }
}
