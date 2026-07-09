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
        public RunodePower cube;
        public int faceIndex;
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

    /// <summary>
    /// Clears the pending animation queue.
    /// </summary>
    public void ResetQueue()
    {
        if (processRoutine != null)
        {
            StopCoroutine(processRoutine);
            processRoutine = null;
        }

        // Snap everything in the queue to its final state instantly
        // to ensure visual state matches logical state before next recalc.
        while (updateQueue.Count > 0)
        {
            var update = updateQueue.Dequeue();
            ApplyVisualDirect(update.cube, update.faceIndex, update.color, update.isObstructed);
        }
    }

    /// <summary>
    /// Queues a visual update for a specific cube face.
    /// </summary>
    public void UpdateFaceVisuals(RunodePower cube, int faceIndex, Color color, bool isObstructed, bool instant = false)
    {
        if (cube == null || faceIndex < 0 || faceIndex >= cube.allFaces.Count) return;

        // Skip if the visual is already at the target state
        var face = cube.allFaces[faceIndex];
        if (face.faceSprite != null && face.faceSprite.color == color)
        {
            return;
        }

        if (instant)
        {
            ApplyVisualDirect(cube, faceIndex, color, isObstructed);
            return;
        }

        // Detect if we are depowering (turning to white)
        bool isDepowering = (color == Color.white);

        updateQueue.Enqueue(new FaceUpdate 
        { 
            cube = cube, 
            faceIndex = faceIndex, 
            color = color, 
            isObstructed = isObstructed,
            isDepowering = isDepowering
        });

        if (processRoutine == null)
        {
            processRoutine = StartCoroutine(ProcessQueue());
        }
    }

    private IEnumerator ProcessQueue()
    {
        while (updateQueue.Count > 0)
        {
            var update = updateQueue.Dequeue();
            ApplyVisualDirect(update.cube, update.faceIndex, update.color, update.isObstructed);
            
            float delay = update.isDepowering ? depowerDelay : propagationDelay;
            if (delay > 0)
            {
                yield return new WaitForSeconds(delay);
            }
        }
        processRoutine = null;
    }

    private void ApplyVisualDirect(RunodePower cube, int faceIndex, Color color, bool isObstructed)
    {
        if (cube == null || faceIndex < 0 || faceIndex >= cube.allFaces.Count) return;
        
        var face = cube.allFaces[faceIndex];
        if (face.faceSprite == null) return;

        if (isObstructed)
        {
            face.faceSprite.color = new Color(0.08f, 0.08f, 0.08f);
            return;
        }

        face.faceSprite.color = color;
    }
}
