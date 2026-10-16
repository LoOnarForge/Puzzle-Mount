using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LocalZShuttle : MonoBehaviour
{
    private const float ArrivalThreshold = 0.001f;
    private const float MinShaftLength = 0.01f;
    private const float MinVisibleDistance = 1f;
    private const float MaxShaftDistance = 4f;
    private static readonly Vector3 DefaultLocalNearEdge = new Vector3(0f, 0f, -0.5f);
    private static readonly Vector3 DefaultLocalFarEdge = new Vector3(0f, 0f, 0.5f);

    [Header("Shuttle")]
    [SerializeField] private Transform objectToMove;
    [SerializeField] private float speed = 1f;

    [Header("Energy Shaft")]
    [SerializeField] private Transform shaftStart;
    [SerializeField] private Transform shaftEnd;
    [SerializeField] private float shaftWidth = 1f;

    private Vector3 localNearEdge = DefaultLocalNearEdge;
    private Vector3 localFarEdge = DefaultLocalFarEdge;
    private Vector3 startWorldPosition;
    private Vector3 endWorldPosition;
    private SpriteRenderer[] movedSprites;
    private Renderer[] movedRenderers;
    private Renderer[] shaftRenderers;
    private SpriteShapeTrail spriteShapeTrail;
    private bool shaftMode;
    private float currentShaftLength;

    private void Start()
    {
        shaftMode = shaftStart != null && shaftEnd != null;

        if (objectToMove == null)
        {
            Debug.LogWarning($"{nameof(LocalZShuttle)} on {name} has no object assigned to move.", this);
            enabled = false;
            return;
        }

        CacheShaftEdgePoints();
        CacheRenderers();

        if (shaftMode)
            FitShaftBetweenAnchors();
        else
            CalculateEdgePositionsFromBounds();

        objectToMove.position = startWorldPosition;
        StartCoroutine(ShuttleLoop());
    }

    private void LateUpdate()
    {
        if (!shaftMode)
            return;

        FitShaftBetweenAnchors();
    }

    // Sizes this object between the shaft anchors and refreshes the shuttle path.
    private void FitShaftBetweenAnchors()
    {
        Vector3 directionGuess = shaftEnd.position - shaftStart.position;
        if (directionGuess.sqrMagnitude < MinShaftLength * MinShaftLength)
        {
            SetVisualsVisible(false);
            SetTrailEmitting(false);
            return;
        }

        Vector3 forward = directionGuess.normalized;
        Vector3 start = GetAnchorConnectionPoint(shaftStart, forward);
        Vector3 end = GetAnchorConnectionPoint(shaftEnd, -forward);

        directionGuess = end - start;
        float length = directionGuess.magnitude;
        currentShaftLength = length;

        if (length < MinShaftLength)
        {
            SetVisualsVisible(false);
            SetTrailEmitting(false);
            return;
        }

        forward = directionGuess / length;
        transform.rotation = Quaternion.LookRotation(forward, GetShaftUp(forward));
        ApplyShaftScale(length);
        AlignNearEdgeTo(start);
        UpdateShuttleEndpoints();

        bool visible = length >= MinVisibleDistance;
        SetVisualsVisible(visible);
        SetTrailEmitting(visible);
    }

    // Uses the anchor bounds face toward the travel direction, not the anchor pivot.
    private static Vector3 GetAnchorConnectionPoint(Transform anchor, Vector3 direction)
    {
        if (anchor == null)
            return Vector3.zero;

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

    private void AlignNearEdgeTo(Vector3 worldStart)
    {
        Vector3 nearWorld = transform.TransformPoint(localNearEdge);
        transform.position += worldStart - nearWorld;
    }

    private float GetDistanceAdjustedSpeed()
    {
        return speed * (currentShaftLength / MaxShaftDistance);
    }

    private void ApplyShaftScale(float length)
    {
        Vector3 targetWorldScale = new Vector3(shaftWidth, shaftWidth, length);

        if (transform.parent != null)
        {
            Vector3 parentScale = transform.parent.lossyScale;
            transform.localScale = new Vector3(
                targetWorldScale.x / parentScale.x,
                targetWorldScale.y / parentScale.y,
                targetWorldScale.z / parentScale.z);
        }
        else
        {
            transform.localScale = targetWorldScale;
        }
    }

    private static Vector3 GetShaftUp(Vector3 forward)
    {
        if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f)
            return Vector3.forward;

        return Vector3.up;
    }

    private void UpdateShuttleEndpoints()
    {
        startWorldPosition = transform.TransformPoint(localNearEdge);
        endWorldPosition = transform.TransformPoint(localFarEdge);
    }

    // Moves the assigned object from the near Z edge to the far Z edge of this transform, then loops.
    private IEnumerator ShuttleLoop()
    {
        while (true)
        {
            if (currentShaftLength < MinShaftLength)
            {
                yield return null;
                continue;
            }

            while (Vector3.Distance(objectToMove.position, endWorldPosition) > ArrivalThreshold)
            {
                objectToMove.position = Vector3.MoveTowards(
                    objectToMove.position,
                    endWorldPosition,
                    GetDistanceAdjustedSpeed() * Time.deltaTime);

                yield return null;
            }

            SetObjectBSpriteVisible(false);
            objectToMove.position = startWorldPosition;

            if (spriteShapeTrail != null)
                spriteShapeTrail.ResetSpawnPosition();

            SetObjectBSpriteVisible(currentShaftLength >= MinVisibleDistance);
        }
    }

    private void CacheShaftEdgePoints()
    {
        if (!TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
            return;

        Bounds bounds = meshFilter.sharedMesh.bounds;
        localNearEdge = new Vector3(bounds.center.x, bounds.center.y, bounds.min.z);
        localFarEdge = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z);
    }

    private void CalculateEdgePositionsFromBounds()
    {
        if (!TryGetBounds(out Bounds bounds))
        {
            Debug.LogWarning($"{nameof(LocalZShuttle)} on {name} needs a Renderer or Collider to measure travel distance.", this);
            enabled = false;
            return;
        }

        Vector3 minLocal = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        Vector3 maxLocal = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 localCorner = transform.InverseTransformPoint(corner);
                    minLocal = Vector3.Min(minLocal, localCorner);
                    maxLocal = Vector3.Max(maxLocal, localCorner);
                }
            }
        }

        Vector3 localCenter = transform.InverseTransformPoint(center);
        localNearEdge = new Vector3(localCenter.x, localCenter.y, minLocal.z);
        localFarEdge = new Vector3(localCenter.x, localCenter.y, maxLocal.z);

        startWorldPosition = transform.TransformPoint(localNearEdge);
        endWorldPosition = transform.TransformPoint(localFarEdge);
    }

    private bool TryGetBounds(out Bounds bounds)
    {
        if (TryGetComponent(out Collider collider))
        {
            bounds = collider.bounds;
            return true;
        }

        if (TryGetComponent(out Renderer renderer))
        {
            bounds = renderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private void CacheRenderers()
    {
        movedSprites = objectToMove.GetComponentsInChildren<SpriteRenderer>();
        spriteShapeTrail = objectToMove.GetComponent<SpriteShapeTrail>();

        List<Renderer> objectRenderers = new List<Renderer>();
        foreach (Renderer renderer in objectToMove.GetComponentsInChildren<Renderer>())
            objectRenderers.Add(renderer);

        movedRenderers = objectRenderers.ToArray();

        List<Renderer> shaft = new List<Renderer>();
        CollectShaftRenderers(transform, shaft);
        shaftRenderers = shaft.ToArray();
    }

    private void CollectShaftRenderers(Transform current, List<Renderer> list)
    {
        if (IsUnderObjectToMove(current))
            return;

        Renderer[] renderers = current.GetComponents<Renderer>();
        list.AddRange(renderers);

        for (int i = 0; i < current.childCount; i++)
            CollectShaftRenderers(current.GetChild(i), list);
    }

    private bool IsUnderObjectToMove(Transform current)
    {
        return objectToMove != null && (current == objectToMove || current.IsChildOf(objectToMove));
    }

    private void SetVisualsVisible(bool visible)
    {
        foreach (Renderer renderer in shaftRenderers)
            renderer.enabled = visible;

        SetObjectBSpriteVisible(visible);
    }

    private void SetObjectBSpriteVisible(bool visible)
    {
        foreach (SpriteRenderer sprite in movedSprites)
            sprite.enabled = visible;

        foreach (Renderer renderer in movedRenderers)
            renderer.enabled = visible;
    }

    private void SetTrailEmitting(bool emitting)
    {
        if (spriteShapeTrail != null)
            spriteShapeTrail.SetEmitting(emitting);
    }
}
