using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Lever : MonoBehaviour
{
    private const int SocketIndex = 0;

    [System.Serializable]
    private class SocketSlot
    {
        public GameObject socketObject;
        public int requiredColorIndex;
        public int requiredMw = 1;
        public int allocatedMw;
        public PowerSource poweringSource;
    }

    [SerializeField] private SocketSlot socket = new SocketSlot();
    [SerializeField] private Transform leverHandle;
    [SerializeField] private List<GameObject> connectedDevices = new List<GameObject>();
    [SerializeField] private bool isPowered;

    private readonly List<MediumDoor> mediumDoors = new List<MediumDoor>();

    private bool isLeverMoving;
    private bool isAtPositiveAngle = true;

    private void Awake()
    {
        Debug.Assert(HasSolidClickCollider(), $"{nameof(Lever)} on {name} requires at least one non-trigger collider for clicks and blocking cubes.", this);
        Debug.Assert(leverHandle != null, $"{nameof(Lever)} on {name} requires a lever handle assigned.", this);

        BuildConnectedDeviceLists();
        InitialSocketConfiguration();
    }

    private bool HasSolidClickCollider()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>();

        foreach (Collider childCollider in colliders)
        {
            if (!childCollider.isTrigger)
                return true;
        }

        return false;
    }

    // Called by MouseInteraction when this lever is clicked.
    public void MouseClickDetected()
    {
        if (isLeverMoving)
            return;

        StartCoroutine(LeverSwitchingMovement());

        if (!isPowered)
            return;

        NotifyConnectedDevices();
    }

    // Called by a DevicePowerSocket when its power state changes.
    public void UpdateSocketStateChangeToOwner(int ownerIndex, int allocatedMw, PowerSource poweringSource)
    {
        if (ownerIndex != SocketIndex)
            return;

        socket.allocatedMw = allocatedMw;
        socket.poweringSource = poweringSource;
        RefreshPoweredState();
    }

    // Builds typed device lists once from the inspector object list.
    private void BuildConnectedDeviceLists()
    {
        mediumDoors.Clear();

        foreach (GameObject deviceObject in connectedDevices)
        {
            if (deviceObject == null)
                continue;

            MediumDoor mediumDoor = deviceObject.GetComponent<MediumDoor>();
            if (mediumDoor != null)
                mediumDoors.Add(mediumDoor);
        }
    }

    // Claims the assigned socket once and pushes required color and MW to it.
    private void InitialSocketConfiguration()
    {
        Debug.Assert(socket.socketObject != null, $"{nameof(Lever)} on {name} requires a socket object assigned.", this);

        if (socket.socketObject == null)
            return;

        DevicePowerSocket device = socket.socketObject.GetComponent<DevicePowerSocket>();
        Debug.Assert(device != null, $"{nameof(Lever)} on {name} socket object requires a {nameof(DevicePowerSocket)}.", this);

        device.InitialSocketConfiguration(SocketIndex, socket.requiredColorIndex, socket.requiredMw);
        RefreshPoweredState();
    }

    private void RefreshPoweredState()
    {
        isPowered = socket.allocatedMw >= socket.requiredMw;
    }

    // Lerps the lever handle local X between +20 and -20.
    private IEnumerator LeverSwitchingMovement()
    {
        isLeverMoving = true;

        float startAngle = isAtPositiveAngle ? 20f : -20f;
        float endAngle = isAtPositiveAngle ? -20f : 20f;
        float elapsed = 0f;

        while (elapsed < 0.25f)
        {
            elapsed += Time.deltaTime;
            float angleX = Mathf.Lerp(startAngle, endAngle, Mathf.Clamp01(elapsed / 0.25f));
            leverHandle.localRotation = Quaternion.Euler(angleX, 0f, 0f);
            yield return null;
        }

        leverHandle.localRotation = Quaternion.Euler(endAngle, 0f, 0f);
        isAtPositiveAngle = !isAtPositiveAngle;
        isLeverMoving = false;
    }

    // Calls OnLeverOperated on every cached connected device.
    private void NotifyConnectedDevices()
    {
        foreach (MediumDoor mediumDoor in mediumDoors)
            mediumDoor.OnLeverOperated();
    }
}
