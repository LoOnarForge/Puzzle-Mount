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

    // Two fully independent animated tracks per source — one for powering up, one for depowering.
    // Keeping them separate (rather than one merged, distance-sorted queue) means a power-up wave
    // and a depower wave for the same source run truly in parallel: each has its own list and its
    // own coroutine, so neither one's pacing or ordering can be disturbed by the other. A List (not
    // a Queue) so SubmitBatch can merge a partial batch into whatever's already pending on that
    // track without discarding unrelated, still-pending faces.
    private Dictionary<PowerSource, List<FaceVisualUpdate>> sourcePendingPower = new Dictionary<PowerSource, List<FaceVisualUpdate>>();
    private Dictionary<PowerSource, List<FaceVisualUpdate>> sourcePendingDepower = new Dictionary<PowerSource, List<FaceVisualUpdate>>();
    private Dictionary<PowerSource, Coroutine> sourceRoutinesPower = new Dictionary<PowerSource, Coroutine>();
    private Dictionary<PowerSource, Coroutine> sourceRoutinesDepower = new Dictionary<PowerSource, Coroutine>();

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
    // 2. Splits everything else by direction (power vs depower) and groups by source, merging into
    //    that source's power or depower track — only superseding entries for the same face on that
    //    track; unrelated pending faces are left alone so an unaffected branch keeps animating
    //    uninterrupted.
    // 3. Re-sorts each touched track strictly downstream by distance.
    public void SubmitBatch(List<FaceVisualUpdate> batch)
    {
        if (IsGameOver || batch == null) return;

        Dictionary<PowerSource, List<FaceVisualUpdate>> groupedPower = new Dictionary<PowerSource, List<FaceVisualUpdate>>();
        Dictionary<PowerSource, List<FaceVisualUpdate>> groupedDepower = new Dictionary<PowerSource, List<FaceVisualUpdate>>();
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

            bool isDepower = update.color == Color.white;
            Dictionary<PowerSource, List<FaceVisualUpdate>> targetGroup = isDepower ? groupedDepower : groupedPower;

            if (!targetGroup.TryGetValue(update.source, out List<FaceVisualUpdate> list))
            {
                list = new List<FaceVisualUpdate>();
                targetGroup[update.source] = list;
            }
            list.Add(update);
        }

        foreach (var update in immediate)
        {
            if (!TryApplyUpdate(update)) return;
        }

        MergeIntoTrack(groupedPower, sourcePendingPower, sourcePendingDepower, sourceRoutinesPower, isDepowerTrack: false);
        MergeIntoTrack(groupedDepower, sourcePendingDepower, sourcePendingPower, sourceRoutinesDepower, isDepowerTrack: true);
    }

    // Merges incoming updates (already grouped by source) into one direction's track per source,
    // starting that track's coroutine if it isn't already running. Also purges any stale entry for
    // the same faces from the OTHER track — if a face's fate flipped direction this pass, a
    // leftover entry sitting on the opposite track would still fire later and fight this outcome.
    private void MergeIntoTrack(
        Dictionary<PowerSource, List<FaceVisualUpdate>> incomingBySource,
        Dictionary<PowerSource, List<FaceVisualUpdate>> ownTrack,
        Dictionary<PowerSource, List<FaceVisualUpdate>> otherTrack,
        Dictionary<PowerSource, Coroutine> ownRoutines,
        bool isDepowerTrack)
    {
        foreach (var pair in incomingBySource)
        {
            PowerSource source = pair.Key;
            List<FaceVisualUpdate> incoming = pair.Value;

            if (!ownTrack.TryGetValue(source, out List<FaceVisualUpdate> pending))
            {
                pending = new List<FaceVisualUpdate>();
                ownTrack[source] = pending;
            }

            HashSet<RunodeFace> incomingFaces = new HashSet<RunodeFace>();
            foreach (var update in incoming)
                incomingFaces.Add(update.face);

            if (otherTrack.TryGetValue(source, out List<FaceVisualUpdate> otherPending))
                otherPending.RemoveAll(p => incomingFaces.Contains(p.face));

            pending.RemoveAll(p => incomingFaces.Contains(p.face));
            pending.AddRange(incoming);
            pending.Sort((a, b) => a.distanceFromSource.CompareTo(b.distanceFromSource));

            if (!ownRoutines.TryGetValue(source, out Coroutine routine) || routine == null)
                ownRoutines[source] = StartCoroutine(ProcessSourceQueue(source, ownTrack, ownRoutines, isDepowerTrack));
        }
    }

    // Plays one source's single-direction wave: strictly in downstream order, at a steady pace,
    // until its pending list (which SubmitBatch may merge more into mid-flight) runs dry. Entirely
    // independent of the other direction's track/coroutine for the same source.
    private IEnumerator ProcessSourceQueue(PowerSource source, Dictionary<PowerSource, List<FaceVisualUpdate>> track, Dictionary<PowerSource, Coroutine> routines, bool isDepowerTrack)
    {
        float delay = isDepowerTrack ? depowerDelay : powerUpDelay;

        while (true)
        {
            if (IsGameOver)
            {
                track[source].Clear();
                routines[source] = null;
                yield break;
            }

            List<FaceVisualUpdate> pending = track[source];
            if (pending.Count == 0) break;

            FaceVisualUpdate update = pending[0];
            pending.RemoveAt(0);

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (!TryApplyUpdate(update))
            {
                track[source].Clear();
                routines[source] = null;
                yield break;
            }
        }

        routines[source] = null;
    }

    // A face is already correctly displayed if its current color layer matches the target, OR if
    // it never actually needed to change in the first place:
    // - Depowering: lastPoweredBySource is only ever nulled by PowerManager's own final pass once
    //   it has fully confirmed a face is genuinely unpowered. Since a whole recalculation always
    //   finishes before PowerDisplayManager ever sees the batch, lastPoweredBySource already holds
    //   the true, final answer by the time this runs. If it still matches this update's source,
    //   the face never actually lost power from that source (this depower is a stray/eager one
    //   queued for other reasons) — skip it so it doesn't visually reset.
    // - Powering: a color match alone isn't enough to rule out a genuine change of ownership,
    //   which still needs to go through the short circuit check, so ownership must also match.
    private bool IsAlreadyCorrect(FaceVisualUpdate update)
    {
        RunodeFace face = update.face;

        if (update.color == Color.white)
            return face.lastPoweredBySource == update.source;

        if (face.powerColorLayer != update.color) return false;
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
    }

    private void ApplyVisualDirect(RunodeFace face, Color color, bool isObstructed)
    {
        if (face == null) return;

        face.powerColorLayer = color;
        face.UpdateSpriteVisuals();
    }
}
