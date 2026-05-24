using UnityEngine;

/// Attach to each trigger child on a PowerCube face or PowerSource face.
/// Passively detects neighboring triggers and asks PowerManager to recalculate.
/// Does NOT propagate power - that is handled by PowerSource via BFS.
public class PowerConnectionTrigger : MonoBehaviour
{
    [Header("REFERENCES")]
    public PowerCube cubeScript;
    public PowerSource parentPowerSource;

    [Header("STATE")]
    public PowerConnectionTrigger currentNeighbor;
    public bool isPowered = false;
    public Color currentPowerColor = Color.white;
    public int distanceFromSource = 0;

    private void OnTriggerEnter(Collider other)
    {
        PowerConnectionTrigger neighbor = other.GetComponent<PowerConnectionTrigger>();
        if (neighbor == null) return;

        currentNeighbor = neighbor;
    }

    private void OnTriggerExit(Collider other)
    {
        PowerConnectionTrigger neighbor = other.GetComponent<PowerConnectionTrigger>();
        if (neighbor == null) return;

        if (currentNeighbor == neighbor)
            currentNeighbor = null;
    }

    /// Clears power state before BFS recalculation.
    public void ClearPowerState()
    {
        isPowered = false;
        currentPowerColor = Color.white;
        distanceFromSource = 0;
    }
}