using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(1000)]
[ExecuteAlways]
[DisallowMultipleComponent]
public class LaserBeam : MonoBehaviour
{
    public enum ActivatorMode
    {
        PressurePlate,
        Lever,
        Button
    }

    private const string BeamLineChildName = "BeamLineB";
    private const string LegacyBeamLineChildAName = "BeamLineA";
    private const float ObstructionDistanceEpsilon = 0.001f;
    private const float MinRayHitDistance = 0.02f;
    private const int InitialRaycastHitCapacity = 24;
    private const int MaxRaycastHitCapacity = 256;
    private static readonly int MainColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int EndFadeLengthId = Shader.PropertyToID("_EndFadeLength");
    private const float BeamMaterialEmissionStrength = 1f;
    private const float DefaultBeamEndFadeLength = 0.18f;
    private const float ButtonShutdownFadeDuration = 1f;
    private static readonly Color BeamOffColor = new Color(0f, 0f, 0f, 0f);

#if UNITY_EDITOR
    private bool editorLineSetupQueued;
#endif

    [Header("ACTIVATOR")]
    [SerializeField] private ActivatorMode activatorMode = ActivatorMode.PressurePlate;
    [SerializeField] private List<GameObject> connectedDevices = new List<GameObject>();
    [SerializeField] private SkullBehaviour skullBehaviour;

    [Header("RANGE")]
    [FormerlySerializedAs("beamRangeB")]
    [Min(0f)]
    [SerializeField] private float beamRange = 10f;

    [Header("HIT")]
    [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;

    [Header("COLOR")]
    [ColorUsage(true, true)]
    [FormerlySerializedAs("beamColor")]
    [SerializeField] private Color beamOpenedColor = Color.red;
    [ColorUsage(true, true)]
    [SerializeField] private Color beamClosedColor = Color.red;

    private Color beamColor;

    [Header("LINE")]
    [FormerlySerializedAs("beamStartB")]
    [SerializeField] private Transform beamStart;
    [SerializeField] private Material beamMaterial;
    [Min(0f)]
    [SerializeField] private float lineWidth = 0.05f;
    [Tooltip("Line renderer only, local Y on this object. Raycasts ignore this.")]
    [SerializeField] private float beamVisualOffsetY;

    private LineRenderer beamLine;
    private MaterialPropertyBlock linePropertyBlock;
    private RaycastHit[] raycastHits;
    private float beamLength;
    private float cachedMaterialEndFadeLength = DefaultBeamEndFadeLength;
    private GameObject currentHit;
    private bool beamObstructed;
    private bool beamEndpointsValid;
    private Color lastAppliedBeamColor;
    private float lastAppliedEndFadeLength = float.NaN;

    private readonly List<MediumDoor> mediumDoors = new List<MediumDoor>();
    private readonly List<TrapDoor> trapDoors = new List<TrapDoor>();
    private readonly List<Bridge> bridges = new List<Bridge>();
    private readonly List<SlideOutPlatform> slideOutPlatforms = new List<SlideOutPlatform>();
    private readonly List<Elevator> elevators = new List<Elevator>();
    private readonly List<PistonHorizontal> pistonHorizontals = new List<PistonHorizontal>();
    private readonly List<PistonVertical> pistonVerticals = new List<PistonVertical>();

    private bool pressurePlateIsPressed;
    private bool buttonConsumed;
    private bool laserDead;
    private bool buttonBeamShuttingDown;
    private float buttonShutdownFadeT;
    private bool prevBeamObstructed;
    private bool skullClosedFromBeam;
    private Coroutine skullTransitionCoroutine;
    private Coroutine buttonShutdownCoroutine;

    public float Range => beamRange;
    public Color BeamColor => beamColor;
    public ActivatorMode Mode => activatorMode;
    public GameObject CurrentHitObject => currentHit;

    private void Reset()
    {
        EnsureBeamLine();
        CacheLineRendererDefaults();
        ApplyBeamMaterial();
        SyncBeamColorForState();
    }

    private void Awake()
    {
        raycastHits = new RaycastHit[InitialRaycastHitCapacity];
        EnsureBeamLine();
        CacheLineRendererDefaults();
        BuildConnectedDeviceLists();

        if (skullBehaviour == null)
            skullBehaviour = GetComponentInChildren<SkullBehaviour>(true);

        ApplyBeamMaterial();
        SyncBeamColorForState();
    }

    private void Start()
    {
        SyncBeamColorForState();
    }

    private void OnEnable()
    {
        EnsureBeamLine();
        ApplyBeamMaterial();
        CacheLineRendererDefaults();

        if (laserDead)
        {
            if (beamLine != null)
                beamLine.enabled = false;
            return;
        }

        CastAndDraw();
        SyncBeamColorForState();
    }

    private void OnValidate()
    {
        beamRange = Mathf.Max(0f, beamRange);
        lineWidth = Mathf.Max(0f, lineWidth);

        QueueEditorLineSetup();

        if (!HasBeamLine())
            return;

        ApplyBeamMaterial();
        CacheLineRendererDefaults();
        BuildConnectedDeviceLists();

        if (Application.isPlaying)
            ApplyBeamColor(force: true);
        else
            SyncBeamColorForState();
    }

    private void LateUpdate()
    {
        if (!HasBeamLine())
            return;

        if (laserDead)
            return;

        CastAndDraw();

        if (Application.isPlaying && beamEndpointsValid && !ButtonShutdownInProgress())
        {
            EvaluateActivatorMode();
            UpdateSkullFromActivationBeam();
        }

        if (!ButtonShutdownInProgress())
            ApplyBeamColor();
    }

    private bool ButtonShutdownInProgress()
    {
        return buttonBeamShuttingDown;
    }

    public void SetBeamColor(Color color)
    {
        beamColor = color;
        ApplyBeamColor(force: true);
    }

    public void SetRange(float value)
    {
        beamRange = Mathf.Max(0f, value);
    }

    private void EnsureBeamLine()
    {
        ResolveBeamStartReference();
        DisableLegacyFakeBeamLine();

        if (beamStart == null)
            return;

        beamLine = beamStart.GetComponent<LineRenderer>();
        if (beamLine == null)
            beamLine = beamStart.gameObject.AddComponent<LineRenderer>();

        LineRenderer lineOnRoot = GetComponent<LineRenderer>();
        if (lineOnRoot != null)
            lineOnRoot.enabled = false;
    }

    private void DisableLegacyFakeBeamLine()
    {
        Transform legacyStart = transform.Find(LegacyBeamLineChildAName);
        if (legacyStart == null)
            return;

        LineRenderer legacyLine = legacyStart.GetComponent<LineRenderer>();
        if (legacyLine != null)
            legacyLine.enabled = false;
    }

    private void ResolveBeamStartReference()
    {
        if (beamStart == null)
            beamStart = transform.Find(BeamLineChildName);

        if (beamStart == null)
            beamStart = transform.Find(LegacyBeamLineChildAName);
    }

    private void ApplyBeamMaterial()
    {
        if (beamMaterial == null || beamLine == null)
            return;

        beamLine.sharedMaterial = beamMaterial;
        RefreshMaterialEndFadeCache();
        lastAppliedEndFadeLength = float.NaN;
    }

#if UNITY_EDITOR
    private void QueueEditorLineSetup()
    {
        if (Application.isPlaying || editorLineSetupQueued)
            return;

        editorLineSetupQueued = true;
        EditorApplication.delayCall += EditorSetupBeamLine;
    }

    private void EditorSetupBeamLine()
    {
        editorLineSetupQueued = false;
        if (this == null)
            return;

        EnsureBeamLine();
        ApplyBeamMaterial();
        CacheLineRendererDefaults();
        CastAndDraw();
        ApplyBeamColor(force: true);
    }
#endif

    private bool HasBeamLine()
    {
        return beamLine != null;
    }

    private void CacheLineRendererDefaults()
    {
        if (beamLine == null)
            return;

        beamLine.enabled = true;
        beamLine.useWorldSpace = true;
        beamLine.positionCount = 2;
        beamLine.alignment = LineAlignment.View;
        beamLine.textureMode = LineTextureMode.Stretch;
        beamLine.numCapVertices = 8;
        beamLine.numCornerVertices = 4;
        beamLine.startWidth = lineWidth;
        beamLine.endWidth = lineWidth;
        beamLine.shadowCastingMode = ShadowCastingMode.Off;
        beamLine.receiveShadows = false;
    }

    private void CastAndDraw()
    {
        ResolveBeamStartReference();
        beamEndpointsValid = false;

        if (beamStart == null)
            return;

        if (!TryGetBeamDirection(out Vector3 direction))
            return;

        UpdateBeamEndpoint(beamStart.position, direction);
        beamObstructed = IsBeamObstructed();
        beamEndpointsValid = true;
    }

    private void UpdateBeamEndpoint(Vector3 origin, Vector3 direction)
    {
        currentHit = null;
        beamLength = Mathf.Max(0f, beamRange);

        Vector3 end = origin + direction * beamLength;

        if (beamRange > 0f && TryGetClosestValidHit(origin, direction, out RaycastHit closestHit))
        {
            beamLength = closestHit.distance;
            currentHit = closestHit.collider.gameObject;
            end = closestHit.point;
        }

        ApplyEndpointToLineRenderer(origin, end);
    }

    private bool TryGetClosestValidHit(Vector3 origin, Vector3 direction, out RaycastHit closestHit)
    {
        closestHit = default;

        if (raycastHits == null || raycastHits.Length == 0)
            raycastHits = new RaycastHit[InitialRaycastHitCapacity];

        int hitCount = Physics.RaycastNonAlloc(origin, direction, raycastHits, beamRange, hitLayers, QueryTriggerInteraction.Ignore);
        while (hitCount == raycastHits.Length && raycastHits.Length < MaxRaycastHitCapacity)
        {
            int newCapacity = Mathf.Min(raycastHits.Length * 2, MaxRaycastHitCapacity);
            System.Array.Resize(ref raycastHits, newCapacity);
            hitCount = Physics.RaycastNonAlloc(origin, direction, raycastHits, beamRange, hitLayers, QueryTriggerInteraction.Ignore);
        }

        float closestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastHits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null || ShouldIgnoreHitCollider(hitCollider))
                continue;

            if (hit.distance < MinRayHitDistance)
                continue;

            if (hit.distance >= closestDistance)
                continue;

            closestDistance = hit.distance;
            closestHit = hit;
            found = true;
        }

        return found;
    }

    private void ApplyEndpointToLineRenderer(Vector3 origin, Vector3 end)
    {
        if (beamLine == null)
            return;

        Vector3 visualOffsetWorld = transform.up * beamVisualOffsetY;

        beamLine.enabled = true;
        beamLine.useWorldSpace = true;
        beamLine.alignment = LineAlignment.View;
        beamLine.SetPosition(0, origin + visualOffsetWorld);
        beamLine.SetPosition(1, end + visualOffsetWorld);
    }

    private bool ShouldIgnoreHitCollider(Collider hitCollider)
    {
        if (hitCollider == null)
            return true;

        Transform hitTransform = hitCollider.transform;

        if (beamLine != null
            && (hitTransform == beamLine.transform || hitTransform.IsChildOf(beamLine.transform)))
            return true;

        if (hitTransform == transform)
            return true;

        return false;
    }

    private bool IsBeamObstructed()
    {
        if (beamRange <= 0f)
            return false;

        return beamLength < beamRange - ObstructionDistanceEpsilon;
    }

    private void EvaluateActivatorMode()
    {
        switch (activatorMode)
        {
            case ActivatorMode.PressurePlate:
                EvaluatePressurePlate();
                break;
            case ActivatorMode.Lever:
                EvaluateLever();
                break;
            case ActivatorMode.Button:
                EvaluateButton();
                break;
        }

        prevBeamObstructed = beamObstructed;
    }

    private void EvaluatePressurePlate()
    {
        if (beamObstructed == pressurePlateIsPressed)
            return;

        pressurePlateIsPressed = beamObstructed;
        NotifyApplyActivatorReverseDevices();
    }

    private void EvaluateLever()
    {
        if (!beamObstructed || prevBeamObstructed)
            return;

        NotifyLeverDevices();
    }

    private void EvaluateButton()
    {
        if (buttonConsumed)
            return;

        if (!beamObstructed || prevBeamObstructed)
            return;

        buttonConsumed = true;
        NotifyApplyActivatorReverseDevices();
        BeginButtonLaserShutdown();
    }

    private void NotifyApplyActivatorReverseDevices()
    {
        foreach (MediumDoor mediumDoor in mediumDoors)
            mediumDoor.ApplyActivatorReverse();

        foreach (TrapDoor trapDoor in trapDoors)
            trapDoor.ApplyActivatorReverse();

        foreach (Bridge bridge in bridges)
            bridge.ApplyActivatorReverse();

        foreach (SlideOutPlatform slideOutPlatform in slideOutPlatforms)
            slideOutPlatform.ApplyActivatorReverse();
    }

    private void NotifyLeverDevices()
    {
        foreach (MediumDoor mediumDoor in mediumDoors)
            mediumDoor.OnLeverOperated();

        foreach (TrapDoor trapDoor in trapDoors)
            trapDoor.OnLeverOperated();

        foreach (Bridge bridge in bridges)
            bridge.OnLeverOperated();

        foreach (Elevator elevator in elevators)
            elevator.OnLeverOperated();

        foreach (PistonHorizontal pistonHorizontal in pistonHorizontals)
            pistonHorizontal.OnLeverOperated();

        foreach (PistonVertical pistonVertical in pistonVerticals)
            pistonVertical.OnLeverOperated();

        foreach (SlideOutPlatform slideOutPlatform in slideOutPlatforms)
            slideOutPlatform.OnLeverOperated();
    }

    private void UpdateSkullFromActivationBeam()
    {
        if (activatorMode == ActivatorMode.Button)
            return;

        if (beamObstructed == skullClosedFromBeam)
            return;

        skullClosedFromBeam = beamObstructed;
        ApplySkullPressedState(beamObstructed);
    }

    private void SyncBeamColorForState()
    {
        beamColor = IsLaserVisualClosed() ? beamClosedColor : beamOpenedColor;
        ApplyBeamColor(force: true);
    }

    private bool IsLaserVisualClosed()
    {
        return skullClosedFromBeam;
    }

    private void BeginButtonLaserShutdown()
    {
        if (skullTransitionCoroutine != null)
        {
            StopCoroutine(skullTransitionCoroutine);
            skullTransitionCoroutine = null;
        }

        if (buttonShutdownCoroutine != null)
            StopCoroutine(buttonShutdownCoroutine);

        buttonBeamShuttingDown = true;
        buttonShutdownFadeT = 0f;
        beamColor = beamOpenedColor;
        ApplyBeamColor(force: true);
        buttonShutdownCoroutine = StartCoroutine(ButtonLaserShutdownSequence());
    }

    private IEnumerator ButtonLaserShutdownSequence()
    {
        Coroutine skullFade = null;
        if (skullBehaviour != null)
            skullFade = StartCoroutine(skullBehaviour.TransitionFadeToOff(ButtonShutdownFadeDuration));

        Color from = beamOpenedColor;
        float fadeStartTime = Time.unscaledTime;

        do
        {
            float elapsed = Time.unscaledTime - fadeStartTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / ButtonShutdownFadeDuration));
            buttonShutdownFadeT = t;
            beamColor = Color.Lerp(from, BeamOffColor, t);
            ApplyBeamColor(force: true);
            yield return null;
        }
        while (Time.unscaledTime - fadeStartTime < ButtonShutdownFadeDuration);

        buttonShutdownFadeT = 1f;
        beamColor = BeamOffColor;
        ApplyBeamColor(force: true);

        if (skullFade != null)
            yield return skullFade;

        FinalizeLaserShutdown();
        buttonShutdownCoroutine = null;
    }

    private void FinalizeLaserShutdown()
    {
        laserDead = true;
        buttonBeamShuttingDown = false;
        buttonShutdownFadeT = 0f;

        if (beamLine != null)
            beamLine.enabled = false;

        if (skullBehaviour != null)
            skullBehaviour.StopLaserParticles();
    }

    private void ApplySkullPressedState(bool beamBlocked)
    {
        if (skullTransitionCoroutine != null)
        {
            StopCoroutine(skullTransitionCoroutine);
            skullTransitionCoroutine = null;
        }

        skullTransitionCoroutine = StartCoroutine(TransitionSkullAndBeams(beamBlocked));
    }

    private IEnumerator TransitionSkullAndBeams(bool beamBlocked)
    {
        Coroutine skullRoutine = null;
        if (skullBehaviour != null)
        {
            skullRoutine = StartCoroutine(beamBlocked
                ? skullBehaviour.TransitionOpenedToClosed()
                : skullBehaviour.TransitionClosedToOpen());
        }

        yield return LerpBeamColors(beamBlocked);

        if (skullRoutine != null)
            yield return skullRoutine;

        skullTransitionCoroutine = null;
    }

    private IEnumerator LerpBeamColors(bool beamBlocked)
    {
        Color from = beamBlocked ? beamOpenedColor : beamClosedColor;
        Color to = beamBlocked ? beamClosedColor : beamOpenedColor;
        float duration = skullBehaviour != null ? skullBehaviour.EyeTransitionDuration : 0.25f;

        if (duration <= 0f)
        {
            beamColor = to;
            ApplyBeamColor(force: true);
            yield break;
        }

        beamColor = from;
        ApplyBeamColor(force: true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            beamColor = Color.Lerp(from, to, t);
            ApplyBeamColor(force: true);
            yield return null;
        }

        beamColor = to;
        ApplyBeamColor(force: true);
    }

    private void BuildConnectedDeviceLists()
    {
        mediumDoors.Clear();
        trapDoors.Clear();
        bridges.Clear();
        slideOutPlatforms.Clear();
        elevators.Clear();
        pistonHorizontals.Clear();
        pistonVerticals.Clear();

        foreach (GameObject deviceObject in connectedDevices)
        {
            if (deviceObject == null)
                continue;

            MediumDoor mediumDoor = deviceObject.GetComponent<MediumDoor>();
            if (mediumDoor != null)
                mediumDoors.Add(mediumDoor);

            TrapDoor trapDoor = deviceObject.GetComponent<TrapDoor>();
            if (trapDoor != null)
                trapDoors.Add(trapDoor);

            Bridge bridge = deviceObject.GetComponent<Bridge>();
            if (bridge != null)
                bridges.Add(bridge);

            SlideOutPlatform slideOutPlatform = deviceObject.GetComponent<SlideOutPlatform>();
            if (slideOutPlatform != null)
                slideOutPlatforms.Add(slideOutPlatform);

            if (activatorMode != ActivatorMode.Lever)
                continue;

            Elevator elevator = deviceObject.GetComponent<Elevator>();
            if (elevator != null)
                elevators.Add(elevator);

            PistonHorizontal pistonHorizontal = deviceObject.GetComponent<PistonHorizontal>();
            if (pistonHorizontal != null)
                pistonHorizontals.Add(pistonHorizontal);

            PistonVertical pistonVertical = deviceObject.GetComponent<PistonVertical>();
            if (pistonVertical != null)
                pistonVerticals.Add(pistonVertical);
        }
    }

    private void ApplyBeamColor(bool force = false)
    {
        if (!HasBeamLine())
            return;

        if (!laserDead)
            beamLine.enabled = true;

        float endFadeLength = GetBeamEndFadeLength();
        if (!force
            && beamColor == lastAppliedBeamColor
            && Mathf.Approximately(endFadeLength, lastAppliedEndFadeLength))
            return;

        lastAppliedBeamColor = beamColor;
        lastAppliedEndFadeLength = endFadeLength;

        Color beamTint = beamColor;
        Color beamEmission = beamColor;
        if (!buttonBeamShuttingDown)
            beamEmission.a = 1f;

        beamLine.startColor = beamTint;
        beamLine.endColor = beamTint;

        linePropertyBlock ??= new MaterialPropertyBlock();
        beamLine.GetPropertyBlock(linePropertyBlock);
        linePropertyBlock.SetColor(MainColorId, beamTint);
        linePropertyBlock.SetColor(BaseColorId, beamTint);
        linePropertyBlock.SetColor(EmissionColorId, beamEmission);
        float emissionStrength = buttonBeamShuttingDown
            ? Mathf.Lerp(BeamMaterialEmissionStrength, 0f, buttonShutdownFadeT)
            : BeamMaterialEmissionStrength;
        linePropertyBlock.SetFloat(EmissionStrengthId, emissionStrength);
        linePropertyBlock.SetFloat(EndFadeLengthId, endFadeLength);
        beamLine.SetPropertyBlock(linePropertyBlock);

        if (skullBehaviour != null && !laserDead)
            skullBehaviour.ApplyLaserParticleColor(beamColor);
    }

    private void RefreshMaterialEndFadeCache()
    {
        if (beamMaterial == null || !beamMaterial.HasProperty(EndFadeLengthId))
        {
            cachedMaterialEndFadeLength = DefaultBeamEndFadeLength;
            return;
        }

        cachedMaterialEndFadeLength = Mathf.Clamp01(beamMaterial.GetFloat(EndFadeLengthId));
    }

    private float GetBeamEndFadeLength()
    {
        if (beamRange <= 0f)
            return 0f;

        if (activatorMode == ActivatorMode.Button || buttonBeamShuttingDown)
            return cachedMaterialEndFadeLength;

        if (beamObstructed)
            return 0f;

        return cachedMaterialEndFadeLength;
    }

    private bool TryGetBeamDirection(out Vector3 direction)
    {
        direction = default;
        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude < 0.0001f)
            return false;

        direction = forward.normalized;
        return true;
    }
}
