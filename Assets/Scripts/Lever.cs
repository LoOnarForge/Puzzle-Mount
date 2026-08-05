using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class Lever : MonoBehaviour
{
    private const int SocketIndex = 0;
    private const float LeverAnglePositive = 20f;
    private const float LeverAngleNegative = -20f;
    private const float LeverMoveDuration = 0.25f;

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

    private TimCubeController timCubeController;
    private Transform timTransform;
    private bool isLeverMoving;
    private bool isAtPositiveAngle = true;
    private float leverHandleLocalY;
    private float leverHandleLocalZ;

    private void Awake()
    {
        Debug.Assert(HasSolidClickCollider(), $"{nameof(Lever)} on {name} requires at least one non-trigger collider for clicks and blocking cubes.", this);
        Debug.Assert(leverHandle != null, $"{nameof(Lever)} on {name} requires a lever handle assigned.", this);

        CacheLeverHandleRotation();
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

    private void CacheLeverHandleRotation()
    {
        if (leverHandle == null)
            return;

        Vector3 localEuler = leverHandle.localEulerAngles;
        leverHandleLocalY = localEuler.y;
        leverHandleLocalZ = localEuler.z;
        leverHandle.localRotation = Quaternion.Euler(LeverAnglePositive, leverHandleLocalY, leverHandleLocalZ);
        isAtPositiveAngle = true;
    }

    private void Start()
    {
        timCubeController = FindAnyObjectByType<TimCubeController>();
        Debug.Assert(timCubeController != null, $"{nameof(Lever)} on {name} requires a {nameof(TimCubeController)} in the scene.", this);

        if (timCubeController != null)
            timTransform = timCubeController.transform;
    }

    private void Update()
    {
        TryDetectLeverClick();
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

    private void TryDetectLeverClick()
    {
        if (Mouse.current == null || Camera.main == null || timCubeController == null || timTransform == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, timCubeController.interactionLayer))
            return;

        Lever hitLever = hit.collider.GetComponentInParent<Lever>();
        if (hitLever != this)
            return;

        float distance = Vector3.ProjectOnPlane(transform.position - timTransform.position, Vector3.up).magnitude;
        if (distance > timCubeController.maxRotationDistance)
            return;

        OperateLever();
    }

    // Animates the lever handle. Notifies connected devices only when powered.
    private void OperateLever()
    {
        if (isLeverMoving)
            return;

        StartCoroutine(AnimateLeverHandle());

        if (!isPowered)
            return;

        NotifyConnectedDevices();
    }

    // Lerps the lever handle local X between +20 and -20.
    private IEnumerator AnimateLeverHandle()
    {
        isLeverMoving = true;

        float startAngle = isAtPositiveAngle ? LeverAnglePositive : LeverAngleNegative;
        float endAngle = isAtPositiveAngle ? LeverAngleNegative : LeverAnglePositive;
        float elapsed = 0f;

        while (elapsed < LeverMoveDuration)
        {
            elapsed += Time.deltaTime;
            float angleX = Mathf.Lerp(startAngle, endAngle, Mathf.Clamp01(elapsed / LeverMoveDuration));
            leverHandle.localRotation = Quaternion.Euler(angleX, leverHandleLocalY, leverHandleLocalZ);
            yield return null;
        }

        leverHandle.localRotation = Quaternion.Euler(endAngle, leverHandleLocalY, leverHandleLocalZ);
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
