using System.Collections.Generic;
using UnityEngine;

// Place in the scene as a fixed power origin.
// Registers itself with PowerManager on Start.
// Runs BFS outward through connected triggers when PowerManager requests recalculation.
[DefaultExecutionOrder(-50)]
public class PowerSource : MonoBehaviour
{
    [Header("POWER SOURCE")]
    public int colorIndex;
    [HideInInspector] public Color powerColor;

    [Header("POWER OPTIONS")]
    public int maxPower = 10;
    public int currentCubesPowered = 0;

    [Header("TRIGGER REFERENCES")]
    public PowerConnectionTrigger upTrigger;
    public PowerConnectionTrigger rightTrigger;
    public PowerConnectionTrigger downTrigger;
    public PowerConnectionTrigger leftTrigger;

    [Header("SPRITE REFERENCE")]
    public SpriteRenderer powerSprite;

    private void Start()
    {
        powerColor = ColorManager.Instance.GetColor(colorIndex);
        UpdateSourceVisual();
        PowerManager.Instance.RegisterSource(this);
        PowerManager.Instance.RequestPowerFlowCheck();
    }

    private void UpdateSourceVisual()
    {
        if (powerSprite != null)
            powerSprite.color = powerColor;
    }

    // BFS from this source outward through all reachable connected triggers.
    // Each cube reached is marked powered with this source's color and distance.
    // Total cubes powered across all branches cannot exceed maxPower.
    // If a cube is already powered by a different source, or a loop is detected: game over.
    public void RunBFS()
    {
        Queue<PowerConnectionTrigger> queue = new Queue<PowerConnectionTrigger>();
        HashSet<PowerConnectionTrigger> visitedTriggers = new HashSet<PowerConnectionTrigger>();
        HashSet<RunodePower> visitedCubes = new HashSet<RunodePower>();
        
        currentCubesPowered = 0;

        EnqueueSourceTrigger(upTrigger, queue, visitedTriggers, visitedCubes);
        EnqueueSourceTrigger(rightTrigger, queue, visitedTriggers, visitedCubes);
        EnqueueSourceTrigger(downTrigger, queue, visitedTriggers, visitedCubes);
        EnqueueSourceTrigger(leftTrigger, queue, visitedTriggers, visitedCubes);

        while (queue.Count > 0)
        {
            PowerConnectionTrigger current = queue.Dequeue();
            RunodePower currentCube = current.parentRunodePower;

            // 1. External Hop: To a neighbor on a DIFFERENT cube (Physical connection)
            PowerConnectionTrigger neighbor = current.currentNeighbor;
            if (neighbor != null && neighbor.gameObject.activeInHierarchy && !neighbor.isObstructed && !visitedTriggers.Contains(neighbor))
            {
                RunodePower neighborCube = neighbor.parentRunodePower;
                bool isNewCube = neighborCube != null && !visitedCubes.Contains(neighborCube);

                if (isNewCube && currentCubesPowered >= maxPower)
                {
                    continue; 
                }

                if (neighbor.isPowered && neighbor.currentPowerColor != powerColor)
                {
                    TriggerGameOver();
                    return;
                }

                if (isNewCube)
                {
                    visitedCubes.Add(neighborCube);
                    currentCubesPowered++;
                }

                SetTriggerPowered(neighbor, current.distanceFromSource + 1, queue, visitedTriggers);
                if (neighborCube != null) neighborCube.MarkFacePowered(neighbor);
            }

            // 2. Internal Bridge Hop: Between faces on the SAME cube (Manual mapping)
            if (currentCube != null)
            {
                PowerConnectionTrigger internalBridge = currentCube.GetInternalNeighbor(current);
                if (internalBridge != null && internalBridge.gameObject.activeInHierarchy && !internalBridge.isObstructed && !visitedTriggers.Contains(internalBridge))
                {
                    SetTriggerPowered(internalBridge, current.distanceFromSource, queue, visitedTriggers);
                    currentCube.MarkFacePowered(internalBridge);
                }
            }

            // 3. Internal Face Traverse: Across the current face line path
            if (currentCube != null)
            {
                var faceNeighbors = currentCube.GetConnectedTriggersOnFace(current);
                foreach (var fn in faceNeighbors)
                {
                    if (fn.gameObject.activeInHierarchy && !visitedTriggers.Contains(fn))
                    {
                        SetTriggerPowered(fn, current.distanceFromSource, queue, visitedTriggers);
                    }
                }
            }
        }

        // Finalize: Refresh visuals for all cubes touched by this power flow
        foreach (var cube in visitedCubes)
        {
            cube.RefreshFaceVisuals(powerColor);
        }
    }

    private void EnqueueSourceTrigger(PowerConnectionTrigger trigger, Queue<PowerConnectionTrigger> queue, HashSet<PowerConnectionTrigger> visitedTriggers, HashSet<RunodePower> visitedCubes)
    {
        if (trigger == null || !trigger.gameObject.activeInHierarchy) return;
        
        trigger.isPowered = true;
        trigger.currentPowerColor = powerColor;
        trigger.distanceFromSource = 0;
        
        visitedTriggers.Add(trigger);
        queue.Enqueue(trigger);

        // If the source trigger is on a cube, count it
        if (trigger.parentRunodePower != null && !visitedCubes.Contains(trigger.parentRunodePower))
        {
            visitedCubes.Add(trigger.parentRunodePower);
            currentCubesPowered++;
        }
    }

    private void SetTriggerPowered(PowerConnectionTrigger trigger, int distance, Queue<PowerConnectionTrigger> queue, HashSet<PowerConnectionTrigger> visited)
    {
        trigger.isPowered = true;
        trigger.currentPowerColor = powerColor;
        trigger.distanceFromSource = distance;
        
        visited.Add(trigger);
        queue.Enqueue(trigger);
    }

    private void TriggerGameOver()
    {
        Debug.Log("[PowerSource] Game Over - illegal connection detected (loop or color mixing).");
        // TODO: hook into your game state manager here.
    }
}