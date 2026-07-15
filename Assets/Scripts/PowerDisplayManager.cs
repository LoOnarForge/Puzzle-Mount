using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerDisplayManager : MonoBehaviour
{
    public static PowerDisplayManager Instance { get; private set; }

    [Header("SETTINGS:")]
    [Range(0f, 1f)]
    [Tooltip("Delay between consecutive cubes powering up in the same chain.")]
    public float powerUpDelay = 0.05f;

    [Range(0f, 1f)]
    [Tooltip("Delay between consecutive cubes depowering in the same chain.")]
    public float depowerDelay = 0.02f;

    // A single buffered face visual change. Batched and handed off by PowerManager once per recalculation.
    public struct FaceVisualUpdate
    {
        public RunodeFace face;
        public Color color;
        public PowerSource source;
        public bool isObstructed;
        public int distanceFromSource;
        public bool instant;
    }

    // One animated wave per source. Keeping them independent means depowering one source never
    // has to wait on another source's power-up wave (or even its own previous wave) to finish.
    private Dictionary<PowerSource, Queue<FaceVisualUpdate>> sourceQueues = new Dictionary<PowerSource, Queue<FaceVisualUpdate>>();
    private Dictionary<PowerSource, Coroutine> sourceRoutines = new Dictionary<PowerSource, Coroutine>();

    // True once a real short circuit has been visually confirmed (a face already held by a
    // different source was about to be colored). Coloring stops permanently at that point.
    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // Single entry point: PowerManager hands over the full current target state (every face
    // that is powered or depowered this recalculation), not an incremental diff. This method:
    // 1. Drops any update that already matches what's currently displayed (no need to replay it).
    // 2. Groups everything else by source and sorts each group strictly downstream by distance.
    // 3. Replaces each source's pending wave with the new one (already-shown progress is kept,
    //    only what's left to animate is superseded), so an interrupted wave always keeps
    //    animating toward a correct, current target instead of snapping or leaving artifacts.
    public void SubmitBatch(List<FaceVisualUpdate> batch)
    {
        if (IsGameOver || batch == null) return;

        Dictionary<PowerSource, List<FaceVisualUpdate>> grouped = new Dictionary<PowerSource, List<FaceVisualUpdate>>();
        List<FaceVisualUpdate> immediate = new List<FaceVisualUpdate>();

        foreach (var update in batch)
        {
            if (update.face == null) continue;
            if (IsAlreadyCorrect(update)) continue;

            // Updates with no owning source (or explicitly marked instant) have nothing to
            // sequence against — apply them right away instead of holding up a wave for them.
            if (update.instant || update.source == null)
            {
                immediate.Add(update);
                continue;
            }

            if (!grouped.TryGetValue(update.source, out List<FaceVisualUpdate> list))
            {
                list = new List<FaceVisualUpdate>();
                grouped[update.source] = list;
            }
            list.Add(update);
        }

        foreach (var update in immediate)
        {
            if (!TryApplyUpdate(update)) return;
        }

        foreach (var pair in grouped)
        {
            PowerSource source = pair.Key;
            List<FaceVisualUpdate> list = pair.Value;
            list.Sort((a, b) => a.distanceFromSource.CompareTo(b.distanceFromSource));

            if (!sourceQueues.TryGetValue(source, out Queue<FaceVisualUpdate> queue))
            {
                queue = new Queue<FaceVisualUpdate>();
                sourceQueues[source] = queue;
            }

            queue.Clear();
            foreach (var update in list)
                queue.Enqueue(update);

            if (!sourceRoutines.TryGetValue(source, out Coroutine routine) || routine == null)
                sourceRoutines[source] = StartCoroutine(ProcessSourceQueue(source));
        }
    }

    // Plays one source's wave: strictly in downstream order, at a steady pace, until its queue
    // (which SubmitBatch may refill mid-flight) runs dry.
    private IEnumerator ProcessSourceQueue(PowerSource source)
    {
        Queue<FaceVisualUpdate> queue = sourceQueues[source];

        while (queue.Count > 0)
        {
            if (IsGameOver)
            {
                queue.Clear();
                sourceRoutines[source] = null;
                yield break;
            }

            var update = queue.Dequeue();

            bool isDepowering = update.color == Color.white;
            float delay = isDepowering ? depowerDelay : powerUpDelay;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (!TryApplyUpdate(update))
            {
                queue.Clear();
                sourceRoutines[source] = null;
                yield break;
            }
        }

        sourceRoutines[source] = null;
    }

    // A face is already correctly displayed if its current color layer matches the target:
    // for depowering, the owning source no longer matters once it's visually off; for powering,
    // it must also already be owned by the same source (a color match alone isn't enough to
    // rule out a genuine change of ownership, which still needs to go through the short circuit check).
    private bool IsAlreadyCorrect(FaceVisualUpdate update)
    {
        RunodeFace face = update.face;
        if (face.powerColorLayer != update.color) return false;
        if (update.color == Color.white) return true;
        return face.lastPoweredBySource == update.source;
    }

    // Applies a single update, or detects a real short circuit at the moment of coloring:
    // a face already held by a different source is about to be colored by this one.
    private bool TryApplyUpdate(FaceVisualUpdate update)
    {
        if (update.face == null) return true;

        bool isDepowering = (update.color == Color.white);
        if (!isDepowering && update.face.lastPoweredBySource != null && update.face.lastPoweredBySource != update.source)
        {
            TriggerGameOver(update.face, update.source);
            return false;
        }

        ApplyVisualDirect(update.face, update.color, update.isObstructed);
        return true;
    }

    private void TriggerGameOver(RunodeFace face, PowerSource incomingSource)
    {
        if (IsGameOver) return;
        IsGameOver = true;
        Debug.Log($"[PowerDisplayManager] GAME OVER: short circuit at {face.transform.root.name}/{face.name} between {face.lastPoweredBySource?.name} and {incomingSource?.name}.");
    }

    private void ApplyVisualDirect(RunodeFace face, Color color, bool isObstructed)
    {
        if (face == null) return;

        face.powerColorLayer = color;
        face.UpdateSpriteVisuals();
    }
}
