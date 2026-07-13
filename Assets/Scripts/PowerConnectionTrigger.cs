using UnityEngine;

public class PowerConnectionTrigger : MonoBehaviour
{
    [Header("PRIMARY DETAILS:")]
    public bool isPowered = false;
    public bool isObstructed = false;
    public Color currentPowerColor = Color.white;

    [Header("SECONDARY DETAILS:")]
    public RunodePower parentRunodePower;
    public RunodeFace parentRunodeFace;
    public int distanceFromSource = 0;
    public int sourceMW = 0;

    private void Awake()
    {
        parentRunodePower = GetComponentInParent<RunodePower>();
        parentRunodeFace  = GetComponentInParent<RunodeFace>();
    }

    public void ClearPowerState()
    {
        isPowered = false;
        currentPowerColor = Color.white;
        distanceFromSource = 0;
        sourceMW = 0;
    }

    // Sets obstructed state. BFS filters via isObstructed; neighbor reference is preserved for restoration.
    public void SetObstructed(bool obstructed)
    {
        isObstructed = obstructed;
    }
}