using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Piston : MonoBehaviour
{
    private const int SocketCount = 2;
    private const float GridUnit = 1f;
    private const float DefaultMoveSpeed = 5f;
    private const float PushDetectHalf = 0.45f;
    private const float FaceTopTolerance = 0.1f;
    private const float TimCrushGap = 0.85f;
    private const float TimLaneTolerance = 0.55f;
    private const float TimHeightTolerance = 1.5f;
    private const float OpposingAxisDotThreshold = -0.9f;
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
    [SerializeField] private bool isVertical;
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
    private bool timKilledDuringMove;
    private Piston timKillPartner;
    private Collider faceCollider;
    private readonly Collider[] overlapHits = new Collider[16];

    private void Awake()
    {
        Debug.Assert(pistonFace != null, $"{nameof(Piston)} on {name} requires Pistons Face assigned.", this);
        faceCollider = pistonFace.GetComponent<Collider>();
        Debug.Assert(faceCollider != null, $"{nameof(Piston)} on {name} requires a collider on Pistons Face.", this);
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

    private Vector3 GetTravelAxisLocal()
    {
        return isVertical ? Vector3.up : Vector3.forward;
    }

    private Vector3 GetWorldPush()
    {
        return isVertical ? Vector3.up : RunodeMovement.GetCardinalAxis(transform.forward);
    }

    private void ApplyStagePosition(int stage)
    {
        pistonFace.localPosition = faceHomeLocalPosition + GetTravelAxisLocal() * (stage * GridUnit);
    }

    // True when the piston face is at stage 0.
    public bool IsRetracted => currentStage == 0;

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
        timKilledDuringMove = false;
        timKillPartner = null;

        Vector3 worldPush = GetWorldPush();
        if (nextStage > currentStage && !CanAdvanceStage(worldPush))
        {
            stageDirection = 1;

            if (currentStage > 0)
                yield return AnimateToStage(0);

            isMoving = false;
            yield break;
        }

        yield return AnimateToStage(nextStage);

        if (timKilledDuringMove)
        {
            stageDirection = 1;
            Piston partner = timKillPartner;
            timKillPartner = null;
            yield return RetractAfterTimKill(partner);
        }

        isMoving = false;
    }

    private IEnumerator AnimateToStage(int nextStage)
    {
        Vector3 start = pistonFace.localPosition;
        Vector3 end = faceHomeLocalPosition + GetTravelAxisLocal() * (nextStage * GridUnit);
        Vector3 startWorldPosition = pistonFace.position;
        Vector3 endWorldPosition = pistonFace.parent.TransformPoint(end);
        bool carryRunodes = isVertical;

        List<RunodeMovement> carriedRunodes = carryRunodes ? GetRunodesOnFace() : null;

        if (carryRunodes)
        {
            foreach (RunodeMovement runode in carriedRunodes)
                runode.SetKinematic(true);
        }

        float distance = Vector3.Distance(start, end);
        float duration = moveSpeed > 0f ? distance / moveSpeed : 0f;
        Vector3 previousWorldPosition = startWorldPosition;

        bool isExtendingHorizontally = !carryRunodes && nextStage > currentStage;
        Vector3 worldPush = GetWorldPush();

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            pistonFace.localPosition = Vector3.Lerp(start, end, t);

            if (carryRunodes)
            {
                Vector3 delta = pistonFace.position - previousWorldPosition;
                MoveCarriedRunodes(carriedRunodes, delta);
                previousWorldPosition = pistonFace.position;
            }
            else if (isExtendingHorizontally)
            {
                Piston crushPartner = TrySandwichCrushTim(worldPush);
                if (crushPartner != null)
                    timKillPartner = crushPartner;
            }

            yield return null;
        }

        if (carryRunodes)
        {
            Vector3 finalDelta = endWorldPosition - pistonFace.position;
            pistonFace.localPosition = end;
            MoveCarriedRunodes(carriedRunodes, finalDelta);

            foreach (RunodeMovement runode in carriedRunodes)
                runode.SetKinematic(false);
        }
        else
        {
            pistonFace.localPosition = end;

            if (isExtendingHorizontally)
            {
                Piston crushPartner = TrySandwichCrushTim(worldPush);
                if (crushPartner != null)
                    timKillPartner = crushPartner;
            }
        }

        RefreshRunodesNearFace(startWorldPosition);
        RefreshRunodesNearFace(endWorldPosition);

        currentStage = nextStage;
    }

    private static void RefreshRunodesNearFace(Vector3 worldPosition)
    {
        RunodeCube.RefreshConnectionsNearPoint(worldPosition);
    }

    private List<RunodeMovement> GetRunodesOnFace()
    {
        List<RunodeMovement> carriedRunodes = new List<RunodeMovement>();
        Bounds bounds = faceCollider.bounds;
        RunodeMovement[] allRunodes = Object.FindObjectsByType<RunodeMovement>();

        foreach (RunodeMovement runode in allRunodes)
        {
            Vector3 position = runode.transform.position;

            bool overFace =
                position.x >= bounds.min.x && position.x <= bounds.max.x &&
                position.z >= bounds.min.z && position.z <= bounds.max.z;

            bool onOrAboveFace = position.y >= bounds.max.y - FaceTopTolerance;

            if (overFace && onOrAboveFace)
                carriedRunodes.Add(runode);
        }

        return carriedRunodes;
    }

    private static void MoveCarriedRunodes(List<RunodeMovement> carriedRunodes, Vector3 delta)
    {
        foreach (RunodeMovement runode in carriedRunodes)
            runode.transform.position += delta;
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

            Piston opposingPiston = hit.GetComponentInParent<Piston>();
            if (opposingPiston != null && opposingPiston != this)
            {
                if (isHorizontal && IsTimBetweenOpposingPistons(worldPush, opposingPiston))
                {
                    timKillPartner = opposingPiston;
                    continue;
                }

                return false;
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
            {
                tim.ApplyDeathToss(worldPush);
                timKilledDuringMove = true;
            }
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

    // Retracts this piston and any paired piston after Tim was crushed at full extension.
    private IEnumerator RetractAfterTimKill(Piston partner)
    {
        if (!IsFaceRetracted())
            yield return AnimateToStage(0);

        if (partner != null && !partner.IsFaceRetracted())
        {
            partner.StopAllCoroutines();
            yield return partner.StartCoroutine(partner.RetractAfterTimKillInternal());
        }
    }

    // Retracts a paired piston when the other piston finished a Tim crush at full extension.
    private IEnumerator RetractAfterTimKillInternal()
    {
        stageDirection = 1;
        isMoving = true;
        timKilledDuringMove = false;
        timKillPartner = null;

        if (!IsFaceRetracted())
            yield return AnimateToStage(0);

        isMoving = false;
    }

    private bool IsFaceRetracted()
    {
        return Vector3.Distance(pistonFace.localPosition, faceHomeLocalPosition) < 0.001f && currentStage == 0;
    }

    private bool IsTimBetweenOpposingPistons(Vector3 worldPush, Piston opposingPiston)
    {
        if (isVertical || opposingPiston.isVertical || Mathf.Abs(worldPush.y) > 0.5f)
            return false;

        Vector3 axis = RunodeMovement.GetCardinalAxis(worldPush);
        if (axis.sqrMagnitude < 0.01f)
            return false;

        Vector3 otherPush = opposingPiston.GetWorldPush();
        Vector3 otherAxis = RunodeMovement.GetCardinalAxis(otherPush);
        if (Vector3.Dot(axis, otherAxis) > OpposingAxisDotThreshold)
            return false;

        Bounds thisBounds = faceCollider.bounds;
        Bounds otherBounds = opposingPiston.faceCollider.bounds;
        Vector3 thisFacePoint = GetFaceFrontPoint(thisBounds, axis);
        Vector3 otherFacePoint = GetFaceFrontPoint(otherBounds, -axis);
        float thisAxisPos = Vector3.Dot(thisFacePoint, axis);
        float otherAxisPos = Vector3.Dot(otherFacePoint, axis);
        float minFace = Mathf.Min(thisAxisPos, otherAxisPos);
        float maxFace = Mathf.Max(thisAxisPos, otherAxisPos);

        CharacterMovement[] allCharacters = Object.FindObjectsByType<CharacterMovement>();
        foreach (CharacterMovement tim in allCharacters)
        {
            if (tim.IsDead)
                continue;

            if (!IsTimInCrushLane(tim.transform.position, axis, thisBounds, otherBounds))
                continue;

            float timAxisPos = Vector3.Dot(tim.transform.position, axis);
            if (timAxisPos >= minFace - PushDetectHalf && timAxisPos <= maxFace + PushDetectHalf)
                return true;
        }

        return false;
    }

    // Kills Tim when he is caught between this face and an opposing piston face on the same axis.
    private Piston TrySandwichCrushTim(Vector3 worldPush)
    {
        if (isVertical || Mathf.Abs(worldPush.y) > 0.5f)
            return null;

        Vector3 axis = RunodeMovement.GetCardinalAxis(worldPush);
        if (axis.sqrMagnitude < 0.01f)
            return null;

        Vector3 thisFacePoint = GetFaceFrontPoint(faceCollider.bounds, axis);
        float thisAxisPos = Vector3.Dot(thisFacePoint, axis);
        Bounds thisBounds = faceCollider.bounds;

        Piston[] allPistons = Object.FindObjectsByType<Piston>();
        foreach (Piston other in allPistons)
        {
            if (other == this || other.isVertical)
                continue;

            Vector3 otherPush = other.GetWorldPush();
            Vector3 otherAxis = RunodeMovement.GetCardinalAxis(otherPush);
            if (Vector3.Dot(axis, otherAxis) > OpposingAxisDotThreshold)
                continue;

            Vector3 otherFacePoint = GetFaceFrontPoint(other.faceCollider.bounds, -axis);
            float otherAxisPos = Vector3.Dot(otherFacePoint, axis);
            float gap = Mathf.Abs(thisAxisPos - otherAxisPos);

            if (gap > TimCrushGap)
                continue;

            Bounds otherBounds = other.faceCollider.bounds;
            float minFace = Mathf.Min(thisAxisPos, otherAxisPos);
            float maxFace = Mathf.Max(thisAxisPos, otherAxisPos);

            CharacterMovement[] allCharacters = Object.FindObjectsByType<CharacterMovement>();
            foreach (CharacterMovement tim in allCharacters)
            {
                if (tim.IsDead)
                    continue;

                if (!IsTimInCrushLane(tim.transform.position, axis, thisBounds, otherBounds))
                    continue;

                float timAxisPos = Vector3.Dot(tim.transform.position, axis);
                if (timAxisPos < minFace - PushDetectHalf || timAxisPos > maxFace + PushDetectHalf)
                    continue;

                tim.ApplyDeathToss(axis);
                timKilledDuringMove = true;
                return other;
            }
        }

        return null;
    }

    private static bool IsTimInCrushLane(Vector3 timPosition, Vector3 axis, Bounds thisBounds, Bounds otherBounds)
    {
        if (Mathf.Abs(timPosition.y - thisBounds.center.y) > TimHeightTolerance)
            return false;

        if (Mathf.Abs(axis.x) > 0.5f)
        {
            float minZ = Mathf.Min(thisBounds.min.z, otherBounds.min.z) - TimLaneTolerance;
            float maxZ = Mathf.Max(thisBounds.max.z, otherBounds.max.z) + TimLaneTolerance;
            return timPosition.z >= minZ && timPosition.z <= maxZ;
        }

        float minX = Mathf.Min(thisBounds.min.x, otherBounds.min.x) - TimLaneTolerance;
        float maxX = Mathf.Max(thisBounds.max.x, otherBounds.max.x) + TimLaneTolerance;
        return timPosition.x >= minX && timPosition.x <= maxX;
    }

    private static Vector3 GetFaceFrontPoint(Bounds bounds, Vector3 direction)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);
        float absZ = Mathf.Abs(direction.z);

        Vector3 face = center;
        if (absX >= absY && absX >= absZ)
            face.x += Mathf.Sign(direction.x) * extents.x;
        else if (absY >= absX && absY >= absZ)
            face.y += Mathf.Sign(direction.y) * extents.y;
        else
            face.z += Mathf.Sign(direction.z) * extents.z;

        return face;
    }
}
