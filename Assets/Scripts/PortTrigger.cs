using UnityEngine;

public enum PortID { Up, Right, Down, Left }

public class PortTrigger : MonoBehaviour
{
    public RunodeFace2 parentFace;
    public PortID portName;
    public LayerMask obstructionMask;

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & obstructionMask) != 0)
            parentFace.SetInternalPortBlocked(portName, true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & obstructionMask) != 0)
            parentFace.SetInternalPortBlocked(portName, false);
    }
}
