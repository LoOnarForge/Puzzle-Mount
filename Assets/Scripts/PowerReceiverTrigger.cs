using UnityEngine;

/// <summary>
/// Sits on the active connection point collider on the ReceiverCube.
/// Detects when a powered cube face touches it and passes color and power to PowerReceiverBase.
/// Power stops here, nothing propagates beyond this point.
/// </summary>
public class PowerReceiverTrigger : MonoBehaviour
{
    private PowerReceiverBase receiverBase;

    private void Awake()
    {
        receiverBase = GetComponentInParent<PowerReceiverBase>();
    }

    private void OnTriggerStay(Collider other)
    {
        PowerConnectionTrigger incomingFace = other.GetComponent<PowerConnectionTrigger>();
        if (incomingFace == null) return;
        if (!incomingFace.isPowered) return;
        if (incomingFace.parentPowerSource == null) return;

        PowerSource source = incomingFace.parentPowerSource;
        Color incomingColor = source.powerColor;
        int remainingPower = source.maxPower - source.powerUsage;

        receiverBase.OnPowerConnected(incomingColor, remainingPower);
    }

    private void OnTriggerExit(Collider other)
    {
        PowerConnectionTrigger incomingFace = other.GetComponent<PowerConnectionTrigger>();
        if (incomingFace == null) return;

        receiverBase.OnPowerDisconnected();
    }
}
