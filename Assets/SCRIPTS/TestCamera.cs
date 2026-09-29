using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// Standalone test: oriented box toward target hides nearby blocking meshes (shadows only). Does not modify CameraFollow.
public class TestCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform lookTarget;
    public bool findTimAutomatically = true;

    public CameraFollow cameraFollow;

    [Header("Occlusion volume")]
    [Tooltip("Full size toward the target: width (right), height (up), depth at default zoom.")]
    public Vector3 volumeSize = new Vector3(1f, 2f, 8f);

    [Tooltip("Near end of the box from the camera. X=right, Y=up, Z=forward toward the target.")]
    public Vector3 volumeStartOffset = Vector3.zero;

    [Tooltip("When off, Volume Size is used exactly (no scroll-based depth change).")]
    public bool scaleBoxDepthWithScroll = true;

    [Range(0.1f, 1f)]
    [Tooltip("Box depth at max scroll-in, as a fraction of Volume Size Z. Default zoom and first half of scroll-in stay 100%; only the second half of scroll-in lerps down to this.")]
    public float volumeDepthScaleAtMaxZoom = 0.7f;

    [Tooltip("Physics layers to test. Default: everything.")]
    public LayerMask raycastLayers = ~0;

    [Header("Volume display")]
    public bool showVolumeBox = true;
    public Color volumeGizmoColor = new Color(1f, 0.85f, 0.1f, 1f);

    private const int VolumeEdgeCount = 12;
    private static readonly int[] VolumeEdgeCornerA = { 0, 1, 3, 2, 4, 5, 7, 6, 0, 1, 2, 3 };
    private static readonly int[] VolumeEdgeCornerB = { 1, 3, 2, 0, 5, 7, 6, 4, 4, 5, 6, 7 };

    private CharacterMovement tim;
    private LineRenderer[] volumeEdgeLines;
    private Material volumeLineMaterial;
    private readonly Vector3[] volumeCorners = new Vector3[8];
    private readonly Collider[] overlapColliders = new Collider[64];
    private readonly Dictionary<Renderer, ShadowCastingMode> hiddenRenderers = new Dictionary<Renderer, ShadowCastingMode>();
    private readonly HashSet<Renderer> frameHits = new HashSet<Renderer>();
    private readonly List<Renderer> restoreList = new List<Renderer>();

    private void Start()
    {
        if (cameraFollow == null)
            cameraFollow = GetComponent<CameraFollow>();

        if (findTimAutomatically)
        {
            tim = FindAnyObjectByType<CharacterMovement>();
            if (lookTarget == null && tim != null)
                lookTarget = tim.CameraFollowTarget;
        }
    }

    private float GetEffectiveDepth()
    {
        if (!scaleBoxDepthWithScroll)
            return volumeSize.z;

        float closeAmount = cameraFollow != null ? cameraFollow.ZoomCloseAmount : 0f;
        float depthScale = 1f;
        if (closeAmount > 0.5f)
        {
            float secondHalf = (closeAmount - 0.5f) / 0.5f;
            depthScale = Mathf.Lerp(1f, volumeDepthScaleAtMaxZoom, secondHalf);
        }

        return volumeSize.z * depthScale;
    }

    private void OnDisable()
    {
        RestoreAll();
        SetVolumeLinesVisible(false);
    }

    private void OnDestroy()
    {
        if (volumeLineMaterial != null)
            Destroy(volumeLineMaterial);
    }

    private void OnDrawGizmos()
    {
        if (!showVolumeBox)
            return;

        if (!TryGetVolumeFrame(out Vector3 volumeOrigin, out Vector3 boxCenter, out Quaternion orientation, out Vector3 halfExtents, out Vector3 aimDir))
            return;

        BuildVolumeCorners(boxCenter, orientation, halfExtents, volumeCorners);
        Gizmos.color = volumeGizmoColor;
        for (int i = 0; i < VolumeEdgeCount; i++)
            Gizmos.DrawLine(volumeCorners[VolumeEdgeCornerA[i]], volumeCorners[VolumeEdgeCornerB[i]]);
    }

    private void LateUpdate()
    {
        if (lookTarget == null)
        {
            RestoreAll();
            SetVolumeLinesVisible(false);
            return;
        }

        if (!TryGetVolumeFrame(out Vector3 volumeOrigin, out Vector3 boxCenter, out Quaternion orientation, out Vector3 halfExtents, out Vector3 aimDir))
            return;

        float effectiveDepth = GetEffectiveDepth();

        frameHits.Clear();
        CollectRenderersInOcclusionVolume(volumeOrigin, aimDir, boxCenter, halfExtents, orientation, effectiveDepth, frameHits);

        if (showVolumeBox)
            DrawVolumeWireframePlayMode(boxCenter, orientation, halfExtents);
        else
            SetVolumeLinesVisible(false);

        if (hiddenRenderers.Count > 0)
        {
            restoreList.Clear();
            foreach (KeyValuePair<Renderer, ShadowCastingMode> entry in hiddenRenderers)
            {
                if (entry.Key == null || !frameHits.Contains(entry.Key))
                    restoreList.Add(entry.Key);
            }

            for (int i = 0; i < restoreList.Count; i++)
                RestoreRenderer(restoreList[i]);
        }

        foreach (Renderer renderer in frameHits)
        {
            if (renderer == null || hiddenRenderers.ContainsKey(renderer))
                continue;

            hiddenRenderers[renderer] = renderer.shadowCastingMode;
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    private bool TryGetVolumeFrame(
        out Vector3 volumeOrigin,
        out Vector3 boxCenter,
        out Quaternion orientation,
        out Vector3 halfExtents,
        out Vector3 aimDir)
    {
        volumeOrigin = default;
        boxCenter = default;
        orientation = Quaternion.identity;
        halfExtents = default;
        aimDir = default;

        Transform target = lookTarget;
        if (target == null && tim != null)
            target = tim.CameraFollowTarget;
        if (target == null)
            return false;

        Vector3 camPos = transform.position;
        Vector3 toTarget = target.position - camPos;
        if (toTarget.sqrMagnitude < 0.001f)
            return false;

        aimDir = toTarget.normalized;
        GetVolumeAxes(aimDir, out _, out Vector3 squareUp);
        orientation = Quaternion.LookRotation(aimDir, squareUp);

        volumeOrigin = camPos + orientation * volumeStartOffset;
        float effectiveDepth = GetEffectiveDepth();
        halfExtents = new Vector3(volumeSize.x * 0.5f, volumeSize.y * 0.5f, effectiveDepth * 0.5f);
        boxCenter = volumeOrigin + aimDir * halfExtents.z;
        return true;
    }

    private static void BuildVolumeCorners(Vector3 boxCenter, Quaternion orientation, Vector3 halfExtents, Vector3[] corners)
    {
        corners[0] = boxCenter + orientation * new Vector3(-halfExtents.x, -halfExtents.y, -halfExtents.z);
        corners[1] = boxCenter + orientation * new Vector3(halfExtents.x, -halfExtents.y, -halfExtents.z);
        corners[2] = boxCenter + orientation * new Vector3(-halfExtents.x, halfExtents.y, -halfExtents.z);
        corners[3] = boxCenter + orientation * new Vector3(halfExtents.x, halfExtents.y, -halfExtents.z);
        corners[4] = boxCenter + orientation * new Vector3(-halfExtents.x, -halfExtents.y, halfExtents.z);
        corners[5] = boxCenter + orientation * new Vector3(halfExtents.x, -halfExtents.y, halfExtents.z);
        corners[6] = boxCenter + orientation * new Vector3(-halfExtents.x, halfExtents.y, halfExtents.z);
        corners[7] = boxCenter + orientation * new Vector3(halfExtents.x, halfExtents.y, halfExtents.z);
    }

    private void DrawVolumeWireframePlayMode(Vector3 boxCenter, Quaternion orientation, Vector3 halfExtents)
    {
        EnsureVolumeEdgeLines();
        BuildVolumeCorners(boxCenter, orientation, halfExtents, volumeCorners);

        for (int i = 0; i < VolumeEdgeCount; i++)
        {
            LineRenderer line = volumeEdgeLines[i];
            if (line == null)
                continue;

            line.startColor = volumeGizmoColor;
            line.endColor = volumeGizmoColor;
            line.enabled = true;
            line.SetPosition(0, volumeCorners[VolumeEdgeCornerA[i]]);
            line.SetPosition(1, volumeCorners[VolumeEdgeCornerB[i]]);
        }
    }

    private void EnsureVolumeEdgeLines()
    {
        if (volumeEdgeLines != null && volumeEdgeLines.Length == VolumeEdgeCount)
            return;

        volumeEdgeLines = new LineRenderer[VolumeEdgeCount];
        EnsureVolumeLineMaterial();

        for (int i = 0; i < VolumeEdgeCount; i++)
        {
            Transform existing = transform.Find("TestCameraVolumeEdge_" + i);
            GameObject lineObject = existing != null ? existing.gameObject : new GameObject("TestCameraVolumeEdge_" + i);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            if (line == null)
                line = lineObject.AddComponent<LineRenderer>();

            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.015f;
            line.endWidth = 0.015f;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = volumeLineMaterial;
            line.enabled = false;

            volumeEdgeLines[i] = line;
        }
    }

    private void EnsureVolumeLineMaterial()
    {
        if (volumeLineMaterial != null)
            return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        volumeLineMaterial = new Material(shader);
        volumeLineMaterial.color = volumeGizmoColor;
        if (volumeLineMaterial.HasProperty("_BaseColor"))
            volumeLineMaterial.SetColor("_BaseColor", volumeGizmoColor);
    }

    private void SetVolumeLinesVisible(bool visible)
    {
        if (volumeEdgeLines == null)
            return;

        for (int i = 0; i < volumeEdgeLines.Length; i++)
        {
            if (volumeEdgeLines[i] != null)
                volumeEdgeLines[i].enabled = visible;
        }
    }

    private void CollectRenderersInOcclusionVolume(
        Vector3 volumeOrigin,
        Vector3 aimDir,
        Vector3 boxCenter,
        Vector3 halfExtents,
        Quaternion orientation,
        float effectiveDepth,
        HashSet<Renderer> into)
    {
        int hitCount = Physics.OverlapBoxNonAlloc(
            boxCenter,
            halfExtents,
            overlapColliders,
            orientation,
            raycastLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
            AddRenderersInVolumeForCollider(overlapColliders[i], volumeOrigin, aimDir, boxCenter, halfExtents, orientation, effectiveDepth, into);
    }

    private void AddRenderersInVolumeForCollider(
        Collider collider,
        Vector3 volumeOrigin,
        Vector3 aimDir,
        Vector3 boxCenter,
        Vector3 halfExtents,
        Quaternion boxOrientation,
        float effectiveDepth,
        HashSet<Renderer> into)
    {
        if (collider == null)
            return;

        Transform hitTransform = collider.transform;
        if (tim != null && hitTransform.IsChildOf(tim.transform))
            return;

        Transform searchRoot = hitTransform.parent != null ? hitTransform.parent : hitTransform;
        Renderer[] candidates = searchRoot.GetComponentsInChildren<Renderer>();

        for (int i = 0; i < candidates.Length; i++)
        {
            Renderer candidate = candidates[i];
            if (candidate == null || !candidate.enabled)
                continue;

            float depthAlongAim = Vector3.Dot(candidate.bounds.center - volumeOrigin, aimDir);
            if (depthAlongAim < 0f || depthAlongAim > effectiveDepth)
                continue;

            if (!RendererBoundsOverlapOrientedBox(candidate.bounds, boxCenter, halfExtents, boxOrientation))
                continue;

            into.Add(candidate);
        }
    }

    private static bool RendererBoundsOverlapOrientedBox(
        Bounds rendererBounds,
        Vector3 boxCenter,
        Vector3 boxHalfExtents,
        Quaternion boxOrientation)
    {
        Vector3 localCenter = Quaternion.Inverse(boxOrientation) * (rendererBounds.center - boxCenter);
        Vector3 rendererExtents = rendererBounds.extents;

        return Mathf.Abs(localCenter.x) <= boxHalfExtents.x + rendererExtents.x
            && Mathf.Abs(localCenter.y) <= boxHalfExtents.y + rendererExtents.y
            && Mathf.Abs(localCenter.z) <= boxHalfExtents.z + rendererExtents.z;
    }

    private void GetVolumeAxes(Vector3 aimDir, out Vector3 squareRight, out Vector3 squareUp)
    {
        squareRight = Vector3.Cross(aimDir, Vector3.up);
        if (squareRight.sqrMagnitude < 0.0001f)
            squareRight = Vector3.Cross(aimDir, Vector3.forward);
        squareRight.Normalize();
        squareUp = Vector3.Cross(squareRight, aimDir).normalized;
    }

    private void RestoreRenderer(Renderer renderer)
    {
        if (renderer != null && hiddenRenderers.TryGetValue(renderer, out ShadowCastingMode originalMode))
            renderer.shadowCastingMode = originalMode;

        hiddenRenderers.Remove(renderer);
    }

    private void RestoreAll()
    {
        foreach (KeyValuePair<Renderer, ShadowCastingMode> entry in hiddenRenderers)
        {
            if (entry.Key != null)
                entry.Key.shadowCastingMode = entry.Value;
        }

        hiddenRenderers.Clear();
        frameHits.Clear();
    }
}
