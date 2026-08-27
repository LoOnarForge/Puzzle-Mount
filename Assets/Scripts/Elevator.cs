using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Elevator : MonoBehaviour
{
    private const int SocketCount = 2;
    private const float DefaultMoveSpeed = 2f;
    private const float PlatformTopTolerance = 0.1f;

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

    [SerializeField] private Transform platform;
    [SerializeField] private List<Transform> stops = new List<Transform>();
    [SerializeField] private float moveSpeed = DefaultMoveSpeed;
    [SerializeField] private int currentStopIndex;
    [SerializeField] private float deathForceMultiplier = 1.5f;
    [SerializeField] private bool isPowered;

    private bool isElevatorMoving;
    public bool IsMoving => isElevatorMoving;
    private Collider platformCollider;
    private CharacterMovement tim;
    private Vector3 currentMoveDirection;
    private readonly Collider[] crushOverlapResults = new Collider[8];

    private void Awake()
    {
        if (platform != null)
            platformCollider = platform.GetComponent<Collider>();

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
        if (!isPowered || isElevatorMoving || platform == null || stops.Count < 2)
            return;

        int nextStopIndex = (currentStopIndex + 1) % stops.Count;

        if (stops[nextStopIndex] == null)
            return;

        StartCoroutine(MoveToStop(nextStopIndex));
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

        RefreshElevatorState();
    }

    private void RefreshElevatorState()
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

    // Lerps the platform in a straight line to the next stop and carries Runodes on it.
    private IEnumerator MoveToStop(int nextStopIndex)
    {
        isElevatorMoving = true;

        Vector3 startPosition = platform.position;
        Vector3 endPosition = stops[nextStopIndex].position;
        float distance = Vector3.Distance(startPosition, endPosition);
        currentMoveDirection = distance > 0.001f ? (endPosition - startPosition).normalized : Vector3.zero;

        List<Elevator> carriedElevators = GetElevatorsOnPlatform();
        List<RunodeMovement> carriedRunodes = GetAllCarriedRunodes(carriedElevators);

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
                MoveCarriedElevators(carriedElevators, delta);
                TryCrushTim();

                previousPosition = newPosition;
                yield return null;
            }
        }

        Vector3 finalDelta = endPosition - platform.position;
        platform.position = endPosition;
        MoveCarriedRunodes(carriedRunodes, finalDelta);
        MoveCarriedElevators(carriedElevators, finalDelta);

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

    private void MoveCarriedRunodes(List<RunodeMovement> carriedRunodes, Vector3 delta)
    {
        foreach (RunodeMovement runode in carriedRunodes)
            runode.transform.position += delta;
    }

    private List<RunodeMovement> GetRunodesOnPlatform()
    {
        List<RunodeMovement> carriedRunodes = new List<RunodeMovement>();
        AppendRunodesOnCollider(platformCollider, carriedRunodes);
        return carriedRunodes;
    }

    private List<RunodeMovement> GetAllCarriedRunodes(List<Elevator> carriedElevators)
    {
        List<RunodeMovement> carriedRunodes = GetRunodesOnPlatform();

        foreach (Elevator carriedElevator in carriedElevators)
            AppendRunodesOnCollider(carriedElevator.platformCollider, carriedRunodes);

        return carriedRunodes;
    }

    private static void AppendRunodesOnCollider(Collider carrierCollider, List<RunodeMovement> carriedRunodes)
    {
        if (carrierCollider == null)
            return;

        Bounds bounds = carrierCollider.bounds;
        RunodeMovement[] allRunodes = Object.FindObjectsByType<RunodeMovement>();

        foreach (RunodeMovement runode in allRunodes)
        {
            if (carriedRunodes.Contains(runode))
                continue;

            Vector3 position = runode.transform.position;

            bool overPlatform =
                position.x >= bounds.min.x && position.x <= bounds.max.x &&
                position.z >= bounds.min.z && position.z <= bounds.max.z;

            bool onOrAbovePlatform =
                position.y >= bounds.max.y - PlatformTopTolerance;

            if (overPlatform && onOrAbovePlatform)
                carriedRunodes.Add(runode);
        }
    }

    private List<Elevator> GetElevatorsOnPlatform()
    {
        List<Elevator> carriedElevators = new List<Elevator>();
        HashSet<Elevator> scanned = new HashSet<Elevator> { this };
        Queue<Elevator> platformSources = new Queue<Elevator>();
        platformSources.Enqueue(this);

        while (platformSources.Count > 0)
        {
            Elevator source = platformSources.Dequeue();
            if (source.platformCollider == null)
                continue;

            Bounds bounds = source.platformCollider.bounds;

            foreach (Elevator candidate in Object.FindObjectsByType<Elevator>())
            {
                if (candidate == this || carriedElevators.Contains(candidate) || candidate.IsMoving)
                    continue;

                if (!IsRestingOnPlatform(candidate, bounds))
                    continue;

                carriedElevators.Add(candidate);

                if (scanned.Add(candidate))
                    platformSources.Enqueue(candidate);
            }
        }

        return carriedElevators;
    }

    private static bool IsRestingOnPlatform(Elevator elevator, Bounds carrierBounds)
    {
        Collider otherCollider = elevator.platformCollider;
        if (otherCollider == null)
            return false;

        Bounds otherBounds = otherCollider.bounds;

        bool overPlatform =
            otherBounds.center.x >= carrierBounds.min.x && otherBounds.center.x <= carrierBounds.max.x &&
            otherBounds.center.z >= carrierBounds.min.z && otherBounds.center.z <= carrierBounds.max.z;

        bool onOrAbovePlatform =
            otherBounds.min.y >= carrierBounds.max.y - PlatformTopTolerance;

        return overPlatform && onOrAbovePlatform;
    }

    private static void MoveCarriedElevators(List<Elevator> carriedElevators, Vector3 delta)
    {
        foreach (Elevator carriedElevator in carriedElevators)
            carriedElevator.transform.position += delta;
    }
}
