using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Piston : MonoBehaviour
{
    private const int SocketCount = 2;
    private const float GridUnit = 1f;
    private const float DefaultMoveSpeed = 5f;
    private const float PushDetectHalf = 0.45f;

    [System.Serializable]
    private class SocketSlot
    {
        public GameObject socketObject;
        public bool isEnabled;
        public int requiredColorIndex;
        public int requiredMw = 1;
        public int allocatedMw;
        public PowerSource poweringSource;
    }

    [SerializeField] private SocketSlot[] sockets = new SocketSlot[SocketCount]
    {
        new SocketSlot(),
        new SocketSlot()
    };

    [SerializeField] private Transform pistonFace;
    [SerializeField] private int maxStage = 3;
    [SerializeField] private float moveSpeed = DefaultMoveSpeed;
    [SerializeField] private bool isPowered;

    private Vector3 faceHomeLocalPosition;
    private Vector3 facePushLocalDirection;
    private int currentStage;
    private int stageDirection = 1;
    private bool isMoving;
    private readonly Collider[] overlapHits = new Collider[16];

    private void Awake()
    {
        Debug.Assert(pistonFace != null, $"{nameof(Piston)} on {name} requires Pistons Face assigned.", this);
        faceHomeLocalPosition = pistonFace.localPosition;
        facePushLocalDirection = (pistonFace.localRotation * Vector3.forward).normalized;
        InitialSocketConfiguration();
    }

    // Called by a DevicePowerSocket when its power state changes.
    public void UpdateSocketStateToOwner(int ownerIndex, int allocatedMw, PowerSource poweringSource)
    {
        if (ownerIndex < 0 || ownerIndex >= sockets.Length)
            return;

        sockets[ownerIndex].allocatedMw = allocatedMw;
        sockets[ownerIndex].poweringSource = poweringSource;
        RefreshPistonState();
    }

    // Called by a powered Lever when the player operates it.
    public void OnLeverOperated()
    {
        if (!isPowered || isMoving || maxStage <= 0)
            return;

        int nextStage = currentStage + stageDirection;
        if (nextStage > maxStage)
        {
            nextStage = maxStage - 1;
            stageDirection = -1;
        }
        else if (nextStage < 0)
        {
            nextStage = 1;
            stageDirection = 1;
        }

        StartCoroutine(MoveOneStage(nextStage));
    }

    // Claims assigned sockets once and pushes required color and MW to each.
    private void InitialSocketConfiguration()
    {
        for (int i = 0; i < sockets.Length; i++)
        {
            SocketSlot slot = sockets[i];

            if (slot.socketObject == null)
                continue;

            if (!slot.isEnabled)
            {
                slot.socketObject.SetActive(false);
                slot.allocatedMw = 0;
                slot.poweringSource = null;
                continue;
            }

            DevicePowerSocket device = slot.socketObject.GetComponent<DevicePowerSocket>();
            if (device == null)
                continue;

            slot.socketObject.SetActive(true);
            device.InitialSocketConfiguration(i, slot.requiredColorIndex, slot.requiredMw);
        }

        RefreshPistonState();
    }

    private void RefreshPistonState()
    {
        bool hasEnabledSocket = false;
        bool allPowered = true;

        foreach (SocketSlot slot in sockets)
        {
            if (!slot.isEnabled || slot.socketObject == null)
                continue;

            hasEnabledSocket = true;

            if (slot.allocatedMw < slot.requiredMw)
                allPowered = false;
        }

        isPowered = hasEnabledSocket && allPowered;
    }

    private IEnumerator MoveOneStage(int nextStage)
    {
        isMoving = true;

        Vector3 worldPush = RunodeMovement.GetCardinalAxis(pistonFace.forward);
        if (nextStage > currentStage && Mathf.Abs(worldPush.y) < 0.5f && !TryPushRunodes(worldPush))
        {
            isMoving = false;
            yield break;
        }

        Vector3 start = pistonFace.localPosition;
        Vector3 end = faceHomeLocalPosition + facePushLocalDirection * (nextStage * GridUnit);
        float duration = moveSpeed > 0f ? GridUnit / moveSpeed : 0f;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            pistonFace.localPosition = Vector3.Lerp(start, end, t);
            yield return null;
        }

        pistonFace.localPosition = end;
        currentStage = nextStage;
        isMoving = false;
    }

    private bool TryPushRunodes(Vector3 worldPush)
    {
        Vector3 center = pistonFace.position + worldPush * GridUnit * 0.5f;
        int count = Physics.OverlapBoxNonAlloc(center, Vector3.one * PushDetectHalf, overlapHits, Quaternion.identity);

        HashSet<RunodeMovement> cubesInFront = new HashSet<RunodeMovement>();
        for (int i = 0; i < count; i++)
        {
            RunodeMovement runode = overlapHits[i].GetComponentInParent<RunodeMovement>();
            if (runode != null && !runode.IsBusy)
                cubesInFront.Add(runode);
        }

        if (cubesInFront.Count == 0)
            return true;

        foreach (RunodeMovement cube in cubesInFront)
        {
            if (!cube.CanPushSingle(worldPush))
                return false;
        }

        foreach (RunodeMovement cube in cubesInFront)
            cube.PushSingle(worldPush);

        return true;
    }
}
