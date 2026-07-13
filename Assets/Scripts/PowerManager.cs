using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-40)]
public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance { get; private set; }

    private List<PowerSource> sources = new List<PowerSource>();
    private List<RunodeFace> registeredFaces = new List<RunodeFace>();
    private bool recalculationRequested = false;
    private Transform lastAlteredTransform;
    private Coroutine recalculationRoutine;

    private void Awake()
    {
        Instance = this;
    }

    // Registers a face to the tracking list for power clearing and invalidation.
    public void RegisterRunodeFace(RunodeFace face)
    {
        if (!registeredFaces.Contains(face))
            registeredFaces.Add(face);
    }

    // Removes a face from tracking.
    public void UnregisterRunodeFace(RunodeFace face)
    {
        registeredFaces.Remove(face);
    }

    // Called by PowerSources on Start to register themselves.
    public void RegisterSource(PowerSource source)
    {
        if (!sources.Contains(source))
            sources.Add(source);
    }

    // Called by FaceObstructionDetector and other RunodePower-aware callers.
    public void RequestPowerFlowCheck(RunodePower alteredCube = null)
    {
        recalculationRequested = true;
        if (alteredCube != null) lastAlteredTransform = alteredCube.transform;
    }

    // Called by movement systems that do not depend on RunodePower directly.
    public void RequestPowerFlowCheck(Transform source)
    {
        recalculationRequested = true;
        if (source != null) lastAlteredTransform = source;
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

        // 1. Perform spatial sweeps.
        HashSet<ObstructionController> sweptControllers = new HashSet<ObstructionController>();
        foreach (RunodeFace face in registeredFaces)
        {
            ObstructionController oc = face.GetComponentInParent<ObstructionController>();
            if (oc != null && sweptControllers.Add(oc))
                oc.PerformSpatialSweep();
        }

        // 2. Prepare for new calculation: Global Logic Clear.
        // This prevents Source B from clearing Source A's results during the sequential BFS loop.
        foreach (RunodeFace face in registeredFaces)
        {
            face.Clear(false);
        }

        // Clear source-specific tracking lists.
        foreach (PowerSource source in sources)
        {
            source.poweredFaces.Clear();
            source.currentFacesPowered = 0;
        }

        // 3. Visual Clear for the affected area (requested for immediate feedback).
        if (lastAlteredTransform != null)
            InvalidateSubtree(lastAlteredTransform, true);

        // 4. Each source independently runs BFS.
        float delay = PowerDisplayManager.Instance != null ? PowerDisplayManager.Instance.propagationDelay : 0.05f;
        foreach (PowerSource source in sources)
        {
            yield return StartCoroutine(source.RunBFS(delay));
        }

        // 5. Final Pass: Visual Clear for any faces that lost power.
        foreach (RunodeFace face in registeredFaces)
        {
            if (!face.isFacePowered && face.faceSprite != null && face.faceSprite.color != Color.white)
            {
                face.Clear(true);
            }
        }

        recalculationInProgress = false;
        recalculationRoutine = null;
        lastAlteredTransform = null;

        if (needsAnotherRecalculation)
        {
            needsAnotherRecalculation = false;
            recalculationRequested = true;
        }
    }

    private void ClearAllFaceStates(bool visual = true)
    {
        foreach (RunodeFace face in registeredFaces)
            face.Clear(visual);
    }

    public void InvalidateSubtree(Transform root, bool visual = true)
    {
        Queue<RunodeFace> toClear = new Queue<RunodeFace>();
        foreach (var face in root.GetComponentsInChildren<RunodeFace>())
        {
            toClear.Enqueue(face);
        }

        HashSet<RunodeFace> cleared = new HashSet<RunodeFace>();
        while (toClear.Count > 0)
        {
            RunodeFace current = toClear.Dequeue();
            if (current == null || cleared.Contains(current)) continue;

            cleared.Add(current);
            current.Clear(visual); 

            foreach (RunodeFace face in registeredFaces)
            {
                if (face.parentFace == current)
                    toClear.Enqueue(face);
            }
        }
    }
}
