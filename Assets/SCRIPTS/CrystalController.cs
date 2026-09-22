using System;
using System.Collections.Generic;
using UnityEngine;

public class CrystalController : MonoBehaviour
{
    public const int CrystalColorTypeCount = 6;

    private const float MinColorLerpDuration = 0f;
    private const float MaxColorLerpDuration = 2f;

    private static readonly string[] CrystalSetHeaderLabels =
    {
        "RED RUBY",
        "CRYSTAL TYPE 2",
        "CRYSTAL TYPE 3",
        "CRYSTAL TYPE 4",
        "CRYSTAL TYPE 5",
        "CRYSTAL TYPE 6",
    };

    [Serializable]
    public class CrystalColorPair
    {
        public Color baseColor;
        public Color fresnelColor;
        public Color parallaxColor;
    }

    [Serializable]
    public class CrystalTypeList
    {
        public CrystalColorPair brightColors = new CrystalColorPair();
        public CrystalColorPair darkColors = new CrystalColorPair();
        public List<GameObject> crystals = new List<GameObject>();
    }

    private class CrystalColorLerpState
    {
        public GameObject crystal;
        public Color fromBase;
        public Color fromFresnel;
        public Color fromParallax;
        public Color toBase;
        public Color toFresnel;
        public Color toParallax;
        public Color currentBase;
        public Color currentFresnel;
        public Color currentParallax;
        public float elapsed;
    }

    private class CrystalLightLerpState
    {
        public Color fromColor;
        public Color toColor;
        public Color currentColor;
        public float fromIntensity;
        public float toIntensity;
        public float currentIntensity;
        public float elapsed;
    }

    [Header("BOULDER BASE:")]
    [SerializeField] private Renderer boulderBase;
    [SerializeField] private Color boulderBaseColor = Color.white;

    [SerializeField] private Light crystalLight;
    [SerializeField] private float[] defaultLightIntensityPerType = new float[CrystalColorTypeCount];

    [Header("COLOR LERP:")]
    [SerializeField] [Range(MinColorLerpDuration, MaxColorLerpDuration)] private float colorLerpDuration = 0.25f;

    [SerializeField] private CrystalTypeList[] crystalSets = new CrystalTypeList[CrystalColorTypeCount];

    [SerializeField] private int lastColorIndex = -1;
    [SerializeField] private int lastMaxMw;
    [SerializeField] private int lastAvailableMw;

    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseColorTintPropertyId = Shader.PropertyToID("_BaseColorTint");
    private static readonly int RockColorPropertyId = Shader.PropertyToID("_RockColor");
    private static readonly int FresnelColorPropertyId = Shader.PropertyToID("_FresnelColor");
    private static readonly int ParallaxColorPropertyId = Shader.PropertyToID("_ParallaxColor");

    private readonly Dictionary<int, CrystalColorLerpState> crystalColorStates = new Dictionary<int, CrystalColorLerpState>();

    private MaterialPropertyBlock propertyBlock;
    private CrystalLightLerpState lightLerpState;
    private float lightIntensityAtPlayStart;
    private Color lightColorAtPlayStart;
    private bool lightEnabledAtPlayStart;
    private bool lightPlaySnapshotTaken;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ApplyBoulderBaseColor();
        SnapshotLightAtPlayStart();
        EnsureLightLerpStateInitialized();
    }

    private void OnDestroy()
    {
        if (crystalLight == null || !lightPlaySnapshotTaken)
            return;

        crystalLight.intensity = lightIntensityAtPlayStart;
        crystalLight.color = lightColorAtPlayStart;
        crystalLight.enabled = lightEnabledAtPlayStart;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (colorLerpDuration <= MinColorLerpDuration)
            return;

        float deltaTime = Time.deltaTime;
        TickColorLerps(deltaTime);
        TickLightLerp(deltaTime);
    }

    public static string GetSetHeaderLabel(int setIndex)
    {
        if (setIndex < 0 || setIndex >= CrystalSetHeaderLabels.Length)
            return $"CRYSTAL TYPE {setIndex + 1}";

        return CrystalSetHeaderLabels[setIndex];
    }

    private void OnValidate()
    {
        colorLerpDuration = Mathf.Clamp(colorLerpDuration, MinColorLerpDuration, MaxColorLerpDuration);
        EnsureCrystalSetCount();
        EnsureDefaultLightIntensityCount();

        ApplyBoulderBaseColor();

        if (!Application.isPlaying && lastColorIndex >= 0)
            ApplyPowerSourceState(lastColorIndex, lastMaxMw, lastMaxMw);
    }

    // Applies the boulder base tint via MaterialPropertyBlock.
    private void ApplyBoulderBaseColor()
    {
        if (boulderBase == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        boulderBase.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(RockColorPropertyId, boulderBaseColor);
        propertyBlock.SetColor(BaseColorPropertyId, boulderBaseColor);
        boulderBase.SetPropertyBlock(propertyBlock);
    }

    private void SnapshotLightAtPlayStart()
    {
        if (crystalLight == null || lightPlaySnapshotTaken)
            return;

        lightIntensityAtPlayStart = crystalLight.intensity;
        lightColorAtPlayStart = crystalLight.color;
        lightEnabledAtPlayStart = crystalLight.enabled;
        lightPlaySnapshotTaken = true;
    }

    // Updates which crystals are active for a Power Source (capacity = maxMw, color picks the list).
    public void ApplyPowerSourceState(int colorIndex, int maxMw, int availableMw)
    {
        EnsureCrystalSetCount();

        lastColorIndex = colorIndex;
        lastMaxMw = Mathf.Max(0, maxMw);
        lastAvailableMw = Mathf.Max(0, availableMw);

        int activeSetIndex = Mathf.Clamp(colorIndex, 0, CrystalColorTypeCount - 1);

        for (int setIndex = 0; setIndex < CrystalColorTypeCount; setIndex++)
        {
            CrystalTypeList set = crystalSets[setIndex];
            if (set == null || set.crystals == null)
                continue;

            bool isActiveColorSet = setIndex == activeSetIndex;
            int enabledCount = isActiveColorSet ? lastMaxMw : 0;

            for (int crystalIndex = 0; crystalIndex < set.crystals.Count; crystalIndex++)
            {
                GameObject crystal = set.crystals[crystalIndex];
                if (crystal == null || !IsCrystalUnderThisController(crystal))
                    continue;

                bool isEnabled = crystalIndex < enabledCount;
                crystal.SetActive(isEnabled);

                if (!isActiveColorSet || !isEnabled)
                    continue;

                bool useDarkColors = crystalIndex >= lastAvailableMw;
                CrystalColorPair targetColors = useDarkColors ? set.darkColors : set.brightColors;
                SetCrystalColorTarget(crystal, targetColors);
            }
        }

        if (!Application.isPlaying)
            return;

        CrystalTypeList activeSet = crystalSets[activeSetIndex];
        UpdateLightFromPowerSourceState(activeSet, lastAvailableMw, lastMaxMw);
    }

    private void EnsureLightLerpStateInitialized()
    {
        if (lightLerpState != null)
            return;

        float initialIntensity = lastColorIndex >= 0
            ? GetDefaultLightIntensity(lastColorIndex)
            : lightIntensityAtPlayStart;

        lightLerpState = new CrystalLightLerpState
        {
            currentColor = lightColorAtPlayStart,
            currentIntensity = initialIntensity,
            elapsed = MaxColorLerpDuration
        };
    }

    private void UpdateLightFromPowerSourceState(CrystalTypeList activeSet, int brightCrystalCount, int activeCrystalCount)
    {
        if (crystalLight == null || activeSet == null)
            return;

        EnsureLightLerpStateInitialized();

        float brightRatio = activeCrystalCount > 0
            ? Mathf.Clamp01(brightCrystalCount / (float)activeCrystalCount)
            : 0f;

        Color brightLightColor = GetLightColorFromPair(activeSet.brightColors);
        Color darkLightColor = GetLightColorFromPair(activeSet.darkColors);
        Color targetColor = Color.Lerp(darkLightColor, brightLightColor, brightRatio);
        int colorTypeIndex = Mathf.Clamp(lastColorIndex, 0, CrystalColorTypeCount - 1);
        float targetIntensity = GetDefaultLightIntensity(colorTypeIndex) * brightRatio;

        SetLightTarget(targetColor, targetIntensity);
    }

    private void SetLightTarget(Color targetColor, float targetIntensity)
    {
        if (crystalLight == null)
            return;

        EnsureLightLerpStateInitialized();

        if (!Application.isPlaying || colorLerpDuration <= MinColorLerpDuration)
        {
            ApplyLightState(targetColor, targetIntensity);
            SyncLightLerpState(targetColor, targetIntensity);
            return;
        }

        if (lightLerpState == null)
        {
            lightLerpState = new CrystalLightLerpState();
            SyncLightLerpState(crystalLight.color, crystalLight.intensity);
        }

        if (IsLightLerpingToTarget(targetColor, targetIntensity))
            return;

        lightLerpState.fromColor = lightLerpState.currentColor;
        lightLerpState.fromIntensity = lightLerpState.currentIntensity;
        lightLerpState.toColor = targetColor;
        lightLerpState.toIntensity = targetIntensity;
        lightLerpState.elapsed = 0f;
    }

    private void TickLightLerp(float deltaTime)
    {
        if (crystalLight == null || lightLerpState == null)
            return;

        if (lightLerpState.elapsed >= colorLerpDuration)
            return;

        lightLerpState.elapsed += deltaTime;
        float t = Mathf.Clamp01(lightLerpState.elapsed / colorLerpDuration);
        lightLerpState.currentColor = Color.Lerp(lightLerpState.fromColor, lightLerpState.toColor, t);
        lightLerpState.currentIntensity = Mathf.Lerp(lightLerpState.fromIntensity, lightLerpState.toIntensity, t);
        ApplyLightState(lightLerpState.currentColor, lightLerpState.currentIntensity);
    }

    private void ApplyLightState(Color color, float intensity)
    {
        if (crystalLight == null)
            return;

        crystalLight.color = color;
        crystalLight.intensity = intensity;
    }

    private void SyncLightLerpState(Color color, float intensity)
    {
        if (lightLerpState == null)
            lightLerpState = new CrystalLightLerpState();

        lightLerpState.fromColor = color;
        lightLerpState.toColor = color;
        lightLerpState.currentColor = color;
        lightLerpState.fromIntensity = intensity;
        lightLerpState.toIntensity = intensity;
        lightLerpState.currentIntensity = intensity;
        lightLerpState.elapsed = MaxColorLerpDuration;
    }

    private bool IsLightLerpingToTarget(Color targetColor, float targetIntensity)
    {
        if (lightLerpState == null)
            return false;

        return ColorsApproximatelyEqual(lightLerpState.toColor, targetColor)
            && Mathf.Approximately(lightLerpState.toIntensity, targetIntensity);
    }

    private static Color GetLightColorFromPair(CrystalColorPair colors)
    {
        if (colors == null)
            return Color.black;

        return new Color(
            (colors.baseColor.r + colors.fresnelColor.r + colors.parallaxColor.r) / 3f,
            (colors.baseColor.g + colors.fresnelColor.g + colors.parallaxColor.g) / 3f,
            (colors.baseColor.b + colors.fresnelColor.b + colors.parallaxColor.b) / 3f,
            (colors.baseColor.a + colors.fresnelColor.a + colors.parallaxColor.a) / 3f);
    }

    // Starts or retargets a per-crystal color lerp from the current displayed colors.
    private void SetCrystalColorTarget(GameObject crystal, CrystalColorPair targetColors)
    {
        if (crystal == null || targetColors == null)
            return;

        if (!Application.isPlaying || colorLerpDuration <= MinColorLerpDuration)
        {
            ApplyColorsToCrystal(crystal, targetColors);
            SyncLerpStateToColors(crystal, targetColors);
            return;
        }

        int crystalId = crystal.GetInstanceID();
        if (!crystalColorStates.TryGetValue(crystalId, out CrystalColorLerpState state))
        {
            state = new CrystalColorLerpState { crystal = crystal };
            crystalColorStates[crystalId] = state;
            InitializeLerpStateFromTarget(state, targetColors);
            ApplyColorsFromLerpState(state);
            return;
        }

        state.crystal = crystal;

        if (IsLerpingToTarget(state, targetColors))
            return;

        state.fromBase = state.currentBase;
        state.fromFresnel = state.currentFresnel;
        state.fromParallax = state.currentParallax;
        state.toBase = targetColors.baseColor;
        state.toFresnel = targetColors.fresnelColor;
        state.toParallax = targetColors.parallaxColor;
        state.elapsed = 0f;
    }

    private void TickColorLerps(float deltaTime)
    {
        foreach (CrystalColorLerpState state in crystalColorStates.Values)
        {
            if (state.crystal == null || !state.crystal.activeInHierarchy)
                continue;

            if (state.elapsed >= colorLerpDuration)
                continue;

            state.elapsed += deltaTime;
            float t = Mathf.Clamp01(state.elapsed / colorLerpDuration);
            state.currentBase = Color.Lerp(state.fromBase, state.toBase, t);
            state.currentFresnel = Color.Lerp(state.fromFresnel, state.toFresnel, t);
            state.currentParallax = Color.Lerp(state.fromParallax, state.toParallax, t);
            ApplyColorsFromLerpState(state);
        }
    }

    private void ApplyColorsFromLerpState(CrystalColorLerpState state)
    {
        ApplyColorsToCrystal(
            state.crystal,
            state.currentBase,
            state.currentFresnel,
            state.currentParallax);
    }

    private void ApplyColorsToCrystal(GameObject crystal, CrystalColorPair colors)
    {
        if (colors == null)
            return;

        ApplyColorsToCrystal(crystal, colors.baseColor, colors.fresnelColor, colors.parallaxColor);
    }

    private void ApplyColorsToCrystal(GameObject crystal, Color baseColor, Color fresnelColor, Color parallaxColor)
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (crystal == null)
            return;

        Renderer[] renderers = crystal.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorTintPropertyId, baseColor);
            propertyBlock.SetColor(FresnelColorPropertyId, fresnelColor);
            propertyBlock.SetColor(ParallaxColorPropertyId, parallaxColor);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private static void InitializeLerpStateFromTarget(CrystalColorLerpState state, CrystalColorPair targetColors)
    {
        state.fromBase = targetColors.baseColor;
        state.fromFresnel = targetColors.fresnelColor;
        state.fromParallax = targetColors.parallaxColor;
        state.toBase = targetColors.baseColor;
        state.toFresnel = targetColors.fresnelColor;
        state.toParallax = targetColors.parallaxColor;
        state.currentBase = targetColors.baseColor;
        state.currentFresnel = targetColors.fresnelColor;
        state.currentParallax = targetColors.parallaxColor;
        state.elapsed = MaxColorLerpDuration;
    }

    private void SyncLerpStateToColors(GameObject crystal, CrystalColorPair colors)
    {
        int crystalId = crystal.GetInstanceID();
        if (!crystalColorStates.TryGetValue(crystalId, out CrystalColorLerpState state))
        {
            state = new CrystalColorLerpState { crystal = crystal };
            crystalColorStates[crystalId] = state;
        }

        state.crystal = crystal;
        InitializeLerpStateFromTarget(state, colors);
    }

    private bool IsLerpingToTarget(CrystalColorLerpState state, CrystalColorPair targetColors)
    {
        return ColorsApproximatelyEqual(state.toBase, targetColors.baseColor)
            && ColorsApproximatelyEqual(state.toFresnel, targetColors.fresnelColor)
            && ColorsApproximatelyEqual(state.toParallax, targetColors.parallaxColor);
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r)
            && Mathf.Approximately(a.g, b.g)
            && Mathf.Approximately(a.b, b.b)
            && Mathf.Approximately(a.a, b.a);
    }

    private bool IsCrystalUnderThisController(GameObject crystal)
    {
        return crystal.transform.IsChildOf(transform);
    }

    private float GetDefaultLightIntensity(int colorIndex)
    {
        EnsureDefaultLightIntensityCount();
        int i = Mathf.Clamp(colorIndex, 0, CrystalColorTypeCount - 1);
        return defaultLightIntensityPerType[i];
    }

    private void EnsureDefaultLightIntensityCount()
    {
        if (defaultLightIntensityPerType == null || defaultLightIntensityPerType.Length != CrystalColorTypeCount)
            defaultLightIntensityPerType = new float[CrystalColorTypeCount];
    }

    private void EnsureCrystalSetCount()
    {
        if (crystalSets == null || crystalSets.Length != CrystalColorTypeCount)
            crystalSets = new CrystalTypeList[CrystalColorTypeCount];

        for (int i = 0; i < CrystalColorTypeCount; i++)
        {
            if (crystalSets[i] == null)
                crystalSets[i] = new CrystalTypeList();

            if (crystalSets[i].brightColors == null)
                crystalSets[i].brightColors = new CrystalColorPair();

            if (crystalSets[i].darkColors == null)
                crystalSets[i].darkColors = new CrystalColorPair();
        }
    }
}
