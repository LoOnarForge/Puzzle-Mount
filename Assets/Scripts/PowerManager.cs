using System.Collections.Generic;
using UnityEngine;


[DefaultExecutionOrder(-40)]
public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance { get; private set; }

    private List<PowerSource> sources = new List<PowerSource>();
    private List<RunodePower> registeredRunodes = new List<RunodePower>();
    private bool recalculationRequested = false;
    private RunodePower lastAlteredCube;

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
    public void RequestPowerFlowCheck(RunodePower alteredCube = null)
    {
        recalculationRequested = true;
        if (alteredCube != null) lastAlteredCube = alteredCube;
    }

    private void LateUpdate()
    {
        if (!recalculationRequested) return;

        recalculationRequested = false;
        RecalculateAllSources();
        lastAlteredCube = null;
    }

    private void RecalculateAllSources()
    {
        if (PowerDisplayManager.Instance != null) PowerDisplayManager.Instance.ResetQueue();

        // 1. Perform spatial sweeps for all cubes to update their obstruction states
        foreach (RunodePower runode in registeredRunodes)
        {
            if (runode.obstructionController != null)
            {
                runode.obstructionController.PerformSpatialSweep();
            }
        }

        // 2. Handle Invalidation
        if (lastAlteredCube != null)
        {
            InvalidateSubtree(lastAlteredCube);
        }
        else
        {
            // If no specific cube was altered, we still do a full clear for safety (e.g. game start)
            ClearAllCubeStates();
        }

        // 3. Each source independently runs BFS. 
        // Note: RunBFS needs to be able to resume from existing frontier or handle already powered cubes.
        foreach (PowerSource source in sources)
        {
            source.RunBFS();
        }
    }

    private void InvalidateSubtree(RunodePower root)
    {
        Queue<RunodePower> toClear = new Queue<RunodePower>();
        toClear.Enqueue(root);

        // We also need to clear anything that WAS fed by this root
        foreach (var runode in registeredRunodes)
        {
            if (runode.parentCube == root)
            {
                toClear.Enqueue(runode);
            }
        }

        // Standard BFS-style subtree invalidation
        HashSet<RunodePower> cleared = new HashSet<RunodePower>();
        while (toClear.Count > 0)
        {
            RunodePower current = toClear.Dequeue();
            if (current == null || cleared.Contains(current)) continue;

            cleared.Add(current);
            current.ClearPowerState();

            // Find children (cubes that have 'current' as their parent)
            foreach (var runode in registeredRunodes)
            {
                if (runode.parentCube == current)
                {
                    toClear.Enqueue(runode);
                }
            }
        }
    }

    private void ClearAllCubeStates()
    {
        // Faster lookup: only iterate over registered active runodes.
        foreach (RunodePower runode in registeredRunodes)
        {
            // By default, ClearPowerState calls ApplyFaceColor with instant = false.
            runode.ClearPowerState();
        }
    }
}
