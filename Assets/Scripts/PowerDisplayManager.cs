using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerDisplayManager : MonoBehaviour
{
    public static PowerDisplayManager Instance { get; private set; }

    [Header("SETTINGS:")]
    [Range(0f, 1f)]
    [Tooltip("Standard delay between consecutive cubes powering up.")]
    public float powerUpDelay = 0.05f;

    [Range(0f, 1f)]
    [Tooltip("Delay for the very first cube to power up after an alteration.")]
    public float initialPowerUpDelay = 0.2f;

    [Range(0f, 1f)]
    [Tooltip("Delay between consecutive cubes depowering.")]
    public float depowerDelay = 0.02f;

    private bool isFirstInSequence = true;

    // A single buffered face visual change. Batched and handed off by PowerManager once per recalculation.
    public struct FaceVisualUpdate
    {
        public RunodeFace face;
        public Color color;
        public PowerSource source;
        public bool isObstructed;
        public bool instant;
    }

    private Queue<FaceVisualUpdate> updateQueue = new Queue<FaceVisualUpdate>();
    private Coroutine processRoutine;

    // True once a real short circuit has been visually confirmed (a face already held by a
    // different source was about to be colored). Coloring stops permanently at that point.
    public bool IsGameOver { get; private set; }

    private void Awake()
    {
        Instance = this;
    }

    // Single entry point: replaces any in-progress animation with a new batch of visual updates.
    // Any leftover updates from a previous batch are snapped to their final state instantly.
    public void SubmitBatch(List<FaceVisualUpdate> batch)
    {
        if (IsGameOver) return;

        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }

        if (updateQueue.Count > 0)

        while (updateQueue.Count > 0)
        {
            var leftover = updateQueue.Dequeue();

            if (!TryApplyUpdate(leftover))
            {
                updateQueue.Clear();
                return;
            }
        }

        isFirstInSequence = true;

        if (batch == null) return;

        foreach (var update in batch)
        {
            if (update.face == null) continue;
            updateQueue.Enqueue(update);
        }

        if (updateQueue.Count > 0)
            processRoutine = StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        while (updateQueue.Count > 0)
        {
            var update = updateQueue.Dequeue();

            bool isDepowering = (update.color == Color.white);

            float delay = 0f;
            if (!update.instant)
            {
                if (isDepowering)
                {
                    delay = depowerDelay;
                }
                else if (isFirstInSequence)
                {
                    delay = initialPowerUpDelay;
                    isFirstInSequence = false;
                }
                else
                {
                    delay = powerUpDelay;
                }
            }

            if (delay > 0)
                yield return new WaitForSeconds(delay);

            if (!TryApplyUpdate(update))
            {
                updateQueue.Clear();
                processRoutine = null;
                yield break;
            }
        }
        processRoutine = null;
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
