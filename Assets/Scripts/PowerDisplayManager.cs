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

    private struct FaceUpdate
    {
        public RunodeFace face;
        public Color color;
        public bool isObstructed;
        public bool isDepowering;
    }

    private Queue<FaceUpdate> updateQueue = new Queue<FaceUpdate>();
    private Coroutine processRoutine;

    private void Awake()
    {
        Instance = this;
    }

    // Discards all pending updates without applying them. Use this to abort active animations.
    public void DiscardQueue()
    {
        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }
        updateQueue.Clear();
        isFirstInSequence = true;
    }

    // Clears the pending animation queue, snapping all queued updates to their final state.
    public void ResetQueue()
    {
        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }

        while (updateQueue.Count > 0)
        {
            var update = updateQueue.Dequeue();
            ApplyVisualDirect(update.face, update.color, update.isObstructed);
        }
        isFirstInSequence = true;
    }

    // Queues a visual update for a specific face.
    public void UpdateFaceVisuals(RunodeFace face, Color color, bool isObstructed, bool instant = false)
    {
        if (face == null) return;

        if (face.faceSprite != null && face.faceSprite.color == color)
            return;

        if (instant)
        {
            ApplyVisualDirect(face, color, isObstructed);
            return;
        }

        bool isDepowering = (color == Color.white);

        updateQueue.Enqueue(new FaceUpdate
        {
            face = face,
            color = color,
            isObstructed = isObstructed,
            isDepowering = isDepowering
        });

        if (processRoutine == null)
            processRoutine = StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        while (updateQueue.Count > 0)
        {
            var update = updateQueue.Dequeue();
            ApplyVisualDirect(update.face, update.color, update.isObstructed);

            float delay;
            if (update.isDepowering)
            {
                delay = depowerDelay;
            }
            else
            {
                if (isFirstInSequence)
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
        }
        processRoutine = null;
    }

    private void ApplyVisualDirect(RunodeFace face, Color color, bool isObstructed)
    {
        if (face == null) return;

        face.powerColorLayer = color;
        face.UpdateSpriteVisuals();
    }
}
