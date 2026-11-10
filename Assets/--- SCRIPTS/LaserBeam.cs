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

    private const int BeamCount = 2;
    private const int ActivationBeamIndex = 1;
    private const string BeamLineChildAName = "BeamLineA";
    private const string BeamLineChildBName = "BeamLineB";
    private const float ObstructionDistanceEpsilon = 0.001f;
    private const float MinRayHitDistance = 0.02f;
    private static readonly int MainColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int EndFadeLengthId = Shader.PropertyToID("_EndFadeLength");
    private const float BeamMaterialEmissionStrength = 1f;
    private const float DefaultBeamEndFadeLength = 0.18f;

#if UNITY_EDITOR
    private bool editorLineSetupQueued;
#endif

    [Header("ACTIVATOR")]
    [SerializeField] private ActivatorMode activatorMode = ActivatorMode.PressurePlate;
    [SerializeField] private List<GameObject> connectedDevices = new List<GameObject>();
    [SerializeField] private SkullBehaviour skullBehaviour;

    [Header("RANGE")]
    [FormerlySerializedAs("range")]
    [Min(0f)]
    [SerializeField] private float beamRangeA = 10f;
    [Min(0f)]
    [SerializeField] private float beamRangeB = 10f;

    [Header("HIT")]
    [SerializeField] private LayerMask hitLayers = Physics.DefaultRaycastLayers;

    [Header("COLOR")]
    [ColorUsage(true, true)]
    [FormerlySerializedAs("beamColor")]
    [SerializeField] private Color beamOpenedColor = Color.red;
    [ColorUsage(true, true)]
    [SerializeField] private Color beamClosedColor = Color.red;

    private Color beamColor;
    [Header("LINES")]
    [SerializeField] private Transform beamStartA;
    [SerializeField] private Transform beamStartB;
    [SerializeField] private Material beamMaterial;
    [Min(0f)]
    [SerializeField] private float lineWidth = 0.05f;
    [Tooltip("Line renderers only, local Y on this object. Raycasts ignore this.")]
    [SerializeField] private float beamVisualOffsetY;

    private LineRenderer[] beamLines;
    private readonly MaterialPropertyBlock[] linePropertyBlocks = new MaterialPropertyBlock[BeamCount];
    private readonly float[] beamLengths = new float[BeamCount];
    private readonly GameObject[] currentHits = new GameObject[BeamCount];
    private readonly bool[] beamObstructed = new bool[BeamCount];
    private bool beamEndpointsValid;

    private readonly List<MediumDoor> mediumDoors = new List<MediumDoor>();
    private readonly List<TrapDoor> trapDoors = new List<TrapDoor>();
    private readonly List<Bridge> bridges = new List<Bridge>();
    private readonly List<SlideOutPlatform> slideOutPlatforms = new List<SlideOutPlatform>();
    private readonly List<Elevator> elevators = new List<Elevator>();
    private readonly List<PistonHorizontal> pistonHorizontals = new List<PistonHorizontal>();
    private readonly List<PistonVertical> pistonVerticals = new List<PistonVertical>();

    private bool pressurePlateIsPressed;
    private bool buttonConsumed;
    private bool prevActivationBeamObstructed;
    private bool skullClosedFromBeam;
    private Coroutine skullTransitionCoroutine;

    public float Range => GetBeamRange(ActivationBeamIndex);
    public Color BeamColor => beamColor;
    public ActivatorMode Mode => activatorMode;
    public GameObject CurrentHitObjectA => currentHits[0];
    public GameObject CurrentHitObjectB => currentHits[1];

    private void Reset()
    {
        EnsureBeamLineChildren();
        CacheLineRendererDefaults();
        ApplyBeamMaterial();
        SyncBeamColorForState();
    }

    private void Awake()
    {
        EnsureBeamLineChildren();
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
        EnsureBeamLineChildren();
        ApplyBeamMaterial();
        CacheLineRendererDefaults();
        CastAndDraw();
        SyncBeamColorForState();
    }

    private void OnValidate()
    {
        beamRangeA = Mathf.Max(0f, beamRangeA);
        beamRangeB = Mathf.Max(0f, beamRangeB);
        lineWidth = Mathf.Max(0f, lineWidth);

        QueueEditorLineSetup();

        if (!HasRequiredBeamLines())
            return;

        ApplyBeamMaterial();
        CacheLineRendererDefaults();
        BuildConnectedDeviceLists();

        if (Application.isPlaying)
            ApplyBeamColor();
        else
            SyncBeamColorForState();
    }

    private void LateUpdate()
    {
        if (!HasRequiredBeamLines())
            return;

        CastAndDraw();
        ApplyBeamColor();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying || !beamEndpointsValid)
            return;

        EvaluateActivatorMode();
        UpdateSkullFromActivationBeam();
    }

    // Sets beam color from the Inspector or code.
    public void SetBeamColor(Color color)
    {
        beamColor = color;
        ApplyBeamColor();
    }

    // Sets max beam reach before a blocking hit.
    public void SetRange(float value)
    {
        value = Mathf.Max(0f, value);
        beamRangeB = value;
    }

    private float GetBeamRange(int beamIndex)
    {
        return beamIndex == 0 ? beamRangeA : beamRangeB;
    }

    private void EnsureBeamLineChildren()
    {
        ResolveBeamStartReferences();

        beamLines = new LineRenderer[BeamCount];
        beamLines[0] = GetOrCreateLineOnStart(beamStartA, BeamLineChildAName);
        beamLines[1] = GetOrCreateLineOnStart(beamStartB, BeamLineChildBName);

        LineRenderer lineOnRoot = GetComponent<LineRenderer>();
        if (lineOnRoot != null)
            lineOnRoot.enabled = false;
    }

    private void ResolveBeamStartReferences()
    {
        if (beamStartA == null)
            beamStartA = transform.Find(BeamLineChildAName);

        if (beamStartB == null)
            beamStartB = transform.Find(BeamLineChildBName);
    }

    private LineRenderer GetOrCreateLineOnStart(Transform start, string childName)
    {
        if (start == null)
            start = transform.Find(childName);

        if (start == null)
            return null;

        LineRenderer line = start.GetComponent<LineRenderer>();
        if (line != null)
            return line;

        return start.gameObject.AddComponent<LineRenderer>();
    }

    private void ApplyBeamMaterial()
    {
        if (beamMaterial == null || !HasRequiredBeamLines())
            return;

        for (int i = 0; i < BeamCount; i++)
            beamLines[i].sharedMaterial = beamMaterial;
    }

#if UNITY_EDITOR
    private void QueueEditorLineSetup()
    {
        if (Application.isPlaying || editorLineSetupQueued)
            return;

        editorLineSetupQueued = true;
        EditorApplication.delayCall += EditorSetupBeamLines;
    }

    private void EditorSetupBeamLines()
    {
        editorLineSetupQueued = false;
        if (this == null)
            return;

        EnsureBeamLineChildren();
        ApplyBeamMaterial();
        CacheLineRendererDefaults();
        CastAndDraw();
        ApplyBeamColor();
    }
#endif

    private bool HasRequiredBeamLines()
    {
        return beamLines != null
            && beamLines.Length >= BeamCount
            && beamLines[0] != null
            && beamLines[1] != null;
    }

    private void CacheLineRendererDefaults()
    {
        if (!HasRequiredBeamLines())
            return;

        for (int i = 0; i < BeamCount; i++)
        {
            LineRenderer line = beamLines[i];
            line.enabled = true;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 8;
            line.numCornerVertices = 4;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
        }
    }

    private void CastAndDraw()
    {
        ResolveBeamStartReferences();
        beamEndpointsValid = false;

        if (!TryGetBeamStarts(out Vector3 startA, out Vector3 startB))
            return;

        if (!TryGetBeamDirection(out Vector3 direction))
            return;

        UpdateBeamEndpoint(0, startA, direction);
        UpdateBeamEndpoint(1, startB, direction);

        beamObstructed[0] = IsBeamObstructed(0);
        beamObstructed[1] = IsBeamObstructed(1);
        beamEndpointsValid = true;
    }

    private void UpdateBeamEndpoint(int index, Vector3 origin, Vector3 direction)
    {
        currentHits[index] = null;
        float beamRange = GetBeamRange(index);
        beamLengths[index] = Mathf.Max(0f, beamRange);

        Vector3 end = origin + direction * beamLengths[index];

        if (beamRange > 0f)
        {
            RaycastHit[] hits = Physics.RaycastAll(origin, direction, beamRange, hitLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null || ShouldIgnoreHitCollider(hitCollider))
                    continue;

                if (hits[i].distance < MinRayHitDistance)
                    continue;

                beamLengths[index] = hits[i].distance;
                currentHits[index] = hitCollider.gameObject;
                end = hits[i].point;
                break;
            }
        }
        ApplyEndpointToLineRenderer(index, origin, end);
    }

    private void ApplyEndpointToLineRenderer(int index, Vector3 origin, Vector3 end)
    {
        if (!HasRequiredBeamLines() || beamLines[index] == null)
            return;

        Vector3 visualOffsetWorld = transform.up * beamVisualOffsetY;

        LineRenderer line = beamLines[index];
        line.enabled = true;
        line.useWorldSpace = true;
        line.alignment = LineAlignment.View;
        line.SetPosition(0, origin + visualOffsetWorld);
        line.SetPosition(1, end + visualOffsetWorld);
    }

    private bool ShouldIgnoreHitCollider(Collider hitCollider)
    {
        if (hitCollider == null)
            return true;

        Transform hitTransform = hitCollider.transform;

        if (beamLines != null)
        {
            for (int i = 0; i < BeamCount; i++)
            {
                LineRenderer line = beamLines[i];
                if (line == null)
                    continue;

                if (hitTransform == line.transform || hitTransform.IsChildOf(line.transform))
                    return true;
            }
        }

        if (hitTransform == transform)
            return true;

        return false;
    }

    private bool IsBeamObstructed(int beamIndex)
    {
        float beamRange = GetBeamRange(beamIndex);
        if (beamRange <= 0f)
            return false;

        return beamLengths[beamIndex] < beamRange - ObstructionDistanceEpsilon;
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

        prevActivationBeamObstructed = IsActivationBeamObstructed();
    }

    private bool IsActivationBeamObstructed()
    {
        return beamObstructed[ActivationBeamIndex];
    }

    private void EvaluatePressurePlate()
    {
        bool pressed = IsActivationBeamObstructed();
        if (pressed == pressurePlateIsPressed)
            return;

        pressurePlateIsPressed = pressed;
        NotifyPressurePlateDevices(pressed);
    }

    private void EvaluateLever()
    {
        bool obstructed = IsActivationBeamObstructed();
        if (!obstructed || prevActivationBeamObstructed)
            return;

        NotifyLeverDevices();
    }

    private void EvaluateButton()
    {
        if (buttonConsumed)
            return;

        bool crossedNow = IsActivationBeamObstructed();
        if (!crossedNow || prevActivationBeamObstructed)
            return;

        buttonConsumed = true;
        NotifyButtonDevices();
    }

    private void NotifyPressurePlateDevices(bool pressed)
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

    private void NotifyButtonDevices()
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

    private void UpdateSkullFromActivationBeam()
    {
        bool obstructed = IsActivationBeamObstructed();
        if (obstructed == skullClosedFromBeam)
            return;

        skullClosedFromBeam = obstructed;
        ApplySkullPressedState(obstructed);
    }

    private void SyncBeamColorForState()
    {
        beamColor = skullClosedFromBeam ? beamClosedColor : beamOpenedColor;
        ApplyBeamColor();
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
            ApplyBeamColor();
            yield break;
        }

        beamColor = from;
        ApplyBeamColor();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            beamColor = Color.Lerp(from, to, t);
            ApplyBeamColor();
            yield return null;
        }

        beamColor = to;
        ApplyBeamColor();
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

    private void ApplyBeamColor()
    {
        if (!HasRequiredBeamLines())
            return;

        Color beamTint = beamColor;
        Color beamEmission = beamColor;
        beamEmission.a = 1f;

        for (int i = 0; i < BeamCount; i++)
        {
            LineRenderer line = beamLines[i];

            line.startColor = beamTint;
            line.endColor = beamTint;

            linePropertyBlocks[i] ??= new MaterialPropertyBlock();
            line.GetPropertyBlock(linePropertyBlocks[i]);
            linePropertyBlocks[i].SetColor(MainColorId, beamTint);
            linePropertyBlocks[i].SetColor(BaseColorId, beamTint);
            linePropertyBlocks[i].SetColor(EmissionColorId, beamEmission);
            linePropertyBlocks[i].SetFloat(EmissionStrengthId, BeamMaterialEmissionStrength);
            linePropertyBlocks[i].SetFloat(EndFadeLengthId, GetBeamEndFadeLength(i));
            line.SetPropertyBlock(linePropertyBlocks[i]);
        }
    }

    private float GetMaterialEndFadeLength()
    {
        if (beamMaterial == null || !beamMaterial.HasProperty(EndFadeLengthId))
            return DefaultBeamEndFadeLength;

        return Mathf.Clamp01(beamMaterial.GetFloat(EndFadeLengthId));
    }

    private float GetBeamEndFadeLength(int beamIndex)
    {
        if (beamObstructed[beamIndex])
            return 0f;

        float beamRange = GetBeamRange(beamIndex);
        if (beamRange <= 0f)
            return 0f;

        return GetMaterialEndFadeLength();
    }

    private bool TryGetBeamStarts(out Vector3 startA, out Vector3 startB)
    {
        startA = default;
        startB = default;

        ResolveBeamStartReferences();

        if (beamStartA == null || beamStartB == null)
            return false;

        startA = beamStartA.position;
        startB = beamStartB.position;
        return true;
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
