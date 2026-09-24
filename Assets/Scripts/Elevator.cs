using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elevator : MonoBehaviour
{
    private const int SocketCount = 2;
    private const float DefaultMoveSpeed = 2f;
    private const float GridUnit = 1f;
    private const float PlatformOccupancyHeight = GridUnit * 2f;
    private const float PlatformOccupancyXZ = GridUnit - 0.1f;

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

    [SerializeField] private Transform platform;
    [SerializeField] private List<Transform> stops = new List<Transform>();
    [SerializeField] private float moveSpeed = DefaultMoveSpeed;
    [SerializeField] private int currentStopIndex;
    [SerializeField] private float deathForceMultiplier = 1.5f;
    [SerializeField] private bool isPowered;

    private bool isElevatorMoving;
    public bool IsMoving => isElevatorMoving;
    public int StageCount => stops.Count;
    private Collider platformCollider;
    private CharacterMovement tim;
    private Vector3 currentMoveDirection;
    private readonly Collider[] crushOverlapResults = new Collider[8];
    private readonly Collider[] platformOccupancyResults = new Collider[16];
    private int platformOccupancyMask;
    private int timJonesLayer;

    private void Awake()
    {
        if (platform != null)
            platformCollider = platform.GetComponent<Collider>();

        platformOccupancyMask = LayerMask.GetMask("Runodes", "TimJones");
        timJonesLayer = LayerMask.NameToLayer("TimJones");

        if (platform != null && stops.Count > 0)
        {
            currentStopIndex = Mathf.Clamp(currentStopIndex, 0, stops.Count - 1);

            if (stops[currentStopIndex] != null)
                platform.position = stops[currentStopIndex].position;
        }

        InitialSocketConfiguration();
    }

    private void Start()
    {
        tim = FindAnyObjectByType<CharacterMovement>();
    }

    // Called by a DevicePowerSocket when its power state changes.
    public void UpdateSocketStateToOwner(int ownerIndex, int allocatedMw, PowerSource poweringSource)
    {
        if (ownerIndex < 0 || ownerIndex >= sockets.Length)
            return;

        sockets[ownerIndex].allocatedMw = allocatedMw;
        sockets[ownerIndex].poweringSource = poweringSource;
        RefreshElevatorState();
    }

    // Called by a powered Lever when the player operates it.
    public void OnLeverOperated()
    {
        if ((!isSelfPowered && !isPowered) || isElevatorMoving || platform == null || stops.Count < 2)
            return;

        if (IsPlatformOccupancyBlocked(out _))
            return;

        int nextStopIndex = (currentStopIndex + 1) % stops.Count;

        if (stops[nextStopIndex] == null)
            return;

        StartCoroutine(MoveToStop(nextStopIndex));
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

        RefreshElevatorState();
    }

    private void RefreshElevatorState()
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

    // Lerps the platform in a straight line to the next stop and carries Runodes on it.
    private IEnumerator MoveToStop(int nextStopIndex)
    {
        isElevatorMoving = true;

        Vector3 startPosition = platform.position;
        Vector3 endPosition = stops[nextStopIndex].position;
        float distance = Vector3.Distance(startPosition, endPosition);
        currentMoveDirection = distance > 0.001f ? (endPosition - startPosition).normalized : Vector3.zero;

        if (IsPlatformOccupancyBlocked(out RunodeMovement carriedRunode))
        {
            isElevatorMoving = false;
            yield break;
        }

        List<RunodeMovement> carriedRunodes = new List<RunodeMovement>();
        if (carriedRunode != null)
            carriedRunodes.Add(carriedRunode);

        foreach (RunodeMovement runode in carriedRunodes)
            runode.SetKinematic(true);

        if (distance > 0.001f && moveSpeed > 0f)
        {
            float moveTime = distance / moveSpeed;
            float elapsed = 0f;
            Vector3 previousPosition = startPosition;

            while (elapsed < moveTime)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / moveTime);
                Vector3 newPosition = Vector3.Lerp(startPosition, endPosition, progress);
                Vector3 delta = newPosition - previousPosition;

                platform.position = newPosition;
                MoveCarriedRunodes(carriedRunodes, delta);
                // TryCrushTim();

                previousPosition = newPosition;
                yield return null;
            }
        }

        Vector3 finalDelta = endPosition - platform.position;
        platform.position = endPosition;
        MoveCarriedRunodes(carriedRunodes, finalDelta);

        foreach (RunodeMovement runode in carriedRunodes)
        {
            runode.SetKinematic(false);

            RunodeCube runodeCube = runode.GetComponent<RunodeCube>();
            if (runodeCube != null)
                runodeCube.RefreshCubeAndAdjacentConnections();
        }

        currentStopIndex = nextStopIndex;
        isElevatorMoving = false;
        currentMoveDirection = Vector3.zero;
    }

    /*
    private void TryCrushTim()
    {
        if (tim == null || tim.IsDead || platformCollider == null || currentMoveDirection == Vector3.zero)
            return;

        Bounds killBounds = platformCollider.bounds;
        Vector3 killExtents = killBounds.extents * 1.2f;

        int hitCount = Physics.OverlapBoxNonAlloc(
            killBounds.center,
            killExtents,
            crushOverlapResults,
            platformCollider.transform.rotation);

        for (int i = 0; i < hitCount; i++)
        {
            CharacterMovement hitTim = crushOverlapResults[i].GetComponent<CharacterMovement>();
            if (hitTim == null)
                hitTim = crushOverlapResults[i].GetComponentInParent<CharacterMovement>();

            if (hitTim != tim)
                continue;

            tim.ApplyDeathToss(-currentMoveDirection, deathForceMultiplier);
            return;
        }
    }
    */

    private void MoveCarriedRunodes(List<RunodeMovement> carriedRunodes, Vector3 delta)
    {
        foreach (RunodeMovement runode in carriedRunodes)
            runode.transform.position += delta;
    }

    // True when Tim is in the platform stack volume or more than one Runode is.
    private bool IsPlatformOccupancyBlocked(out RunodeMovement singleRunode)
    {
        singleRunode = null;

        if (platformCollider == null || platform == null)
            return true;

        if (!TryGetPlatformOccupancyBox(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation))
            return true;

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            platformOccupancyResults,
            orientation,
            platformOccupancyMask,
            QueryTriggerInteraction.Collide);

        RunodeMovement foundRunode = null;
        int runodeCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = platformOccupancyResults[i];
            if (hit == null)
                continue;

            if (IsTimOnPlatform(hit))
                return true;

            if (!TryGetRunodeFromCollider(hit, out RunodeMovement runode))
                continue;

            if (foundRunode == runode)
                continue;

            runodeCount++;
            if (runodeCount > 1)
                return true;

            foundRunode = runode;
        }

        singleRunode = foundRunode;
        return false;
    }

    private bool TryGetPlatformOccupancyBox(out Vector3 center, out Vector3 halfExtents, out Quaternion orientation)
    {
        center = default;
        halfExtents = default;
        orientation = Quaternion.identity;

        if (platformCollider == null || platform == null)
            return false;

        Bounds platformBounds = platformCollider.bounds;
        float halfHeight = PlatformOccupancyHeight * 0.5f;
        float halfXZ = PlatformOccupancyXZ * 0.5f;

        center = new Vector3(
            platformBounds.center.x,
            platformBounds.max.y + halfHeight,
            platformBounds.center.z);

        halfExtents = new Vector3(halfXZ, halfHeight, halfXZ);
        orientation = platform.rotation;
        return true;
    }

    private bool IsTimOnPlatform(Collider hit)
    {
        if (hit.gameObject.layer == timJonesLayer)
            return true;

        return hit.GetComponentInParent<CharacterMovement>() != null;
    }

    private static bool TryGetRunodeFromCollider(Collider hit, out RunodeMovement runode)
    {
        runode = hit.GetComponentInParent<RunodeMovement>();
        return runode != null;
    }
}
