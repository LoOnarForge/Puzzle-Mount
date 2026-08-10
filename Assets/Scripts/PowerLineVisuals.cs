using System.Collections;
using UnityEngine;

// Owns the visual sprite color for a single Power Line face: power color fades and obstruction darken/brighten.
// RunodeLine calls into this; it holds no power logic of its own.
public class PowerLineVisuals : MonoBehaviour
{
    [SerializeField] private SpriteRenderer lineSprite;

    private const float DarkeningSpeed = 20f;

    // Mirrors RunodeLine's isFaceBlocked so in-progress fades can keep re-checking it every frame.
    private bool isBlocked;

    private Coroutine colorPowerLineCoroutine;
    private Coroutine decolorPowerLineCoroutine;
    private Coroutine darkenSpriteLineCoroutine;
    private Coroutine brightenSpriteLineCoroutine;

    private void Awake()
    {
        Debug.Assert(lineSprite != null, $"{nameof(PowerLineVisuals)} on {name} requires a line sprite.", this);
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
        Color startingColor = lineSprite.color;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            lineSprite.color = Color.Lerp(startingColor, targetColor, elapsedTime / duration);
            yield return null;
        }

        lineSprite.color = isBlocked ? Color.black : targetColor;
        colorPowerLineCoroutine = null;
    }

    private IEnumerator DecolorPowerLine(float duration)
    {
        Color startingColor = lineSprite.color;
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            lineSprite.color = Color.Lerp(startingColor, isBlocked ? Color.black : Color.white, elapsedTime / duration);
            yield return null;
        }

        lineSprite.color = isBlocked ? Color.black : Color.white;
        decolorPowerLineCoroutine = null;
    }

    private IEnumerator DarkenSpriteLine()
    {
        while (lineSprite.color != Color.black)
        {
            lineSprite.color = Color.Lerp(lineSprite.color, Color.black, Time.deltaTime * DarkeningSpeed);
            yield return null;
        }

        lineSprite.color = Color.black;
        darkenSpriteLineCoroutine = null;
    }

    private IEnumerator BrightenSpriteLine()
    {
        while (lineSprite.color != Color.white)
        {
            lineSprite.color = Color.Lerp(lineSprite.color, Color.white, Time.deltaTime * DarkeningSpeed);
            yield return null;
        }

        lineSprite.color = Color.white;
        brightenSpriteLineCoroutine = null;
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
