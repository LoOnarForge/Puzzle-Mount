using UnityEngine;

[DisallowMultipleComponent]
public class PistonShaft : MonoBehaviour
{
    private const float MinSpan = 0.01f;
    private const float FullLightSpan = 1f;
    private const float RetractedSpanTolerance = 0.005f;

    private static readonly Vector3 DefaultLocalNearEdge = new Vector3(0f, 0f, -0.5f);

    [Header("Anchors")]
    [SerializeField] private Transform baseAnchor;
    [SerializeField] private Transform faceAnchor;

    private const float LightOffSpan = 1.25f;
    private const float LightFullFadeSpan = 1.35f;

    [Header("Light")]
    [SerializeField] private Light shaftLight;

    private Transform shaftColliderObject;
    private Vector3 baseLocalScale;
    private Vector3 localNearEdge = DefaultLocalNearEdge;
    private Vector3 colliderBaseLocalScale = Vector3.one;
    private Vector3 colliderLocalNearEdge = DefaultLocalNearEdge;
    private float colliderBaseWorldLength = 1f;
    private Collider shaftCollider;
    private Transform shaftColliderTransform;
    private BoxCollider shaftBoxCollider;
    private Renderer[] shaftRenderers;
    private float baseLightIntensity;
    private float retractedSpan = -1f;
    private float currentSpan;

    private void Awake()
    {
        baseLocalScale = transform.localScale;
        CacheShaftEdgePoint();
        CacheShaftRenderers();

        if (shaftLight != null)
            baseLightIntensity = shaftLight.intensity;

        CacheShaftColliderObject();
    }

    private void Start()
    {
        TryCaptureRetractedSpan();
    }

    private void LateUpdate()
    {
        TryCaptureRetractedSpan();

        if (baseAnchor == null || faceAnchor == null)
        {
            SetShaftVisible(false);
            SetShaftColliderActive(false);
            ApplyShaftLight(0f);
            return;
        }

        if (!TryGetSpan(out Vector3 start, out _, out Vector3 forward, out float rawSpan))
        {
            SetShaftVisible(false);
            SetShaftColliderActive(false);
            ApplyShaftLight(0f);
            return;
        }

        ApplyShaftLight(rawSpan);

        currentSpan = rawSpan;
        transform.rotation = Quaternion.LookRotation(forward, GetShaftUp(forward));
        ApplyShaftLength(rawSpan);
        AlignNearEdgeTo(start);
        SetShaftVisible(true);
        SyncShaftColliderObject(start, forward, GetShaftUp(forward), rawSpan);
    }

    // Receives the shaft collider object assigned on the parent Piston.
    public void SetShaftColliderObject(Transform colliderObject)
    {
        shaftColliderObject = colliderObject;
        CacheShaftColliderObject();
    }

    // Legacy entry point kept for existing Collider references.
    public void SetShaftCollider(Collider collider)
    {
        SetShaftColliderObject(collider != null ? collider.transform : null);
    }

    private void TryCaptureRetractedSpan()
    {
        if (retractedSpan >= 0f || baseAnchor == null || faceAnchor == null)
            return;

        if (IsParentPistonExtended())
            return;

        if (TryGetSpan(out _, out _, out _, out float span))
            retractedSpan = span;
    }

    private bool IsWithinRetractedSpan(float span)
    {
        if (retractedSpan < 0f)
            return span <= MinSpan;

        return span <= retractedSpan + RetractedSpanTolerance;
    }

    private void ApplyShaftLight(float span)
    {
        if (shaftLight == null)
            return;

        if (span <= LightOffSpan)
        {
            shaftLight.enabled = false;
            return;
        }

        if (retractedSpan < 0f || span <= retractedSpan + RetractedSpanTolerance)
        {
            shaftLight.enabled = false;
            return;
        }

        shaftLight.enabled = true;
        float travelDim = Mathf.Clamp01(Mathf.InverseLerp(retractedSpan, FullLightSpan, span));
        float openDim = Mathf.Clamp01(Mathf.InverseLerp(LightOffSpan, LightFullFadeSpan, span));
        shaftLight.intensity = baseLightIntensity * travelDim * openDim;
    }

    public float GetCurrentSpan()
    {
        return currentSpan;
    }

    private bool TryGetSpan(out Vector3 start, out Vector3 end, out Vector3 forward, out float span)
    {
        Vector3 directionGuess = faceAnchor.position - baseAnchor.position;
        if (directionGuess.sqrMagnitude < MinSpan * MinSpan)
        {
            start = default;
            end = default;
            forward = default;
            span = 0f;
            return false;
        }

        forward = directionGuess.normalized;
        start = GetAnchorConnectionPoint(baseAnchor, forward);
        end = GetAnchorConnectionPoint(faceAnchor, -forward);

        directionGuess = end - start;
        span = directionGuess.magnitude;
        if (span < MinSpan)
        {
            forward = default;
            return false;
        }

        forward = directionGuess / span;
        return true;
    }

    private void ApplyShaftLength(float span)
    {
        float baseWorldLength = GetShaftWorldLengthForLocalScaleZ(baseLocalScale.z);
        if (baseWorldLength < MinSpan)
        {
            transform.localScale = new Vector3(baseLocalScale.x, baseLocalScale.y, span);
            return;
        }

        float scaleZ = baseLocalScale.z * span / baseWorldLength;
        transform.localScale = new Vector3(baseLocalScale.x, baseLocalScale.y, scaleZ);
    }

    private float GetShaftWorldLengthForLocalScaleZ(float localScaleZ)
    {
        Vector3 savedScale = transform.localScale;
        transform.localScale = new Vector3(baseLocalScale.x, baseLocalScale.y, localScaleZ);
        float worldLength = transform.TransformVector(Vector3.forward).magnitude;
        transform.localScale = savedScale;
        return worldLength;
    }

    private void AlignNearEdgeTo(Vector3 worldStart)
    {
        Vector3 nearWorld = transform.TransformPoint(localNearEdge);
        transform.position += worldStart - nearWorld;
    }

    private void CacheShaftColliderObject()
    {
        shaftCollider = null;
        shaftBoxCollider = null;
        shaftColliderTransform = shaftColliderObject;

        if (shaftColliderTransform == null)
            return;

        shaftCollider = shaftColliderTransform.GetComponent<Collider>();
        shaftBoxCollider = shaftColliderTransform.GetComponent<BoxCollider>();

        if (shaftBoxCollider == null)
        {
            Debug.LogWarning($"{nameof(PistonShaft)} on {name} requires a {nameof(BoxCollider)} on the shaft collider object for length sync.", this);
            return;
        }

        colliderBaseLocalScale = shaftColliderTransform.localScale;
        Vector3 size = shaftBoxCollider.size;
        Vector3 center = shaftBoxCollider.center;
        colliderLocalNearEdge = new Vector3(center.x, center.y, center.z - size.z * 0.5f);
        colliderBaseWorldLength = size.z * shaftColliderTransform.TransformVector(Vector3.forward).magnitude;
    }

    private void SyncShaftColliderObject(Vector3 worldStart, Vector3 forward, Vector3 up, float span)
    {
        if (shaftColliderTransform == null)
            return;

        if (shaftCollider != null)
            shaftCollider.enabled = true;

        shaftColliderTransform.rotation = Quaternion.LookRotation(forward, up);
        ApplyColliderObjectLength(span);
        AlignColliderObjectNearEdgeTo(worldStart);
    }

    private void ApplyColliderObjectLength(float span)
    {
        if (colliderBaseWorldLength < MinSpan)
            return;

        float scaleZ = colliderBaseLocalScale.z * span / colliderBaseWorldLength;
        shaftColliderTransform.localScale = new Vector3(
            colliderBaseLocalScale.x,
            colliderBaseLocalScale.y,
            scaleZ);
    }

    private void AlignColliderObjectNearEdgeTo(Vector3 worldStart)
    {
        Vector3 nearWorld = shaftColliderTransform.TransformPoint(colliderLocalNearEdge);
        shaftColliderTransform.position += worldStart - nearWorld;
    }

    private void SetShaftColliderActive(bool active)
    {
        if (shaftCollider != null)
            shaftCollider.enabled = active;
    }

    private void SetShaftVisible(bool visible)
    {
        foreach (Renderer renderer in shaftRenderers)
        {
            if (renderer != null)
                renderer.enabled = visible;
        }
    }

    private void CacheShaftEdgePoint()
    {
        if (!TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
            return;

        Bounds bounds = meshFilter.sharedMesh.bounds;
        localNearEdge = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
    }

    private void CacheShaftRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        var shaftOnly = new System.Collections.Generic.List<Renderer>();

        foreach (Renderer renderer in renderers)
        {
            if (shaftLight != null && renderer.transform.IsChildOf(shaftLight.transform))
                continue;

            shaftOnly.Add(renderer);
        }

        shaftRenderers = shaftOnly.ToArray();
    }

    private static Vector3 GetAnchorConnectionPoint(Transform anchor, Vector3 direction)
    {
        direction.Normalize();

        if (!TryGetAnchorBounds(anchor, out Bounds bounds))
            return anchor.position;

        return GetBoundsFacePoint(bounds, direction);
    }

    private static bool TryGetAnchorBounds(Transform anchor, out Bounds bounds)
    {
        if (anchor.TryGetComponent(out Collider collider))
        {
            bounds = collider.bounds;
            return true;
        }

        if (anchor.TryGetComponent(out Renderer renderer))
        {
            bounds = renderer.bounds;
            return true;
        }

        Renderer[] renderers = anchor.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return true;
    }

    private static Vector3 GetBoundsFacePoint(Bounds bounds, Vector3 direction)
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

    private bool IsParentPistonExtended()
    {
        Piston piston = GetComponentInParent<Piston>();
        if (piston != null)
            return !piston.IsRetracted;

        PistonHorizontal horizontal = GetComponentInParent<PistonHorizontal>();
        if (horizontal != null)
            return !horizontal.IsRetracted;

        PistonVertical vertical = GetComponentInParent<PistonVertical>();
        if (vertical != null)
            return !vertical.IsRetracted;

        return false;
    }

    private static Vector3 GetShaftUp(Vector3 forward)
    {
        if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f)
            return Vector3.forward;

        return Vector3.up;
    }
}
