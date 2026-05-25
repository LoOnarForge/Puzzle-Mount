using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Elevator platform controlled by a LeverController.
/// Visits stops in order 0 → 1 → 2 → ... → last → 0 → ...
/// Mid-travel Toggle calls are ignored.
/// Cargo Runodes are locked (kinematic) for the entire trip so nothing can push them off-grid.
/// If a Runode blocks the path the elevator waits obstacleReturnDelay seconds then returns to the previous stop.
/// On arrival the elevator and all cargo are snapped to exact positions.
public class ElevatorController : MonoBehaviour, ILeverTarget
{
    [Header("STOPS")]
    [Tooltip("World-space positions to visit in order.")]
    public List<Vector3> stops = new List<Vector3>();

    [Header("TRAVEL")]
    [Tooltip("Speed in meters per second.")]
    public float metersPerSecond = 2f;

    [Tooltip("Height of one Runode cube on the platform. Shifts the upward obstacle probe above the carried cube.")]
    public float cargoHeight = 1f;

    [Tooltip("Seconds to wait after detecting a persistent obstacle before returning to the previous stop.")]
    public float obstacleReturnDelay = 0.3f;

    [Header("DEBUG")]
    public int  currentStopIndex  = 0;
    public bool isMoving          = false;
    public bool isBlocked         = false;

    private const int   RunodeLayer       = 13;
    private const float LookAheadDistance = 0.3f;

    private Collider platformCollider;
    private int      previousStopIndex = 0;

    // Snapshot of each rider taken at trip start so we can restore and snap on arrival.
    private struct RiderState
    {
        public Rigidbody            rb;
        public RigidbodyConstraints originalConstraints;
        public bool                 originalIsKinematic;
    }

    private readonly List<RiderState> lockedRiders = new List<RiderState>();

    private void Start()
    {
        platformCollider = GetComponent<Collider>();
    }

    /// Required by ILeverTarget.
    public void SetPowerColor(Color color) { }

    /// Advances to the next stop. Ignored while already moving.
    public void Toggle()
    {
        if (isMoving) return;
        if (stops == null || stops.Count < 2) return;

        previousStopIndex = currentStopIndex;
        currentStopIndex  = (currentStopIndex + 1) % stops.Count;
        StartCoroutine(TravelTo(stops[currentStopIndex]));
    }

    private IEnumerator TravelTo(Vector3 destination)
    {
        isMoving  = true;
        isBlocked = false;

        LockCargo();

        Vector3 origin    = transform.position;
        float   distance  = Vector3.Distance(origin, destination);
        float   duration  = distance / Mathf.Max(metersPerSecond, 0.01f);
        Vector3 direction = (destination - origin).normalized;
        float   elapsed   = 0f;
        float   blockedTimer = 0f;
        bool    needsReturn  = false;

        while (elapsed < duration)
        {
            if (IsRunodeInPath(direction))
            {
                isBlocked     = true;
                blockedTimer += Time.deltaTime;

                if (blockedTimer >= obstacleReturnDelay)
                {
                    // Obstacle persisted too long — abort and return to previous stop.
                    needsReturn = true;
                    break;
                }

                yield return null;
                continue;
            }

            // Path is clear — reset block state and advance.
            isBlocked    = false;
            blockedTimer = 0f;
            elapsed     += Time.deltaTime;

            float   t      = Mathf.Clamp01(elapsed / duration);
            float   smooth = Mathf.SmoothStep(0f, 1f, t);
            Vector3 next   = Vector3.Lerp(origin, destination, smooth);
            Vector3 delta  = next - transform.position;

            transform.position = next;
            MoveRiders(delta);

            yield return null;
        }

        if (needsReturn)
        {
            isBlocked = false;

            // Cargo stays locked — ReleaseCargo is called at the end of the return trip.
            int returnIndex  = previousStopIndex;
            currentStopIndex = returnIndex;
            yield return StartCoroutine(TravelTo(stops[returnIndex]));
        }
        else
        {
            // Successful arrival — snap elevator precisely and release cargo.
            transform.position = destination;
            isMoving           = false;
            isBlocked          = false;
            ReleaseCargo();
        }
    }

    // -------------------------------------------------------------------------
    // Obstacle detection
    // -------------------------------------------------------------------------

    /// Returns true if a Runode is directly in the elevator's path.
    /// Only layer 13 is queried — walls, floors and other geometry are invisible.
    private bool IsRunodeInPath(Vector3 direction)
    {
        if (platformCollider == null) return false;

        Bounds  b      = platformCollider.bounds;
        Vector3 absDir = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));

        float forwardExtent = Vector3.Dot(b.extents, absDir);

        // Skip past a Runode riding on top when ascending.
        if (direction.y > 0.5f)
            forwardExtent += cargoHeight;

        Vector3 castCenter = transform.position + direction * (forwardExtent + LookAheadDistance * 0.5f);

        Vector3 halfExtents = new Vector3(
            absDir.x > 0.5f ? LookAheadDistance * 0.5f : b.extents.x,
            absDir.y > 0.5f ? LookAheadDistance * 0.5f : b.extents.y,
            absDir.z > 0.5f ? LookAheadDistance * 0.5f : b.extents.z
        );

        Collider[] hits = Physics.OverlapBox(castCenter, halfExtents, Quaternion.identity, 1 << RunodeLayer);
        return hits.Length > 0;
    }

    // -------------------------------------------------------------------------
    // Rider management
    // -------------------------------------------------------------------------

    /// Shifts all locked riders by the delta the platform moved this frame.
    private void MoveRiders(Vector3 delta)
    {
        foreach (RiderState state in lockedRiders)
        {
            if (state.rb != null)
                state.rb.transform.root.position += delta;
        }
    }

    /// Finds all Runodes on the platform surface and makes them kinematic so nothing can push them.
    private void LockCargo()
    {
        lockedRiders.Clear();

        if (platformCollider == null) return;

        Bounds  b           = platformCollider.bounds;
        Vector3 probeCenter = new Vector3(b.center.x, b.max.y + 0.1f, b.center.z);
        Vector3 probeHalf   = new Vector3(b.extents.x, 0.15f, b.extents.z);

        Collider[]         hits = Physics.OverlapBox(probeCenter, probeHalf, Quaternion.identity, 1 << RunodeLayer);
        HashSet<Transform> seen = new HashSet<Transform>();

        foreach (Collider col in hits)
        {
            if (col.isTrigger) continue;

            Transform root = col.transform.root;
            if (seen.Contains(root)) continue;
            seen.Add(root);

            Rigidbody rb = root.GetComponentInChildren<Rigidbody>();
            if (rb == null) continue;

            lockedRiders.Add(new RiderState
            {
                rb                  = rb,
                originalConstraints = rb.constraints,
                originalIsKinematic = rb.isKinematic
            });

            rb.isKinematic = true;
        }
    }

    /// Restores each rider's physics state.
    private void ReleaseCargo()
    {
        foreach (RiderState state in lockedRiders)
        {
            if (state.rb == null) continue;

            state.rb.isKinematic = state.originalIsKinematic;
            state.rb.constraints = state.originalConstraints;
        }

        lockedRiders.Clear();
    }

    // -------------------------------------------------------------------------
    // Utility
    // -------------------------------------------------------------------------

}
