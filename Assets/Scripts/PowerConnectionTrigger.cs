using UnityEngine;

public class PowerConnectionTrigger : MonoBehaviour
{
    [Header("PRIMARY DETAILS:")]
    public bool isPowered = false;
    public bool isObstructed = false;
    public Color currentPowerColor = Color.white;
    public PowerSource parentPowerSource;

    [Header("SECONDARY DETAILS:")]
    public RunodePower parentRunodePower;
    public RunodePower neighboursRunodePower;
    public PowerConnectionTrigger currentNeighbor;
    public int distanceFromSource = 0;

    private void Awake()
    {
        parentRunodePower = GetComponentInParent<RunodePower>();
    }

    private void OnTriggerEnter(Collider other)
    {
        PowerConnectionTrigger neighbor = other.GetComponent<PowerConnectionTrigger>();
        if (neighbor == null) return;
        if (neighbor == this) return;
        if (currentNeighbor == neighbor) return;

        currentNeighbor = neighbor;
        neighboursRunodePower = neighbor.parentRunodePower;
    }

    private void OnTriggerExit(Collider other)
    {
        PowerConnectionTrigger neighbor = other.GetComponent<PowerConnectionTrigger>();
        if (neighbor == null) return;

        if (currentNeighbor == neighbor)
        {
            currentNeighbor = null;
            neighboursRunodePower = null;
        }
    }

    public void ClearPowerState()
    {
        isPowered = false;
        currentPowerColor = Color.white;
        distanceFromSource = 0;
    }

    // Sets obstructed state. BFS filters via isObstructed; neighbor reference is preserved for restoration.
    public void SetObstructed(bool obstructed)
    {
        isObstructed = obstructed;
    }
}