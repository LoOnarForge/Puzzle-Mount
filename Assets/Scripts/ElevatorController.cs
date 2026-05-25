using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Elevator platform controlled by a LeverController.
/// Type stop positions directly into the Stops list.
/// Visits stops in order 0 → 1 → 2 → ... → last → 0 → ...
/// Mid-travel Toggle calls are ignored.
/// Runodes tagged "Runode" resting on the platform travel with it.
public class ElevatorController : MonoBehaviour, ILeverTarget
{
    [Header("STOPS")]
    [Tooltip("World-space positions to visit in order.")]
    public List<Vector3> stops = new List<Vector3>();

    [Header("TRAVEL")]
    [Tooltip("Speed in meters per second. 2 = crosses 2 units per second.")]
    public float metersPerSecond = 2f;

    [Header("DEBUG")]
    public int  currentStopIndex = 0;
    public bool isMoving         = false;

    private Collider platformCollider;

    private void Start()
    {
        platformCollider = GetComponent<Collider>();
    }

    /// Required by ILeverTarget — not used yet.
    public void SetPowerColor(Color color) { }

    /// Advances to the next stop. Ignored while already moving.
    public void Toggle()
    {
        if (isMoving) return;
        if (stops == null || stops.Count < 2) return;

        currentStopIndex = (currentStopIndex + 1) % stops.Count;
        StartCoroutine(TravelTo(stops[currentStopIndex]));
    }

    private IEnumerator TravelTo(Vector3 destination)
    {
        isMoving = true;

        Vector3 origin   = transform.position;
        float   distance = Vector3.Distance(origin, destination);
        float   duration = distance / Mathf.Max(metersPerSecond, 0.01f);
        float   elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float   t      = Mathf.Clamp01(elapsed / duration);
            float   smooth = Mathf.SmoothStep(0f, 1f, t);
            Vector3 next   = Vector3.Lerp(origin, destination, smooth);
            Vector3 delta  = next - transform.position;

            transform.position = next;
            MoveRiders(delta);

            yield return null;
        }

        transform.position = destination;
        isMoving           = false;
    }

    /// Shifts any Runode resting on top of the platform by the same delta the platform moved.
    private void MoveRiders(Vector3 delta)
    {
        if (platformCollider == null) return;

        Bounds  b           = platformCollider.bounds;
        Vector3 probeCenter = new Vector3(b.center.x, b.max.y + 0.1f, b.center.z);
        Vector3 probeHalf   = new Vector3(b.extents.x * 0.85f, 0.15f, b.extents.z * 0.85f);

        Collider[]         hits  = Physics.OverlapBox(probeCenter, probeHalf);
        HashSet<Transform> moved = new HashSet<Transform>();

        foreach (Collider col in hits)
        {
            if (col.isTrigger)               continue;
            if (col.gameObject == gameObject) continue;
            if (!col.CompareTag("Runode"))    continue;

            Transform root = col.transform.root;
            if (moved.Contains(root))        continue;
            moved.Add(root);

            root.position += delta;
        }
    }
}
