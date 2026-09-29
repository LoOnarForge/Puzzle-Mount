using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// Handles Action Mode (cardinal follow) for Tim Jones.
/// Coordinates with LeyasCamera for Inspection Mode.
public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public bool findPlayerAutomatically = true;

    [Header("ACTION MODE SETTINGS")]
    [Tooltip("Adjusts speed and damping (0 = Lazy, 1 = Snappy)")]
    [Range(0.01f, 1f)] public float cameraResponsiveness = 0.5f;
    
    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0, 5, -8);
    public bool constrainY = false;
    public float fixedYPosition = 10f;
    [Tooltip("Offsets the screen position without changing the camera angle")]
    public float screenFramingOffset = 0f;

    [Header("View Height & Zoom")]
    [Tooltip("Camera height above Tim")]
    public float cameraHeight = 9f;
    [Tooltip("Distance from Tim (adjust with scroll wheel)")]
    public float zoomDistance = 10f;
    public float minZoomDistance = 6f;
    public float maxZoomDistance = 14f;
    [Tooltip("Distance change per scroll notch")]
    public float scrollZoomStep = 0.4f;
    [Tooltip("Extra scroll-in range beyond min zoom (fixed angle, dolly only)")]
    public float maxExtraCloseDistance = 4f;
    
    private Vector3 lookAtOffset = Vector3.up * 2f;
    private float extraCloseDistance;
    private float pitchDownAmount;
    
    [Header("Manual Control")]
    public Key clockwiseKey = Key.E;
    public Key counterClockwiseKey = Key.Q;
    public Key resetKey = Key.R;
    public Key inspectionToggleKey = Key.Tab;
    public float rotationCooldown = 0.25f;

    [Header("CAMERA ROTATION SETTINGS")]
    public float cameraRotationThreshold = 50.0f;

    [Header("INSPECTION MODE (LEYA)")]
    public LeyasCamera leyaController;

    [Header("Occlusion volume")]
    public bool enableOcclusionMeshHide = true;

    [Tooltip("Full size toward the target: width (right), height (up), depth at default zoom.")]
    public Vector3 volumeSize = new Vector3(1f, 2f, 8f);

    [Tooltip("Near end of the box from the camera. X=horizontal right, Y=world up, Z=horizontal toward Tim (no camera pitch).")]
    public Vector3 volumeStartOffset = Vector3.zero;

    [Tooltip("When off, Volume Size is used exactly (no scroll-based depth change).")]
    public bool scaleBoxDepthWithScroll = true;

    [Range(0.1f, 1f)]
    [Tooltip("Box depth at max scroll-in, as a fraction of Volume Size Z. Default zoom and first half of scroll-in stay 100%; only the second half of scroll-in lerps down to this.")]
    public float volumeDepthScaleAtMaxZoom = 0.7f;

    [Tooltip("Layers whose colliders are tested for occlusion.")]
    public LayerMask occlusionHideLayers = ~0;

    [Tooltip("Draw occlusion box wireframe in play mode.")]
    public bool showVolumeBox;

    public Color volumeGizmoColor = new Color(1f, 0.85f, 0.1f, 1f);

    public bool IsInspectionMode => isInspectionMode;
    public int CurrentAngleIndex => currentAngleIndex;

    // 0 at default zoom, 1 when fully scrolled in toward the target (for occlusion volume depth).
    public float ZoomCloseAmount
    {
        get
        {
            if (pitchDownAmount > 0f)
                return 0f;

            float zoomPart = Mathf.Clamp01(Mathf.InverseLerp(BaseZoomDistance, minZoomDistance, zoomDistance));
            float extraPart = MaxExtraCloseDistance > 0f
                ? Mathf.Clamp01(extraCloseDistance / MaxExtraCloseDistance)
                : 0f;
            return 0.5f * zoomPart + 0.5f * extraPart;
        }
    }

    private Vector3 velocity = Vector3.zero;
    private CharacterMovement tim;
    private Camera actionCamera;
    private float lastRotationTime;
    
    private const float BaseZoomDistance = 10f;
    private const float PitchDownHeadroomScrollTicks = 6f;
    private const float ExtraCloseScrollTickBonus = 2f;

    private Vector3[] presetOffsets = new Vector3[]
    {
        new Vector3(-1.5f, 0, -10),  // North 
        new Vector3(10, 0, -1.5f),   // East  
        new Vector3(1.5f, 0, 10),    // South 
        new Vector3(-10, 0, 1.5f)    // West  
    };
    
    private int currentAngleIndex = 0;
    private int originalAngleIndex = 0;
    private bool isInspectionMode = false;

    private const int VolumeEdgeCount = 12;
    private static readonly int[] VolumeEdgeCornerA = { 0, 1, 3, 2, 4, 5, 7, 6, 0, 1, 2, 3 };
    private static readonly int[] VolumeEdgeCornerB = { 1, 3, 2, 0, 5, 7, 6, 4, 4, 5, 6, 7 };

    private LineRenderer[] volumeEdgeLines;
    private Material volumeLineMaterial;
    private readonly Vector3[] volumeCorners = new Vector3[8];
    private readonly Collider[] overlapColliders = new Collider[64];
    private readonly Dictionary<Renderer, ShadowCastingMode> proximityHiddenRenderers = new Dictionary<Renderer, ShadowCastingMode>();
    private readonly HashSet<Renderer> frameHiddenRenderers = new HashSet<Renderer>();

    private float DampingTime => Mathf.Lerp(0.35f, 0.02f, cameraResponsiveness);
    private float RotationLerpSpeed => Mathf.Lerp(2f, 20f, cameraResponsiveness);
    private float MaxPitchDownAmount => Mathf.Max(0f, cameraHeight - PitchDownHeadroomScrollTicks * scrollZoomStep);
    private float MaxExtraCloseDistance => maxExtraCloseDistance + ExtraCloseScrollTickBonus * scrollZoomStep;

    private void Awake()
    {
        actionCamera = GetComponent<Camera>();
        ApplyCurrentOffset();
    }

    private void OnDisable()
    {
        RestoreAllHiddenMeshes();
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

        if (!TryGetOcclusionVolumeFrame(out Vector3 boxCenter, out Quaternion orientation, out Vector3 halfExtents))
            return;

        BuildVolumeCorners(boxCenter, orientation, halfExtents, volumeCorners);
        Gizmos.color = volumeGizmoColor;
        for (int i = 0; i < VolumeEdgeCount; i++)
            Gizmos.DrawLine(volumeCorners[VolumeEdgeCornerA[i]], volumeCorners[VolumeEdgeCornerB[i]]);
    }

    private void OnValidate()
    {
        zoomDistance = Mathf.Clamp(zoomDistance, minZoomDistance, maxZoomDistance);
        extraCloseDistance = Mathf.Clamp(extraCloseDistance, 0f, MaxExtraCloseDistance);
        pitchDownAmount = Mathf.Clamp(pitchDownAmount, 0f, MaxPitchDownAmount);
        if (presetOffsets != null && presetOffsets.Length > 0)
            ApplyCurrentOffset();
    }

    private void ApplyCurrentOffset()
    {
        Vector3 preset = presetOffsets[currentAngleIndex];

        if (pitchDownAmount > 0f)
        {
            float maxScale = maxZoomDistance / BaseZoomDistance;
            offset = new Vector3(
                preset.x * maxScale,
                cameraHeight - pitchDownAmount,
                preset.z * maxScale);
            return;
        }

        if (extraCloseDistance > 0f)
        {
            float minScale = minZoomDistance / BaseZoomDistance;
            Vector3 minOffset = new Vector3(preset.x * minScale, cameraHeight, preset.z * minScale);
            if (minOffset.sqrMagnitude > 0.0001f)
                offset = minOffset - minOffset.normalized * extraCloseDistance;
            else
                offset = minOffset;
            return;
        }

        float zoomScale = zoomDistance / BaseZoomDistance;
        offset = new Vector3(preset.x * zoomScale, cameraHeight, preset.z * zoomScale);
    }
    
    private void Start()
    {
        tim = FindAnyObjectByType<CharacterMovement>();
        if (findPlayerAutomatically && target == null && tim != null) target = tim.transform;
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        HandleInput();
        
        if (!isInspectionMode)
        {
            UpdateActionMode();
            UpdateOcclusionVolumeMeshHide();
        }
        else
        {
            RestoreAllHiddenMeshes();
        }
    }
    
    private void HandleInput()
    {
        if (Keyboard.current == null) return;

        // Toggle Inspection Mode via Leya
        if (Keyboard.current[inspectionToggleKey].wasPressedThisFrame)
        {
            ToggleInspectionMode();
        }

        if (isInspectionMode) return;

        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                HandleScrollZoom(Mathf.Sign(scroll));
                ApplyCurrentOffset();
            }
        }

        // Action Mode Controls
        if (Keyboard.current[resetKey].wasPressedThisFrame) ResetToDefault();
        if (Keyboard.current[clockwiseKey].wasPressedThisFrame) CycleClockwise();
        if (Keyboard.current[counterClockwiseKey].wasPressedThisFrame) CycleCounterClockwise();
    }

    private void HandleScrollZoom(float scrollDirection)
    {
        // Positive scroll = zoom in, negative = zoom out
        if (scrollDirection > 0f)
        {
            if (pitchDownAmount > 0f)
            {
                pitchDownAmount = Mathf.Max(0f, pitchDownAmount - scrollZoomStep);
                return;
            }

            if (zoomDistance > minZoomDistance)
            {
                zoomDistance = Mathf.Max(minZoomDistance, zoomDistance - scrollZoomStep);
                return;
            }

            extraCloseDistance = Mathf.Min(MaxExtraCloseDistance, extraCloseDistance + scrollZoomStep);
        }
        else
        {
            if (extraCloseDistance > 0f)
            {
                extraCloseDistance = Mathf.Max(0f, extraCloseDistance - scrollZoomStep);
                return;
            }

            if (zoomDistance < maxZoomDistance)
            {
                zoomDistance = Mathf.Min(maxZoomDistance, zoomDistance + scrollZoomStep);
                return;
            }

            pitchDownAmount = Mathf.Min(MaxPitchDownAmount, pitchDownAmount + scrollZoomStep);
        }
    }

    private void ToggleInspectionMode()
    {
        if (leyaController == null) return;

        if (!isInspectionMode)
        {
            isInspectionMode = true;
            originalAngleIndex = currentAngleIndex;
            actionCamera.enabled = false;
            RestoreAllHiddenMeshes();
            
            if (tim != null) tim.SetMovementEnabled(false);
            
            leyaController.Activate(transform.position, transform.rotation);
        }
        else
        {
            isInspectionMode = false;
            
            leyaController.DeactivateWithTransition(transform.position, transform.rotation, () => {
                actionCamera.enabled = true;
                if (tim != null) tim.SetMovementEnabled(true);
            });
        }
    }

    private void UpdateActionMode()
    {
        Transform followTarget = tim != null ? tim.CameraFollowTarget : target;
        if (followTarget == null)
            return;

        Vector3 targetPos = followTarget.position + offset;
        if (constrainY) targetPos.y = fixedYPosition;
        
        // Add framing offset relative to the camera's right axis to shift the camera position
        targetPos += transform.right * screenFramingOffset;
        
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, DampingTime);

        // Calculate look target with the same framing offset so the camera doesn't turn back to center Tim
        Vector3 lookAtPos = (followTarget.position + lookAtOffset) + (transform.right * screenFramingOffset);
        Vector3 direction = (lookAtPos - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, RotationLerpSpeed * Time.deltaTime);
        }
    }

    private float GetEffectiveOcclusionDepth()
    {
        if (!scaleBoxDepthWithScroll)
            return volumeSize.z;

        float closeAmount = ZoomCloseAmount;
        float depthScale = 1f;
        if (closeAmount > 0.5f)
        {
            float secondHalf = (closeAmount - 0.5f) / 0.5f;
            depthScale = Mathf.Lerp(1f, volumeDepthScaleAtMaxZoom, secondHalf);
        }

        return volumeSize.z * depthScale;
    }

    private void UpdateOcclusionVolumeMeshHide()
    {
        if (!enableOcclusionMeshHide)
        {
            RestoreAllHiddenMeshes();
            return;
        }

        if (!TryGetOcclusionVolumeFrame(out Vector3 volumeOrigin, out Vector3 boxCenter, out Quaternion orientation, out Vector3 halfExtents, out Vector3 aimDir))
        {
            RestoreAllHiddenMeshes();
            SetVolumeLinesVisible(false);
            return;
        }

        float effectiveDepth = GetEffectiveOcclusionDepth();
        frameHiddenRenderers.Clear();

        int hitCount = Physics.OverlapBoxNonAlloc(
            boxCenter,
            halfExtents,
            overlapColliders,
            orientation,
            occlusionHideLayers,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
            AddOcclusionRenderersForCollider(overlapColliders[i], volumeOrigin, aimDir, boxCenter, halfExtents, orientation, effectiveDepth);

        if (showVolumeBox)
            DrawVolumeWireframePlayMode(boxCenter, orientation, halfExtents);
        else
            SetVolumeLinesVisible(false);

        if (proximityHiddenRenderers.Count > 0)
        {
            tempRestoreList.Clear();
            foreach (Renderer renderer in proximityHiddenRenderers.Keys)
            {
                if (renderer == null || !frameHiddenRenderers.Contains(renderer))
                    tempRestoreList.Add(renderer);
            }

            for (int i = 0; i < tempRestoreList.Count; i++)
                RestoreProximityHiddenRenderer(tempRestoreList[i]);
        }

        foreach (Renderer renderer in frameHiddenRenderers)
        {
            if (renderer == null || proximityHiddenRenderers.ContainsKey(renderer))
                continue;

            proximityHiddenRenderers[renderer] = renderer.shadowCastingMode;
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }
    }

    private bool TryGetOcclusionVolumeFrame(
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

        Transform followTarget = tim != null ? tim.CameraFollowTarget : target;
        if (followTarget == null)
            return false;

        Vector3 camPos = transform.position;
        Vector3 toTarget = followTarget.position - camPos;
        if (toTarget.sqrMagnitude < 0.001f)
            return false;

        aimDir = toTarget.normalized;
        GetOcclusionVolumeAxes(aimDir, out _, out Vector3 squareUp);
        orientation = Quaternion.LookRotation(aimDir, squareUp);

        volumeOrigin = camPos + orientation * volumeStartOffset;
        float effectiveDepth = GetEffectiveOcclusionDepth();
        halfExtents = new Vector3(volumeSize.x * 0.5f, volumeSize.y * 0.5f, effectiveDepth * 0.5f);
        boxCenter = volumeOrigin + aimDir * halfExtents.z;
        return true;
    }

    private bool TryGetOcclusionVolumeFrame(out Vector3 boxCenter, out Quaternion orientation, out Vector3 halfExtents)
    {
        return TryGetOcclusionVolumeFrame(out _, out boxCenter, out orientation, out halfExtents, out _);
    }

    private void AddOcclusionRenderersForCollider(
        Collider collider,
        Vector3 volumeOrigin,
        Vector3 aimDir,
        Vector3 boxCenter,
        Vector3 halfExtents,
        Quaternion boxOrientation,
        float effectiveDepth)
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

            frameHiddenRenderers.Add(candidate);
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
            Transform existing = transform.Find("CameraFollowVolumeEdge_" + i);
            GameObject lineObject = existing != null ? existing.gameObject : new GameObject("CameraFollowVolumeEdge_" + i);
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

    private void GetOcclusionVolumeAxes(Vector3 aimDir, out Vector3 squareRight, out Vector3 squareUp)
    {
        squareRight = Vector3.Cross(aimDir, Vector3.up);
        if (squareRight.sqrMagnitude < 0.0001f)
            squareRight = Vector3.Cross(aimDir, Vector3.forward);
        squareRight.Normalize();
        squareUp = Vector3.Cross(squareRight, aimDir).normalized;
    }

    private void RestoreProximityHiddenRenderer(Renderer renderer)
    {
        if (renderer != null && proximityHiddenRenderers.TryGetValue(renderer, out ShadowCastingMode originalMode))
            renderer.shadowCastingMode = originalMode;

        proximityHiddenRenderers.Remove(renderer);
    }

    private readonly List<Renderer> tempRestoreList = new List<Renderer>();

    private void RestoreAllHiddenMeshes()
    {
        foreach (KeyValuePair<Renderer, ShadowCastingMode> entry in proximityHiddenRenderers)
        {
            if (entry.Key != null)
                entry.Key.shadowCastingMode = entry.Value;
        }

        proximityHiddenRenderers.Clear();
        frameHiddenRenderers.Clear();
    }

    public void CycleClockwise()
    {
        if (Time.unscaledTime < lastRotationTime + rotationCooldown) return;

        currentAngleIndex = (currentAngleIndex + 1) % presetOffsets.Length;
        ApplyCurrentOffset();
        lastRotationTime = Time.unscaledTime;
    }
    
    public void CycleCounterClockwise()
    {
        if (Time.unscaledTime < lastRotationTime + rotationCooldown) return;

        currentAngleIndex = (currentAngleIndex - 1 + presetOffsets.Length) % presetOffsets.Length;
        ApplyCurrentOffset();
        lastRotationTime = Time.unscaledTime;
    }
    
    public void ResetToDefault()
    {
        if (Time.unscaledTime < lastRotationTime + rotationCooldown) return;

        currentAngleIndex = 0;
        ApplyCurrentOffset();
        lastRotationTime = Time.unscaledTime;
    }

    public Vector3 GetMovementDirectionForCameraAngle(Vector2 moveInput)
    {
        // Define forward and right directions for each camera angle
        Vector3 forward, right;
        
        switch (currentAngleIndex)
        {
            case 0: // North view
                forward = Vector3.forward;  // North (0,0,1)
                right = Vector3.right;      // East (1,0,0)
                break;
            case 1: // East view
                forward = Vector3.left;     // West (-1,0,0)
                right = Vector3.forward;    // North (0,0,1)
                break;
            case 2: // South view
                forward = Vector3.back;     // South (0,0,-1)
                right = Vector3.left;       // West (-1,0,0)
                break;
            case 3: // West view
                forward = Vector3.right;    // East (1,0,0)
                right = Vector3.back;       // South (0,0,-1)
                break;
            default:
                forward = Vector3.forward;
                right = Vector3.right;
                break;
        }
        
        // Calculate movement direction: forward * W/S input + right * A/D input
        return forward * moveInput.y + right * moveInput.x;
    }
}
