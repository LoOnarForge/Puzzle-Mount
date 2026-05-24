using UnityEngine;

/// Sits on the active connection point collider on the ReceiverCube.
/// Detects when a powered trigger touches it and passes color and power to PowerReceiverBase.
/// Power stops here - nothing propagates beyond this point.
public class PowerReceiverTrigger : MonoBehaviour
{
    private PowerReceiverBase receiverBase;

    private void Awake()
    {
        receiverBase = GetComponentInParent<PowerReceiverBase>();
    }

    private void OnTriggerEnter(Collider other)
    {
        EvaluateContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        EvaluateContact(other);
    }

    private void OnTriggerExit(Collider other)
    {
        PowerConnectionTrigger incomingFace = other.GetComponent<PowerConnectionTrigger>();
        if (incomingFace == null) return;

        receiverBase.OnPowerDisconnected();
    }

    private void EvaluateContact(Collider other)
    {
        PowerConnectionTrigger incomingFace = other.GetComponent<PowerConnectionTrigger>();
        if (incomingFace == null) return;

        if (!incomingFace.isPowered)
        {
            receiverBase.OnPowerDisconnected();
            return;
        }

        receiverBase.OnPowerConnected(incomingFace.currentPowerColor, incomingFace.distanceFromSource);
    }
}
