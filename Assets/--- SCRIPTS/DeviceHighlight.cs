using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class DeviceHighlight : MonoBehaviour
{
    private const string OutlineChildName = "Activator Outline Mesh";

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [ColorUsage(true, true)] [SerializeField] private Color pulseColor = Color.white;
    [Min(0.01f)] [SerializeField] private float pulseFadeIn = 0.1f;
    [Min(0.01f)] [SerializeField] private float pulseFadeOut = 0.45f;

    private Coroutine pulseCoroutine;
    private bool ready;
    private MaterialPropertyBlock block;
    private readonly List<MeshRenderer> renderers = new List<MeshRenderer>();
    private readonly List<Color> restingBaseColors = new List<Color>();
    private readonly List<bool> hasBaseColor = new List<bool>();
    private readonly List<bool> hasEmission = new List<bool>();

    private void Awake()
    {
        block = new MaterialPropertyBlock();
    }

    private void Start()
    {
        StartCoroutine(Init());
    }

    private IEnumerator Init()
    {
        yield return null;
        RemoveLeftoverOutlineChildren();
        BuildRendererLists();
        ready = true;
    }

    // Called when a connection-line pulse reaches this device.
    public void FlashFromConnectionPulse()
    {
        if (!Application.isPlaying)
            return;
        if (pulseCoroutine != null)
            StopCoroutine(pulseCoroutine);
        pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        while (!ready)
            yield return null;
        if (renderers.Count == 0)
        {
            pulseCoroutine = null;
            yield break;
        }

        yield return FadePhase(pulseFadeIn, fadeIn: true);
        yield return FadePhase(pulseFadeOut, fadeIn: false);
        SetIntensity(0f);
        ClearBlocks();
        pulseCoroutine = null;
    }

    private IEnumerator FadePhase(float duration, bool fadeIn)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
            float intensity = fadeIn ? SmoothStep(t) : 1f - SmoothStep(t);
            SetIntensity(intensity);
            yield return null;
        }
        SetIntensity(fadeIn ? 1f : 0f);
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    private void SetIntensity(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);
        for (int i = 0; i < renderers.Count; i++)
        {
            MeshRenderer r = renderers[i];
            if (r == null)
                continue;

            r.GetPropertyBlock(block);
            if (intensity <= 0f)
            {
                if (hasBaseColor[i])
                    block.SetColor(BaseColorId, restingBaseColors[i]);
                if (hasEmission[i])
                    block.SetColor(EmissionColorId, Color.black);
            }
            else
            {
                if (hasBaseColor[i])
                    block.SetColor(BaseColorId, Color.Lerp(restingBaseColors[i], pulseColor, intensity));
                if (hasEmission[i])
                    block.SetColor(EmissionColorId, pulseColor * intensity);
            }
            r.SetPropertyBlock(block);
        }
    }

    private void ClearBlocks()
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
                renderers[i].SetPropertyBlock(null);
        }
    }

    private void BuildRendererLists()
    {
        renderers.Clear();
        restingBaseColors.Clear();
        hasBaseColor.Clear();
        hasEmission.Clear();

        if (!HasDeviceComponentOnRoot())
        {
            Debug.LogWarning($"{nameof(DeviceHighlight)} on '{name}' must sit on the device root (MediumDoor, Bridge, etc.).", this);
            return;
        }

        foreach (MeshRenderer r in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == null || ShouldSkip(r))
                continue;

            Material mat = r.sharedMaterial;
            bool baseColor = mat != null && mat.HasProperty(BaseColorId);
            bool emission = mat != null && mat.HasProperty(EmissionColorId);
            if (!baseColor && !emission)
                continue;

            renderers.Add(r);
            hasBaseColor.Add(baseColor);
            hasEmission.Add(emission);
            restingBaseColors.Add(baseColor ? mat.GetColor(BaseColorId) : Color.white);
        }
    }

    private bool HasDeviceComponentOnRoot()
    {
        return GetComponent<MediumDoor>() != null
            || GetComponent<TrapDoor>() != null
            || GetComponent<Bridge>() != null
            || GetComponent<SlideOutPlatform>() != null
            || GetComponent<PistonHorizontal>() != null
            || GetComponent<PistonVertical>() != null
            || GetComponent<Piston>() != null;
    }

    private void RemoveLeftoverOutlineChildren()
    {
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t != null && (t.name == OutlineChildName || t.name.StartsWith(OutlineChildName)))
                Destroy(t.gameObject);
        }
    }

    private static bool ShouldSkip(MeshRenderer r)
    {
        string n = r.gameObject.name;
        if (n == OutlineChildName || n.StartsWith(OutlineChildName))
            return true;
        if (n.Trim() == "Activator Link End")
            return true;
        return false;
    }
}
