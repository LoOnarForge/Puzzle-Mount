using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class SkullBehaviour : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private const float EyeFadeOutEnd = 0.52f;
    private const float EyeFadeInStart = 0.48f;
    private const float EyeTransitionDurationMin = 0f;
    private const float EyeTransitionDurationMax = 1f;
    private const float EyeFlashDurationMin = 0f;
    private const float EyeFlashDurationMax = 1f;
    private const float EyeFlashBoost = 2.5f;

    [SerializeField] private Renderer emissionRenderer;

    [ColorUsage(true, true)]
    [FormerlySerializedAs("emissionColor")]
    [SerializeField] private Color defaultColor = Color.white;

    [ColorUsage(true, true)]
    [FormerlySerializedAs("openColor")]
    [SerializeField] private Color openedColor = Color.white;

    [ColorUsage(true, true)]
    [SerializeField] private Color closedColor = Color.white;

    [SerializeField] [Range(EyeTransitionDurationMin, EyeTransitionDurationMax)] private float eyeTransitionDuration = 0.25f;

    [SerializeField] [Range(EyeFlashDurationMin, EyeFlashDurationMax)] private float eyeFlashDuration = 0.075f;

    [SerializeField] private ParticleSystem transitionCompleteParticleSystem;
    [SerializeField] private bool isPSActive = true;

    // Absorbed into defaultColor (HDR) — kept so existing prefab intensity values migrate once.
    [SerializeField] [HideInInspector] private float emissionIntensity = 1f;

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (emissionRenderer == null)
            emissionRenderer = GetComponent<Renderer>();

        MigrateLegacyIntensity();
        if (isPSActive)
            SetTransitionCompleteParticleActive(false);
        ApplyEmission(defaultColor);
    }

    private void Start()
    {
        ApplyEmission(defaultColor);
    }

    private void OnValidate()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        if (emissionRenderer == null)
            emissionRenderer = GetComponent<Renderer>();

        MigrateLegacyIntensity();

        if (!Application.isPlaying)
            ApplyEmission(defaultColor);
    }

    // Default (idle) → closed — same timing/flash as the former button eye press transition.
    public IEnumerator TransitionDefaultToClosed()
    {
        yield return RunEmissionTransition(defaultColor, closedColor);
    }

    // Closed → open — same transition logic, different endpoints.
    public IEnumerator TransitionClosedToOpen()
    {
        yield return RunEmissionTransition(closedColor, openedColor);
    }

    private IEnumerator RunEmissionTransition(Color startEmission, Color endEmission)
    {
        if (emissionRenderer == null)
            yield break;

        if (isPSActive)
            SetTransitionCompleteParticleActive(false);

        if (eyeTransitionDuration <= 0f)
        {
            ApplyEmission(endEmission);
            EnableTransitionCompleteParticle();
            yield break;
        }

        ApplyEmission(startEmission);

        float elapsed = 0f;
        while (elapsed < eyeTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / eyeTransitionDuration);
            ApplyEmission(LerpEmissionWithFlash(startEmission, endEmission, t));
            yield return null;
        }

        ApplyEmission(endEmission);
        EnableTransitionCompleteParticle();
    }

    // Only A or B at a time (no mixed hue). Flash is extra brightness on that same color at the handoff — never additive white/RGB mix.
    private Color LerpEmissionWithFlash(Color from, Color to, float t)
    {
        t = Mathf.Clamp01(t);

        float flashPhase = Mathf.Clamp01((t - (0.5f - eyeFlashDuration)) / (eyeFlashDuration * 2f));
        float flashMul = 1f + EyeFlashBoost * Mathf.Sin(flashPhase * Mathf.PI);

        Color result;
        if (t < 0.5f)
        {
            float fade = 1f - Mathf.SmoothStep(0f, EyeFadeOutEnd, t);
            result = from * (fade * flashMul);
        }
        else
        {
            float fade = Mathf.SmoothStep(EyeFadeInStart, 1f, t);
            result = to * (fade * flashMul);
        }

        result.a = Mathf.Lerp(from.a, to.a, t);
        return result;
    }

    private void EnableTransitionCompleteParticle()
    {
        if (!isPSActive || transitionCompleteParticleSystem == null)
            return;

        SetTransitionCompleteParticleActive(true);
        transitionCompleteParticleSystem.Play();
    }

    private void SetTransitionCompleteParticleActive(bool active)
    {
        if (transitionCompleteParticleSystem == null)
            return;

        transitionCompleteParticleSystem.gameObject.SetActive(active);
    }

    private void MigrateLegacyIntensity()
    {
        if (Mathf.Approximately(emissionIntensity, 1f))
            return;

        defaultColor *= emissionIntensity;
        emissionIntensity = 1f;
    }

    private void ApplyEmission(Color color)
    {
        if (emissionRenderer == null)
            return;

        emissionRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(EmissionColorId, color);
        emissionRenderer.SetPropertyBlock(propertyBlock);
    }
}
