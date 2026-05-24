using UnityEngine;

public class PowerConnectionTrigger : MonoBehaviour
{
    [Header("PRIMARY DETAILS:")]
    public bool isPowered = false;
    public Color currentPowerColor = Color.white;
    public PowerSource parentPowerSource;

    [Header("SECONDARY DETAILS:")]
    public PowerCube parentCubeScript;
    public PowerCube neighboursCubeScript;
    public PowerConnectionTrigger currentNeighbor;
    public int distanceFromSource = 0;

    private void Awake()
    {
        parentCubeScript = GetComponentInParent<PowerCube>();
    }

    private void OnTriggerEnter(Collider other)
    {
        PowerConnectionTrigger neighbor = other.GetComponent<PowerConnectionTrigger>();
        if (neighbor == null) return;

        // Guard: prevent self-connection
        if (neighbor == this) return;

        // Guard: prevent re-setting same neighbor repeatedly (no change = skip)
        if (currentNeighbor == neighbor) return;

        // Store neighbor's cube for inspector/debug view only
        currentNeighbor = neighbor;
        neighboursCubeScript = neighbor.parentCubeScript;
    }

    private void OnTriggerExit(Collider other)
    {
        PowerConnectionTrigger neighbor = other.GetComponent<PowerConnectionTrigger>();
        if (neighbor == null) return;
      
        // Guard: only clear if this is the actual active connection
        if (currentNeighbor == neighbor)
        {
            currentNeighbor = null;
            neighboursCubeScript = null;
        }
    }

    public void ClearPowerState()
    {
        isPowered = false;
        currentPowerColor = Color.white;
        distanceFromSource = 0;
    }
}