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
    // A List (not a Queue) so SubmitBatch can merge a partial batch (e.g. just one rotated cube's
    // subtree) into whatever's already pending without discarding unrelated, still-pending faces.
    private Dictionary<PowerSource, List<FaceVisualUpdate>> sourcePending = new Dictionary<PowerSource, List<FaceVisualUpdate>>();
    private Dictionary<PowerSource, Coroutine> sourceRoutines = new Dictionary<PowerSource, Coroutine>();

    // True once a real short circuit has been visually confirmed (a face already held by a
    // different source was about to be colored). Coloring stops permanently at that point.
    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // Single entry point: PowerManager hands over updates for whatever faces changed this pass —
    // this can be a full network snapshot (a normal recalculation) or a small partial batch (an
    // instant subtree wipe kicked off the moment a rotation/move starts). Either way this method:
    // 1. Drops any update that already matches what's currently displayed (no need to replay it).
    // 2. Groups everything else by source and merges it into that source's pending wave, only
    //    superseding entries for the same face — unrelated pending faces on the same source are
    //    left alone so an unaffected branch keeps powering up uninterrupted.
    // 3. Re-sorts each touched source's pending wave strictly downstream by distance.
    public void SubmitBatch(List<FaceVisualUpdate> batch)
    {
        if (IsGameOver || batch == null) return;

        Dictionary<PowerSource, List<FaceVisualUpdate>> grouped = new Dictionary<PowerSource, List<FaceVisualUpdate>>();
        List<FaceVisualUpdate> immediate = new List<FaceVisualUpdate>();

        foreach (var update in batch)
        {
            if (update.face == null) continue;
            if (IsAlreadyCorrect(update)) continue;

            Debug.Log($"[PowerDebug] Queuing {(update.color == Color.white ? "DEPOWER" : "POWER")} for {update.face.transform.root.name}/{update.face.name} src={update.source?.name ?? "null"} dist={update.distanceFromSource}");

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
            List<FaceVisualUpdate> incoming = pair.Value;

            if (!sourcePending.TryGetValue(source, out List<FaceVisualUpdate> pending))
            {
                pending = new List<FaceVisualUpdate>();
                sourcePending[source] = pending;
            }

            HashSet<RunodeFace> incomingFaces = new HashSet<RunodeFace>();
            foreach (var update in incoming)
                incomingFaces.Add(update.face);

            pending.RemoveAll(p => incomingFaces.Contains(p.face));
            pending.AddRange(incoming);
            pending.Sort((a, b) => a.distanceFromSource.CompareTo(b.distanceFromSource));

            if (!sourceRoutines.TryGetValue(source, out Coroutine routine) || routine == null)
                sourceRoutines[source] = StartCoroutine(ProcessSourceQueue(source));
        }
    }

    // Plays one source's wave: strictly in downstream order, at a steady pace, until its pending
    // list (which SubmitBatch may merge more into mid-flight) runs dry.
    private IEnumerator ProcessSourceQueue(PowerSource source)
    {
        while (true)
        {
            if (IsGameOver)
            {
                sourcePending[source].Clear();
                sourceRoutines[source] = null;
                yield break;
            }

            List<FaceVisualUpdate> pending = sourcePending[source];
            if (pending.Count == 0) break;

            FaceVisualUpdate update = pending[0];
            pending.RemoveAt(0);

            bool isDepowering = update.color == Color.white;
            float delay = isDepowering ? depowerDelay : powerUpDelay;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (!TryApplyUpdate(update))
            {
                sourcePending[source].Clear();
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
