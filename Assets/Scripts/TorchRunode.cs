using System.Collections;
using UnityEngine;

public class TorchRunode : MonoBehaviour
{
    private const int SocketIndex = 0;
    private const int AnyPowerColor = -1;
    private const float EmissionStrength = 2f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int MinBrightnessId = Shader.PropertyToID("_MinBrightness");
    private static readonly int LightInfluenceId = Shader.PropertyToID("_LightInfluence");

    [SerializeField] private GameObject powerSocketObject;
    [SerializeField] private Light pointLight;
    [SerializeField] private RuneTorchSpriteLibrary spriteLibrary;
    [SerializeField] private SpriteRenderer topFaceSprite;
    [SerializeField] private ObstructionPort topFaceObstructionPort;
    [SerializeField] private int maxPower = 4;
    [SerializeField] private float baseLightRange = 4f;
    [SerializeField] private float baseLightIntensity = 4f;
    [SerializeField] private bool ignorePowerColor;
    [SerializeField][Range(0f, 5f)] private float fadeDuration = 0.2f;
    [SerializeField][Range(0f, 1f)] private float obstructionOffDelay = 0.1f;
    [SerializeField][Range(0f, 1f)] private float powerOnDelay = 0.2f;

    [SerializeField] private int allocatedMw;
    [SerializeField] private PowerSource poweringSource;
    [SerializeField] private bool isLit;
    [SerializeField] private float displayedIntensity;

    private DevicePowerSocket devicePowerSocket;
    private LinePort socketPort;
    private MaterialPropertyBlock spritePropBlock;
    private Color defaultLightColor;
    private Color deliveredPowerColor = Color.white;
    private Coroutine fadeCoroutine;
    private float displayedSpriteBlend;
    private int displayedMw;
    private bool isFadingOff;
    private bool isFadingOn;

    private bool topFaceWasObstructed;
    private Coroutine obstructionOffCoroutine;

    private void Awake()
    {
        Debug.Assert(powerSocketObject != null, $"{nameof(TorchRunode)} on {name} requires a power socket object.", this);
        Debug.Assert(pointLight != null, $"{nameof(TorchRunode)} on {name} requires a point light.", this);
        Debug.Assert(maxPower >= 1, $"{nameof(TorchRunode)} on {name} requires max power of at least 1.", this);

        devicePowerSocket = powerSocketObject.GetComponent<DevicePowerSocket>();
        socketPort = powerSocketObject.GetComponent<LinePort>();
        Debug.Assert(devicePowerSocket != null, $"{nameof(TorchRunode)} on {name} requires a {nameof(DevicePowerSocket)} on the power socket object.", this);
        Debug.Assert(socketPort != null, $"{nameof(TorchRunode)} on {name} requires a {nameof(LinePort)} on the power socket object.", this);

        if (topFaceObstructionPort == null)
            topFaceObstructionPort = GetComponentInChildren<ObstructionPort>();

        defaultLightColor = pointLight.color;

        Sprite torchSprite = spriteLibrary.GetRandomSprite();
        topFaceSprite.sprite = torchSprite;
        topFaceSprite.size = torchSprite.bounds.size * 1.98f;
        topFaceSprite.gameObject.SetActive(true);
        spritePropBlock = new MaterialPropertyBlock();

        InitialSocketConfiguration();
        RefreshTopFaceObstruction();
        RefreshLightState(true);
    }

    private void LateUpdate()
    {
        displayedIntensity = pointLight.intensity;
        RefreshTopFaceObstruction();
        RefreshSocketConnection();
        SyncLightFromSocket();
    }

    // Called by a DevicePowerSocket when its power state changes.
    public void UpdateSocketStateToOwner(int ownerIndex, int newAllocatedMw, PowerSource newPoweringSource)
    {
        if (ownerIndex != SocketIndex)
            return;

        allocatedMw = newAllocatedMw;
        poweringSource = newPoweringSource;
        deliveredPowerColor = ResolveDeliveredColor(newPoweringSource);
        RefreshLightState();
    }

    // Pushes max MW cap to the assigned socket once at startup.
    private void InitialSocketConfiguration()
    {
        if (devicePowerSocket == null)
            return;

        devicePowerSocket.InitialSocketConfiguration(SocketIndex, AnyPowerColor, maxPower);
    }

    // Top-face obstruction only affects torch visuals; power stays on the socket until the giver/path changes.
    private void RefreshTopFaceObstruction()
    {
        if (topFaceObstructionPort == null)
            return;

        topFaceObstructionPort.RefreshObstructionState();

        bool isObstructed = topFaceObstructionPort.IsObstructed;
        if (isObstructed == topFaceWasObstructed)
            return;

        topFaceWasObstructed = isObstructed;

        if (obstructionOffCoroutine != null)
            StopCoroutine(obstructionOffCoroutine);

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
            isFadingOn = false;
        }

        if (isObstructed)
            obstructionOffCoroutine = StartCoroutine(ObstructionOffAfterDelay());
        else
            RefreshLightState();
    }

    private IEnumerator ObstructionOffAfterDelay()
    {
        yield return new WaitForSeconds(obstructionOffDelay);
        if (topFaceObstructionPort != null && topFaceObstructionPort.IsObstructed)
            ApplyLightState(0, Color.white, 0f);
        obstructionOffCoroutine = null;
    }

    // Device socket ports are not refreshed by RunodeLine; detect disconnects here.
    private void RefreshSocketConnection()
    {
        if (socketPort == null)
            return;

        socketPort.RefreshPortObstructionState();
        socketPort.RefreshPortConnection();
    }

    private void SyncLightFromSocket()
    {
        if (devicePowerSocket == null)
            return;

        int socketMw = devicePowerSocket.AllocatedMw;
        if (socketMw == allocatedMw)
            return;

        allocatedMw = socketMw;

        if (allocatedMw <= 0)
            poweringSource = null;

        RefreshLightState();
    }

    private bool IsTopFaceObstructed()
    {
        return topFaceObstructionPort != null && topFaceObstructionPort.IsObstructed;
    }

    private int GetDisplayMw()
    {
        return IsTopFaceObstructed() ? 0 : allocatedMw;
    }

    private void RefreshLightState(bool instant = false)
    {
        int displayMw = GetDisplayMw();
        isLit = displayMw >= 1;

        if (fadeCoroutine != null)
        {
            if (isFadingOff)
            {
                if (allocatedMw >= 1 && poweringSource != null && !IsTopFaceObstructed())
                {
                    StopCoroutine(fadeCoroutine);
                    fadeCoroutine = null;
                    isFadingOff = false;
                }
                else
                {
                    return;
                }
            }
            else if (isFadingOn)
            {
                if (!isLit)
                {
                    StopCoroutine(fadeCoroutine);
                    fadeCoroutine = null;
                    isFadingOn = false;
                }
                else
                {
                    return;
                }
            }
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        bool wasLit = displayedMw >= 1;
        bool depowering = wasLit && !isLit;
        bool poweringOn = displayedMw < 1 && allocatedMw >= 1 && !IsTopFaceObstructed();

        if (instant || fadeDuration <= 0f)
        {
            isFadingOff = false;
            isFadingOn = false;
            ApplyLightState(displayMw, GetLightColor(), isLit ? 1f : 0f);
            return;
        }

        if (depowering)
        {
            isFadingOff = true;
            isFadingOn = false;
            fadeCoroutine = StartCoroutine(FadeLightOff());
            return;
        }

        if (poweringOn)
        {
            isFadingOn = true;
            isFadingOff = false;
            fadeCoroutine = StartCoroutine(FadeLightOn());
            return;
        }

        ApplyLightState(displayMw, GetLightColor(), isLit ? 1f : 0f);
    }

    private static Color ResolveDeliveredColor(PowerSource source)
    {
        if (source == null || ColorManager.Instance == null)
            return Color.white;

        return ColorManager.Instance.GetColor(source.ColorIndex);
    }

    private Color GetLightColor()
    {
        if (ignorePowerColor)
            return defaultLightColor;

        return deliveredPowerColor;
    }

    private IEnumerator FadeLightOn()
    {
        yield return new WaitForSeconds(powerOnDelay);
        if (allocatedMw < 1 || IsTopFaceObstructed())
        {
            isFadingOn = false;
            fadeCoroutine = null;
            yield break;
        }

        int mw = allocatedMw;
        Color lightColor = GetLightColor();
        float startIntensity = 0f;
        float startRange = 0f;
        float endIntensity = GetLightIntensity(mw);
        float endRange = GetLightRange(mw);
        float startEmissionBlend = 0f;
        float endEmissionBlend = 1f;

        pointLight.enabled = true;
        pointLight.color = lightColor;
        ApplySpriteVisual(lightColor, startEmissionBlend);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);

            pointLight.intensity = Mathf.Lerp(startIntensity, endIntensity, t);
            pointLight.range = Mathf.Lerp(startRange, endRange, t);
            ApplySpriteVisual(lightColor, Mathf.Lerp(startEmissionBlend, endEmissionBlend, t));

            yield return null;
        }

        ApplyLightState(mw, lightColor, endEmissionBlend);
        isFadingOn = false;
        fadeCoroutine = null;
    }

    private IEnumerator FadeLightOff()
    {
        float startIntensity = pointLight.intensity;
        float startRange = pointLight.range;
        Color fadeColor = pointLight.color;
        float startEmissionBlend = displayedSpriteBlend;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);

            pointLight.intensity = Mathf.Lerp(startIntensity, 0f, t);
            pointLight.range = Mathf.Lerp(startRange, 0f, t);
            ApplySpriteVisual(fadeColor, Mathf.Lerp(startEmissionBlend, 0f, t));

            yield return null;
        }

        isFadingOff = false;
        ApplyLightState(0, Color.white, 0f);
        fadeCoroutine = null;
    }

    private void ApplyLightState(int mw, Color color, float spriteBlend)
    {
        displayedMw = mw;
        displayedSpriteBlend = spriteBlend;
        isLit = mw >= 1;

        if (!isLit)
        {
            pointLight.enabled = false;
            ApplySpriteVisual(Color.white, 0f);
            return;
        }

        pointLight.enabled = true;
        pointLight.color = color;
        pointLight.range = GetLightRange(mw);
        pointLight.intensity = GetLightIntensity(mw);
        ApplySpriteVisual(color, spriteBlend);
    }

    private void ApplySpriteVisual(Color color, float emissionBlend)
    {
        topFaceSprite.GetPropertyBlock(spritePropBlock);

        if (emissionBlend <= 0f)
        {
            spritePropBlock.SetColor(BaseColorId, Color.white);
            spritePropBlock.SetColor(EmissionColorId, Color.black);
            spritePropBlock.SetFloat(EmissionStrengthId, 0f);
        }
        else
        {
            spritePropBlock.SetColor(BaseColorId, color);
            spritePropBlock.SetColor(EmissionColorId, color);
            spritePropBlock.SetFloat(EmissionStrengthId, EmissionStrength * emissionBlend);
        }

        spritePropBlock.SetFloat(MinBrightnessId, 0f);
        spritePropBlock.SetFloat(LightInfluenceId, 1f);
        topFaceSprite.SetPropertyBlock(spritePropBlock);
        displayedSpriteBlend = emissionBlend;
    }

    private float GetLightRange(int mw)
    {
        return baseLightRange + mw;
    }

    private float GetLightIntensity(int mw)
    {
        return baseLightIntensity + mw;
    }
}
