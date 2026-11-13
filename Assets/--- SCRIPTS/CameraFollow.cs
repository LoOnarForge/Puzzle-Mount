using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
    [Tooltip("Default follow distance (used to pick the starting point on the scroll path).")]
    public float zoomDistance = 10f;
    [Header("Scroll wheel")]
    [Tooltip("How many stops along the path one wheel notch skips (1 = one stop, 2 = jump two stops, etc.).")]
    [Min(1)] public int pathStepsPerScroll = 1;
    [Tooltip("How long the camera glides after each scroll.")]
    [Min(0.02f)] public float scrollPathSmoothTime = 0.12f;
    [Tooltip("Death sentence path glide speed as a fraction of scroll ease (0.5 = half speed).")]
    [Range(0.05f, 1f)] public float deathSentenceScrollSpeedScale = 0.5f;

    [FormerlySerializedAs("scrollStopCount")]
    [HideInInspector] public int scrollStepCount = 20;

    [HideInInspector] public float scrollZoomStep = 0.4f;
    [HideInInspector] public float maxExtraCloseDistance = 4f;
    [HideInInspector] public float maxFarSideDollyDistance = 4f;
    [HideInInspector] public float minZoomDistance = 6f;
    [HideInInspector] public float maxZoomDistance = 14f;

    [HideInInspector] public bool useScrollPathMarkers = true;
    [HideInInspector] public Vector3[] scrollPathStopsLocal;

    private const string ScrollPathMarkerNamePrefix = "Zoom Point";
    private const int ScrollPathBakeSamples = 64;

    private Vector3 lookAtOffset = Vector3.up * 2f;
    private float extraCloseDistance;
    private float pitchDownAmount;
    private float extraFarLowerAmount;
    private float farSideDollyDistance;
    private int scrollStopIndex;
    private float scrollPathT;
    private float scrollPathTargetT;
    private float scrollPathTVelocity;
    private float scrollPathFurthestT = -1f;
    private int scrollPathFurthestStopIndex = -1;
    private bool furthestExtraScrollTowardHigh;
    private bool furthestExtraScrollTowardLow;
    private int scrollOutNotchesFromClose = -1;
    private float defaultZoomDistance;
    
    [Header("Manual Control")]
    public Key clockwiseKey = Key.E;
    public Key counterClockwiseKey = Key.Q;
    public Key resetKey = Key.R;
    public Key inspectionToggleKey = Key.Tab;
    public float rotationCooldown = 0.25f;

    [Header("CAMERA ROTATION SETTINGS")]
    public float cameraRotationThreshold = 50.0f;
    [Tooltip("Stationary free orbit: drag sensitivity (degrees per screen pixel).")]
    [Range(0.4f, 2f)] public float freeOrbitRotationSpeed = 0.4f;

    [Header("INSPECTION MODE (LEYA)")]
    public LeyasCamera leyaController;

    [Header("Local Camera Occlusion")]
    [Range(0f, 1f)]
    [Tooltip("Rectangle aspect at both quads. Middle = square. Lower = wider. Higher = taller. Independent of quad size sliders.")]
    public float localOcclusionShapeRatio = 0.5f;

    [Tooltip("Master switch: local camera occlusion and gizmo. Off restores all hidden renderers.")]
    public bool enableOcclusionMeshHide = false;

    [Tooltip("Test: if assigned, occluding renderers swap to this material instead of being hidden. Originals restore when out of the volume.")]
    public Material occlusionGhostMaterial;

    [Tooltip("Occluders whose bounds are within this distance of Tim get the ghost material. Farther occluders are hidden.")]
    [Range(0f, 10f)]
    public float ghostDistanceFromTim = 2f;

    [Tooltip("Layers whose renderers are tested for mesh occlusion (no physics).")]
    public LayerMask occlusionHideLayers = ~0;

    [Tooltip("Aim override. If unset, uses Target transform, then Tim pivot plus look-at height.")]
    public Transform occlusionAimPoint;

    [Tooltip("Tim's feet height for low-object culling. Renderers with top at or below this height are never hidden.")]
    public Transform occlusionFeetReference;

    [HideInInspector] [FormerlySerializedAs("occlusionInwardTilt")]
    public float occlusionInwardTilt = 0.35f;

    [FormerlySerializedAs("occlusionNearQuadSizeScale")]
    [Range(0.1f, 8f)]
    [Tooltip("World size of the camera-side quad only.")]
    public float cameraQuadSize = 1f;

    [FormerlySerializedAs("occlusionNearPlaneDistance")]
    [Range(-2f, 2f)]
    [Tooltip("Camera-side quad offset along camera-to-aim from the camera. -2 = 2m behind camera, 0 = at camera, +2 = 2m in front.")]
    public float cameraQuadDistance = 0.25f;

    [Range(0.1f, 8f)]
    [Tooltip("World size of the aim-side quad only.")]
    public float farSideQuadSize = 1f;

    [FormerlySerializedAs("occlusionEndMarginBeforeAim")]
    [Range(-2f, 2f)]
    [Tooltip("Aim-side vertical quad offset along horizontal camera-to-target from the aim point. -2 = 2m toward camera, 0 = at Tim plane, +2 = 2m past target.")]
    public float farQuadDistance = 0f;

    [Tooltip("Draw local occlusion wireframe in play mode and scene gizmos.")]
    public bool showVolumeBox = true;

    public Color volumeGizmoColor = new Color(1f, 0.85f, 0.1f, 1f);

    [HideInInspector] public Vector3 volumeSize = new Vector3(1f, 2f, 8f);
    [HideInInspector] public Vector3 volumeStartOffset = Vector3.zero;
    [HideInInspector] public bool scaleBoxDepthWithScroll = true;
    [HideInInspector] public float volumeDepthScaleAtMaxZoom = 0.7f;
    [HideInInspector] public float occlusionBelowAimMargin = 0.35f;
    [HideInInspector] public float occlusionPastAimMargin = 0.05f;

    [Header("X-Ray Silhouette (Test)")]
    [Tooltip("Draw an X-ray silhouette over parts of Tim hidden behind walls, stairs, or rocks.")]
    public bool enableXRaySilhouette = true;
    public Color xRayFillColor = new Color(0.2f, 0.75f, 1f, 0.25f);
    public Color xRayRimColor = new Color(0.45f, 0.92f, 1f, 0.9f);
    [Range(0.5f, 6f)] public float xRayRimPower = 2.2f;

    [Header("Shader Cutout (Test)")]
    [Tooltip("Drives global cutout uniforms for materials using Custom/SimpleLitCutout.")]
    public bool enableShaderCutout = true;
    [Range(0.5f, 6f)] public float cutoutWidthScale = 2.5f;
    [Range(0.5f, 4f)] public float cutoutHeightScale = 1.5f;
    [Range(0.05f, 2f)] public float cutoutEdgeSoftness = 0.45f;
    [Range(0.2f, 8f)] public float cutoutNoiseScale = 2.2f;
    [Range(0f, 1f)] public float cutoutNoiseStrength = 0.28f;
    [ColorUsage(false, true)] public Color cutoutEdgeColor = new Color(1f, 0.72f, 0.28f, 1f);
    [Range(0.05f, 1f)] public float cutoutEdgeGlowWidth = 0.35f;
    [Range(0f, 3f)] public float cutoutEdgeGlowIntensity = 1.15f;
    public bool enableCutoutFeetSafeBox = true;
    public Vector3 cutoutFeetBoxHalfExtents = new Vector3(1.25f, 1f, 1.25f);
    public float cutoutFeetBoxTopOffset = 0.12f;
    [Range(0.02f, 1f)] public float cutoutFeetBoxFeather = 0.18f;

    public bool IsInspectionMode => isInspectionMode;
    public int CurrentAngleIndex => currentAngleIndex;

    // 0 at default zoom, 1 when fully scrolled in toward the target (for occlusion volume depth).
    public float ZoomCloseAmount
    {
        get
        {
            if (UsesScrollPath())
            {
                int maxStop = Mathf.Max(1, ActiveScrollStopCount - 1);
                return 1f - Mathf.Clamp01(scrollPathT);
            }

            if (UsesFarViewOffset())
                return 0f;

            float zoomPart = Mathf.Clamp01(Mathf.InverseLerp(BaseZoomDistance, minZoomDistance, zoomDistance));
            float extraPart = MaxExtraCloseDistance > 0f
                ? Mathf.Clamp01(extraCloseDistance / MaxExtraCloseDistance)
                : 0f;
            return 0.5f * zoomPart + 0.5f * extraPart;
        }
    }

    private int ActiveScrollStopCount => scrollStepCount;

    public bool UsesScrollPath() => scrollPathStopsLocal != null && scrollPathStopsLocal.Length >= 2;

    private static float GetCardinalYawDegrees(int cardinalIndex)
    {
        int i = ((cardinalIndex % 4) + 4) % 4;
        return CardinalYawDegrees[i];
    }

    private float ActiveOrbitYawDegrees =>
        isFreeOrbitActive ? freeOrbitYawDegrees : GetCardinalYawDegrees(currentAngleIndex);

    public Vector3 OrbitLocalOffsetToWorld(Vector3 orbitLocalNorth)
    {
        return Quaternion.Euler(0f, ActiveOrbitYawDegrees, 0f) * orbitLocalNorth;
    }

    public Vector3 WorldOffsetToOrbitLocal(Vector3 worldOffset)
    {
        return Quaternion.Inverse(Quaternion.Euler(0f, ActiveOrbitYawDegrees, 0f)) * worldOffset;
    }

    private Vector3 velocity = Vector3.zero;
    private CharacterMovement tim;
    private Camera actionCamera;
    private float lastRotationTime;
    
    private const float BaseZoomDistance = 10f;
    private const float PitchDownHeadroomScrollTicks = 6f;
    private const float ExtraCloseScrollTickBonus = 2f;
    private const float FarLowerScrollTicks = 1f;
    private const float FarSideDollyScrollTickBonus = 2f;
    private const float MinCameraClearanceAboveLookAt = 0.25f;
    private const float MaxFreeOrbitDragStepDegrees = 90f;
    private const float FreeOrbitMaxDragDegreesPerSecondFactor = 200f;
    private const float DeathSentencePanSpeedDivisor = 10f;
    private const float DeathSentenceZoomScrollTicksPerSecond = 4f;

    private Vector3[] presetOffsets = new Vector3[]
    {
        new Vector3(-1.5f, 0, -10),  // North 
        new Vector3(10, 0, -1.5f),   // East  
        new Vector3(1.5f, 0, 10),    // South 
        new Vector3(-10, 0, 1.5f)    // West  
    };

    // Must match GetMovementDirectionForCameraAngle (not index * 90°).
    private static readonly float[] CardinalYawDegrees = { 0f, -90f, 180f, 90f };
    
    private int currentAngleIndex = 0;
    private int originalAngleIndex = 0;
    private bool isInspectionMode = false;
    private bool isFreeOrbitActive;
    private float freeOrbitYawDegrees;

    private enum DeathSentencePhase
    {
        None,
        Zooming,
        Panning
    }

    private DeathSentencePhase deathSentencePhase = DeathSentencePhase.None;
    private bool wasAirborneDeathPending;

    private const int VolumeEdgeCount = 12;
    private const int LocalOcclusionAimMarkerEdgeCount = 3;
    private const int LocalOcclusionLineCount = VolumeEdgeCount + LocalOcclusionAimMarkerEdgeCount;
    private const float LocalOcclusionShapeMaxStretch = 2f;
    private const float LocalOcclusionQuadSizeReferenceDistance = 1f;
    private const int OcclusionHullPlaneCount = 6;
    private const int OcclusionHullUniqueEdgeDirectionCount = 6;
    private const float OcclusionHullInsideEpsilon = 0.001f;
    private const float OcclusionBelowFeetMaxYMargin = 0.08f;
    private const float MinOcclusionVolumeDepth = 0.05f;
    private const float MinOcclusionQuadHeight = 0.05f;
    private const float MinOcclusionAxisMagnitudeSq = 0.000001f;
    private static readonly int[] VolumeEdgeCornerA = { 0, 1, 3, 2, 4, 5, 7, 6, 0, 1, 2, 3 };
    private static readonly int[] VolumeEdgeCornerB = { 1, 3, 2, 0, 5, 7, 6, 4, 4, 5, 6, 7 };
    private static readonly int[] LocalOcclusionFarFaceCornerA = { 0, 1, 3, 2 };
    private static readonly int[] LocalOcclusionFarFaceCornerB = { 1, 3, 2, 0 };

    private LineRenderer[] volumeEdgeLines;
    private Material volumeLineMaterial;
    private readonly Vector3[] volumeCorners = new Vector3[8];
    private readonly Collider[] overlapColliders = new Collider[64];
    private readonly Vector3[] localOcclusionNearCorners = new Vector3[4];
    private readonly Vector3[] localOcclusionFarCorners = new Vector3[4];
    private readonly Plane[] occlusionClipPlanes = new Plane[OcclusionHullPlaneCount];
    private readonly Vector3[] occlusionEdgeDirections = new Vector3[OcclusionHullUniqueEdgeDirectionCount];
    private readonly Vector3[] obbAxesScratch = new Vector3[3];
    private Bounds occlusionHullBounds;
    private Vector3 occlusionHullCenter;
    private Vector3 localOcclusionAimPosition;
    private Vector3 localOcclusionViewUp;
    private Vector3 localOcclusionAimDirection;
    private Vector3 localOcclusionCameraForward;
    private static readonly int XRayFillColorId = Shader.PropertyToID("_FillColor");
    private static readonly int XRayRimColorId = Shader.PropertyToID("_RimColor");
    private static readonly int XRayRimPowerId = Shader.PropertyToID("_RimPower");
    private const string XRayShaderName = "Custom/TimXRaySilhouette";

    private static readonly int CutoutEnabledId = Shader.PropertyToID("_TimCutoutEnabled");
    private static readonly int CutoutNearCenterId = Shader.PropertyToID("_TimCutoutNearCenter");
    private static readonly int CutoutFarCenterId = Shader.PropertyToID("_TimCutoutFarCenter");
    private static readonly int CutoutAxisForwardId = Shader.PropertyToID("_TimCutoutAxisForward");
    private static readonly int CutoutAxisRightId = Shader.PropertyToID("_TimCutoutAxisRight");
    private static readonly int CutoutShapeParamsId = Shader.PropertyToID("_TimCutoutShapeParams");
    private static readonly int CutoutVisualParamsId = Shader.PropertyToID("_TimCutoutVisualParams");
    private static readonly int CutoutEdgeColorId = Shader.PropertyToID("_TimCutoutEdgeColor");
    private static readonly int CutoutFeetBoxCenterId = Shader.PropertyToID("_TimCutoutFeetBoxCenter");
    private static readonly int CutoutFeetBoxHalfExtentsId = Shader.PropertyToID("_TimCutoutFeetBoxHalfExtents");

    private Vector3 localOcclusionHorizAimDir;
    private Vector3 localOcclusionHorizRight;

    private Material xRayMaterial;
    private TimXRaySilhouettePass xRayPass;
    private readonly List<Renderer> timXRayRenderers = new List<Renderer>();
    private readonly List<Renderer> occlusionMeshCandidates = new List<Renderer>();
    private readonly Dictionary<Renderer, ShadowCastingMode> proximityHiddenRenderers = new Dictionary<Renderer, ShadowCastingMode>();
    private readonly HashSet<Renderer> frameHiddenRenderers = new HashSet<Renderer>();
    private readonly Dictionary<Renderer, Material[]> ghostSwappedOriginalMaterials = new Dictionary<Renderer, Material[]>();

    private float DampingTime => Mathf.Lerp(0.35f, 0.02f, cameraResponsiveness);
    private float RotationLerpSpeed => Mathf.Lerp(2f, 20f, cameraResponsiveness);
    private float MaxPitchDownAmount => Mathf.Max(0f, cameraHeight - PitchDownHeadroomScrollTicks * scrollZoomStep);
    private float MaxExtraCloseDistance => maxExtraCloseDistance + ExtraCloseScrollTickBonus * scrollZoomStep;
    private float MaxExtraFarLower => scrollZoomStep * FarLowerScrollTicks;
    private float MaxFarSideDollyDistance => maxFarSideDollyDistance + FarSideDollyScrollTickBonus * scrollZoomStep;
    private float MaxFarViewDropTotal =>
        Mathf.Max(0f, cameraHeight - lookAtOffset.y - MinCameraClearanceAboveLookAt);

    private float EffectiveMaxPitchDownAmount =>
        Mathf.Min(MaxPitchDownAmount, Mathf.Max(0f, MaxFarViewDropTotal - MaxExtraFarLower));

    private void Awake()
    {
        actionCamera = GetComponent<Camera>();
        defaultZoomDistance = zoomDistance;
    }

    private int FindDefaultStopOnScrollPath()
    {
        int maxWheelStop = Mathf.Max(1, ActiveScrollStopCount - 1);
        int best = 0;
        float bestError = float.MaxValue;

        for (int stop = 0; stop <= maxWheelStop; stop++)
        {
            float t = stop / (float)maxWheelStop;
            Vector3 world = OrbitLocalOffsetToWorld(EvaluateScrollPathAtUserT(t));
            float horiz = new Vector3(world.x, 0f, world.z).magnitude;
            float error = Mathf.Abs(horiz - defaultZoomDistance) + Mathf.Abs(world.y - cameraHeight);
            if (error < bestError)
            {
                bestError = error;
                best = stop;
            }
        }

        return best;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RestoreAllHiddenMeshes();
        SetVolumeLinesVisible(false);
        DisableShaderCutoutGlobals();
    }

    private void OnDestroy()
    {
        if (volumeLineMaterial != null)
            Destroy(volumeLineMaterial);

        if (xRayMaterial != null)
            CoreUtils.Destroy(xRayMaterial);
    }

    private void OnDrawGizmos()
    {
        if ((!enableOcclusionMeshHide && !enableShaderCutout) || !showVolumeBox)
            return;

        if (!TryBuildLocalCameraOcclusionVolume())
            return;

        CopyLocalOcclusionCornersToVolumeArray();
        Gizmos.color = volumeGizmoColor;
        for (int i = 0; i < VolumeEdgeCount; i++)
            Gizmos.DrawLine(volumeCorners[VolumeEdgeCornerA[i]], volumeCorners[VolumeEdgeCornerB[i]]);

        float markerHalf = 0.12f;
        GetOcclusionVolumeAxes(localOcclusionAimDirection, out Vector3 markerRight, out Vector3 markerUp);
        Vector3 aim = localOcclusionAimPosition;
        Gizmos.DrawLine(aim - markerRight * markerHalf, aim + markerRight * markerHalf);
        Gizmos.DrawLine(aim - markerUp * markerHalf, aim + markerUp * markerHalf);
        Gizmos.DrawLine(aim, aim - localOcclusionAimDirection * markerHalf);

        if (enableShaderCutout && enableCutoutFeetSafeBox && TryGetCutoutFeetBox(out Vector3 feetBoxCenter, out Vector3 feetBoxHalfExtents))
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.8f);
            Gizmos.DrawWireCube(feetBoxCenter, feetBoxHalfExtents * 2f);
        }
    }

    private void OnValidate()
    {
        // Inspector edits during Play Mode must not rebuild the scroll path or reset the camera offset.
        if (Application.isPlaying)
            return;

        scrollStepCount = Mathf.Max(2, ActiveScrollStopCount);
        pathStepsPerScroll = Mathf.Max(1, pathStepsPerScroll);
        scrollStopIndex = Mathf.Clamp(scrollStopIndex, 0, ActiveScrollStopCount - 1);
        scrollOutNotchesFromClose = -1;
        zoomDistance = Mathf.Clamp(zoomDistance, minZoomDistance, maxZoomDistance);
        extraCloseDistance = Mathf.Clamp(extraCloseDistance, 0f, MaxExtraCloseDistance);
        farSideDollyDistance = Mathf.Clamp(farSideDollyDistance, 0f, MaxFarSideDollyDistance);
        ClampFarViewDropAmounts();
        if (useScrollPathMarkers && target != null)
            TryBuildScrollPathFromMarkers();

        if (presetOffsets != null && presetOffsets.Length > 0)
            ApplyCurrentOffset();
    }

    private void ApplyCurrentOffset()
    {
        if (UsesScrollPath())
        {
            offset = OrbitLocalOffsetToWorld(EvaluateScrollPathAtUserT(scrollPathT));
            scrollStopIndex = ScrollPathTToStopIndex(scrollPathT);
            return;
        }

        if (UsesFarViewOffset())
        {
            ApplyFarViewOffset();
            return;
        }

        if (extraCloseDistance > 0f)
        {
            float minScale = minZoomDistance / BaseZoomDistance;
            Vector3 minOffset = GetHorizontalOffset(minScale);
            minOffset.y = cameraHeight;
            if (minOffset.sqrMagnitude > 0.0001f)
                offset = minOffset - minOffset.normalized * extraCloseDistance;
            else
                offset = minOffset;
            return;
        }

        float zoomScale = zoomDistance / BaseZoomDistance;
        Vector3 flat = GetHorizontalOffset(zoomScale);
        offset = new Vector3(flat.x, cameraHeight, flat.z);
    }

    private Vector3 GetHorizontalOffset(float zoomScale)
    {
        if (isFreeOrbitActive)
        {
            Vector3 reference = presetOffsets[0];
            Vector3 flat = Quaternion.Euler(0f, freeOrbitYawDegrees, 0f) * new Vector3(reference.x, 0f, reference.z);
            return new Vector3(flat.x * zoomScale, 0f, flat.z * zoomScale);
        }

        Vector3 preset = presetOffsets[currentAngleIndex];
        return new Vector3(preset.x * zoomScale, 0f, preset.z * zoomScale);
    }

    private void ApplyScrollStop(int stop)
    {
        int maxStop = ActiveScrollStopCount - 1;
        stop = Mathf.Clamp(stop, 0, maxStop);
        scrollStopIndex = stop;
        if (UsesScrollPath())
        {
            float t = ScrollPathStopIndexToT(stop);
            scrollPathT = t;
            scrollPathTargetT = t;
            scrollPathTVelocity = 0f;
            ApplyCurrentOffset();
            return;
        }

        SetScrollStateToCloseEnd();
        if (maxStop <= 0)
        {
            ApplyCurrentOffset();
            return;
        }

        int notchesOut = Mathf.RoundToInt((maxStop - stop) * GetScrollOutNotchesFromClose() / (float)maxStop);
        for (int i = 0; i < notchesOut; i++)
            ScrollOutOneNotch();

        ApplyCurrentOffset();
    }

    private void SetScrollStateToCloseEnd()
    {
        extraCloseDistance = MaxExtraCloseDistance;
        zoomDistance = minZoomDistance;
        pitchDownAmount = 0f;
        extraFarLowerAmount = 0f;
        farSideDollyDistance = 0f;
    }

    private void SetScrollStateToFarEnd()
    {
        extraCloseDistance = 0f;
        zoomDistance = maxZoomDistance;
        pitchDownAmount = EffectiveMaxPitchDownAmount;
        extraFarLowerAmount = MaxExtraFarLower;
        farSideDollyDistance = MaxFarSideDollyDistance;
        ClampFarViewDropAmounts();
    }

    private int GetScrollOutNotchesFromClose()
    {
        if (scrollOutNotchesFromClose >= 0)
            return scrollOutNotchesFromClose;

        SetScrollStateToCloseEnd();
        int count = 0;
        while (CanScrollOutOneMoreNotch())
        {
            ScrollOutOneNotch();
            count++;
        }
        scrollOutNotchesFromClose = count;
        return count;
    }

    private bool CanScrollOutOneMoreNotch()
    {
        if (extraCloseDistance > 0.0001f) return true;
        if (zoomDistance < maxZoomDistance - 0.0001f) return true;
        if (pitchDownAmount < EffectiveMaxPitchDownAmount - 0.0001f) return true;
        if (extraFarLowerAmount < MaxExtraFarLower - 0.0001f) return true;
        if (farSideDollyDistance < MaxFarSideDollyDistance - 0.0001f) return true;
        return false;
    }

    private void ScrollOutOneNotch()
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

        if (pitchDownAmount < EffectiveMaxPitchDownAmount)
        {
            pitchDownAmount = Mathf.Min(EffectiveMaxPitchDownAmount, pitchDownAmount + scrollZoomStep);
            ClampFarViewDropAmounts();
            return;
        }

        if (extraFarLowerAmount < MaxExtraFarLower)
        {
            extraFarLowerAmount = Mathf.Min(MaxExtraFarLower, extraFarLowerAmount + scrollZoomStep);
            ClampFarViewDropAmounts();
            return;
        }

        farSideDollyDistance = Mathf.Min(MaxFarSideDollyDistance, farSideDollyDistance + scrollZoomStep);
    }

    private void ScrollInOneNotch()
    {
        if (farSideDollyDistance > 0f)
        {
            farSideDollyDistance = Mathf.Max(0f, farSideDollyDistance - scrollZoomStep);
            return;
        }

        if (extraFarLowerAmount > 0f)
        {
            extraFarLowerAmount = Mathf.Max(0f, extraFarLowerAmount - scrollZoomStep);
            return;
        }

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

    private int FindScrollStopForDefaultZoom()
    {
        int maxStop = Mathf.Max(1, scrollStepCount - 1);
        int best = 0;
        float bestError = float.MaxValue;

        for (int stop = 0; stop <= maxStop; stop++)
        {
            ApplyScrollStop(stop);
            float error = Mathf.Abs(zoomDistance - defaultZoomDistance)
                + extraCloseDistance * 4f
                + farSideDollyDistance * 2f
                + pitchDownAmount
                + extraFarLowerAmount;
            if (error < bestError)
            {
                bestError = error;
                best = stop;
            }
        }

        return best;
    }
    
    private void Start()
    {
        tim = FindAnyObjectByType<CharacterMovement>();
        if (findPlayerAutomatically && target == null && tim != null) target = tim.transform;

        if (useScrollPathMarkers)
            TryBuildScrollPathFromMarkers();

        scrollStopIndex = UsesScrollPath() ? FindDefaultStopOnScrollPath() : FindScrollStopForDefaultZoom();
        ApplyScrollStop(scrollStopIndex);
        CacheOcclusionMeshCandidates();
        CacheTimXRayRenderers();
        RestoreAllHiddenMeshes();
    }

    private void CacheTimXRayRenderers()
    {
        timXRayRenderers.Clear();
        if (tim == null)
            return;

        Renderer[] renderers = tim.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer is SkinnedMeshRenderer || renderer is MeshRenderer)
                timXRayRenderers.Add(renderer);
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (!enableXRaySilhouette || isInspectionMode)
            return;

        if (actionCamera == null)
            actionCamera = GetComponent<Camera>();
        if (cam != actionCamera)
            return;

        if (tim == null)
            return;

        if (timXRayRenderers.Count == 0)
            CacheTimXRayRenderers();
        if (timXRayRenderers.Count == 0)
            return;

        if (!EnsureXRaySilhouettePass())
            return;

        xRayMaterial.SetColor(XRayFillColorId, xRayFillColor);
        xRayMaterial.SetColor(XRayRimColorId, xRayRimColor);
        xRayMaterial.SetFloat(XRayRimPowerId, xRayRimPower);
        xRayPass.SetRenderers(timXRayRenderers);

        UniversalAdditionalCameraData additionalData = cam.GetUniversalAdditionalCameraData();
        if (additionalData != null && additionalData.scriptableRenderer != null)
            additionalData.scriptableRenderer.EnqueuePass(xRayPass);
    }

    private bool EnsureXRaySilhouettePass()
    {
        if (xRayPass != null && xRayMaterial != null)
            return true;

        Shader shader = Shader.Find(XRayShaderName);
        if (shader == null)
            return false;

        xRayMaterial = CoreUtils.CreateEngineMaterial(shader);
        xRayPass = new TimXRaySilhouettePass(xRayMaterial);
        return true;
    }

    private Transform GetFollowPivotTransform()
    {
        if (tim != null && tim.CameraFollowTarget != null)
            return tim.CameraFollowTarget;
        return target;
    }

    public bool TryBuildScrollPathFromMarkers()
    {
        if (!useScrollPathMarkers || target == null)
            return false;

        Transform pivot = GetFollowPivotTransform();
        if (pivot == null)
            pivot = target;

        Transform[] all = target.GetComponentsInChildren<Transform>(true);
        var guideMarkers = new List<(int order, Transform transform)>();
        var furthestMarkers = new List<Transform>();

        foreach (Transform t in all)
        {
            if (t == target || !t.name.StartsWith(ScrollPathMarkerNamePrefix))
                continue;

            if (t.name.IndexOf("furthest", System.StringComparison.OrdinalIgnoreCase) >= 0)
                furthestMarkers.Add(t);
            else
                guideMarkers.Add((ParseScrollMarkerOrder(t.name), t));
        }

        if (guideMarkers.Count < 2)
            return false;

        guideMarkers.Sort((a, b) => a.order.CompareTo(b.order));

        Vector3 pivotWorld = pivot.position;
        var waypoints = new List<Vector3>(guideMarkers.Count);
        foreach (var guide in guideMarkers)
            waypoints.Add(guide.transform.position - pivotWorld);

        Vector3 furthestWaypointOffset = Vector3.zero;
        bool hasFurthestMarker = false;
        if (furthestMarkers.Count > 0)
        {
            Transform furthest = furthestMarkers[0];
            furthestWaypointOffset = furthest.position - pivotWorld;
            hasFurthestMarker = true;
            int insertAt = FindFurthestInsertIndex(waypoints, furthestWaypointOffset);
            insertAt = Mathf.Clamp(insertAt, 1, waypoints.Count);
            waypoints.Insert(insertAt, furthestWaypointOffset);
        }

        int sampleCount = Mathf.Max(2, ScrollPathBakeSamples);
        scrollPathStopsLocal = SampleCatmullRomPath(waypoints, sampleCount);

        scrollPathFurthestStopIndex = -1;
        if (hasFurthestMarker)
        {
            float best = float.MaxValue;
            for (int i = 0; i < scrollPathStopsLocal.Length; i++)
            {
                float dist = (scrollPathStopsLocal[i] - furthestWaypointOffset).sqrMagnitude;
                if (dist < best)
                {
                    best = dist;
                    scrollPathFurthestStopIndex = i;
                }
            }

            scrollPathStopsLocal[scrollPathFurthestStopIndex] = furthestWaypointOffset;
            scrollPathFurthestT = scrollPathFurthestStopIndex / (float)(sampleCount - 1);
        }
        else
        {
            scrollPathFurthestT = -1f;
        }

        int maxWheelStop = Mathf.Max(1, ActiveScrollStopCount - 1);
        scrollStopIndex = Mathf.Clamp(scrollStopIndex, 0, maxWheelStop);
        float startT = ScrollPathStopIndexToT(scrollStopIndex);
        scrollPathT = startT;
        scrollPathTargetT = startT;
        scrollPathTVelocity = 0f;
        ResetFurthestGateState();
        return true;
    }

    private float ScrollPathStopIndexToT(int stop)
    {
        int maxStop = Mathf.Max(1, ActiveScrollStopCount - 1);
        return Mathf.Clamp01(stop / (float)maxStop);
    }

    private int ScrollPathTToStopIndex(float t)
    {
        int maxStop = Mathf.Max(1, ActiveScrollStopCount - 1);
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(t) * maxStop), 0, maxStop);
    }

    private Vector3 EvaluateScrollPathAtT(float t)
    {
        if (scrollPathStopsLocal == null || scrollPathStopsLocal.Length == 0)
            return Vector3.zero;

        if (scrollPathStopsLocal.Length == 1)
            return scrollPathStopsLocal[0];

        t = Mathf.Clamp01(t);
        float scaled = t * (scrollPathStopsLocal.Length - 1);
        int i = Mathf.FloorToInt(scaled);
        int j = Mathf.Min(i + 1, scrollPathStopsLocal.Length - 1);
        float u = scaled - i;
        return Vector3.Lerp(scrollPathStopsLocal[i], scrollPathStopsLocal[j], u);
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static Vector3[] SampleCatmullRomPath(IReadOnlyList<Vector3> points, int sampleCount)
    {
        var samples = new Vector3[sampleCount];
        if (points == null || points.Count == 0)
            return samples;

        if (points.Count == 1)
        {
            for (int i = 0; i < sampleCount; i++)
                samples[i] = points[0];
            return samples;
        }

        if (points.Count == 2)
        {
            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)(sampleCount - 1);
                samples[i] = Vector3.Lerp(points[0], points[1], t);
            }
            return samples;
        }

        int segmentCount = points.Count - 1;
        for (int i = 0; i < sampleCount; i++)
        {
            float u = i / (float)(sampleCount - 1) * segmentCount;
            int seg = Mathf.Min(Mathf.FloorToInt(u), segmentCount - 1);
            float t = u - seg;

            Vector3 p0 = points[Mathf.Max(seg - 1, 0)];
            Vector3 p1 = points[seg];
            Vector3 p2 = points[seg + 1];
            Vector3 p3 = points[Mathf.Min(seg + 2, points.Count - 1)];
            samples[i] = CatmullRom(p0, p1, p2, p3, t);
        }

        return samples;
    }

    private static int ParseScrollMarkerOrder(string objectName)
    {
        Match match = Regex.Match(objectName, @"\((\d+)\)\s*$");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int n))
            return n;

        if (objectName.IndexOf("Zoom Point 01", System.StringComparison.OrdinalIgnoreCase) >= 0
            && objectName.IndexOf("(", System.StringComparison.Ordinal) < 0)
            return 0;

        return 0;
    }

    private static int FindFurthestInsertIndex(IReadOnlyList<Vector3> points, Vector3 furthest)
    {
        if (points.Count < 2)
            return points.Count;

        float bestDist = float.MaxValue;
        int insertAt = 1;
        for (int i = 0; i < points.Count - 1; i++)
        {
            float dist = SqrDistancePointToSegment(furthest, points[i], points[i + 1]);
            if (dist < bestDist)
            {
                bestDist = dist;
                insertAt = i + 1;
            }
        }

        return insertAt;
    }

    private static float SqrDistancePointToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float mag = ab.sqrMagnitude;
        if (mag < 0.00001f)
            return (point - a).sqrMagnitude;

        float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / mag);
        Vector3 closest = a + ab * t;
        return (point - closest).sqrMagnitude;
    }

    private void ResetFurthestGateState()
    {
        furthestExtraScrollTowardHigh = false;
        furthestExtraScrollTowardLow = false;
    }

    private float ScrollPathNotchT() => 1f / Mathf.Max(1, scrollStepCount - 1);

    private float ScrollPathWheelDeltaT() => ScrollPathNotchT() * Mathf.Max(1, pathStepsPerScroll);

    private bool IsCameraAtFurthestStop()
    {
        if (scrollPathFurthestT < 0f)
            return false;

        return Mathf.Abs(scrollPathT - FurthestPathUserT) <= ScrollPathNotchT() * 0.55f;
    }

    private void ApplyScrollPathWheel(float scrollDirection)
    {
        float notch = ScrollPathNotchT();
        float step = ScrollPathWheelDeltaT();
        bool towardZoomOut = scrollDirection < 0f;
        float furthestT = FurthestPathUserT;
        if (furthestT < 0f)
            furthestT = -1f;

        if (towardZoomOut)
        {
            if (furthestT >= 0f && IsCameraAtFurthestStop() && scrollPathTargetT <= furthestT + notch * 0.25f)
            {
                if (!furthestExtraScrollTowardHigh)
                {
                    furthestExtraScrollTowardHigh = true;
                    scrollPathTargetT = furthestT;
                    return;
                }
            }

            float nextT = Mathf.Min(1f, scrollPathTargetT + step);
            if (furthestT >= 0f && !furthestExtraScrollTowardHigh
                && scrollPathT < furthestT - notch * 0.25f && nextT > furthestT)
                nextT = furthestT;

            scrollPathTargetT = nextT;
            if (furthestExtraScrollTowardHigh && nextT > furthestT + notch * 0.15f)
                furthestExtraScrollTowardHigh = false;
        }
        else
        {
            if (furthestT >= 0f && IsCameraAtFurthestStop() && scrollPathTargetT >= furthestT - notch * 0.25f)
            {
                if (!furthestExtraScrollTowardLow)
                {
                    furthestExtraScrollTowardLow = true;
                    scrollPathTargetT = furthestT;
                    return;
                }
            }

            float nextT = Mathf.Max(0f, scrollPathTargetT - step);
            if (furthestT >= 0f && !furthestExtraScrollTowardLow
                && scrollPathT > furthestT + notch * 0.25f && nextT < furthestT)
                nextT = furthestT;

            scrollPathTargetT = nextT;
            if (furthestExtraScrollTowardLow && nextT < furthestT - notch * 0.15f)
                furthestExtraScrollTowardLow = false;
        }
    }

    private void TickScrollPathTravel()
    {
        if (!UsesScrollPath())
            return;

        float smoothTime = scrollPathSmoothTime;
        if (deathSentencePhase == DeathSentencePhase.Zooming && UsesScrollPath())
            smoothTime = scrollPathSmoothTime / Mathf.Max(0.05f, deathSentenceScrollSpeedScale);

        scrollPathT = Mathf.SmoothDamp(scrollPathT, scrollPathTargetT, ref scrollPathTVelocity, smoothTime);
        ApplyCurrentOffset();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (isInspectionMode && tim != null && (!tim.IsGrounded || tim.IsDead))
            ExitInspectionMode();
        
        HandleInput();

        if (!isInspectionMode)
            TickScrollPathTravel();
        
        if (!isInspectionMode)
        {
            UpdateDeathSentenceBehaviour();
            UpdateFreeOrbitSnapOnMove();
            UpdateActionMode();
            if (enableOcclusionMeshHide)
                UpdateLocalCameraOcclusionMeshHide();
            else
                RestoreAllHiddenMeshes();

            if (enableShaderCutout)
                UpdateShaderCutoutGlobals();
            else
                DisableShaderCutoutGlobals();

            if (!enableOcclusionMeshHide && !enableShaderCutout)
                SetVolumeLinesVisible(false);
        }
        else
        {
            RestoreAllHiddenMeshes();
            SetVolumeLinesVisible(false);
            DisableShaderCutoutGlobals();
        }
    }
    
    private void HandleInput()
    {
        if (Keyboard.current != null && Keyboard.current[inspectionToggleKey].wasPressedThisFrame)
            ToggleInspectionMode();

        if (isInspectionMode) return;

        if (deathSentencePhase != DeathSentencePhase.None)
            return;

        float scrollDelta = 0f;
        if (Mouse.current != null)
            scrollDelta = Mouse.current.scroll.ReadValue().y;
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Mathf.Abs(scrollDelta) < 0.01f)
            scrollDelta = Input.mouseScrollDelta.y;
#endif
        if (Mathf.Abs(scrollDelta) > 0.01f)
            HandleScrollZoom(Mathf.Sign(scrollDelta));

        if (Keyboard.current == null)
            return;

        if (Keyboard.current[resetKey].wasPressedThisFrame) ResetToDefault();
        if (Keyboard.current[clockwiseKey].wasPressedThisFrame) CycleClockwise();
        if (Keyboard.current[counterClockwiseKey].wasPressedThisFrame) CycleCounterClockwise();

        if (Keyboard.current[Key.Digit1].wasPressedThisFrame)
            SnapScrollPathBookmark(ScrollPathBookmark.MaxZoomIn);
        if (Keyboard.current[Key.Digit2].wasPressedThisFrame)
            SnapScrollPathBookmark(ScrollPathBookmark.Furthest);
        if (Keyboard.current[Key.Digit3].wasPressedThisFrame)
            SnapScrollPathBookmark(ScrollPathBookmark.MaxZoomOut);
    }

    private enum ScrollPathBookmark
    {
        MaxZoomIn,
        Furthest,
        MaxZoomOut
    }

    private void SnapScrollPathBookmark(ScrollPathBookmark bookmark)
    {
        if (!UsesScrollPath())
            return;

        float t = bookmark switch
        {
            ScrollPathBookmark.MaxZoomIn => 0f,
            ScrollPathBookmark.MaxZoomOut => 1f,
            ScrollPathBookmark.Furthest => FurthestPathUserT,
            _ => scrollPathT
        };

        if (bookmark == ScrollPathBookmark.Furthest && t < 0f)
            return;

        SnapScrollPathToT(t);
    }

    private void SnapScrollPathToT(float t)
    {
        t = Mathf.Clamp01(t);
        scrollPathT = t;
        scrollPathTargetT = t;
        scrollPathTVelocity = 0f;
        ResetFurthestGateState();
        scrollStopIndex = ScrollPathTToStopIndex(t);
        ApplyCurrentOffset();
    }

    private const int ScrollPathTrimStopsFromZoomIn = 5;
    private const int DeathSentenceStopsInFromFurthest = 15;

    private float ScrollPathCurveTMinFromTrim() => ScrollPathNotchT() * ScrollPathTrimStopsFromZoomIn;

    private float ScrollPathUserTToCurveT(float userT)
    {
        float tMin = ScrollPathCurveTMinFromTrim();
        return Mathf.Lerp(tMin, 1f, Mathf.Clamp01(userT));
    }

    private float ScrollPathCurveTToUserT(float curveT)
    {
        float tMin = ScrollPathCurveTMinFromTrim();
        float span = 1f - tMin;
        if (span < 0.0001f)
            return 0f;
        return Mathf.Clamp01((curveT - tMin) / span);
    }

    private float FurthestPathUserT =>
        scrollPathFurthestT < 0f ? -1f : ScrollPathCurveTToUserT(scrollPathFurthestT);

    private Vector3 EvaluateScrollPathAtUserT(float userT)
    {
        return EvaluateScrollPathAtT(ScrollPathUserTToCurveT(userT));
    }

    private void SnapScrollPathDeathSentenceZoom()
    {
        if (!UsesScrollPath())
            return;

        float t = FurthestPathUserT >= 0f
            ? FurthestPathUserT - ScrollPathNotchT() * DeathSentenceStopsInFromFurthest
            : 1f;

        scrollPathTargetT = Mathf.Clamp01(t);
        scrollPathTVelocity = 0f;
        ResetFurthestGateState();
    }

    private void HandleScrollZoom(float scrollDirection)
    {
        int maxStop = ActiveScrollStopCount - 1;

        if (UsesScrollPath())
        {
            ApplyScrollPathWheel(scrollDirection);
            return;
        }

        int legacyNext = scrollStopIndex;
        if (scrollDirection > 0f)
            legacyNext = Mathf.Max(0, scrollStopIndex - 1);
        else
            legacyNext = Mathf.Min(maxStop, scrollStopIndex + 1);

        ApplyScrollStop(legacyNext);
    }

    private bool CanEnterInspectionMode()
    {
        if (tim == null)
            return true;

        return tim.IsGrounded && !tim.IsDead;
    }

    private void ToggleInspectionMode()
    {
        if (leyaController == null) return;

        if (!isInspectionMode)
        {
            if (!CanEnterInspectionMode())
                return;

            isInspectionMode = true;
            isFreeOrbitActive = false;
            originalAngleIndex = currentAngleIndex;
            actionCamera.enabled = false;
            RestoreAllHiddenMeshes();
            
            if (tim != null) tim.SetMovementEnabled(false);
            
            leyaController.Activate(transform.position, transform.rotation);
        }
        else
        {
            ExitInspectionMode();
        }
    }

    private void ExitInspectionMode()
    {
        if (leyaController == null || !isInspectionMode)
            return;

        isInspectionMode = false;

        leyaController.DeactivateWithTransition(transform.position, transform.rotation, () => {
            actionCamera.enabled = true;
            if (tim != null) tim.SetMovementEnabled(true);
        });
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

    private bool TryGetOcclusionAimWorldPosition(out Vector3 aimWorldPosition)
    {
        aimWorldPosition = default;

        if (occlusionAimPoint != null)
        {
            aimWorldPosition = occlusionAimPoint.position;
            return true;
        }

        Transform pivot = GetFollowPivotTransform();
        if (target != null)
        {
            if (pivot != null && target == pivot)
                aimWorldPosition = pivot.position + lookAtOffset;
            else
                aimWorldPosition = target.position;

            return true;
        }

        if (pivot == null)
            return false;

        aimWorldPosition = pivot.position + lookAtOffset;
        return true;
    }

    // Fills occlusionMeshCandidates from scene renderers on occlusionHideLayers (no colliders).
    private void CacheOcclusionMeshCandidates()
    {
        occlusionMeshCandidates.Clear();
        int layerMask = occlusionHideLayers.value;
        if (layerMask == 0)
            return;

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!RendererIsOnOcclusionLayer(renderer.gameObject, layerMask))
                continue;

            if (tim != null && renderer.transform.IsChildOf(tim.transform))
                continue;

            if (renderer.transform.IsChildOf(transform))
                continue;

            occlusionMeshCandidates.Add(renderer);
        }
    }

    private void UpdateLocalCameraOcclusionMeshHide()
    {
        if (!enableOcclusionMeshHide)
        {
            RestoreAllHiddenMeshes();
            SetVolumeLinesVisible(false);
            return;
        }

        if (!TryBuildLocalCameraOcclusionVolume())
        {
            RestoreAllHiddenMeshes();
            SetVolumeLinesVisible(false);
            return;
        }

        if (occlusionMeshCandidates.Count == 0)
            CacheOcclusionMeshCandidates();

        frameHiddenRenderers.Clear();

        for (int i = 0; i < occlusionMeshCandidates.Count; i++)
        {
            Renderer renderer = occlusionMeshCandidates[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (ShouldExcludeRendererFromLocalOcclusion(renderer))
                continue;

            if (RendererIntersectsLocalOcclusionVolume(renderer))
                frameHiddenRenderers.Add(renderer);
        }

        if (showVolumeBox)
            DrawLocalOcclusionWireframePlayMode();
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
            if (renderer == null)
                continue;

            if (proximityHiddenRenderers.ContainsKey(renderer))
            {
                bool isGhosted = ghostSwappedOriginalMaterials.ContainsKey(renderer);
                if (isGhosted == ShouldGhostOcclusionRenderer(renderer))
                    continue;

                RestoreProximityHiddenRenderer(renderer);
            }

            HideOcclusionRenderer(renderer);
        }
    }

    private bool TryBuildLocalCameraOcclusionVolume()
    {
        Vector3 camPos = transform.position;

        if (!TryGetOcclusionAimWorldPosition(out Vector3 aimWorldPosition))
            return false;

        if (actionCamera == null)
            actionCamera = GetComponent<Camera>();
        if (actionCamera == null)
            return false;

        Vector3 toAim = aimWorldPosition - camPos;
        if (toAim.sqrMagnitude < 0.001f)
            return false;

        Vector3 aimDir = toAim.normalized;
        localOcclusionAimDirection = aimDir;
        localOcclusionAimPosition = aimWorldPosition;
        GetOcclusionVolumeAxes(aimDir, out _, out localOcclusionViewUp);

        Quaternion camRot = transform.rotation;
        Vector3 forward = camRot * Vector3.forward;
        localOcclusionCameraForward = forward;

        if (Vector3.Dot(toAim, forward) < 0.05f)
            return false;

        Vector3 horizToAim = new Vector3(toAim.x, 0f, toAim.z);
        Vector3 horizAimDir;
        if (horizToAim.sqrMagnitude >= MinOcclusionAxisMagnitudeSq)
        {
            horizAimDir = horizToAim.normalized;
        }
        else
        {
            Vector3 camHoriz = Vector3.ProjectOnPlane(forward, Vector3.up);
            horizAimDir = camHoriz.sqrMagnitude >= MinOcclusionAxisMagnitudeSq ? camHoriz.normalized : Vector3.forward;
        }

        Vector3 horizRight = Vector3.Cross(Vector3.up, horizAimDir).normalized;
        localOcclusionHorizAimDir = horizAimDir;
        localOcclusionHorizRight = horizRight;

        Vector3 cameraQuadCenter = camPos + aimDir * cameraQuadDistance;
        Vector3 aimQuadCenter = aimWorldPosition + horizAimDir * farQuadDistance;

        float horizSpan = Vector3.Dot(aimQuadCenter - cameraQuadCenter, horizAimDir);
        if (horizSpan < MinOcclusionVolumeDepth)
        {
            if (Vector3.Dot(aimWorldPosition - cameraQuadCenter, horizAimDir) < MinOcclusionVolumeDepth)
                return false;

            aimQuadCenter = cameraQuadCenter + horizAimDir * MinOcclusionVolumeDepth;
        }

        float fovHalfTan = Mathf.Tan(actionCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        GetLocalOcclusionShapeHalfExtents(
            LocalOcclusionQuadSizeReferenceDistance * fovHalfTan * cameraQuadSize,
            out float nearHorizontalHalf,
            out float nearVerticalHalf);
        GetLocalOcclusionShapeHalfExtents(
            LocalOcclusionQuadSizeReferenceDistance * fovHalfTan * farSideQuadSize,
            out float farHorizontalHalf,
            out float farVerticalHalf);

        float nearBottomY = cameraQuadCenter.y - nearVerticalHalf;
        float nearTopY = cameraQuadCenter.y + nearVerticalHalf;
        float farBottomY = aimQuadCenter.y - farVerticalHalf;
        float farTopY = aimQuadCenter.y + farVerticalHalf;

        if (occlusionFeetReference != null)
        {
            float feetY = occlusionFeetReference.position.y;
            nearBottomY = Mathf.Max(nearBottomY, feetY);
            nearTopY = Mathf.Max(nearTopY, nearBottomY + MinOcclusionQuadHeight);
            farBottomY = Mathf.Max(farBottomY, feetY);
            farTopY = Mathf.Max(farTopY, farBottomY + MinOcclusionQuadHeight);
        }

        Vector3 nearBaseXZ = new Vector3(cameraQuadCenter.x, 0f, cameraQuadCenter.z);
        localOcclusionNearCorners[0] = nearBaseXZ - horizRight * nearHorizontalHalf + Vector3.up * nearBottomY;
        localOcclusionNearCorners[1] = nearBaseXZ + horizRight * nearHorizontalHalf + Vector3.up * nearBottomY;
        localOcclusionNearCorners[2] = nearBaseXZ - horizRight * nearHorizontalHalf + Vector3.up * nearTopY;
        localOcclusionNearCorners[3] = nearBaseXZ + horizRight * nearHorizontalHalf + Vector3.up * nearTopY;

        Vector3 farBaseXZ = new Vector3(aimQuadCenter.x, 0f, aimQuadCenter.z);
        localOcclusionFarCorners[0] = farBaseXZ - horizRight * farHorizontalHalf + Vector3.up * farBottomY;
        localOcclusionFarCorners[1] = farBaseXZ + horizRight * farHorizontalHalf + Vector3.up * farBottomY;
        localOcclusionFarCorners[2] = farBaseXZ - horizRight * farHorizontalHalf + Vector3.up * farTopY;
        localOcclusionFarCorners[3] = farBaseXZ + horizRight * farHorizontalHalf + Vector3.up * farTopY;

        CopyLocalOcclusionCornersToVolumeArray();

        occlusionHullCenter = Vector3.zero;
        occlusionHullBounds = new Bounds(volumeCorners[0], Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            occlusionHullCenter += volumeCorners[i];
            occlusionHullBounds.Encapsulate(volumeCorners[i]);
        }

        occlusionHullCenter *= 0.125f;

        occlusionClipPlanes[0] = BuildHullFacePlane(localOcclusionNearCorners[0], localOcclusionNearCorners[1], localOcclusionNearCorners[2]);
        occlusionClipPlanes[1] = BuildHullFacePlane(localOcclusionFarCorners[2], localOcclusionFarCorners[1], localOcclusionFarCorners[0]);

        for (int i = 0; i < 4; i++)
        {
            int cornerA = LocalOcclusionFarFaceCornerA[i];
            int cornerB = LocalOcclusionFarFaceCornerB[i];
            occlusionClipPlanes[2 + i] = BuildHullFacePlane(
                localOcclusionNearCorners[cornerA],
                localOcclusionNearCorners[cornerB],
                localOcclusionFarCorners[cornerB]);
        }

        occlusionEdgeDirections[0] = horizRight;
        occlusionEdgeDirections[1] = Vector3.up;
        for (int i = 0; i < 4; i++)
            occlusionEdgeDirections[2 + i] = localOcclusionFarCorners[i] - localOcclusionNearCorners[i];

        return true;
    }

    private void UpdateShaderCutoutGlobals()
    {
        if (!TryBuildLocalCameraOcclusionVolume())
        {
            DisableShaderCutoutGlobals();
            if (!enableOcclusionMeshHide)
                SetVolumeLinesVisible(false);
            return;
        }

        if (!enableOcclusionMeshHide)
        {
            if (showVolumeBox)
                DrawLocalOcclusionWireframePlayMode();
            else
                SetVolumeLinesVisible(false);
        }

        Vector3 nearCenter = 0.25f * (localOcclusionNearCorners[0] + localOcclusionNearCorners[1] + localOcclusionNearCorners[2] + localOcclusionNearCorners[3]);
        Vector3 farCenter = 0.25f * (localOcclusionFarCorners[0] + localOcclusionFarCorners[1] + localOcclusionFarCorners[2] + localOcclusionFarCorners[3]);
        float nearHalfWidth = 0.5f * Vector3.Distance(localOcclusionNearCorners[0], localOcclusionNearCorners[1]) * cutoutWidthScale;
        float nearHalfHeight = 0.5f * Vector3.Distance(localOcclusionNearCorners[0], localOcclusionNearCorners[2]) * cutoutHeightScale;
        float farHalfWidth = 0.5f * Vector3.Distance(localOcclusionFarCorners[0], localOcclusionFarCorners[1]) * cutoutWidthScale;
        float farHalfHeight = 0.5f * Vector3.Distance(localOcclusionFarCorners[0], localOcclusionFarCorners[2]) * cutoutHeightScale;

        Shader.SetGlobalFloat(CutoutEnabledId, 1f);
        Shader.SetGlobalVector(CutoutNearCenterId, new Vector4(nearCenter.x, nearCenter.y, nearCenter.z, 0f));
        Shader.SetGlobalVector(CutoutFarCenterId, new Vector4(farCenter.x, farCenter.y, farCenter.z, 0f));
        Shader.SetGlobalVector(CutoutAxisForwardId, new Vector4(localOcclusionHorizAimDir.x, localOcclusionHorizAimDir.y, localOcclusionHorizAimDir.z, 0f));
        Shader.SetGlobalVector(CutoutAxisRightId, new Vector4(localOcclusionHorizRight.x, localOcclusionHorizRight.y, localOcclusionHorizRight.z, 0f));
        Shader.SetGlobalVector(CutoutShapeParamsId, new Vector4(nearHalfWidth, nearHalfHeight, farHalfWidth, farHalfHeight));
        Shader.SetGlobalVector(CutoutVisualParamsId, new Vector4(cutoutEdgeSoftness, cutoutNoiseScale, cutoutNoiseStrength, cutoutEdgeGlowWidth));

        Color edgeCol = cutoutEdgeColor;
        edgeCol.a = cutoutEdgeGlowIntensity;
        Shader.SetGlobalVector(CutoutEdgeColorId, edgeCol);

        if (enableCutoutFeetSafeBox && TryGetCutoutFeetBox(out Vector3 feetBoxCenter, out Vector3 feetBoxHalfExtents))
        {
            Shader.SetGlobalVector(CutoutFeetBoxCenterId, new Vector4(feetBoxCenter.x, feetBoxCenter.y, feetBoxCenter.z, 1f));
            Shader.SetGlobalVector(CutoutFeetBoxHalfExtentsId, new Vector4(feetBoxHalfExtents.x, feetBoxHalfExtents.y, feetBoxHalfExtents.z, cutoutFeetBoxFeather));
        }
        else
        {
            Shader.SetGlobalVector(CutoutFeetBoxCenterId, Vector4.zero);
            Shader.SetGlobalVector(CutoutFeetBoxHalfExtentsId, Vector4.zero);
        }
    }

    private static void DisableShaderCutoutGlobals()
    {
        Shader.SetGlobalFloat(CutoutEnabledId, 0f);
    }

    private bool TryGetCutoutFeetBox(out Vector3 boxCenter, out Vector3 halfExtents)
    {
        boxCenter = default;
        halfExtents = new Vector3(
            Mathf.Max(0.05f, cutoutFeetBoxHalfExtents.x),
            Mathf.Max(0.05f, cutoutFeetBoxHalfExtents.y),
            Mathf.Max(0.05f, cutoutFeetBoxHalfExtents.z));

        Vector3 referencePos;
        if (occlusionFeetReference != null)
            referencePos = occlusionFeetReference.position;
        else if (tim != null)
            referencePos = tim.transform.position;
        else if (target != null)
            referencePos = target.position;
        else
            return false;

        float topY = referencePos.y + cutoutFeetBoxTopOffset;
        boxCenter = new Vector3(referencePos.x, topY - halfExtents.y, referencePos.z);
        return true;
    }

    private Plane BuildHullFacePlane(Vector3 cornerA, Vector3 cornerB, Vector3 cornerC)
    {
        Vector3 normal = Vector3.Cross(cornerB - cornerA, cornerC - cornerA);
        if (normal.sqrMagnitude < 0.0001f)
            return new Plane(Vector3.up, cornerA);

        normal.Normalize();
        Plane plane = new Plane(normal, cornerA);
        if (plane.GetDistanceToPoint(occlusionHullCenter) < 0f)
            plane = new Plane(-normal, cornerA);

        return plane;
    }

    private bool RendererIntersectsLocalOcclusionVolume(Renderer renderer)
    {
        Bounds worldBounds = renderer.bounds;
        if (!occlusionHullBounds.Intersects(worldBounds))
            return false;

        TryExtractRendererObb(renderer, worldBounds, out Vector3 obbCenter, out Vector3 obbExtents);
        return ObbIntersectsOcclusionHull(obbCenter, obbExtents);
    }

    private void TryExtractRendererObb(Renderer renderer, Bounds worldBoundsFallback, out Vector3 center, out Vector3 extents)
    {
        if (!renderer.isPartOfStaticBatch
            && renderer is MeshRenderer
            && renderer.TryGetComponent(out MeshFilter meshFilter)
            && meshFilter.sharedMesh != null)
        {
            Bounds localBounds = meshFilter.sharedMesh.bounds;
            Matrix4x4 localToWorld = renderer.localToWorldMatrix;

            Vector3 col0 = localToWorld.GetColumn(0);
            Vector3 col1 = localToWorld.GetColumn(1);
            Vector3 col2 = localToWorld.GetColumn(2);

            float len0Sq = col0.sqrMagnitude;
            float len1Sq = col1.sqrMagnitude;
            float len2Sq = col2.sqrMagnitude;

            if (len0Sq >= MinOcclusionAxisMagnitudeSq
                && len1Sq >= MinOcclusionAxisMagnitudeSq
                && len2Sq >= MinOcclusionAxisMagnitudeSq)
            {
                float len0 = Mathf.Sqrt(len0Sq);
                float len1 = Mathf.Sqrt(len1Sq);
                float len2 = Mathf.Sqrt(len2Sq);

                Vector3 ax0 = col0 / len0;
                Vector3 ax1 = col1 / len1;
                Vector3 ax2 = col2 / len2;

                if (Mathf.Abs(Vector3.Dot(ax0, ax1)) < 0.01f
                    && Mathf.Abs(Vector3.Dot(ax0, ax2)) < 0.01f
                    && Mathf.Abs(Vector3.Dot(ax1, ax2)) < 0.01f)
                {
                    obbAxesScratch[0] = ax0;
                    obbAxesScratch[1] = ax1;
                    obbAxesScratch[2] = ax2;
                    center = localToWorld.MultiplyPoint3x4(localBounds.center);
                    Vector3 localExtents = localBounds.extents;
                    extents = new Vector3(localExtents.x * len0, localExtents.y * len1, localExtents.z * len2);
                    return;
                }
            }
        }

        obbAxesScratch[0] = Vector3.right;
        obbAxesScratch[1] = Vector3.up;
        obbAxesScratch[2] = Vector3.forward;
        center = worldBoundsFallback.center;
        extents = worldBoundsFallback.extents;
    }

    private bool ObbIntersectsOcclusionHull(Vector3 obbCenter, Vector3 obbExtents)
    {
        Vector3 ax0 = obbAxesScratch[0];
        Vector3 ax1 = obbAxesScratch[1];
        Vector3 ax2 = obbAxesScratch[2];

        for (int i = 0; i < OcclusionHullPlaneCount; i++)
        {
            Plane plane = occlusionClipPlanes[i];
            Vector3 normal = plane.normal;
            float projectedRadius =
                Mathf.Abs(Vector3.Dot(normal, ax0)) * obbExtents.x
                + Mathf.Abs(Vector3.Dot(normal, ax1)) * obbExtents.y
                + Mathf.Abs(Vector3.Dot(normal, ax2)) * obbExtents.z;

            if (plane.GetDistanceToPoint(obbCenter) + projectedRadius < OcclusionHullInsideEpsilon)
                return false;
        }

        for (int i = 0; i < 3; i++)
        {
            Vector3 axis = obbAxesScratch[i];
            float obbCenterProj = Vector3.Dot(axis, obbCenter);
            float obbRadius = i == 0 ? obbExtents.x : (i == 1 ? obbExtents.y : obbExtents.z);
            if (!HullOverlapsIntervalOnAxis(axis, obbCenterProj, obbRadius))
                return false;
        }

        for (int i = 0; i < 3; i++)
        {
            Vector3 obbAxis = obbAxesScratch[i];
            for (int j = 0; j < OcclusionHullUniqueEdgeDirectionCount; j++)
            {
                Vector3 cross = Vector3.Cross(obbAxis, occlusionEdgeDirections[j]);
                if (cross.sqrMagnitude < MinOcclusionAxisMagnitudeSq)
                    continue;

                Vector3 axis = cross.normalized;
                float obbCenterProj = Vector3.Dot(axis, obbCenter);
                float obbRadius =
                    Mathf.Abs(Vector3.Dot(axis, ax0)) * obbExtents.x
                    + Mathf.Abs(Vector3.Dot(axis, ax1)) * obbExtents.y
                    + Mathf.Abs(Vector3.Dot(axis, ax2)) * obbExtents.z;

                if (!HullOverlapsIntervalOnAxis(axis, obbCenterProj, obbRadius))
                    return false;
            }
        }

        return true;
    }

    private bool HullOverlapsIntervalOnAxis(Vector3 axis, float obbCenterProj, float obbRadius)
    {
        float hullMin = float.MaxValue;
        float hullMax = float.MinValue;

        for (int i = 0; i < 8; i++)
        {
            float proj = Vector3.Dot(axis, volumeCorners[i]);
            if (proj < hullMin) hullMin = proj;
            if (proj > hullMax) hullMax = proj;
        }

        return hullMax - OcclusionHullInsideEpsilon >= obbCenterProj - obbRadius
            && hullMin + OcclusionHullInsideEpsilon <= obbCenterProj + obbRadius;
    }

    private void CopyLocalOcclusionCornersToVolumeArray()
    {
        for (int i = 0; i < 4; i++)
        {
            volumeCorners[i] = localOcclusionNearCorners[i];
            volumeCorners[i + 4] = localOcclusionFarCorners[i];
        }
    }

    private static bool RendererIsOnOcclusionLayer(GameObject go, int layerMask)
    {
        for (Transform node = go.transform; node != null; node = node.parent)
        {
            if ((layerMask & (1 << node.gameObject.layer)) != 0)
                return true;
        }

        return false;
    }

    private bool ShouldExcludeRendererFromLocalOcclusion(Renderer renderer)
    {
        if (renderer == null)
            return true;

        if (tim != null && tim.IsTransformPartOfCurrentGroundSupport(renderer.transform))
            return true;

        if (occlusionFeetReference != null)
        {
            float feetY = occlusionFeetReference.position.y;
            if (renderer.bounds.max.y <= feetY + OcclusionBelowFeetMaxYMargin)
                return true;
        }

        return false;
    }

#if false // Legacy overlap-box occlusion (inspector fields hidden above).
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
            if (renderer == null)
                continue;

            if (proximityHiddenRenderers.ContainsKey(renderer))
            {
                bool isGhosted = ghostSwappedOriginalMaterials.ContainsKey(renderer);
                if (isGhosted == ShouldGhostOcclusionRenderer(renderer))
                    continue;

                RestoreProximityHiddenRenderer(renderer);
            }

            HideOcclusionRenderer(renderer);
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
        SetVolumeLinePositionsFromCorners();
    }
#endif

    private void DrawLocalOcclusionWireframePlayMode()
    {
        EnsureVolumeEdgeLines();
        CopyLocalOcclusionCornersToVolumeArray();
        SetVolumeLinePositionsFromCorners();
        DrawLocalOcclusionAimMarkerLines();
    }

    private void DrawLocalOcclusionAimMarkerLines()
    {
        if (volumeEdgeLines == null || volumeEdgeLines.Length < LocalOcclusionLineCount)
            return;

        float markerHalf = 0.12f;
        GetOcclusionVolumeAxes(localOcclusionAimDirection, out Vector3 markerRight, out Vector3 markerUp);
        Vector3 aim = localOcclusionAimPosition;

        for (int i = 0; i < LocalOcclusionAimMarkerEdgeCount; i++)
        {
            LineRenderer line = volumeEdgeLines[VolumeEdgeCount + i];
            if (line == null)
                continue;

            Vector3 start;
            Vector3 end;
            if (i == 0)
            {
                start = aim - markerRight * markerHalf;
                end = aim + markerRight * markerHalf;
            }
            else if (i == 1)
            {
                start = aim - markerUp * markerHalf;
                end = aim + markerUp * markerHalf;
            }
            else
            {
                start = aim;
                end = aim - localOcclusionAimDirection * markerHalf;
            }

            line.startColor = volumeGizmoColor;
            line.endColor = volumeGizmoColor;
            line.enabled = true;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }
    }

    private void SetVolumeLinePositionsFromCorners()
    {
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

        for (int i = VolumeEdgeCount; i < LocalOcclusionLineCount; i++)
        {
            if (volumeEdgeLines[i] != null)
                volumeEdgeLines[i].enabled = false;
        }
    }

    private void EnsureVolumeEdgeLines()
    {
        if (volumeEdgeLines != null && volumeEdgeLines.Length == LocalOcclusionLineCount)
            return;

        volumeEdgeLines = new LineRenderer[LocalOcclusionLineCount];
        EnsureVolumeLineMaterial();

        for (int i = 0; i < LocalOcclusionLineCount; i++)
        {
            Transform existing = transform.Find("CameraFollowVolumeEdge_" + i);
            GameObject lineObject = existing != null ? existing.gameObject : new GameObject("CameraFollowVolumeEdge_" + i);
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.GetComponent<LineRenderer>();
            if (line == null)
                line = lineObject.AddComponent<LineRenderer>();

            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.positionCount = 2;
            line.startWidth = 0.004f;
            line.endWidth = 0.004f;
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

    private void GetLocalOcclusionShapeHalfExtents(float baseQuadHalf, out float horizontalHalf, out float verticalHalf)
    {
        horizontalHalf = baseQuadHalf;
        verticalHalf = baseQuadHalf;
        float shape = localOcclusionShapeRatio;
        if (shape < 0.5f)
        {
            float t = shape / 0.5f;
            horizontalHalf = baseQuadHalf * Mathf.Lerp(LocalOcclusionShapeMaxStretch, 1f, t);
        }
        else if (shape > 0.5f)
        {
            float t = (shape - 0.5f) / 0.5f;
            verticalHalf = baseQuadHalf * Mathf.Lerp(1f, LocalOcclusionShapeMaxStretch, t);
        }
    }

    // True when the ghost material is assigned and the renderer's bounds are within ghostDistanceFromTim of Tim.
    private bool ShouldGhostOcclusionRenderer(Renderer renderer)
    {
        if (occlusionGhostMaterial == null)
            return false;

        Transform timTransform = tim != null ? tim.transform : target;
        if (timTransform == null)
            return false;

        Vector3 timPosition = timTransform.position;
        Vector3 closestPoint = renderer.bounds.ClosestPoint(timPosition);
        return (closestPoint - timPosition).sqrMagnitude <= ghostDistanceFromTim * ghostDistanceFromTim;
    }

    // Hides an occluding renderer: ghost material when close to Tim, otherwise renders shadows only.
    private void HideOcclusionRenderer(Renderer renderer)
    {
        proximityHiddenRenderers[renderer] = renderer.shadowCastingMode;

        if (!ShouldGhostOcclusionRenderer(renderer))
        {
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            return;
        }

        Material[] originalMaterials = renderer.sharedMaterials;
        ghostSwappedOriginalMaterials[renderer] = originalMaterials;

        Material[] ghostMaterials = new Material[originalMaterials.Length];
        for (int i = 0; i < ghostMaterials.Length; i++)
            ghostMaterials[i] = occlusionGhostMaterial;

        renderer.sharedMaterials = ghostMaterials;
    }

    private void RestoreProximityHiddenRenderer(Renderer renderer)
    {
        if (renderer != null && proximityHiddenRenderers.TryGetValue(renderer, out ShadowCastingMode originalMode))
            renderer.shadowCastingMode = originalMode;

        if (renderer != null && ghostSwappedOriginalMaterials.TryGetValue(renderer, out Material[] originalMaterials))
            renderer.sharedMaterials = originalMaterials;

        proximityHiddenRenderers.Remove(renderer);
        ghostSwappedOriginalMaterials.Remove(renderer);
    }

    private readonly List<Renderer> tempRestoreList = new List<Renderer>();

    private void RestoreAllHiddenMeshes()
    {
        foreach (KeyValuePair<Renderer, ShadowCastingMode> entry in proximityHiddenRenderers)
        {
            if (entry.Key != null)
                entry.Key.shadowCastingMode = entry.Value;
        }

        foreach (KeyValuePair<Renderer, Material[]> entry in ghostSwappedOriginalMaterials)
        {
            if (entry.Key != null)
                entry.Key.sharedMaterials = entry.Value;
        }

        proximityHiddenRenderers.Clear();
        ghostSwappedOriginalMaterials.Clear();
        frameHiddenRenderers.Clear();
    }

    // Stationary-only: drag orbit from MMB (TimCubeController). Moving uses cardinal MMB steps.
    public void ApplyFreeOrbitDrag(float screenDeltaX)
    {
        if (isInspectionMode)
            return;

        if (!isFreeOrbitActive)
            BeginFreeOrbitFromCurrentView();

        float degrees = screenDeltaX * freeOrbitRotationSpeed;
        float frameStepCap = freeOrbitRotationSpeed * 200f * Time.deltaTime;
        float maxStep = Mathf.Min(MaxFreeOrbitDragStepDegrees, frameStepCap);
        freeOrbitYawDegrees += Mathf.Clamp(degrees, -maxStep, maxStep);
        ApplyCurrentOffset();
    }

    private void BeginFreeOrbitFromCurrentView()
    {
        Vector3 horizontal = GetActionHorizontalOffsetXZ();
        freeOrbitYawDegrees = HorizontalOffsetToFreeOrbitYawDegrees(horizontal);
        isFreeOrbitActive = true;
    }

    private float HorizontalOffsetToFreeOrbitYawDegrees(Vector3 horizontalXZ)
    {
        Vector3 north = presetOffsets[0];
        Vector3 northFlat = new Vector3(north.x, 0f, north.z);
        if (northFlat.sqrMagnitude < 0.0001f)
            return GetCardinalYawDegrees(currentAngleIndex);

        Vector3 currentFlat = new Vector3(horizontalXZ.x, 0f, horizontalXZ.z);
        if (currentFlat.sqrMagnitude < 0.0001f)
            return GetCardinalYawDegrees(currentAngleIndex);

        return Vector3.SignedAngle(northFlat, currentFlat, Vector3.up);
    }

    private void UpdateDeathSentenceBehaviour()
    {
        if (tim == null)
            return;

        if (tim.IsAirborneDeathPending && !wasAirborneDeathPending)
        {
            deathSentencePhase = DeathSentencePhase.Zooming;
            SnapScrollPathDeathSentenceZoom();
        }

        wasAirborneDeathPending = tim.IsAirborneDeathPending;

        if (deathSentencePhase == DeathSentencePhase.Zooming)
        {
            TickDeathSentenceZoom();

            if (tim.IsDead)
                BeginDeathSentencePan();
        }
        else if (deathSentencePhase == DeathSentencePhase.Panning)
        {
            TickDeathSentencePan();
        }
    }

    private void TickDeathSentenceZoom()
    {
        if (UsesScrollPath())
            return;

        float zoomStep = scrollZoomStep * DeathSentenceZoomScrollTicksPerSecond * Time.deltaTime;

        if (farSideDollyDistance > 0f)
        {
            farSideDollyDistance = Mathf.Max(0f, farSideDollyDistance - zoomStep);
            ApplyCurrentOffset();
            return;
        }

        if (extraFarLowerAmount > 0f)
        {
            extraFarLowerAmount = Mathf.Max(0f, extraFarLowerAmount - zoomStep);
            ApplyCurrentOffset();
            return;
        }

        if (pitchDownAmount > 0f)
        {
            pitchDownAmount = Mathf.Max(0f, pitchDownAmount - zoomStep);
            ApplyCurrentOffset();
            return;
        }

        if (zoomDistance > minZoomDistance)
        {
            zoomDistance = Mathf.Max(minZoomDistance, zoomDistance - zoomStep);
            ApplyCurrentOffset();
            return;
        }

        if (extraCloseDistance < MaxExtraCloseDistance)
        {
            extraCloseDistance = Mathf.Min(MaxExtraCloseDistance, extraCloseDistance + zoomStep);
            ApplyCurrentOffset();
        }
    }

    private void BeginDeathSentencePan()
    {
        deathSentencePhase = DeathSentencePhase.Panning;
        BeginFreeOrbitFromCurrentView();
        ApplyCurrentOffset();
    }

    private void TickDeathSentencePan()
    {
        float degreesPerSecond = freeOrbitRotationSpeed * FreeOrbitMaxDragDegreesPerSecondFactor / DeathSentencePanSpeedDivisor;
        freeOrbitYawDegrees += degreesPerSecond * Time.deltaTime;
        ApplyCurrentOffset();
    }

    private void UpdateFreeOrbitSnapOnMove()
    {
        if (tim == null || !isFreeOrbitActive)
            return;

        if (deathSentencePhase == DeathSentencePhase.Panning)
            return;

        // Snap when the player is giving move input, not while coasting to a stop.
        if (!tim.IsStationary)
            SnapFreeOrbitToNearestCardinal();
    }

    private int FindNearestCardinalIndex()
    {
        Vector3 currentHorizontal = GetActionHorizontalOffsetXZ();
        if (currentHorizontal.sqrMagnitude < 0.0001f)
            return currentAngleIndex;

        int bestIndex = 0;
        float bestSqDistance = float.MaxValue;

        for (int i = 0; i < presetOffsets.Length; i++)
        {
            Vector3 candidateHorizontal = GetCardinalHorizontalOffsetXZ(i);
            float sq = (candidateHorizontal - currentHorizontal).sqrMagnitude;
            if (sq < bestSqDistance)
            {
                bestSqDistance = sq;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void SnapFreeOrbitToNearestCardinal()
    {
        currentAngleIndex = FindNearestCardinalIndex();
        isFreeOrbitActive = false;
        freeOrbitYawDegrees = GetCardinalYawDegrees(currentAngleIndex);
        ApplyCurrentOffset();
    }

    private bool IsAtFarZoomOut()
    {
        return extraCloseDistance <= 0.0001f && zoomDistance >= maxZoomDistance - 0.001f;
    }

    private bool UsesFarViewOffset()
    {
        if (!IsAtFarZoomOut())
            return false;

        return pitchDownAmount > 0f
            || extraFarLowerAmount > 0f
            || farSideDollyDistance > 0f;
    }

    private void ApplyFarViewOffset()
    {
        float maxScale = maxZoomDistance / BaseZoomDistance;
        Vector3 horizontal = GetHorizontalOffset(maxScale);
        float y = cameraHeight - pitchDownAmount - extraFarLowerAmount;
        offset = BuildFarViewOffset(horizontal, y);
    }

    private Vector3 BuildFarViewOffset(Vector3 horizontal, float y)
    {
        Vector3 xz = new Vector3(horizontal.x, 0f, horizontal.z);
        float xzMag = xz.magnitude;
        if (farSideDollyDistance > 0f && xzMag > 0.0001f)
        {
            float newMag = Mathf.Max(0f, xzMag - farSideDollyDistance);
            Vector3 dir = xz / xzMag;
            return new Vector3(dir.x * newMag, y, dir.z * newMag);
        }

        return new Vector3(horizontal.x, y, horizontal.z);
    }

    private void ClampFarViewDropAmounts()
    {
        float maxDrop = MaxFarViewDropTotal;
        float maxPitch = EffectiveMaxPitchDownAmount;
        pitchDownAmount = Mathf.Clamp(pitchDownAmount, 0f, Mathf.Min(maxPitch, maxDrop));
        float remaining = Mathf.Max(0f, maxDrop - pitchDownAmount);
        extraFarLowerAmount = Mathf.Clamp(extraFarLowerAmount, 0f, Mathf.Min(MaxExtraFarLower, remaining));
    }

    private Vector3 GetActionHorizontalOffsetXZ()
    {
        if (UsesScrollPath())
        {
            Vector3 onPath = OrbitLocalOffsetToWorld(EvaluateScrollPathAtUserT(scrollPathT));
            return new Vector3(onPath.x, 0f, onPath.z);
        }

        if (UsesFarViewOffset())
        {
            float maxScale = maxZoomDistance / BaseZoomDistance;
            Vector3 horizontal = GetHorizontalOffset(maxScale);
            float y = cameraHeight - pitchDownAmount - extraFarLowerAmount;
            Vector3 farOffset = BuildFarViewOffset(horizontal, y);
            return new Vector3(farOffset.x, 0f, farOffset.z);
        }

        if (extraCloseDistance > 0f)
        {
            float minScale = minZoomDistance / BaseZoomDistance;
            Vector3 minOffset = GetHorizontalOffset(minScale);
            minOffset.y = cameraHeight;
            Vector3 dolly = minOffset.sqrMagnitude > 0.0001f
                ? minOffset - minOffset.normalized * extraCloseDistance
                : minOffset;
            return new Vector3(dolly.x, 0f, dolly.z);
        }

        float zoomScale = zoomDistance / BaseZoomDistance;
        Vector3 flat = GetHorizontalOffset(zoomScale);
        return new Vector3(flat.x, 0f, flat.z);
    }

    private Vector3 GetCardinalHorizontalOffsetXZ(int cardinalIndex)
    {
        int savedIndex = currentAngleIndex;
        bool savedFree = isFreeOrbitActive;

        currentAngleIndex = cardinalIndex;
        isFreeOrbitActive = false;
        Vector3 horizontal = GetActionHorizontalOffsetXZ();

        currentAngleIndex = savedIndex;
        isFreeOrbitActive = savedFree;
        return horizontal;
    }

    private void ExitFreeOrbitToCurrentCardinal()
    {
        if (!isFreeOrbitActive)
            return;

        currentAngleIndex = FindNearestCardinalIndex();
        isFreeOrbitActive = false;
        freeOrbitYawDegrees = GetCardinalYawDegrees(currentAngleIndex);
        ApplyCurrentOffset();
    }

    public void CycleClockwise()
    {
        if (Time.unscaledTime < lastRotationTime + rotationCooldown) return;

        ExitFreeOrbitToCurrentCardinal();
        currentAngleIndex = (currentAngleIndex + 1) % presetOffsets.Length;
        ApplyCurrentOffset();
        lastRotationTime = Time.unscaledTime;
    }
    
    public void CycleCounterClockwise()
    {
        if (Time.unscaledTime < lastRotationTime + rotationCooldown) return;

        ExitFreeOrbitToCurrentCardinal();
        currentAngleIndex = (currentAngleIndex - 1 + presetOffsets.Length) % presetOffsets.Length;
        ApplyCurrentOffset();
        lastRotationTime = Time.unscaledTime;
    }
    
    public void ResetToDefault()
    {
        if (Time.unscaledTime < lastRotationTime + rotationCooldown) return;

        isFreeOrbitActive = false;
        currentAngleIndex = 0;
        freeOrbitYawDegrees = 0f;
        int stop = UsesScrollPath() ? FindDefaultStopOnScrollPath() : FindScrollStopForDefaultZoom();
        ApplyScrollStop(stop);
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
