using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PowerDisplayManager : MonoBehaviour
{
    public static PowerDisplayManager Instance { get; private set; }

    [Header("SETTINGS")]
    public float propagationDelay = 0.05f;
    public float depowerDelay = 0.02f;

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

            float delay = update.isDepowering ? depowerDelay : propagationDelay;
            if (delay > 0)
                yield return new WaitForSeconds(delay);
        }
        processRoutine = null;
    }

    private void ApplyVisualDirect(RunodeFace face, Color color, bool isObstructed)
    {
        if (face == null || face.faceSprite == null) return;

        if (isObstructed)
        {
            face.faceSprite.color = new Color(0.08f, 0.08f, 0.08f);
            return;
        }

        face.faceSprite.color = color;
    }
}
