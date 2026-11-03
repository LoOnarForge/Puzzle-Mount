using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class MovingPlatform : MonoBehaviour
{
    private const int MaxStopCount = 10;
    private const float MinLegDistance = 0.001f;
    private const int MinSpeed = 0;
    private const int MaxSpeed = 10;
    private const int MinStopTimer = 0;
    private const int MaxStopTimer = 10;
    private const float TimPlatformVerticalSlack = 2f;
    private const float TimPlatformHorizontalSlack = 0.25f;

    private static readonly List<MovingPlatform> ActivePlatforms = new List<MovingPlatform>();

    [System.Serializable]
    private class RouteStop
    {
        public Transform stopTransform;
        [Range(MinStopTimer, MaxStopTimer)] public int stopTimer;
        [Range(MinSpeed, MaxSpeed)] public int speed;
    }

    [SerializeField] private bool isTransportingTim;
    [SerializeField] private bool isTransportingRunodes;
    [SerializeField] private Collider occupancyTrigger;
    [SerializeField] private Transform platform;
    [SerializeField] private List<RouteStop> stops = new List<RouteStop>();
    [SerializeField] private int currentStopIndex;

    private bool isPlatformMoving;
    private bool legInProgress;
    private float legElapsed;
    private float legDuration;
    private Vector3 legStartPosition;
    private Vector3 legEndPosition;
    private List<RunodeMovement> legRunodes = new List<RunodeMovement>();
    private Vector3 platformMotionDelta;

    private readonly Collider[] platformOccupancyResults = new Collider[16];
    private int platformOccupancyMask;
    private CharacterMovement sceneTim;

    private void OnEnable()
    {
        if (!ActivePlatforms.Contains(this))
            ActivePlatforms.Add(this);
    }

    private void OnDisable()
    {
        ActivePlatforms.Remove(this);
        legInProgress = false;
        isPlatformMoving = false;
    }

    private void Awake()
    {
        if (platform == null)
            platform = transform;

        platformOccupancyMask = LayerMask.GetMask("Runodes", "TimJones");

        Debug.Assert(occupancyTrigger != null, $"{nameof(MovingPlatform)} on {name} requires {nameof(occupancyTrigger)} assigned.", this);
        if (occupancyTrigger != null)
        {
            Debug.Assert(occupancyTrigger.isTrigger, $"{nameof(MovingPlatform)} on {name} requires {nameof(occupancyTrigger)} to be a trigger.", this);

            if (!occupancyTrigger.transform.IsChildOf(platform))
                occupancyTrigger.transform.SetParent(platform, true);
        }

        if (stops.Count > 0)
        {
            currentStopIndex = Mathf.Clamp(currentStopIndex, 0, stops.Count - 1);
            SnapToStop(currentStopIndex);
        }
    }

    private void Start()
    {
        sceneTim = FindAnyObjectByType<CharacterMovement>();
        StartCoroutine(RouteLoop());
    }

    private void Update()
    {
        Vector3 platformPositionBefore = platform.position;

        if (legInProgress)
            TickLeg(Time.deltaTime);

        platformMotionDelta = platform.position - platformPositionBefore;
    }

    // Called at the start of Tim's Update so platform carry happens before ground checks and gravity.
    public static void ApplyCarriedMotionToTim(CharacterMovement tim, CharacterController controller)
    {
        if (tim == null || controller == null)
            return;

        foreach (MovingPlatform movingPlatform in ActivePlatforms)
        {
            if (!movingPlatform.TryGetTimCarryDelta(tim, out Vector3 delta))
                continue;

            controller.Move(delta);
            return;
        }
    }

    // True when Tim is in the ride volume of a platform that carries him (used for jump coyote on moving decks).
    public static bool IsTimInActiveTransportArea(CharacterMovement tim)
    {
        if (tim == null)
            return false;

        foreach (MovingPlatform movingPlatform in ActivePlatforms)
        {
            if (!movingPlatform.isTransportingTim)
                continue;

            if (movingPlatform.IsTimRiding(tim))
                return true;
        }

        return false;
    }

    public static bool IsTimPhysicallyOnTransportDeck(CharacterMovement tim)
    {
        if (tim == null)
            return false;

        foreach (MovingPlatform movingPlatform in ActivePlatforms)
        {
            if (!movingPlatform.isTransportingTim)
                continue;

            if (movingPlatform.IsTimStandingOnPlatform(tim))
                return true;
        }

        return false;
    }

    private bool TryGetTimCarryDelta(CharacterMovement tim, out Vector3 delta)
    {
        delta = default;

        if (!isTransportingTim || !isPlatformMoving || platformMotionDelta.sqrMagnitude <= 0f)
            return false;

        if (!IsTimRiding(tim))
            return false;

        delta = platformMotionDelta;
        return true;
    }

    private bool IsTimRiding(CharacterMovement tim)
    {
        return tim == GetTimInPlatformArea();
    }

    private IEnumerator RouteLoop()
    {
        while (true)
        {
            if (stops.Count < 2 || platform == null)
            {
                yield return null;
                continue;
            }

            RouteStop stop = stops[currentStopIndex];
            if (stop == null || stop.stopTransform == null)
            {
                yield return null;
                continue;
            }

            int waitSeconds = Mathf.Clamp(stop.stopTimer, MinStopTimer, MaxStopTimer);
            if (waitSeconds > 0)
                yield return new WaitForSeconds(waitSeconds);

            int nextStopIndex = (currentStopIndex + 1) % stops.Count;
            RouteStop nextStop = stops[nextStopIndex];
            if (nextStop == null || nextStop.stopTransform == null)
            {
                yield return null;
                continue;
            }

            int legSpeed = Mathf.Clamp(stop.speed, MinSpeed, MaxSpeed);
            yield return WaitUntilLegCanStart();

            if (TryBeginLeg(nextStopIndex, legSpeed))
            {
                while (legInProgress)
                    yield return null;

                currentStopIndex = nextStopIndex;
            }
        }
    }

    private IEnumerator WaitUntilLegCanStart()
    {
        while (IsLegStartBlocked())
            yield return null;
    }

    private bool IsLegStartBlocked()
    {
        if (TryGetOccupancy(out CharacterMovement timOnPlatform, out List<RunodeMovement> runodes))
        {
            if (timOnPlatform != null && !isTransportingTim)
                return true;

            if (runodes.Count > 0 && !isTransportingRunodes)
                return true;

            if (isTransportingRunodes && runodes.Count > 1)
                return true;
        }

        return false;
    }

    private bool TryBeginLeg(int toStopIndex, int legSpeed)
    {
        Transform toStop = stops[toStopIndex].stopTransform;
        legStartPosition = platform.position;
        legEndPosition = toStop.position;
        float distance = Vector3.Distance(legStartPosition, legEndPosition);

        TryGetOccupancy(out CharacterMovement timOnPlatform, out List<RunodeMovement> carriedRunodes);

        if (timOnPlatform != null && !isTransportingTim)
            return false;

        if (!isTransportingRunodes && carriedRunodes.Count > 0)
            return false;

        if (isTransportingRunodes && carriedRunodes.Count > 1)
            return false;

        legRunodes = carriedRunodes;
        foreach (RunodeMovement runode in legRunodes)
            runode.SetKinematic(true);

        legElapsed = 0f;
        legDuration = legSpeed > 0 && distance > MinLegDistance ? distance / legSpeed : 0f;

        if (legDuration <= 0f)
        {
            SetPlatformWorldPosition(legEndPosition);
            FinishLegRunodes();
            return true;
        }

        isPlatformMoving = true;
        legInProgress = true;
        return true;
    }

    private void TickLeg(float deltaTime)
    {
        legElapsed += deltaTime;
        float progress = legDuration > 0f ? Mathf.Clamp01(legElapsed / legDuration) : 1f;
        Vector3 newPosition = Vector3.Lerp(legStartPosition, legEndPosition, progress);
        Vector3 delta = newPosition - platform.position;

        SetPlatformWorldPosition(newPosition);
        ApplyRunodeTransportDelta(delta, legRunodes);

        if (progress < 1f)
            return;

        SetPlatformWorldPosition(legEndPosition);
        FinishLegRunodes();
        legInProgress = false;
        isPlatformMoving = false;
    }

    private void FinishLegRunodes()
    {
        foreach (RunodeMovement runode in legRunodes)
        {
            runode.SetKinematic(false);

            RunodeCube runodeCube = runode.GetComponent<RunodeCube>();
            if (runodeCube != null)
                runodeCube.RefreshCubeAndAdjacentConnections();
        }

        legRunodes.Clear();
    }

    private void SetPlatformWorldPosition(Vector3 worldPosition)
    {
        platform.position = worldPosition;
    }

    private void ApplyRunodeTransportDelta(Vector3 delta, List<RunodeMovement> carriedRunodes)
    {
        if (delta.sqrMagnitude <= 0f || !isTransportingRunodes)
            return;

        foreach (RunodeMovement runode in carriedRunodes)
            runode.transform.position += delta;
    }

    private void SnapToStop(int stopIndex)
    {
        if (platform == null || stopIndex < 0 || stopIndex >= stops.Count)
            return;

        Transform stop = stops[stopIndex].stopTransform;
        if (stop == null)
            return;

        SetPlatformWorldPosition(stop.position);
    }

    private bool TryGetOccupancy(out CharacterMovement timOnPlatform, out List<RunodeMovement> runodes)
    {
        timOnPlatform = GetTimInPlatformArea();
        runodes = GetRunodesInOccupancyVolume();
        return timOnPlatform != null || runodes.Count > 0;
    }

    private CharacterMovement GetTimInPlatformArea()
    {
        if (sceneTim == null || sceneTim.IsDead)
            return null;

        if (IsCharacterInsideOccupancyVolume(sceneTim))
            return sceneTim;

        if (IsTimStandingOnPlatform(sceneTim))
            return sceneTim;

        return null;
    }

    private bool IsCharacterInsideOccupancyVolume(CharacterMovement tim)
    {
        CharacterController controller = tim.GetComponent<CharacterController>();
        if (controller == null || occupancyTrigger == null)
            return false;

        Physics.SyncTransforms();
        Bounds volume = occupancyTrigger.bounds;

        Vector3 worldCenter = tim.transform.TransformPoint(controller.center);
        Vector3 size = new Vector3(controller.radius * 2f, controller.height, controller.radius * 2f);
        Bounds characterBounds = new Bounds(worldCenter, size);

        return volume.Intersects(characterBounds);
    }

    internal bool IsTimStandingOnPlatform(CharacterMovement tim)
    {
        if (platform == null)
            return false;

        Collider platformCollider = platform.GetComponentInChildren<Collider>();
        if (platformCollider == null || platformCollider == occupancyTrigger)
            return false;

        Bounds bounds = platformCollider.bounds;
        Vector3 timPosition = tim.transform.position;

        if (timPosition.y < bounds.min.y - TimPlatformVerticalSlack || timPosition.y > bounds.max.y + TimPlatformVerticalSlack)
            return false;

        if (timPosition.x < bounds.min.x - TimPlatformHorizontalSlack || timPosition.x > bounds.max.x + TimPlatformHorizontalSlack)
            return false;

        if (timPosition.z < bounds.min.z - TimPlatformHorizontalSlack || timPosition.z > bounds.max.z + TimPlatformHorizontalSlack)
            return false;

        return true;
    }

    private List<RunodeMovement> GetRunodesInOccupancyVolume()
    {
        List<RunodeMovement> runodes = new List<RunodeMovement>();

        if (occupancyTrigger == null)
            return runodes;

        Physics.SyncTransforms();
        Bounds bounds = occupancyTrigger.bounds;

        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            platformOccupancyResults,
            Quaternion.identity,
            platformOccupancyMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = platformOccupancyResults[i];
            if (hit == null || !IsOccupancyHit(hit))
                continue;

            if (TryGetRunodeFromCollider(hit, out RunodeMovement runode) && !runodes.Contains(runode))
                runodes.Add(runode);
        }

        return runodes;
    }

    private bool IsOccupancyHit(Collider hit)
    {
        if (hit == occupancyTrigger)
            return false;

        if (hit.transform.IsChildOf(platform))
            return false;

        return true;
    }

    private static bool TryGetRunodeFromCollider(Collider hit, out RunodeMovement runode)
    {
        runode = hit.GetComponentInParent<RunodeMovement>();
        return runode != null;
    }

    public int MaxStops => MaxStopCount;
}
