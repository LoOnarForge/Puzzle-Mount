using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Piston : MonoBehaviour
{
    private const int SocketCount = 2;
    private const float GridUnit = 1f;
    private const float DefaultMoveSpeed = 5f;
    private const float PushDetectHalf = 0.45f;
    private const string TimLayerName = "TimJones";

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
    [SerializeField] private int startingStage;
    [SerializeField] private bool startExtending = true;
    [SerializeField] private float moveSpeed = DefaultMoveSpeed;
    [SerializeField] private LayerMask obstructionMask;
    [SerializeField] private bool isPowered;

    private Vector3 faceHomeLocalPosition;
    private int currentStage;
    private int stageDirection = 1;
    private bool isMoving;
    private readonly Collider[] overlapHits = new Collider[16];

    private void Awake()
    {
        Debug.Assert(pistonFace != null, $"{nameof(Piston)} on {name} requires Pistons Face assigned.", this);
        faceHomeLocalPosition = pistonFace.localPosition;
        if (obstructionMask.value == 0)
            obstructionMask = ~LayerMask.GetMask(TimLayerName);

        currentStage = Mathf.Clamp(startingStage, 0, maxStage);
        stageDirection = startExtending ? 1 : -1;
        ApplyStagePosition(currentStage);
        InitialSocketConfiguration();
    }

    private void OnValidate()
    {
        maxStage = Mathf.Max(0, maxStage);
        startingStage = Mathf.Clamp(startingStage, 0, maxStage);
    }

    private void ApplyStagePosition(int stage)
    {
        pistonFace.localPosition = faceHomeLocalPosition + Vector3.forward * (stage * GridUnit);
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

        StartCoroutine(MoveToStage(nextStage));
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
                DevicePowerSocket.SetSocketHierarchyActive(slot.socketObject, false);
                slot.allocatedMw = 0;
                slot.poweringSource = null;
                continue;
            }

            DevicePowerSocket device = slot.socketObject.GetComponent<DevicePowerSocket>();
            if (device == null)
                continue;

            DevicePowerSocket.SetSocketHierarchyActive(slot.socketObject, true);
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

    private IEnumerator MoveToStage(int nextStage)
    {
        isMoving = true;

        Vector3 worldPush = RunodeMovement.GetCardinalAxis(transform.forward);
        if (nextStage > currentStage && !CanAdvanceStage(worldPush))
        {
            stageDirection = 1;

            if (currentStage > 0)
                yield return AnimateToStage(0);

            isMoving = false;
            yield break;
        }

        yield return AnimateToStage(nextStage);
        isMoving = false;
    }

    private IEnumerator AnimateToStage(int nextStage)
    {
        Vector3 start = pistonFace.localPosition;
        Vector3 end = faceHomeLocalPosition + Vector3.forward * (nextStage * GridUnit);
        float distance = Vector3.Distance(start, end);
        float duration = moveSpeed > 0f ? distance / moveSpeed : 0f;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            pistonFace.localPosition = Vector3.Lerp(start, end, t);
            yield return null;
        }

        pistonFace.localPosition = end;
        currentStage = nextStage;
    }

    private bool CanAdvanceStage(Vector3 worldPush)
    {
        Vector3 center = pistonFace.position + worldPush * GridUnit * 0.5f;
        int count = Physics.OverlapBoxNonAlloc(center, Vector3.one * PushDetectHalf, overlapHits, Quaternion.identity);

        bool isHorizontal = Mathf.Abs(worldPush.y) < 0.5f;
        HashSet<RunodeMovement> cubesInFront = new HashSet<RunodeMovement>();
        HashSet<CharacterMovement> timsInFront = new HashSet<CharacterMovement>();

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapHits[i];
            if (hit.isTrigger || hit.transform.IsChildOf(transform))
                continue;

            if ((obstructionMask.value & (1 << hit.gameObject.layer)) == 0)
            {
                CharacterMovement tim = hit.GetComponentInParent<CharacterMovement>();
                if (tim != null && !tim.IsDead)
                    timsInFront.Add(tim);

                continue;
            }

            RunodeMovement runode = hit.GetComponentInParent<RunodeMovement>();
            if (runode != null)
            {
                if (runode.IsBusy)
                    return false;

                cubesInFront.Add(runode);
                continue;
            }

            return false;
        }

        if (!isHorizontal)
            return true;

        foreach (RunodeMovement cube in cubesInFront)
        {
            if (!cube.CanPushSingle(worldPush))
                return false;
        }

        foreach (RunodeMovement cube in cubesInFront)
            cube.PushSingle(worldPush);

        foreach (CharacterMovement tim in timsInFront)
        {
            if (!CanTimMoveOneStep(tim, worldPush))
                tim.ApplyDeathToss(worldPush);
        }

        return true;
    }

    private bool CanTimMoveOneStep(CharacterMovement tim, Vector3 worldPush)
    {
        Vector3 pushDir = worldPush.normalized;
        Vector3 target = tim.transform.position + pushDir * GridUnit;
        Vector3 checkCenter = new Vector3(
            Mathf.Round(target.x),
            tim.transform.position.y + 0.5f,
            Mathf.Round(target.z));

        int count = Physics.OverlapBoxNonAlloc(checkCenter, Vector3.one * PushDetectHalf, overlapHits, Quaternion.identity);

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapHits[i];
            if (hit.isTrigger)
                continue;

            if (hit.transform.IsChildOf(tim.transform))
                continue;

            if (hit.transform.IsChildOf(transform))
                continue;

            if ((obstructionMask.value & (1 << hit.gameObject.layer)) == 0)
                continue;

            return false;
        }

        return true;
    }
}
