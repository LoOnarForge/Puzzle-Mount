using System.Collections;
using UnityEngine;

// Owns the visual sprite color for a single Power Line face: power color fades and obstruction darken/brighten.
// RunodeLine calls into this; it holds no power logic of its own.
public class PowerLineVisuals : MonoBehaviour
{
    [SerializeField] private SpriteRenderer lineSprite;

    [Header("COLORS")]
    [Tooltip("Unpowered line color.")]
    [SerializeField] private Color neutralColor = Color.white;
    [Tooltip("Blocked/obstructed line color.")]
    [SerializeField] private Color blockedColor = Color.black;

    [Header("EMISSION")]
    [Tooltip("Bloom glow intensity multiplier (powered lines only).")]
    [SerializeField] private float emissionStrength = 2f;
    [Tooltip("Extra multiplier on top of emission strength (powered lines only).")]
    [SerializeField] private float poweredEmissionMultiplier = 1f;

    [Header("LIGHTING")]
    [Tooltip("Darkest ambient fill (not spot/point lights). 0 = fully dark until a light hits the line.")]
    [Range(0f, 1f)]
    [SerializeField] private float minBrightness = 0f;
    [Tooltip("0 = flat unlit color. 1 = full scene lighting. Test on unpowered lines.")]
    [Range(0f, 1f)]
    [SerializeField] private float lightInfluence = 1f;

    [Header("TRANSITIONS")]
    [Tooltip("Speed of darken/brighten when face gets blocked or cleared. Only visible during that animation.")]
    [SerializeField] private float darkeningSpeed = 20f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int MinBrightnessId = Shader.PropertyToID("_MinBrightness");
    private static readonly int LightInfluenceId = Shader.PropertyToID("_LightInfluence");

    private MaterialPropertyBlock propBlock;

    private Color currentBaseColor = Color.white;
    private Color activePowerColor;

    private bool isPowered;
    // Mirrors RunodeLine's isFaceBlocked so in-progress fades can keep re-checking it every frame.
    private bool isBlocked;

    private Coroutine colorPowerLineCoroutine;
    private Coroutine decolorPowerLineCoroutine;
    private Coroutine darkenSpriteLineCoroutine;
    private Coroutine brightenSpriteLineCoroutine;

    private void Awake()
    {
        Debug.Assert(lineSprite != null, $"{nameof(PowerLineVisuals)} on {name} requires a line sprite.", this);

        propBlock = new MaterialPropertyBlock();
        currentBaseColor = neutralColor;
        isPowered = false;

        lineSprite.color = Color.white;
        ApplyVisualState();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying || lineSprite == null || propBlock == null)
            return;

        RefreshFromTuningFields();
        ApplyVisualState();
    }

    // Re-applies inspector tuning to the current line state (live tweak in Play mode).
    private void RefreshFromTuningFields()
    {
        if (isBlocked)
        {
            currentBaseColor = blockedColor;
            return;
        }

        if (!isPowered && colorPowerLineCoroutine == null && decolorPowerLineCoroutine == null)
            currentBaseColor = neutralColor;
    }

    // Fades the line sprite to the given power color over the supplied duration.
    public void PowerUp(Color color, float duration)
    {
        activePowerColor = color;
        isPowered = true;
        StopBrightenSpriteLineCoroutine();
        StopColorCoroutines();
        colorPowerLineCoroutine = StartCoroutine(ColorPowerLine(color, duration));
    }

    // Fades the line sprite back to neutral (white, or black if blocked) over the supplied duration.
    public void PowerDown(float duration)
    {
        isPowered = false;
        StopColorCoroutines();
        decolorPowerLineCoroutine = StartCoroutine(DecolorPowerLine(duration));
    }

    // Marks the face as obstructed and starts darkening the line sprite to black.
    public void FaceObstructed()
    {
        isBlocked = true;
        StopBrightenSpriteLineCoroutine();
        darkenSpriteLineCoroutine = StartCoroutine(DarkenSpriteLine());
    }

    // Marks the face as cleared and starts brightening the line sprite back to white.
    public void FaceCleared()
    {
        isBlocked = false;
        StopDarkenSpriteLineCoroutine();
        brightenSpriteLineCoroutine = StartCoroutine(BrightenSpriteLine());
    }

    private IEnumerator ColorPowerLine(Color targetColor, float duration)
    {
        Color startingBase = currentBaseColor;
        Color targetBase = isBlocked ? blockedColor : targetColor;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            ApplyBaseColor(Color.Lerp(startingBase, targetBase, t));
            yield return null;
        }

        ApplyBaseColor(targetBase);
        colorPowerLineCoroutine = null;
    }

    private IEnumerator DecolorPowerLine(float duration)
    {
        Color startingBase = currentBaseColor;
        Color targetBase = isBlocked ? blockedColor : neutralColor;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            ApplyBaseColor(Color.Lerp(startingBase, targetBase, t));
            yield return null;
        }

        ApplyBaseColor(targetBase);
        decolorPowerLineCoroutine = null;
    }

    private IEnumerator DarkenSpriteLine()
    {
        while (!ColorsApproximatelyEqual(currentBaseColor, blockedColor))
        {
            ApplyBaseColor(Color.Lerp(currentBaseColor, blockedColor, Time.deltaTime * darkeningSpeed));
            yield return null;
        }

        ApplyBaseColor(blockedColor);
        darkenSpriteLineCoroutine = null;
    }

    private IEnumerator BrightenSpriteLine()
    {
        while (!ColorsApproximatelyEqual(currentBaseColor, neutralColor))
        {
            ApplyBaseColor(Color.Lerp(currentBaseColor, neutralColor, Time.deltaTime * darkeningSpeed));
            yield return null;
        }

        ApplyBaseColor(neutralColor);
        brightenSpriteLineCoroutine = null;
    }

    private Color GetEmissionForPowerColor(Color powerColor)
    {
        return powerColor * poweredEmissionMultiplier;
    }

    private void ApplyBaseColor(Color baseColor)
    {
        currentBaseColor = baseColor;
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        lineSprite.GetPropertyBlock(propBlock);
        propBlock.SetColor(BaseColorId, currentBaseColor);

        float emissionBlend = GetEmissionBlend();
        if (emissionBlend > 0f)
        {
            propBlock.SetColor(EmissionColorId, GetEmissionForPowerColor(currentBaseColor));
            propBlock.SetFloat(EmissionStrengthId, emissionStrength * emissionBlend);
        }
        else
        {
            propBlock.SetColor(EmissionColorId, Color.black);
            propBlock.SetFloat(EmissionStrengthId, 0f);
        }
        propBlock.SetFloat(MinBrightnessId, minBrightness);
        propBlock.SetFloat(LightInfluenceId, lightInfluence);
        lineSprite.SetPropertyBlock(propBlock);
    }

    private float GetEmissionBlend()
    {
        if (!isPowered || isBlocked)
            return 0f;

        float fromNeutral = MaxChannelDiff(currentBaseColor, neutralColor);
        float targetFromNeutral = MaxChannelDiff(activePowerColor, neutralColor);
        if (targetFromNeutral <= 0.001f)
            return fromNeutral > 0.001f ? 1f : 0f;

        return Mathf.Clamp01(fromNeutral / targetFromNeutral);
    }

    private static float MaxChannelDiff(Color a, Color b)
    {
        return Mathf.Max(
            Mathf.Abs(a.r - b.r),
            Mathf.Abs(a.g - b.g),
            Mathf.Abs(a.b - b.b));
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        return MaxChannelDiff(a, b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
    }

    private void StopColorCoroutines()
    {
        if (colorPowerLineCoroutine != null)
        {
            StopCoroutine(colorPowerLineCoroutine);
            colorPowerLineCoroutine = null;
        }

        if (decolorPowerLineCoroutine != null)
        {
            StopCoroutine(decolorPowerLineCoroutine);
            decolorPowerLineCoroutine = null;
        }
    }

    private void StopDarkenSpriteLineCoroutine()
    {
        if (darkenSpriteLineCoroutine == null)
            return;

        StopCoroutine(darkenSpriteLineCoroutine);
        darkenSpriteLineCoroutine = null;
    }

    private void StopBrightenSpriteLineCoroutine()
    {
        if (brightenSpriteLineCoroutine == null)
            return;

        StopCoroutine(brightenSpriteLineCoroutine);
        brightenSpriteLineCoroutine = null;
    }
}
