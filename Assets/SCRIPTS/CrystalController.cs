using System;
using System.Collections.Generic;
using UnityEngine;

public class CrystalController : MonoBehaviour
{
    public const int CrystalColorTypeCount = 6;

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

    [SerializeField] private CrystalTypeList[] crystalSets = new CrystalTypeList[CrystalColorTypeCount];

    [SerializeField] private int lastColorIndex = -1;
    [SerializeField] private int lastMaxMw;
    [SerializeField] private int lastAvailableMw;

    private static readonly int BaseColorTintPropertyId = Shader.PropertyToID("_BaseColorTint");
    private static readonly int FresnelColorPropertyId = Shader.PropertyToID("_FresnelColor");
    private static readonly int ParallaxColorPropertyId = Shader.PropertyToID("_ParallaxColor");

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
    }

    public static string GetSetHeaderLabel(int setIndex)
    {
        if (setIndex < 0 || setIndex >= CrystalSetHeaderLabels.Length)
            return $"CRYSTAL TYPE {setIndex + 1}";

        return CrystalSetHeaderLabels[setIndex];
    }

    private void OnValidate()
    {
        EnsureCrystalSetCount();

        if (lastColorIndex >= 0)
            ApplyPowerSourceState(lastColorIndex, lastMaxMw, lastAvailableMw);
    }

    // Updates which crystals are active for a Power Source (capacity = maxMw, color picks the list).
    public void ApplyPowerSourceState(int colorIndex, int maxMw, int availableMw)
    {
        EnsureCrystalSetCount();

        lastColorIndex = colorIndex;
        lastMaxMw = Mathf.Max(0, maxMw);
        lastAvailableMw = Mathf.Max(0, availableMw);

        int activeSetIndex = Mathf.Clamp(colorIndex, 0, CrystalColorTypeCount - 1);
        int usedMw = Mathf.Max(0, lastMaxMw - lastAvailableMw);

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
                if (crystal == null)
                    continue;

                bool isEnabled = crystalIndex < enabledCount;
                crystal.SetActive(isEnabled);

                if (!isActiveColorSet || !isEnabled)
                    continue;

                bool useDarkColors = crystalIndex < usedMw;
                CrystalColorPair colors = useDarkColors ? set.darkColors : set.brightColors;
                ApplyColorsToCrystal(crystal, colors);
            }
        }
    }

    private void ApplyColorsToCrystal(GameObject crystal, CrystalColorPair colors)
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (colors == null)
            return;

        Renderer[] renderers = crystal.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorTintPropertyId, colors.baseColor);
            propertyBlock.SetColor(FresnelColorPropertyId, colors.fresnelColor);
            propertyBlock.SetColor(ParallaxColorPropertyId, colors.parallaxColor);
            renderer.SetPropertyBlock(propertyBlock);
        }
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
