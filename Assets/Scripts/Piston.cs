using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Piston : MonoBehaviour
{
    private const int SocketCount = 2;
    private const float GridUnit = 1f;
    private const float DefaultMoveSpeed = 5f;
    private const float FaceTopTolerance = 0.1f;
    private const string TimLayerName = "TimJones";
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
    [SerializeField] private Collider shaftCollider;
    [SerializeField] private bool isPowered;

    private Vector3 faceHomeLocalPosition;
    private int currentStage;
    private int stageDirection = 1;
    private bool isMoving;
    private Collider faceCollider;
    private readonly Collider[] overlapHits = new Collider[16];
    private readonly HashSet<RunodeMovement> pushedCubesThisMove = new HashSet<RunodeMovement>();

    private bool extendBlocked;
    private Vector3 cachedWorldExtendDirection;
    private Vector3 cachedTravelAxisInFaceParentLocal;
    private int probeQueryMask;
    private BoxCollider primaryFlatProbe;
    private BoxCollider primaryCubeProbe;
    private BoxCollider secondaryCubeProbe;
    private BoxCollider secondaryFlatProbe;

    private void Awake()
    {
        Debug.Assert(pistonFace != null, $"{nameof(Piston)} on {name} requires Pistons Face assigned.", this);
        faceCollider = pistonFace.GetComponent<Collider>();
        Debug.Assert(faceCollider != null, $"{nameof(Piston)} on {name} requires a collider on Pistons Face.", this);
        faceHomeLocalPosition = pistonFace.localPosition;
        CacheExtensionDirection();
        CacheFaceProbes();
        if (obstructionMask.value == 0)
            obstructionMask = ~LayerMask.GetMask(TimLayerName);

        int timLayerMask = LayerMask.GetMask(TimLayerName);
        probeQueryMask = obstructionMask.value & ~timLayerMask;

        currentStage = Mathf.Clamp(startingStage, 0, maxStage);
        stageDirection = startExtending ? 1 : -1;
        ApplyStagePosition(currentStage);
        InitialSocketConfiguration();

        PistonShaft pistonShaft = GetComponentInChildren<PistonShaft>();
        if (pistonShaft != null)
            pistonShaft.SetShaftCollider(shaftCollider);
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
        if (Mathf.Abs(cachedWorldExtendDirection.y) >= 0.5f)
            return Vector3.up * Mathf.Sign(cachedWorldExtendDirection.y);

        return RunodeMovement.GetCardinalAxis(cachedWorldExtendDirection);
    }

    private bool IsHorizontalExtend()
    {
        return Mathf.Abs(cachedWorldExtendDirection.y) < 0.5f;
    }

    private bool ShouldCarryRunodesOnFace()
    {
        return cachedWorldExtendDirection.y > 0.5f;
    }

    // Reads the Extension Direction marker once at startup to lock extend axis to the hierarchy.
    private void CacheExtensionDirection()
    {
        Transform extensionDirection = FindExtensionDirectionTransform();
        Debug.Assert(extensionDirection != null, $"{nameof(Piston)} on {name} requires a child named '{ExtensionDirectionName}'.", this);

        Transform faceParent = pistonFace.parent;
        Vector3 worldDirection = extensionDirection.position - faceParent.position;
        if (worldDirection.sqrMagnitude < 0.0001f)
            worldDirection = faceParent.forward;

        cachedWorldExtendDirection = worldDirection.normalized;
        cachedTravelAxisInFaceParentLocal = faceParent.InverseTransformDirection(cachedWorldExtendDirection);

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

        Debug.Assert(primaryFlatProbe != null, $"{nameof(Piston)} on {name} requires '{PrimaryFlatProbeName}' under the piston face.", this);
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

        if (nextStage > currentStage)
            yield return ExtendByGridSteps(nextStage);
        else
            yield return AnimateRetractToStage(nextStage);

        if (extendBlocked && currentStage > 0)
        {
            stageDirection = 1;
            extendBlocked = false;
            yield return AnimateRetractToStage(0);
        }

        isMoving = false;
    }

    // Extends one grid step at a time, running probe overlap checks before each step.
    private IEnumerator ExtendByGridSteps(int targetStage)
    {
        List<RunodeMovement> carriedRunodes = ShouldCarryRunodesOnFace() ? GetRunodesOnFace() : null;

        if (carriedRunodes != null && carriedRunodes.Count > 0)
        {
            foreach (RunodeMovement runode in carriedRunodes)
                runode.SetKinematic(true);
        }

        Vector3 startWorldPosition = pistonFace.position;

        while (currentStage < targetStage)
        {
            if (!TryPrepareExtendStep(carriedRunodes, GetWorldPush()))
            {
                extendBlocked = true;
                break;
            }

            yield return AnimateOneGridStep(carriedRunodes);
            if (extendBlocked)
                break;

            currentStage++;
            RefreshRunodesNearFace(pistonFace.position);
        }

        if (carriedRunodes != null && carriedRunodes.Count > 0)
        {
            foreach (RunodeMovement runode in carriedRunodes)
                runode.SetKinematic(false);
        }

        RefreshRunodesNearFace(startWorldPosition);
        RefreshRunodesNearFace(pistonFace.position);
    }

    // Runs probe checks before one 1 m step. Primary flat during the slide stops on anything except Tim.
    private bool TryPrepareExtendStep(List<RunodeMovement> carriedRunodes, Vector3 worldPush)
    {
        if (IsPrimaryFlatPlanningBlocked(carriedRunodes))
            return false;

        if (!IsHorizontalExtend())
            return TryPrepareVerticalStep(carriedRunodes);

        return TryPrepareHorizontalPushStep(worldPush);
    }

    private bool IsPrimaryFlatPlanningBlocked(List<RunodeMovement> carriedRunodes)
    {
        foreach (Collider hit in GetProbeHits(primaryFlatProbe))
        {
            if (TryGetCarriedCube(hit, carriedRunodes, out _))
                continue;

            if (TryGetPiston(hit, out _))
                return true;

            if (IsStaticObstruction(hit))
                return true;
        }

        return false;
    }

    private bool TryPrepareVerticalStep(List<RunodeMovement> carriedRunodes)
    {
        if (GetCubeInProbe(primaryFlatProbe, carriedRunodes) != null)
            return false;

        if (cachedWorldExtendDirection.y < -0.5f)
            return true;

        RunodeMovement cubeInPrimary = GetCubeInProbe(primaryCubeProbe, carriedRunodes);
        RunodeMovement cubeInSecondary = GetCubeInProbe(secondaryCubeProbe, carriedRunodes);
        if (cubeInPrimary != null && cubeInSecondary != null)
            return false;

        return !ProbeHasBlockingObstruction(primaryCubeProbe, carriedRunodes, null);
    }

    private bool TryPrepareHorizontalPushStep(Vector3 worldPush)
    {
        RunodeMovement cubeInPushCell = GetCubeInProbe(primaryCubeProbe, null);
        if (cubeInPushCell == null)
            return true;

        RunodeMovement cubeOnPlatform = GetCubeInProbe(primaryFlatProbe, null);
        if (cubeOnPlatform != cubeInPushCell)
            return true;

        foreach (Collider hit in GetProbeHits(secondaryFlatProbe))
        {
            if (TryGetPiston(hit, out _))
                return false;
        }

        foreach (Collider hit in GetProbeHits(secondaryCubeProbe))
        {
            if (TryGetCube(hit, out RunodeMovement landingCube) && landingCube != cubeInPushCell)
                return false;

            if (TryGetPiston(hit, out _))
                return false;

            if (IsStaticObstruction(hit))
                return false;
        }

        return TryPushCube(cubeInPushCell, worldPush);
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

    private bool ProbeHasBlockingObstruction(BoxCollider probe, List<RunodeMovement> carriedRunodes, RunodeMovement ignoredCube)
    {
        foreach (Collider hit in GetProbeHits(probe))
        {
            if (TryGetCarriedCube(hit, carriedRunodes, out _))
                continue;

            if (TryGetCube(hit, out RunodeMovement cube))
            {
                if (ignoredCube != null && cube == ignoredCube)
                    continue;

                if (pushedCubesThisMove.Contains(cube))
                    continue;

                return true;
            }

            if (TryGetPiston(hit, out _))
                return true;

            if (IsStaticObstruction(hit))
                return true;
        }

        return false;
    }

    private bool HasVerticalUpCubeProbeSlideObstruction(List<RunodeMovement> carriedRunodes)
    {
        return ProbeHasBlockingObstruction(primaryCubeProbe, carriedRunodes, null);
    }

    private bool HasPrimaryFlatSlideObstruction(List<RunodeMovement> carriedRunodes)
    {
        foreach (Collider hit in GetProbeHits(primaryFlatProbe))
        {
            if (TryGetCarriedCube(hit, carriedRunodes, out _))
                continue;

            if (TryGetTim(hit, out _))
                continue;

            if (TryGetCube(hit, out RunodeMovement cube))
            {
                if (pushedCubesThisMove.Contains(cube))
                    continue;

                return true;
            }

            if (TryGetPiston(hit, out _))
                return true;

            if (IsStaticObstruction(hit))
                return true;
        }

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

    private static bool TryGetTim(Collider hit, out CharacterMovement tim)
    {
        tim = hit.GetComponentInParent<CharacterMovement>();
        return tim != null && !tim.IsDead;
    }

    private bool TryGetPiston(Collider hit, out Piston piston)
    {
        piston = hit.GetComponentInParent<Piston>();
        return piston != null && piston != this;
    }

    private bool IsStaticObstruction(Collider hit)
    {
        if (TryGetTim(hit, out _))
            return false;

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
        Vector3 previousWorldPosition = pistonFace.position;
        float duration = moveSpeed > 0f ? GridUnit / moveSpeed : 0f;
        bool stoppedEarly = false;

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

            if (HasPrimaryFlatSlideObstruction(carriedRunodes))
            {
                stoppedEarly = true;
                break;
            }

            if (carriedRunodes != null && carriedRunodes.Count > 0 && ShouldCarryRunodesOnFace()
                && HasVerticalUpCubeProbeSlideObstruction(carriedRunodes))
            {
                stoppedEarly = true;
                break;
            }

            yield return null;
        }

        if (!stoppedEarly)
            pistonFace.localPosition = end;
        else
            extendBlocked = true;

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
        List<RunodeMovement> carriedRunodes = ShouldCarryRunodesOnFace() ? GetRunodesOnFace() : null;

        if (carriedRunodes != null && carriedRunodes.Count > 0)
        {
            foreach (RunodeMovement runode in carriedRunodes)
                runode.SetKinematic(true);
        }

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

        if (carriedRunodes != null && carriedRunodes.Count > 0)
        {
            Vector3 endWorldPosition = pistonFace.parent.TransformPoint(end);
            Vector3 finalDelta = endWorldPosition - pistonFace.position;
            MoveCarriedRunodes(carriedRunodes, finalDelta);

            foreach (RunodeMovement runode in carriedRunodes)
                runode.SetKinematic(false);
        }

        RefreshRunodesNearFace(startWorldPosition);
        RefreshRunodesNearFace(pistonFace.position);

        currentStage = targetStage;
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

}
