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

    private List<PowerDisplayManager.FaceVisualUpdate> pendingVisualUpdates = new List<PowerDisplayManager.FaceVisualUpdate>();

    // Buffers a single face's visual update. Sent to PowerDisplayManager as one batch via FlushVisualUpdates.
    // distanceFromSource is forwarded so PowerDisplayManager can order its animation strictly downstream from the source, regardless of the order updates are queued in here.
    public void QueueVisualUpdate(RunodeFace face, Color color, PowerSource source, bool isObstructed, int distanceFromSource, bool instant = false)
    {
        if (face == null) return;

        pendingVisualUpdates.Add(new PowerDisplayManager.FaceVisualUpdate
        {
            face = face,
            color = color,
            source = source,
            isObstructed = isObstructed,
            distanceFromSource = distanceFromSource,
            instant = instant
        });
    }

    // Hands all buffered visual updates to PowerDisplayManager in a single call.
    // PowerDisplayManager is solely responsible for deciding what actually needs to animate (it skips faces already showing the correct state).
    private void FlushVisualUpdates()
    {
        if (PowerDisplayManager.Instance == null)
        {
            pendingVisualUpdates.Clear();
            return;
        }

        List<PowerDisplayManager.FaceVisualUpdate> batch = pendingVisualUpdates;
        pendingVisualUpdates = new List<PowerDisplayManager.FaceVisualUpdate>();
        PowerDisplayManager.Instance.SubmitBatch(batch);
    }

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
            ClearSubtreeState(lastAlteredTransform, true);

        // 4. Seed all BFS queues.
        foreach (PowerSource source in sources)
        {
            source.InitBFS();
        }

        // 5. Interleaved round-robin BFS: one step per source per round. BFS never halts on
        // conflict — PowerDisplayManager is the sole authority for detecting a short circuit.
        bool anyActive = true;
        while (anyActive)
        {
            anyActive = false;
            foreach (PowerSource source in sources)
            {
                if (!source.HasPendingSteps) continue;
                anyActive = true;
                source.StepBFS();
            }
        }

        // 6. Final Pass: Visual Clear for any faces that lost power. Checked against
        // lastPoweredBySource (the persistent logical claim) rather than the rendered sprite
        // color — a face can be logically unpowered this pass while still visually unlit because
        // its power-up pulse hasn't reached it yet. Using the rendered color here would miss it.
        foreach (RunodeFace face in registeredFaces)
        {
            if (!face.isFacePowered && face.lastPoweredBySource != null)
            {
                face.Clear(true, false);
                face.lastPoweredBySource = null;
            }
        }

        recalculationInProgress = false;
        recalculationRoutine = null;
        lastAlteredTransform = null;

        FlushVisualUpdates();

        if (needsAnotherRecalculation)
        {
            needsAnotherRecalculation = false;
            recalculationRequested = true;
        }

        yield break;
    }

    private void ClearAllFaceStates(bool visual = true)
    {
        foreach (RunodeFace face in registeredFaces)
            face.Clear(visual);
    }

    public void InvalidateSubtree(Transform root, bool visual = true)
    {
        ClearSubtreeState(root, visual);
        if (visual) FlushVisualUpdates();
    }

    // Walks the subtree clearing each face's logic state, buffering the matching visual clear if requested.
    private void ClearSubtreeState(Transform root, bool visual = true)
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
            current.Clear(visual, false); // Use false to respect the depowerDelay wave

            foreach (RunodeFace face in registeredFaces)
            {
                if (face.parentFace == current)
                    toClear.Enqueue(face);
            }
        }
    }
}
