using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(CharacterMovement))]
public class TimCrushEvaluator : MonoBehaviour
{
    private const int OverlapBufferSize = 16;
    private const float MinDirectionSqrMagnitude = 0.0001f;
    private const string TimLayerName = "TimJones";

    [SerializeField] private LayerMask crushLayerMask;
    [SerializeField] private float penetrationThreshold = 0.1f;
    [SerializeField] private float minSandwichPenetration = 0.02f;
    [SerializeField] private float sandwichNormalDotThreshold = -0.3f;
    [SerializeField] private bool drawDebugGizmos;

    private CharacterController controller;
    private CharacterMovement characterMovement;
    private CapsuleCollider probeCapsule;
    private Transform probeTransform;
    private readonly Collider[] overlapResults = new Collider[OverlapBufferSize];

    private struct PenetrationHit
    {
        public Vector3 Direction;
        public float Depth;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        characterMovement = GetComponent<CharacterMovement>();
        CreateProbeCapsule();

        if (crushLayerMask.value == 0)
            crushLayerMask = ~LayerMask.GetMask(TimLayerName);
    }

    private void OnDestroy()
    {
        if (probeTransform != null)
            Destroy(probeTransform.gameObject);
    }

    private void FixedUpdate()
    {
        if (characterMovement == null || characterMovement.IsDead)
            return;

        SyncProbeFromController();

        if (!TryGetCapsulePoints(out Vector3 bottom, out Vector3 top))
            return;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            bottom,
            top,
            controller.radius,
            overlapResults,
            crushLayerMask,
            QueryTriggerInteraction.Ignore);

        PenetrationHit maxHit = default;
        bool hasMaxHit = false;
        PenetrationHit firstSandwichHit = default;
        PenetrationHit secondSandwichHit = default;
        bool hasFirstSandwichHit = false;
        bool hasSandwich = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider other = overlapResults[i];
            if (other == null || ShouldIgnoreCollider(other))
                continue;

            if (!TryGetPenetration(other, out PenetrationHit hit))
                continue;

            if (!hasMaxHit || hit.Depth > maxHit.Depth)
            {
                maxHit = hit;
                hasMaxHit = true;
            }

            if (hit.Depth < minSandwichPenetration)
                continue;

            if (!hasFirstSandwichHit)
            {
                firstSandwichHit = hit;
                hasFirstSandwichHit = true;
                continue;
            }

            if (Vector3.Dot(firstSandwichHit.Direction, hit.Direction) <= sandwichNormalDotThreshold)
            {
                secondSandwichHit = hit;
                hasSandwich = true;
                break;
            }
        }

        if (hasSandwich)
        {
            Vector3 crushDirection = firstSandwichHit.Direction + secondSandwichHit.Direction;
            if (crushDirection.sqrMagnitude < MinDirectionSqrMagnitude)
                crushDirection = firstSandwichHit.Direction;

            ApplyCrushDeath(crushDirection.normalized);
            return;
        }

        if (hasMaxHit && maxHit.Depth >= penetrationThreshold)
            ApplyCrushDeath(maxHit.Direction);
    }

    // Builds a hidden capsule collider that mirrors Tim's CharacterController for penetration tests.
    private void CreateProbeCapsule()
    {
        GameObject probeObject = new GameObject("TimCrushProbe");
        probeObject.transform.SetParent(transform, false);
        probeTransform = probeObject.transform;
        probeCapsule = probeObject.AddComponent<CapsuleCollider>();
        probeCapsule.isTrigger = true;
        probeObject.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    private void SyncProbeFromController()
    {
        probeCapsule.center = controller.center;
        probeCapsule.radius = controller.radius;
        probeCapsule.height = controller.height;
        probeCapsule.direction = 1;
    }

    private bool TryGetCapsulePoints(out Vector3 bottom, out Vector3 top)
    {
        float halfHeight = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        Vector3 worldCenter = transform.TransformPoint(controller.center);
        Vector3 axis = transform.up;

        bottom = worldCenter - axis * halfHeight;
        top = worldCenter + axis * halfHeight;
        return controller.radius > 0f && controller.height > 0f;
    }

    private bool ShouldIgnoreCollider(Collider other)
    {
        if (other.transform.IsChildOf(transform))
            return true;

        if (other == probeCapsule)
            return true;

        if (other.GetComponentInParent<RunodeDebrisChunk>() != null)
            return true;

        return other.isTrigger;
    }

    private bool TryGetPenetration(Collider other, out PenetrationHit hit)
    {
        hit = default;

        if (!Physics.ComputePenetration(
                probeCapsule,
                probeTransform.position,
                probeTransform.rotation,
                other,
                other.transform.position,
                other.transform.rotation,
                out Vector3 separationDirection,
                out float separationDistance))
        {
            return false;
        }

        if (separationDistance <= 0f)
            return false;

        hit.Depth = separationDistance;
        hit.Direction = -separationDirection.normalized;
        return hit.Direction.sqrMagnitude >= MinDirectionSqrMagnitude;
    }

    private void ApplyCrushDeath(Vector3 crushDirection)
    {
        if (crushDirection.sqrMagnitude < MinDirectionSqrMagnitude)
            crushDirection = -transform.forward;

        characterMovement.ApplyDeathToss(crushDirection);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        CharacterController drawController = controller != null ? controller : GetComponent<CharacterController>();
        if (drawController == null)
            return;

        float halfHeight = Mathf.Max(0f, drawController.height * 0.5f - drawController.radius);
        Vector3 worldCenter = transform.TransformPoint(drawController.center);
        Vector3 axis = transform.up;
        Vector3 bottom = worldCenter - axis * halfHeight;
        Vector3 top = worldCenter + axis * halfHeight;

        Gizmos.color = Color.red;
        DrawWireCapsule(bottom, top, drawController.radius);
    }

    private static void DrawWireCapsule(Vector3 bottom, Vector3 top, float radius)
    {
        Gizmos.DrawWireSphere(bottom, radius);
        Gizmos.DrawWireSphere(top, radius);
    }
}
