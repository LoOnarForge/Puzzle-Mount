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
    [Tooltip("Darkest the lit surface can go. Raise if unpowered lines look too dark. Test on unpowered lines.")]
    [Range(0f, 1f)]
    [SerializeField] private float minBrightness = 0.35f;
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
    private Color currentEmissionColor = Color.black;

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
        currentEmissionColor = Color.black;

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
            currentEmissionColor = Color.black;
            return;
        }

        if (ColorsApproximatelyEqual(currentEmissionColor, Color.black))
        {
            currentBaseColor = neutralColor;
            return;
        }

        currentEmissionColor = GetEmissionForBaseColor(currentBaseColor);
    }

    // Fades the line sprite to the given power color over the supplied duration.
    public void PowerUp(Color color, float duration)
    {
        StopBrightenSpriteLineCoroutine();
        StopColorCoroutines();
        colorPowerLineCoroutine = StartCoroutine(ColorPowerLine(color, duration));
    }

    // Fades the line sprite back to neutral (white, or black if blocked) over the supplied duration.
    public void PowerDown(float duration)
    {
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
        Color startingEmission = currentEmissionColor;
        Color targetBase = isBlocked ? blockedColor : targetColor;
        Color targetEmission = isBlocked ? Color.black : GetEmissionForBaseColor(targetColor);
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            ApplyColors(Color.Lerp(startingBase, targetBase, t), Color.Lerp(startingEmission, targetEmission, t));
            yield return null;
        }

        ApplyColors(targetBase, targetEmission);
        colorPowerLineCoroutine = null;
    }

    private IEnumerator DecolorPowerLine(float duration)
    {
        Color startingBase = currentBaseColor;
        Color startingEmission = currentEmissionColor;
        Color targetBase = isBlocked ? blockedColor : neutralColor;
        Color targetEmission = Color.black;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            ApplyColors(Color.Lerp(startingBase, targetBase, t), Color.Lerp(startingEmission, targetEmission, t));
            yield return null;
        }

        ApplyColors(targetBase, targetEmission);
        decolorPowerLineCoroutine = null;
    }

    private IEnumerator DarkenSpriteLine()
    {
        while (!ColorsApproximatelyEqual(currentBaseColor, blockedColor))
        {
            ApplyColors(
                Color.Lerp(currentBaseColor, blockedColor, Time.deltaTime * darkeningSpeed),
                Color.Lerp(currentEmissionColor, Color.black, Time.deltaTime * darkeningSpeed));
            yield return null;
        }

        ApplyColors(blockedColor, Color.black);
        darkenSpriteLineCoroutine = null;
    }

    private IEnumerator BrightenSpriteLine()
    {
        while (!ColorsApproximatelyEqual(currentBaseColor, neutralColor))
        {
            ApplyColors(
                Color.Lerp(currentBaseColor, neutralColor, Time.deltaTime * darkeningSpeed),
                Color.Lerp(currentEmissionColor, Color.black, Time.deltaTime * darkeningSpeed));
            yield return null;
        }

        ApplyColors(neutralColor, Color.black);
        brightenSpriteLineCoroutine = null;
    }

    private Color GetEmissionForBaseColor(Color baseColor)
    {
        if (ColorsApproximatelyEqual(baseColor, neutralColor) || ColorsApproximatelyEqual(baseColor, blockedColor))
            return Color.black;

        return baseColor * emissionStrength * poweredEmissionMultiplier;
    }

    private void ApplyColors(Color baseColor, Color emissionColor)
    {
        currentBaseColor = baseColor;
        currentEmissionColor = emissionColor;
        ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        lineSprite.GetPropertyBlock(propBlock);
        propBlock.SetColor(BaseColorId, currentBaseColor);
        propBlock.SetColor(EmissionColorId, currentEmissionColor);
        propBlock.SetFloat(EmissionStrengthId, emissionStrength);
        propBlock.SetFloat(MinBrightnessId, minBrightness);
        propBlock.SetFloat(LightInfluenceId, lightInfluence);
        lineSprite.SetPropertyBlock(propBlock);
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f
            && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
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
