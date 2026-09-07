using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PistonHorizontal : MonoBehaviour
{
    private const int SocketCount = 2;
    private const float GridUnit = 1f;
    private const float DefaultMoveSpeed = 5f;
    private const string ExtensionDirectionName = "Extension Direction";
    private const string PrimaryFlatProbeName = "Primary Flat Trigger";
    private const string PrimaryCubeProbeName = "Primary Cube Trigger";
    private const string SecondaryCubeProbeName = "Secondary Cube Trigger";
    private const string SecondaryFlatProbeName = "Secondary Flat Trigger";

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
    [SerializeField] private int maxStage = 3;
    [SerializeField] private int startingStage;
    [SerializeField] private bool startExtending = true;
    [SerializeField] private float moveSpeed = DefaultMoveSpeed;
    [SerializeField] private LayerMask obstructionMask;
    [SerializeField] private Transform shaftColliderObject;
    [SerializeField] private bool isPowered;

    private Vector3 faceHomeLocalPosition;
    private int currentStage;
    private int stageDirection = 1;
    private bool isMoving;
    private readonly Collider[] overlapHits = new Collider[16];
    private readonly HashSet<RunodeMovement> pushedCubesThisMove = new HashSet<RunodeMovement>();
    private readonly HashSet<GameObject> primaryCubeSnapshot = new HashSet<GameObject>();

    private bool extendBlocked;
    private bool stepCompleted;
    private Vector3 cachedWorldExtendDirection;
    private Vector3 cachedTravelAxisInFaceParentLocal;
    private Vector3 stepTargetGridCell;
    private int probeQueryMask;
    private BoxCollider primaryFlatProbe;
    private BoxCollider primaryCubeProbe;
    private BoxCollider secondaryCubeProbe;
    private BoxCollider secondaryFlatProbe;

    private void Awake()
    {
        Debug.Assert(pistonFace != null, $"{nameof(PistonHorizontal)} on {name} requires Pistons Face assigned.", this);
        faceHomeLocalPosition = pistonFace.localPosition;
        CacheExtensionDirection();
        CacheFaceProbes();
        probeQueryMask = obstructionMask.value;

        currentStage = Mathf.Clamp(startingStage, 0, maxStage);
        stageDirection = startExtending ? 1 : -1;
        ApplyStagePosition(currentStage);
        InitialSocketConfiguration();

        PistonShaft pistonShaft = GetComponentInChildren<PistonShaft>();
        if (pistonShaft != null)
            pistonShaft.SetShaftColliderObject(shaftColliderObject);
    }

    private void OnValidate()
    {
        maxStage = Mathf.Max(0, maxStage);
        startingStage = Mathf.Clamp(startingStage, 0, maxStage);
    }

    private Vector3 GetTravelAxisLocal()
    {
        return cachedTravelAxisInFaceParentLocal;
    }

    private Vector3 GetWorldPush()
    {
        return RunodeMovement.GetCardinalAxis(cachedWorldExtendDirection);
    }

    // Reads the Extension Direction marker once at startup to lock extend axis to the hierarchy.
    private void CacheExtensionDirection()
    {
        Transform extensionDirection = FindExtensionDirectionTransform();
        Debug.Assert(extensionDirection != null, $"{nameof(PistonHorizontal)} on {name} requires a child named '{ExtensionDirectionName}'.", this);

        Transform faceParent = pistonFace.parent;
        Vector3 worldDirection = extensionDirection.position - faceParent.position;
        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude < 0.0001f)
            worldDirection = faceParent.forward;

        worldDirection.y = 0f;

        if (worldDirection.sqrMagnitude < 0.0001f)
            worldDirection = Vector3.forward;

        cachedWorldExtendDirection = worldDirection.normalized;
        cachedTravelAxisInFaceParentLocal = faceParent.InverseTransformDirection(cachedWorldExtendDirection);
        cachedTravelAxisInFaceParentLocal.y = 0f;

        if (cachedTravelAxisInFaceParentLocal.sqrMagnitude < 0.0001f)
            cachedTravelAxisInFaceParentLocal = Vector3.forward;
        else
            cachedTravelAxisInFaceParentLocal.Normalize();
    }

    // Finds the four face probe colliders used before each 1 m extend step.
    private void CacheFaceProbes()
    {
        primaryFlatProbe = FindProbeCollider(PrimaryFlatProbeName);
        primaryCubeProbe = FindProbeCollider(PrimaryCubeProbeName);
        secondaryCubeProbe = FindProbeCollider(SecondaryCubeProbeName);
        secondaryFlatProbe = FindProbeCollider(SecondaryFlatProbeName);

        Debug.Assert(primaryFlatProbe != null, $"{nameof(PistonHorizontal)} on {name} requires '{PrimaryFlatProbeName}' under the piston face.", this);
    }

    private BoxCollider FindProbeCollider(string probeName)
    {
        foreach (Transform child in pistonFace.GetComponentsInChildren<Transform>(true))
        {
            if (child.name != probeName)
                continue;

            BoxCollider box = child.GetComponent<BoxCollider>();
            if (box != null)
                return box;
        }

        return null;
    }

    private Transform FindExtensionDirectionTransform()
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == ExtensionDirectionName)
                return child;
        }

        return null;
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
        extendBlocked = false;
        pushedCubesThisMove.Clear();
        PistonCellReservation.Release(this);

        if (nextStage > currentStage)
            yield return ExtendByGridSteps(nextStage);
        else
            yield return AnimateRetractToStage(nextStage);

        if (extendBlocked)
        {
            stageDirection = 1;
            extendBlocked = false;
            yield return AnimateRetractToStage(0);
        }

        PistonCellReservation.Release(this);
        isMoving = false;
    }

    // Extends one grid step at a time, running probe overlap checks before each step.
    private IEnumerator ExtendByGridSteps(int targetStage)
    {
        List<RunodeMovement> carriedRunodes = null;

        Vector3 startWorldPosition = pistonFace.position;

        while (currentStage < targetStage)
        {
            CapturePrimaryCubeSnapshot();

            if (!TryPrepareExtendStep(carriedRunodes, GetWorldPush(), out bool hardBlock))
            {
                if (hardBlock)
                    extendBlocked = true;
                break;
            }

            yield return AnimateOneGridStep(carriedRunodes);
            if (extendBlocked || !stepCompleted)
                break;

            currentStage++;
            RefreshRunodesNearFace(pistonFace.position);
        }

        RefreshRunodesNearFace(startWorldPosition);
        RefreshRunodesNearFace(pistonFace.position);
    }

    // Runs probe checks before one 1 m step.
    private bool TryPrepareExtendStep(List<RunodeMovement> carriedRunodes, Vector3 worldPush, out bool hardBlock)
    {
        hardBlock = false;

        if (IsPrimaryFlatPlanningBlocked(carriedRunodes))
        {
            hardBlock = true;
            return false;
        }

        if (!TryPrepareHorizontalPushStep(worldPush, out hardBlock, out RunodeMovement cubeToPush))
            return false;

        if (!TryClaimExtendStepCells(cubeToPush))
        {
            hardBlock = true;
            return false;
        }

        if (cubeToPush != null && !TryPushCube(cubeToPush, worldPush))
            return false;

        return true;
    }

    private bool TryPrepareHorizontalPushStep(Vector3 worldPush, out bool hardBlock, out RunodeMovement cubeToPush)
    {
        hardBlock = false;
        cubeToPush = null;

        RunodeMovement cubeInPushCell = GetCubeInProbe(primaryCubeProbe, null);
        if (cubeInPushCell == null)
            return true;

        RunodeMovement cubeOnPlatform = GetCubeInProbe(primaryFlatProbe, null);
        if (cubeOnPlatform != cubeInPushCell)
            return true;

        // Secondary Flat probe disabled — too sensitive when facing pistons.
        // foreach (Collider hit in GetProbeHits(secondaryFlatProbe))
        // {
        //     if (TryGetPiston(hit, out _))
        //     {
        //         hardBlock = true;
        //         return false;
        //     }
        // }

        foreach (Collider hit in GetProbeHits(secondaryCubeProbe))
        {
            if (TryGetCube(hit, out RunodeMovement landingCube) && landingCube != cubeInPushCell)
            {
                hardBlock = true;
                return false;
            }

            if (TryGetPiston(hit, out _))
            {
                hardBlock = true;
                return false;
            }

            if (IsStaticObstruction(hit))
            {
                hardBlock = true;
                return false;
            }
        }

        if (cubeInPushCell.IsBusy || !cubeInPushCell.CanPushSingle(worldPush))
            return false;

        cubeToPush = cubeInPushCell;
        return true;
    }

    // Claims the face target cell and, when pushing, the cube landing cell for this step.
    private bool TryClaimExtendStepCells(RunodeMovement cubeToPush)
    {
        Vector3 faceTargetWorld = GetFaceTargetWorldPosition();

        if (cubeToPush == null)
            return PistonCellReservation.TryClaimAll(this, PistonCellReservation.Priority.ExtendOnly, faceTargetWorld);

        return PistonCellReservation.TryClaimAll(
            this,
            PistonCellReservation.Priority.PushStep,
            faceTargetWorld,
            GetPushLandingWorldPosition(),
            useSecondCell: true);
    }

    private Vector3 GetFaceTargetWorldPosition()
    {
        Vector3 endLocal = pistonFace.localPosition + GetTravelAxisLocal() * GridUnit;
        return pistonFace.parent.TransformPoint(endLocal);
    }

    private Vector3 GetPushLandingWorldPosition()
    {
        return secondaryCubeProbe.bounds.center;
    }

    private bool IsPrimaryFlatPlanningBlocked(List<RunodeMovement> carriedRunodes)
    {
        foreach (Collider hit in GetProbeHits(primaryFlatProbe))
        {
            if (TryGetCarriedCube(hit, carriedRunodes, out _))
                continue;

            if (TryGetCube(hit, out _))
                continue;

            if (TryGetPiston(hit, out _))
                return true;

            if (IsStaticObstruction(hit))
                return true;
        }

        return false;
    }

    private RunodeMovement GetCubeInProbe(BoxCollider probe, List<RunodeMovement> carriedRunodes)
    {
        foreach (Collider hit in GetProbeHits(probe))
        {
            if (TryGetCarriedCube(hit, carriedRunodes, out _))
                continue;

            if (TryGetCube(hit, out RunodeMovement cube) && !pushedCubesThisMove.Contains(cube))
                return cube;
        }

        return null;
    }

    // Records every entity occupying the forward cell before a 1 m extend step.
    private void CapturePrimaryCubeSnapshot()
    {
        primaryCubeSnapshot.Clear();

        foreach (Collider hit in GetProbeHits(primaryCubeProbe))
        {
            if (TryGetProbeEntityRoot(hit, out GameObject root))
                primaryCubeSnapshot.Add(root);
        }
    }

    // True when Primary Flat touches something unexpected in this step's target cell.
    private bool HasPrimaryFlatUnexpectedContact(List<RunodeMovement> carriedRunodes)
    {
        foreach (Collider hit in GetProbeHits(primaryFlatProbe))
        {
            if (TryGetCarriedCube(hit, carriedRunodes, out _))
                continue;

            if (!TryGetProbeEntityRoot(hit, out GameObject root))
                return true;

            if (primaryCubeSnapshot.Contains(root))
                continue;

            if (TryGetCube(hit, out RunodeMovement cube) && cube.IsBusy)
                return true;

            if (GetGridCell(hit.bounds.center) != stepTargetGridCell)
                continue;

            return true;
        }

        return false;
    }

    private static Vector3 GetGridCell(Vector3 worldPosition)
    {
        return new Vector3(
            Mathf.Round(worldPosition.x),
            worldPosition.y,
            Mathf.Round(worldPosition.z));
    }

    private bool TryGetProbeEntityRoot(Collider hit, out GameObject root)
    {
        if (TryGetCube(hit, out RunodeMovement cube))
        {
            root = cube.gameObject;
            return true;
        }

        Piston legacyPiston = hit.GetComponentInParent<Piston>();
        if (legacyPiston != null)
        {
            root = legacyPiston.gameObject;
            return true;
        }

        PistonHorizontal horizontal = hit.GetComponentInParent<PistonHorizontal>();
        if (horizontal != null && horizontal != this)
        {
            root = horizontal.gameObject;
            return true;
        }

        PistonVertical vertical = hit.GetComponentInParent<PistonVertical>();
        if (vertical != null)
        {
            root = vertical.gameObject;
            return true;
        }

        if (IsStaticObstruction(hit))
        {
            root = hit.gameObject;
            return true;
        }

        root = null;
        return false;
    }

    private bool TryPushCube(RunodeMovement cube, Vector3 worldPush)
    {
        if (cube == null || pushedCubesThisMove.Contains(cube))
            return true;

        if (cube.IsBusy || !cube.CanPushSingle(worldPush))
            return false;

        cube.PushSingle(worldPush);
        pushedCubesThisMove.Add(cube);
        return true;
    }

    private IEnumerable<Collider> GetProbeHits(BoxCollider probe)
    {
        if (probe == null)
            yield break;

        Bounds bounds = probe.bounds;
        int count = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            overlapHits,
            probe.transform.rotation,
            probeQueryMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider hit = overlapHits[i];
            if (ShouldIgnoreProbeHit(hit))
                continue;

            yield return hit;
        }
    }

    private bool ShouldIgnoreProbeHit(Collider hit)
    {
        if (hit == null)
            return true;

        if (hit.transform.IsChildOf(transform))
            return true;

        return IsProbeCollider(hit);
    }

    private static bool IsProbeCollider(Collider hit)
    {
        string objectName = hit.gameObject.name;
        return objectName == PrimaryFlatProbeName
            || objectName == PrimaryCubeProbeName
            || objectName == SecondaryCubeProbeName
            || objectName == SecondaryFlatProbeName;
    }

    private static bool TryGetCube(Collider hit, out RunodeMovement cube)
    {
        cube = hit.GetComponentInParent<RunodeMovement>();
        return cube != null;
    }

    private static bool TryGetCarriedCube(Collider hit, List<RunodeMovement> carriedRunodes, out RunodeMovement cube)
    {
        cube = null;
        if (carriedRunodes == null || !TryGetCube(hit, out RunodeMovement found))
            return false;

        if (!carriedRunodes.Contains(found))
            return false;

        cube = found;
        return true;
    }

    private bool TryGetPiston(Collider hit, out Piston piston)
    {
        piston = hit.GetComponentInParent<Piston>();
        if (piston != null)
            return true;

        PistonHorizontal horizontal = hit.GetComponentInParent<PistonHorizontal>();
        if (horizontal != null && horizontal != this)
            return true;

        if (hit.GetComponentInParent<PistonVertical>() != null)
            return true;

        piston = null;
        return false;
    }

    private bool IsStaticObstruction(Collider hit)
    {
        if ((obstructionMask.value & (1 << hit.gameObject.layer)) == 0)
            return false;

        if (TryGetCube(hit, out _))
            return false;

        if (TryGetPiston(hit, out _))
            return false;

        return true;
    }

    private IEnumerator AnimateOneGridStep(List<RunodeMovement> carriedRunodes)
    {
        Vector3 start = pistonFace.localPosition;
        Vector3 end = start + GetTravelAxisLocal() * GridUnit;
        stepTargetGridCell = GetGridCell(pistonFace.parent.TransformPoint(end));
        Vector3 previousWorldPosition = pistonFace.position;
        float duration = moveSpeed > 0f ? GridUnit / moveSpeed : 0f;
        bool stoppedEarly = false;
        stepCompleted = true;

        if (!PistonCellReservation.OwnsCell(this, GetFaceTargetWorldPosition()))
        {
            extendBlocked = true;
            stepCompleted = false;
            yield break;
        }

        if (pushedCubesThisMove.Count > 0 && !PistonCellReservation.OwnsCell(this, GetPushLandingWorldPosition()))
        {
            extendBlocked = true;
            stepCompleted = false;
            yield break;
        }

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float moveProgress = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float t = Mathf.SmoothStep(0f, 1f, moveProgress);
            pistonFace.localPosition = Vector3.Lerp(start, end, t);

            if (carriedRunodes != null)
            {
                Vector3 delta = pistonFace.position - previousWorldPosition;
                MoveCarriedRunodes(carriedRunodes, delta);
                previousWorldPosition = pistonFace.position;
            }

            if (HasPrimaryFlatUnexpectedContact(carriedRunodes))
            {
                stoppedEarly = true;
                extendBlocked = true;
                break;
            }

            yield return null;
        }

        if (!stoppedEarly)
            pistonFace.localPosition = end;
        else
            stepCompleted = false;

        if (carriedRunodes != null && !stoppedEarly)
        {
            Vector3 finalDelta = pistonFace.parent.TransformPoint(end) - pistonFace.position;
            MoveCarriedRunodes(carriedRunodes, finalDelta);
        }
    }

    private IEnumerator AnimateRetractToStage(int targetStage)
    {
        Vector3 start = pistonFace.localPosition;
        Vector3 end = faceHomeLocalPosition + GetTravelAxisLocal() * (targetStage * GridUnit);
        Vector3 startWorldPosition = pistonFace.position;
        List<RunodeMovement> carriedRunodes = null;

        float distance = Vector3.Distance(start, end);
        float duration = moveSpeed > 0f ? distance / moveSpeed : 0f;
        Vector3 previousWorldPosition = startWorldPosition;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            pistonFace.localPosition = Vector3.Lerp(start, end, t);

            if (carriedRunodes != null)
            {
                Vector3 delta = pistonFace.position - previousWorldPosition;
                MoveCarriedRunodes(carriedRunodes, delta);
                previousWorldPosition = pistonFace.position;
            }

            yield return null;
        }

        pistonFace.localPosition = end;

        RefreshRunodesNearFace(startWorldPosition);
        RefreshRunodesNearFace(pistonFace.position);

        currentStage = targetStage;
    }

    private static void RefreshRunodesNearFace(Vector3 worldPosition)
    {
        RunodeCube.RefreshConnectionsNearPoint(worldPosition);
    }

    private static void MoveCarriedRunodes(List<RunodeMovement> carriedRunodes, Vector3 delta)
    {
        foreach (RunodeMovement runode in carriedRunodes)
            runode.transform.position += delta;
    }
}
