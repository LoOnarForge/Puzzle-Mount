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

    public float EyeTransitionDuration => eyeTransitionDuration;

    [SerializeField] [Range(EyeFlashDurationMin, EyeFlashDurationMax)] private float eyeFlashDuration = 0.075f;

    [SerializeField] private ParticleSystem transitionCompleteParticleSystem;
    [SerializeField] private bool isPSActive = true;
    [SerializeField] private bool isLaserSkull;

    // Absorbed into defaultColor (HDR) — kept so existing prefab intensity values migrate once.
    [SerializeField] [HideInInspector] private float emissionIntensity = 1f;

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (emissionRenderer == null)
            emissionRenderer = GetComponentInChildren<Renderer>(true);

        MigrateLegacyIntensity();
        if (isPSActive)
        {
            if (isLaserSkull)
                EnsureLaserParticlePlaying();
            else
                SetTransitionCompleteParticleActive(false);
        }

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
            emissionRenderer = GetComponentInChildren<Renderer>(true);

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

    // Open → closed — laser beam blocked (PressurePlate uses closed ↔ open on the plate).
    public IEnumerator TransitionOpenedToClosed()
    {
        yield return RunEmissionTransition(openedColor, closedColor);
    }

    public IEnumerator TransitionFadeToOff(float duration)
    {
        yield return RunEmissionTransitionWithDuration(openedColor, Color.black, duration);
    }

    private IEnumerator RunEmissionTransitionWithDuration(Color startEmission, Color endEmission, float duration)
    {
        if (emissionRenderer == null)
            yield break;

        if (duration <= 0f)
        {
            ApplyEmission(endEmission);
            yield break;
        }

        ApplyEmission(startEmission);

        float fadeStartTime = Time.unscaledTime;

        do
        {
            float elapsed = Time.unscaledTime - fadeStartTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyEmission(LerpEmissionSmooth(startEmission, endEmission, t));
            yield return null;
        }
        while (Time.unscaledTime - fadeStartTime < duration);

        ApplyEmission(endEmission);
    }

    private IEnumerator RunEmissionTransition(Color startEmission, Color endEmission)
    {
        if (emissionRenderer == null)
            yield break;

        if (isPSActive && !isLaserSkull)
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
            ApplyEmission(LerpEmissionSmooth(startEmission, endEmission, t));
            yield return null;
        }

        ApplyEmission(endEmission);
        EnableTransitionCompleteParticle();
    }

    private Color LerpEmissionSmooth(Color from, Color to, float t)
    {
        t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
        return Color.Lerp(from, to, t);
    }

    // Sets eye emission immediately (no transition) — e.g. laser skull initial state.
    public void ApplyEyeStateImmediate(bool closed)
    {
        ApplyEmission(closed ? closedColor : openedColor);
    }

    public void ApplyLaserParticleColor(Color color)
    {
        if (!isLaserSkull || !isPSActive || transitionCompleteParticleSystem == null)
            return;

        EnsureLaserParticlePlaying();

        ParticleSystem.MainModule main = transitionCompleteParticleSystem.main;
        main.startColor = color;
    }

    public void StopLaserParticles()
    {
        if (!isLaserSkull || transitionCompleteParticleSystem == null)
            return;

        transitionCompleteParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetTransitionCompleteParticleActive(false);
    }

    private void EnsureLaserParticlePlaying()
    {
        if (!isPSActive || transitionCompleteParticleSystem == null)
            return;

        SetTransitionCompleteParticleActive(true);

        if (!transitionCompleteParticleSystem.isPlaying)
            transitionCompleteParticleSystem.Play(true);
    }

    private void EnableTransitionCompleteParticle()
    {
        if (isLaserSkull || !isPSActive || transitionCompleteParticleSystem == null)
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
