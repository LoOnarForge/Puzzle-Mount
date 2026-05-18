using UnityEngine;

/// <summary>
/// Sits on the ReceiverCube root.
/// Receives color and power from PowerReceiverTrigger.
/// Passes that information to whichever final receiver is assigned.
/// Only one final receiver should be assigned at a time, the rest left empty.
/// </summary>
public class PowerReceiverBase : MonoBehaviour
{
    [Header("FINAL RECEIVERS - assign only one")]
    public EndGateController door;
    public LeverReceiver lever;

    [Header("DEBUG")]
    [SerializeField] private bool isPowered = false;
    [SerializeField] private Color currentColor = Color.clear;
    [SerializeField] private int currentPower = 0;

    /// <summary>Called by PowerReceiverTrigger when a powered face is touching.</summary>
    public void OnPowerConnected(Color incomingColor, int incomingPower)
    {
        if (isPowered) return;

        isPowered = true;
        currentColor = incomingColor;
        currentPower = incomingPower;

        door?.PassColorAndPower(incomingColor, incomingPower);
        lever?.PassColorAndPower(incomingColor, incomingPower);
    }

    /// <summary>Called by PowerReceiverTrigger when the powered face exits.</summary>
    public void OnPowerDisconnected()
    {
        if (!isPowered) return;

        isPowered = false;
        currentColor = Color.clear;
        currentPower = 0;

        door?.OnPowerLost();
        lever?.OnPowerLost();
    }
}
