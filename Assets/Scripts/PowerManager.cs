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
    private Coroutine recalculationRoutine;

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

    private bool recalculationInProgress = false;
    private bool needsAnotherRecalculation = false;

    private void LateUpdate()
    {
        if (!recalculationRequested) return;

        recalculationRequested = false;
        
        if (recalculationInProgress)
        {
            needsAnotherRecalculation = true;
            return;
        }

        recalculationRoutine = StartCoroutine(RecalculateAllSourcesRoutine());
    }

    private System.Collections.IEnumerator RecalculateAllSourcesRoutine()
    {
        recalculationInProgress = true;

        if (PowerDisplayManager.Instance != null) PowerDisplayManager.Instance.ResetQueue();

        // 1. Perform spatial sweeps for all cubes to update their obstruction states
        foreach (RunodePower runode in registeredRunodes)
        {
            if (runode.obstructionController != null)
            {
                runode.obstructionController.PerformSpatialSweep();
            }
        }

        // 2. Handle Invalidation - Logic only (Silent clear to prevent flickering)
        if (lastAlteredCube != null)
        {
            InvalidateSubtree(lastAlteredCube);
        }
        else
        {
            ClearAllCubeStates(false);
        }

        // 3. Each source independently runs BFS. 
        float delay = PowerDisplayManager.Instance != null ? PowerDisplayManager.Instance.propagationDelay : 0.05f;
        foreach (PowerSource source in sources)
        {
            yield return StartCoroutine(source.RunBFS(delay));
        }

        recalculationInProgress = false;
        recalculationRoutine = null;
        lastAlteredCube = null;

        // If a request came in while we were working, run it again now.
        if (needsAnotherRecalculation)
        {
            needsAnotherRecalculation = false;
            recalculationRequested = true;
        }
    }

    private void ClearAllCubeStates(bool visual = true)
    {
        // Faster lookup: only iterate over registered active runodes.
        foreach (RunodePower runode in registeredRunodes)
        {
            runode.ClearPowerState(visual);
        }
    }

    private void InvalidateSubtree(RunodePower root)
    {
        Queue<RunodeFace> toClear = new Queue<RunodeFace>();
        foreach (var face in root.allFaces)
        {
            if (face.isFacePowered) toClear.Enqueue(face);
        }

        // Standard BFS-style subtree invalidation
        HashSet<RunodeFace> cleared = new HashSet<RunodeFace>();
        while (toClear.Count > 0)
        {
            RunodeFace current = toClear.Dequeue();
            if (current == null || cleared.Contains(current)) continue;

            cleared.Add(current);
            RunodePower cube = current.cube;
            if (cube != null) cube.ClearFace(current.faceIndex, false); // SILENT logic clear to avoid flicker

            // Find children (faces that have 'current' as their parent)
            foreach (var runode in registeredRunodes)
            {
                foreach (var face in runode.allFaces)
                {
                    if (face.parentCube == cube && face.parentFaceIndex == current.faceIndex)
                    {
                        toClear.Enqueue(face);
                    }
                }
            }
        }
    }
}
