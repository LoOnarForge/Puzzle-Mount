using System.Collections.Generic;
using UnityEngine;

/// Place in the scene as a fixed power origin.
/// Registers itself with PowerManager on Start.
/// Runs BFS outward through connected triggers when PowerManager requests recalculation.
[DefaultExecutionOrder(-50)]
public class PowerSource : MonoBehaviour
{
    [Header("POWER SOURCE")]
    public int colorIndex;
    [HideInInspector] public Color powerColor;

    [Header("POWER OPTIONS")]
    public int maxPower = 10;

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

    /// BFS from this source outward through all reachable connected triggers.
    /// Each cube reached is marked powered with this source's color and distance.
    /// Total cubes powered across all branches cannot exceed maxPower.
    /// If a cube is already powered by a different source, or a loop is detected: game over.
    public void RunBFS()
    {
        Queue<PowerConnectionTrigger> queue = new Queue<PowerConnectionTrigger>();
        HashSet<PowerConnectionTrigger> visited = new HashSet<PowerConnectionTrigger>();
        int totalCubesPowered = 0;

        EnqueueSourceTrigger(upTrigger, queue, visited);
        EnqueueSourceTrigger(rightTrigger, queue, visited);
        EnqueueSourceTrigger(downTrigger, queue, visited);
        EnqueueSourceTrigger(leftTrigger, queue, visited);

        while (queue.Count > 0)
        {
            PowerConnectionTrigger current = queue.Dequeue();

            if (visited.Contains(current)) continue;
            visited.Add(current);

            PowerConnectionTrigger neighbor = current.currentNeighbor;
            if (neighbor == null) continue;

            if (visited.Contains(neighbor))
            {
                TriggerGameOver();
                return;
            }

            PowerCube neighborCube = neighbor.cubeScript;
            if (neighborCube == null) continue;

            if (neighborCube.IsPowered && neighborCube.poweredBySource != this)
            {
                TriggerGameOver();
                return;
            }

            if (totalCubesPowered >= maxPower) continue;

            int distance = current.distanceFromSource + 1;

            neighbor.isPowered = true;
            neighbor.currentPowerColor = powerColor;
            neighbor.distanceFromSource = distance;

            if (!neighborCube.IsPowered)
            {
                totalCubesPowered++;
                neighborCube.SetPowered(this, powerColor, distance);
            }

            foreach (PowerConnectionTrigger outgoing in neighborCube.GetAllTriggers())
            {
                if (outgoing == neighbor) continue;
                if (visited.Contains(outgoing)) continue;

                outgoing.isPowered = true;
                outgoing.currentPowerColor = powerColor;
                outgoing.distanceFromSource = distance;

                queue.Enqueue(outgoing);
            }
        }
    }

    private void EnqueueSourceTrigger(PowerConnectionTrigger trigger, Queue<PowerConnectionTrigger> queue, HashSet<PowerConnectionTrigger> visited)
    {
        if (trigger == null) return;

        trigger.isPowered = true;
        trigger.currentPowerColor = powerColor;
        trigger.distanceFromSource = 0;

        queue.Enqueue(trigger);
    }

    private void TriggerGameOver()
    {
        Debug.Log("[PowerSource] Game Over - illegal connection detected (loop or color mixing).");
        // TODO: hook into your game state manager here.
    }
}