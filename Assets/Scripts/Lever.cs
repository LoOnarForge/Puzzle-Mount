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
    private readonly List<Elevator> elevators = new List<Elevator>();

    private bool isLeverMoving;
    private bool isAtPositiveAngle = true;

    private void Awake()
    {
        BuildConnectedDeviceLists();
        InitialSocketConfiguration();
    }

     // Called by a DevicePowerSocket when its power state changes. 
    public void UpdateSocketStateToOwner(int ownerIndex, int allocatedMw, PowerSource poweringSource)
    {
        if (ownerIndex != SocketIndex)
            return;

        socket.allocatedMw = allocatedMw;
        socket.poweringSource = poweringSource;
        RefreshLeverState();
    }

   
    
    public void MouseClickDetected()
    {
        if (isLeverMoving)
            return;

        StartCoroutine(LeverSwitchingMovement());

        if (!isPowered)
            return;

        NotifyConnectedDevices();
    }

    private void RefreshLeverState()
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

        foreach (Elevator elevator in elevators)
            elevator.OnLeverOperated();
    }

    // Builds typed device lists once from the inspector object list.
    private void BuildConnectedDeviceLists()
    {
        mediumDoors.Clear();
        elevators.Clear();

        foreach (GameObject deviceObject in connectedDevices)
        {
            if (deviceObject == null)
                continue;

            MediumDoor mediumDoor = deviceObject.GetComponent<MediumDoor>();
            if (mediumDoor != null)
                mediumDoors.Add(mediumDoor);

            Elevator elevator = deviceObject.GetComponent<Elevator>();
            if (elevator != null)
                elevators.Add(elevator);
        }
    }

    // Claims the assigned socket once and pushes required color and MW to it.
    private void InitialSocketConfiguration()
    {
        if (socket.socketObject == null)
            return;

        DevicePowerSocket device = socket.socketObject.GetComponent<DevicePowerSocket>();
        device.InitialSocketConfiguration(SocketIndex, socket.requiredColorIndex, socket.requiredMw);
        RefreshLeverState();
    }
}
