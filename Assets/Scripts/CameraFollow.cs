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

    [Header("Proximity Mesh Hide")]
    [Tooltip("Trigger collider (child of camera). Its size sets hide radius; center follows this object.")]
    public Collider proximityVolume;
    [Tooltip("Layers whose meshes are hidden (shadows-only) when overlapping the volume.")]
    public LayerMask proximityHideLayers = ~0;
    
    public bool IsInspectionMode => isInspectionMode;
    public int CurrentAngleIndex => currentAngleIndex;

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

    private readonly Collider[] overlapResults = new Collider[32];
    private readonly Dictionary<Renderer, ShadowCastingMode> proximityHiddenRenderers = new Dictionary<Renderer, ShadowCastingMode>();
    private readonly HashSet<Renderer> frameHiddenRenderers = new HashSet<Renderer>();
    private MeshCollider[] proximityMeshColliders;

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
        CacheProximityMeshColliders();
    }

    private void CacheProximityMeshColliders()
    {
        MeshCollider[] all = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
        var list = new List<MeshCollider>(all.Length);
        int mask = proximityHideLayers.value;
        for (int i = 0; i < all.Length; i++)
        {
            MeshCollider mc = all[i];
            if (mc != null && (mask & (1 << mc.gameObject.layer)) != 0)
                list.Add(mc);
        }
        proximityMeshColliders = list.ToArray();
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        HandleInput();
        
        if (!isInspectionMode)
        {
            UpdateActionMode();
            UpdateProximityMeshHide();
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

    private void UpdateProximityMeshHide()
    {
        if (proximityVolume == null)
        {
            RestoreAllHiddenMeshes();
            return;
        }

        Vector3 center = proximityVolume.bounds.center;
        float radius = GetProximityRadius();
        int hitCount = Physics.OverlapSphereNonAlloc(
            center,
            radius,
            overlapResults,
            proximityHideLayers,
            QueryTriggerInteraction.Ignore);

        frameHiddenRenderers.Clear();

        for (int i = 0; i < hitCount; i++)
            CollectProximityHideRenderers(overlapResults[i]);

        if (proximityMeshColliders != null)
        {
            for (int i = 0; i < proximityMeshColliders.Length; i++)
            {
                MeshCollider mc = proximityMeshColliders[i];
                if (mc != null && mc.enabled && mc.bounds.Contains(center))
                    CollectProximityHideRenderers(mc);
            }
        }

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

    private void CollectProximityHideRenderers(Collider hit)
    {
        if (hit == null || hit == proximityVolume)
            return;

        if (tim != null && hit.transform.IsChildOf(tim.transform))
            return;

        Renderer[] childRenderers = hit.GetComponentsInChildren<Renderer>();
        for (int r = 0; r < childRenderers.Length; r++)
        {
            Renderer renderer = childRenderers[r];
            if (renderer != null)
                frameHiddenRenderers.Add(renderer);
        }

        Renderer[] parentRenderers = hit.GetComponentsInParent<Renderer>();
        for (int r = 0; r < parentRenderers.Length; r++)
        {
            Renderer renderer = parentRenderers[r];
            if (renderer != null)
                frameHiddenRenderers.Add(renderer);
        }
    }

    private void RestoreProximityHiddenRenderer(Renderer renderer)
    {
        if (renderer != null && proximityHiddenRenderers.TryGetValue(renderer, out ShadowCastingMode originalMode))
            renderer.shadowCastingMode = originalMode;

        proximityHiddenRenderers.Remove(renderer);
    }

    private readonly List<Renderer> tempRestoreList = new List<Renderer>();

    private float GetProximityRadius()
    {
        if (proximityVolume is SphereCollider sphere)
        {
            Vector3 scale = proximityVolume.transform.lossyScale;
            return sphere.radius * Mathf.Max(scale.x, scale.y, scale.z);
        }

        return proximityVolume.bounds.extents.magnitude;
    }

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
