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

    // One independent, self-contained wave of face updates, strictly sorted downstream by
    // distance. Once created, a pulse is never modified, merged, cancelled, or reordered by
    // anything else — it has no awareness of any other pulse, running or finished. It simply
    // steps through its own list at a fixed pace until exhausted, then removes itself.
    private class Pulse
    {
        public List<FaceVisualUpdate> steps;
        public int index;
    }

    // Every source can have any number of pulses running at once, power and depower alike, each
    // its own coroutine over its own list. Multiple pulses for the same source behave like
    // independent trains on the same track: same pace, never colliding, never overtaking, because
    // each one only ever advances through the exact distances it was given at creation time.
    private Dictionary<PowerSource, List<Pulse>> activePulses = new Dictionary<PowerSource, List<Pulse>>();

    // True once a real short circuit has been visually confirmed (a face already held by a
    // different source was about to be colored). Coloring stops permanently at that point.
    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // Single entry point: PowerManager hands over every face whose visual state must change this
    // pass — this can be a full network snapshot (a normal recalculation) or a small partial
    // batch (an instant subtree wipe kicked off the moment a rotation/move starts). This is split
    // by direction (power vs depower) and grouped by source; each resulting group becomes exactly
    // one brand new pulse. Nothing here compares against what's currently displayed, what's
    // already pending, or what any other pulse intends — a batch simply becomes a pulse and runs.
    public void SubmitBatch(List<FaceVisualUpdate> batch)
    {
        if (IsGameOver || batch == null) return;

        Dictionary<PowerSource, List<FaceVisualUpdate>> groupedPower = new Dictionary<PowerSource, List<FaceVisualUpdate>>();
        Dictionary<PowerSource, List<FaceVisualUpdate>> groupedDepower = new Dictionary<PowerSource, List<FaceVisualUpdate>>();
        List<FaceVisualUpdate> immediate = new List<FaceVisualUpdate>();

        foreach (var update in batch)
        {
            if (update.face == null) continue;

            // Updates with no owning source (or explicitly marked instant) have nothing to
            // sequence against — apply them right away instead of holding up a pulse for them.
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

        StartPulses(groupedPower, isDepowerPulse: false);
        StartPulses(groupedDepower, isDepowerPulse: true);
    }

    // Spins up exactly one new pulse per source for this direction's group, sorted strictly
    // downstream by distance, and starts it running immediately alongside whatever pulses are
    // already active for that source.
    private void StartPulses(Dictionary<PowerSource, List<FaceVisualUpdate>> groupedBySource, bool isDepowerPulse)
    {
        foreach (var pair in groupedBySource)
        {
            PowerSource source = pair.Key;
            List<FaceVisualUpdate> steps = pair.Value;
            steps.Sort((a, b) => a.distanceFromSource.CompareTo(b.distanceFromSource));

            Pulse pulse = new Pulse { steps = steps, index = 0 };

            if (!activePulses.TryGetValue(source, out List<Pulse> pulses))
            {
                pulses = new List<Pulse>();
                activePulses[source] = pulses;
            }
            pulses.Add(pulse);

            StartCoroutine(RunPulse(source, pulse, isDepowerPulse));
        }
    }

    // Plays one pulse strictly downstream at a steady pace until its own list runs dry, then
    // removes itself. Entirely blind to every other pulse for this source, including ones running
    // the opposite direction — that independence is what guarantees a power pulse and a depower
    // pulse can run over the same faces without ever fighting, as long as they were started at
    // different times (which a real rotation always is).
    private IEnumerator RunPulse(PowerSource source, Pulse pulse, bool isDepowerPulse)
    {
        float delay = isDepowerPulse ? depowerDelay : powerUpDelay;

        while (pulse.index < pulse.steps.Count)
        {
            if (IsGameOver) break;

            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (IsGameOver) break;

            FaceVisualUpdate update = pulse.steps[pulse.index];
            pulse.index++;

            if (!TryApplyUpdate(update)) break;
        }

        if (activePulses.TryGetValue(source, out List<Pulse> pulses))
            pulses.Remove(pulse);
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
        Debug.Log("GAME OVER LOOSER!");
    }

    private void ApplyVisualDirect(RunodeFace face, Color color, bool isObstructed)
    {
        if (face == null) return;

        face.powerColorLayer = color;
        face.UpdateSpriteVisuals();
    }
}
