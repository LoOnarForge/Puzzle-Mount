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
    private const float MinTravelAxisSqr = 0.0001f;
    private const string TimLayerName = "TimJones";
    private const string PistonBaseObjectName = "Piston Base";

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

    [SerializeField] private bool isSelfPowered;

    [SerializeField] private SocketSlot[] sockets = new SocketSlot[SocketCount]
    {
        new SocketSlot(),
        new SocketSlot()
    };

    [SerializeField] private Transform pistonFace;
    [SerializeField] private Transform pistonBase;
    [SerializeField] private bool isVertical;
    [SerializeField] private int maxStage = 3;
    [SerializeField] private int startingStage;
    [SerializeField] private bool startExtending = true;
    [SerializeField] private float moveSpeed = DefaultMoveSpeed;
    [SerializeField] private LayerMask obstructionMask;
    [SerializeField] private Collider shaftCollider;
    [SerializeField] private bool isPowered;

    private Vector3 faceHomeWorld;
    private Vector3 travelAxisWorld;
    private int currentStage;
    private int stageDirection = 1;
    private bool isMoving;
    private bool timKilledDuringMove;
    private bool isHorizontallyExtending;
    private int targetStageThisMove;
    private Piston timKillPartner;
    private Vector3 horizontalExtendPush;
    private Collider faceCollider;
    private static readonly List<Piston> HorizontalExtenders = new List<Piston>();
    private readonly Collider[] overlapHits = new Collider[16];

    private void Awake()
    {
        Debug.Assert(pistonFace != null, $"{nameof(Piston)} on {name} requires Pistons Face assigned.", this);
        faceCollider = pistonFace.GetComponent<Collider>();
        Debug.Assert(faceCollider != null, $"{nameof(Piston)} on {name} requires a collider on Pistons Face.", this);
        if (obstructionMask.value == 0)
            obstructionMask = ~LayerMask.GetMask(TimLayerName);

        currentStage = Mathf.Clamp(startingStage, 0, maxStage);
        stageDirection = startExtending ? 1 : -1;
        CacheTravelSetup();
        ApplyStagePosition(currentStage);
        InitialSocketConfiguration();

        PistonShaft pistonShaft = GetComponentInChildren<PistonShaft>();
        if (pistonShaft != null)
            pistonShaft.SetShaftCollider(shaftCollider);
    }

    private void LateUpdate()
    {
        if (!isHorizontallyExtending)
            return;

        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < HorizontalExtenders.Count; i++)
                HorizontalExtenders[i].ClampFaceToOpposingPistons(HorizontalExtenders[i].horizontalExtendPush);
        }
    }

    private void OnValidate()
    {
        maxStage = Mathf.Max(0, maxStage);
        startingStage = Mathf.Clamp(startingStage, 0, maxStage);
    }

    private Vector3 GetWorldTravelAxis()
    {
        return travelAxisWorld;
    }

    private bool IsVerticalTravel()
    {
        return isVertical;
    }

    private bool IsHorizontalTravel()
    {
        return !isVertical;
    }

    private void CacheTravelSetup()
    {
        ResolvePistonBase();
        Debug.Assert(pistonBase != null, $"{nameof(Piston)} on {name} requires {PistonBaseObjectName} assigned or present in hierarchy.", this);

        Vector3 baseToFaceWorld = pistonFace.position - pistonBase.position;

        if (isVertical)
        {
            float ySign = baseToFaceWorld.y >= 0f ? 1f : -1f;
            travelAxisWorld = Vector3.up * ySign;
        }
        else
        {
            Vector3 planarWorld = new Vector3(baseToFaceWorld.x, 0f, baseToFaceWorld.z);
            travelAxisWorld = planarWorld.sqrMagnitude >= MinTravelAxisSqr
                ? RunodeMovement.GetCardinalAxis(planarWorld)
                : RunodeMovement.GetCardinalAxis(transform.forward);
        }

        faceHomeWorld = pistonFace.position - travelAxisWorld * (currentStage * GridUnit);
    }

    private void ResolvePistonBase()
    {
        if (pistonBase != null)
            return;

        pistonBase = FindChildTransformByName(transform, PistonBaseObjectName);
    }

    private static Transform FindChildTransformByName(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
                return child;

            Transform found = FindChildTransformByName(child, childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private Vector3 GetFaceWorldPositionForStage(int stage)
    {
        return faceHomeWorld + travelAxisWorld * (stage * GridUnit);
    }

    private Vector3 GetWorldPushForStageDelta(int stageDelta)
    {
        Vector3 axis = GetWorldTravelAxis();
        return stageDelta >= 0 ? axis : -axis;
    }

    private Vector3 GetWorldPush()
    {
        return GetWorldPushForStageDelta(stageDirection);
    }

    private void ApplyStagePosition(int stage)
    {
        pistonFace.position = GetFaceWorldPositionForStage(stage);
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
        if ((!isSelfPowered && !isPowered) || isMoving || maxStage <= 0)
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

        targetStageThisMove = nextStage;
        StartCoroutine(MoveToStage(nextStage));
    }

    // Claims assigned sockets once and pushes required color and MW to each.
    private void InitialSocketConfiguration()
    {
        if (isSelfPowered)
        {
            isPowered = true;
            return;
        }

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
        if (isSelfPowered)
        {
            isPowered = true;
            return;
        }

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

        int stageDelta = nextStage - currentStage;
        Vector3 worldPush = GetWorldPushForStageDelta(stageDelta);
        if (stageDelta > 0 && !CanAdvanceStage(worldPush))
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
        Vector3 startWorldPosition = pistonFace.position;
        Vector3 endWorldPosition = GetFaceWorldPositionForStage(nextStage);
        int stageDelta = nextStage - currentStage;
        bool extending = stageDelta > 0;
        bool carryRunodes = IsVerticalTravel() && extending;
        bool carryCharacters = IsVerticalTravel() && extending;
        bool crushWhileMoving = IsVerticalTravel() && !extending;

        List<RunodeMovement> carriedRunodes = carryRunodes ? GetRunodesOnFace() : null;
        List<CharacterMovement> carriedCharacters = carryCharacters ? GetCharactersOnFace() : null;

        if (carryRunodes)
        {
            foreach (RunodeMovement runode in carriedRunodes)
                runode.SetKinematic(true);
        }

        bool isExtendingHorizontally = IsHorizontalTravel() && extending;
        Vector3 worldPush = GetWorldPushForStageDelta(stageDelta);

        if (isExtendingHorizontally)
        {
            Vector3 travel = endWorldPosition - startWorldPosition;
            float requestedTravel = travel.magnitude;
            if (requestedTravel > 0f)
            {
                float allowedTravel = GetAllowedExtendTravel(worldPush, requestedTravel);
                if (allowedTravel < requestedTravel)
                    endWorldPosition = startWorldPosition + travelAxisWorld * allowedTravel;
            }

            isHorizontallyExtending = true;
            horizontalExtendPush = worldPush;
            if (!HorizontalExtenders.Contains(this))
                HorizontalExtenders.Add(this);
        }

        float distance = Vector3.Distance(startWorldPosition, endWorldPosition);
        float duration = moveSpeed > 0f ? distance / moveSpeed : 0f;
        Vector3 previousWorldPosition = startWorldPosition;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            pistonFace.position = Vector3.Lerp(startWorldPosition, endWorldPosition, t);

            Vector3 delta = pistonFace.position - previousWorldPosition;

            if (carryRunodes)
                MoveCarriedRunodes(carriedRunodes, delta);

            if (carryCharacters)
                MoveCarriedCharacters(carriedCharacters, delta);

            if (isExtendingHorizontally)
            {
                Piston crushPartner = TrySandwichCrushTim(worldPush);
                if (crushPartner != null)
                    timKillPartner = crushPartner;
            }
            else if (crushWhileMoving)
            {
                TryCrushTimInTravelPath(worldPush);
            }

            previousWorldPosition = pistonFace.position;
            yield return null;
        }

        pistonFace.position = endWorldPosition;

        if (carryRunodes)
        {
            Vector3 finalDelta = endWorldPosition - previousWorldPosition;
            MoveCarriedRunodes(carriedRunodes, finalDelta);

            foreach (RunodeMovement runode in carriedRunodes)
                runode.SetKinematic(false);
        }

        if (carryCharacters)
        {
            Vector3 finalDelta = endWorldPosition - previousWorldPosition;
            MoveCarriedCharacters(carriedCharacters, finalDelta);
        }

        if (isExtendingHorizontally)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < HorizontalExtenders.Count; i++)
                    HorizontalExtenders[i].ClampFaceToOpposingPistons(HorizontalExtenders[i].horizontalExtendPush);
            }

            Piston crushPartner = TrySandwichCrushTim(worldPush);
            if (crushPartner != null)
                timKillPartner = crushPartner;
        }
        else if (crushWhileMoving)
        {
            TryCrushTimInTravelPath(worldPush);
        }

        if (isExtendingHorizontally)
        {
            isHorizontallyExtending = false;
            HorizontalExtenders.Remove(this);
        }

        RefreshRunodesNearFace(startWorldPosition);
        RefreshRunodesNearFace(endWorldPosition);

        currentStage = nextStage;
    }

    private static void RefreshRunodesNearFace(Vector3 worldPosition)
    {
        RunodeCube.RefreshConnectionsNearPoint(worldPosition);
    }

    private bool IsOnFacePlatform(Vector3 position)
    {
        Bounds bounds = faceCollider.bounds;

        bool overFace =
            position.x >= bounds.min.x && position.x <= bounds.max.x &&
            position.z >= bounds.min.z && position.z <= bounds.max.z;

        bool onOrAboveFace = position.y >= bounds.max.y - FaceTopTolerance;

        return overFace && onOrAboveFace;
    }

    private List<RunodeMovement> GetRunodesOnFace()
    {
        List<RunodeMovement> carriedRunodes = new List<RunodeMovement>();
        RunodeMovement[] allRunodes = Object.FindObjectsByType<RunodeMovement>();

        foreach (RunodeMovement runode in allRunodes)
        {
            if (IsOnFacePlatform(runode.transform.position))
                carriedRunodes.Add(runode);
        }

        return carriedRunodes;
    }

    private List<CharacterMovement> GetCharactersOnFace()
    {
        List<CharacterMovement> carriedCharacters = new List<CharacterMovement>();
        CharacterMovement[] allCharacters = Object.FindObjectsByType<CharacterMovement>();

        foreach (CharacterMovement character in allCharacters)
        {
            if (character.IsDead)
                continue;

            if (IsOnFacePlatform(character.transform.position))
                carriedCharacters.Add(character);
        }

        return carriedCharacters;
    }

    private static void MoveCarriedRunodes(List<RunodeMovement> carriedRunodes, Vector3 delta)
    {
        foreach (RunodeMovement runode in carriedRunodes)
            runode.transform.position += delta;
    }

    private static void MoveCarriedCharacters(List<CharacterMovement> carriedCharacters, Vector3 delta)
    {
        foreach (CharacterMovement character in carriedCharacters)
            character.transform.position += delta;
    }

    private bool CanAdvanceStage(Vector3 worldPush)
    {
        Vector3 center = pistonFace.position + worldPush * GridUnit * 0.5f;
        int count = Physics.OverlapBoxNonAlloc(center, Vector3.one * PushDetectHalf, overlapHits, Quaternion.identity);

        bool isHorizontal = IsHorizontalTravel();
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
                {
                    if (IsVerticalTravel() && IsOnFacePlatform(tim.transform.position))
                        continue;

                    timsInFront.Add(tim);
                }

                continue;
            }

            RunodeMovement runode = hit.GetComponentInParent<RunodeMovement>();
            if (runode != null)
            {
                if (IsVerticalTravel() && IsOnFacePlatform(runode.transform.position))
                    continue;

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

        if (isHorizontal)
        {
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
        }
        else
        {
            foreach (RunodeMovement cube in cubesInFront)
                return false;

            foreach (CharacterMovement tim in timsInFront)
            {
                if (!CanTimMoveOneStep(tim, worldPush))
                {
                    tim.ApplyDeathToss(worldPush);
                    timKilledDuringMove = true;
                }
            }
        }

        return true;
    }

    private bool CanTimMoveOneStep(CharacterMovement tim, Vector3 worldPush)
    {
        Vector3 pushDir = worldPush.normalized;
        Vector3 target = tim.transform.position + pushDir * GridUnit;
        Vector3 checkCenter;

        if (IsVerticalTravel())
        {
            checkCenter = new Vector3(
                Mathf.Round(tim.transform.position.x),
                target.y + 0.5f,
                Mathf.Round(tim.transform.position.z));
        }
        else
        {
            checkCenter = new Vector3(
                Mathf.Round(target.x),
                tim.transform.position.y + 0.5f,
                Mathf.Round(target.z));
        }

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
        return currentStage == 0 &&
               Vector3.Distance(pistonFace.position, faceHomeWorld) < 0.001f;
    }

    private bool IsTimBetweenOpposingPistons(Vector3 worldPush, Piston opposingPiston)
    {
        if (IsVerticalTravel())
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

    // Caps extend distance so paired pistons meet without overlapping when both advance together.
    private float GetAllowedExtendTravel(Vector3 worldPush, float requestedTravel)
    {
        if (IsVerticalTravel())
            return requestedTravel;

        Vector3 axis = RunodeMovement.GetCardinalAxis(worldPush);
        if (axis.sqrMagnitude < 0.01f)
            return requestedTravel;

        float pushSign = Mathf.Sign(Vector3.Dot(axis, worldPush.normalized));
        if (pushSign == 0f)
            return requestedTravel;

        Bounds thisBounds = faceCollider.bounds;
        float thisAxisPos = Vector3.Dot(GetFaceFrontPoint(thisBounds, axis), axis);
        float smallestGap = float.PositiveInfinity;
        bool opposingAlsoExtending = false;

        Piston[] allPistons = Object.FindObjectsByType<Piston>();
        foreach (Piston other in allPistons)
        {
            if (other == this || other.IsVerticalTravel())
                continue;

            Vector3 otherPush = other.GetWorldPush();
            Vector3 otherAxis = RunodeMovement.GetCardinalAxis(otherPush);
            if (Vector3.Dot(axis, otherAxis) > OpposingAxisDotThreshold)
                continue;

            if (!SharesOpposingPistonLane(axis, thisBounds, other.faceCollider.bounds))
                continue;

            float otherAxisPos = Vector3.Dot(GetFaceFrontPoint(other.faceCollider.bounds, -axis), axis);
            float gap = pushSign > 0f ? otherAxisPos - thisAxisPos : thisAxisPos - otherAxisPos;
            if (gap <= 0f)
                continue;

            smallestGap = Mathf.Min(smallestGap, gap);

            if (other.targetStageThisMove > other.currentStage)
                opposingAlsoExtending = true;
        }

        if (smallestGap == float.PositiveInfinity)
            return requestedTravel;

        float allowedTravel = opposingAlsoExtending ? smallestGap * 0.5f : smallestGap;
        return Mathf.Min(requestedTravel, allowedTravel);
    }

    // Stops this face from lerping past an opposing piston face on the same axis.
    private void ClampFaceToOpposingPistons(Vector3 worldPush)
    {
        if (IsVerticalTravel())
            return;

        Vector3 axis = RunodeMovement.GetCardinalAxis(worldPush);
        if (axis.sqrMagnitude < 0.01f)
            return;

        float pushSign = Mathf.Sign(Vector3.Dot(axis, worldPush.normalized));
        if (pushSign == 0f)
            return;

        Bounds thisBounds = faceCollider.bounds;
        float thisAxisPos = Vector3.Dot(GetFaceFrontPoint(thisBounds, axis), axis);
        float tightestLimit = pushSign > 0f ? float.PositiveInfinity : float.NegativeInfinity;
        bool hasOpposingLimit = false;

        Piston[] allPistons = Object.FindObjectsByType<Piston>();
        foreach (Piston other in allPistons)
        {
            if (other == this || other.IsVerticalTravel())
                continue;

            Vector3 otherPush = other.GetWorldPush();
            Vector3 otherAxis = RunodeMovement.GetCardinalAxis(otherPush);
            if (Vector3.Dot(axis, otherAxis) > OpposingAxisDotThreshold)
                continue;

            if (!SharesOpposingPistonLane(axis, thisBounds, other.faceCollider.bounds))
                continue;

            float otherAxisPos = Vector3.Dot(GetFaceFrontPoint(other.faceCollider.bounds, -axis), axis);
            hasOpposingLimit = true;

            if (pushSign > 0f)
                tightestLimit = Mathf.Min(tightestLimit, otherAxisPos);
            else
                tightestLimit = Mathf.Max(tightestLimit, otherAxisPos);
        }

        if (!hasOpposingLimit)
            return;

        if (pushSign > 0f && thisAxisPos > tightestLimit)
            pistonFace.position -= axis * (thisAxisPos - tightestLimit);
        else if (pushSign < 0f && thisAxisPos < tightestLimit)
            pistonFace.position -= axis * (thisAxisPos - tightestLimit);
    }

    private static bool SharesOpposingPistonLane(Vector3 axis, Bounds thisBounds, Bounds otherBounds)
    {
        if (Mathf.Abs(axis.y) > 0.5f)
        {
            return thisBounds.max.x >= otherBounds.min.x - TimLaneTolerance &&
                   thisBounds.min.x <= otherBounds.max.x + TimLaneTolerance &&
                   thisBounds.max.z >= otherBounds.min.z - TimLaneTolerance &&
                   thisBounds.min.z <= otherBounds.max.z + TimLaneTolerance;
        }

        if (Mathf.Abs(axis.x) > 0.5f)
        {
            return thisBounds.max.z >= otherBounds.min.z - TimLaneTolerance &&
                   thisBounds.min.z <= otherBounds.max.z + TimLaneTolerance;
        }

        return thisBounds.max.x >= otherBounds.min.x - TimLaneTolerance &&
               thisBounds.min.x <= otherBounds.max.x + TimLaneTolerance;
    }

    // Kills Tim when he is caught between this face and an opposing piston face on the same axis.
    private Piston TrySandwichCrushTim(Vector3 worldPush)
    {
        if (IsVerticalTravel())
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
            if (other == this)
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

    // Kills Tim when a vertical piston face closes on him and he has nowhere to go.
    private void TryCrushTimInTravelPath(Vector3 worldPush)
    {
        if (!IsVerticalTravel())
            return;

        Vector3 axis = RunodeMovement.GetCardinalAxis(worldPush);
        if (axis.sqrMagnitude < 0.01f)
            return;

        float pushSign = Mathf.Sign(Vector3.Dot(axis, worldPush.normalized));
        if (pushSign == 0f)
            return;

        Bounds thisBounds = faceCollider.bounds;
        float faceAxisPos = Vector3.Dot(GetFaceFrontPoint(thisBounds, axis), axis);

        CharacterMovement[] allCharacters = Object.FindObjectsByType<CharacterMovement>();
        foreach (CharacterMovement tim in allCharacters)
        {
            if (tim.IsDead)
                continue;

            if (!IsTimInCrushLane(tim.transform.position, axis, thisBounds, thisBounds))
                continue;

            float timAxisPos = Vector3.Dot(tim.transform.position, axis);
            float gapAlongPush = pushSign > 0f ? timAxisPos - faceAxisPos : faceAxisPos - timAxisPos;
            if (gapAlongPush > TimCrushGap || gapAlongPush < -PushDetectHalf)
                continue;

            if (!CanTimMoveOneStep(tim, worldPush))
            {
                tim.ApplyDeathToss(worldPush);
                timKilledDuringMove = true;
            }
        }
    }

    private static bool IsTimInCrushLane(Vector3 timPosition, Vector3 axis, Bounds thisBounds, Bounds otherBounds)
    {
        if (Mathf.Abs(axis.y) > 0.5f)
        {
            return timPosition.x >= Mathf.Min(thisBounds.min.x, otherBounds.min.x) - TimLaneTolerance &&
                   timPosition.x <= Mathf.Max(thisBounds.max.x, otherBounds.max.x) + TimLaneTolerance &&
                   timPosition.z >= Mathf.Min(thisBounds.min.z, otherBounds.min.z) - TimLaneTolerance &&
                   timPosition.z <= Mathf.Max(thisBounds.max.z, otherBounds.max.z) + TimLaneTolerance;
        }

        if (Mathf.Abs(timPosition.y - thisBounds.center.y) > TimHeightTolerance)
            return false;

        if (Mathf.Abs(axis.x) > 0.5f)
        {
            return timPosition.z >= Mathf.Min(thisBounds.min.z, otherBounds.min.z) - TimLaneTolerance &&
                   timPosition.z <= Mathf.Max(thisBounds.max.z, otherBounds.max.z) + TimLaneTolerance;
        }

        return timPosition.x >= Mathf.Min(thisBounds.min.x, otherBounds.min.x) - TimLaneTolerance &&
               timPosition.x <= Mathf.Max(thisBounds.max.x, otherBounds.max.x) + TimLaneTolerance;
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
