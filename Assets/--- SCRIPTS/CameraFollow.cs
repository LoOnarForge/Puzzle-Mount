using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
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

    private static int YawDegreesToCardinalIndex(float yawDegrees)
    {
        int best = 0;
        float bestDelta = float.MaxValue;
        for (int i = 0; i < CardinalYawDegrees.Length; i++)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(yawDegrees, CardinalYawDegrees[i]));
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = i;
            }
        }

        return best;
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
            UpdateOcclusionVolumeMeshHide();
        }
        else
        {
            RestoreAllHiddenMeshes();
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

        if (UsesScrollPath() && !isFreeOrbitActive)
            return currentAngleIndex;

        if (isFreeOrbitActive)
            return YawDegreesToCardinalIndex(freeOrbitYawDegrees);

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
