using System.Collections;
using UnityEngine;

public class RuneTorch : MonoBehaviour
{
    private const int SocketIndex = 0;
    private const float FirstStepIntensityBonus = 0.5f;
    private const float AdditionalIntensityBonusPerMw = 0.25f;
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
    [SerializeField] private int maxPower = 4;
    [SerializeField][Range(0f, 5f)] private float fadeDuration = 0.2f;

    [SerializeField] private int allocatedMw;
    [SerializeField] private PowerSource poweringSource;
    [SerializeField] private bool isLit;
    [SerializeField] private float displayedIntensity;

    private float baseRange;
    private float baseIntensity;
    private DevicePowerSocket devicePowerSocket;
    private LinePort socketPort;
    private MaterialPropertyBlock spritePropBlock;
    private Color deliveredPowerColor = Color.white;
    private Coroutine fadeCoroutine;
    private float displayedSpriteBlend;
    private int displayedMw;

    private void Awake()
    {
        Debug.Assert(powerSocketObject != null, $"{nameof(RuneTorch)} on {name} requires a power socket object.", this);
        Debug.Assert(pointLight != null, $"{nameof(RuneTorch)} on {name} requires a point light.", this);
        Debug.Assert(maxPower >= 1, $"{nameof(RuneTorch)} on {name} requires max power of at least 1.", this);

        devicePowerSocket = powerSocketObject.GetComponent<DevicePowerSocket>();
        socketPort = powerSocketObject.GetComponent<LinePort>();
        Debug.Assert(devicePowerSocket != null, $"{nameof(RuneTorch)} on {name} requires a {nameof(DevicePowerSocket)} on the power socket object.", this);
        Debug.Assert(socketPort != null, $"{nameof(RuneTorch)} on {name} requires a {nameof(LinePort)} on the power socket object.", this);

        baseRange = pointLight.range;
        baseIntensity = pointLight.intensity;

        Sprite torchSprite = spriteLibrary.GetRandomSprite();
        topFaceSprite.sprite = torchSprite;
        topFaceSprite.size = torchSprite.bounds.size * 1.98f;
        topFaceSprite.gameObject.SetActive(true);
        spritePropBlock = new MaterialPropertyBlock();

        InitialSocketConfiguration();
        RefreshLightState(true);
    }

    private void LateUpdate()
    {
        displayedIntensity = pointLight.intensity;
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

        devicePowerSocket.InitialSocketConfiguration(SocketIndex, 0, maxPower);
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

    private static Color ResolveDeliveredColor(PowerSource source)
    {
        if (source == null || ColorManager.Instance == null)
            return Color.white;

        return ColorManager.Instance.GetColor(source.ColorIndex);
    }

    private void RefreshLightState(bool instant = false)
    {
        isLit = allocatedMw >= 1;

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (instant || fadeDuration <= 0f)
        {
            ApplyLightState(allocatedMw, deliveredPowerColor, isLit ? 1f : 0f);
            return;
        }

        fadeCoroutine = StartCoroutine(FadeLightState());
    }

    private IEnumerator FadeLightState()
    {
        int endMw = allocatedMw;
        float startIntensity = pointLight.enabled ? pointLight.intensity : 0f;
        float startRange = pointLight.enabled ? pointLight.range : 0f;
        float endIntensity = endMw >= 1 ? baseIntensity * GetIntensityMultiplier(endMw) : 0f;
        float endRange = endMw >= 1 ? baseRange * GetRangeMultiplier(endMw) : 0f;
        Color fadeColor = endMw >= 1 ? deliveredPowerColor : pointLight.color;
        float startEmissionBlend = displayedSpriteBlend;
        float endEmissionBlend = endMw >= 1 ? 1f : 0f;
        bool fadeSpriteEmission = displayedMw < 1 || endMw < 1;

        if (endMw >= 1)
        {
            pointLight.enabled = true;
            pointLight.color = deliveredPowerColor;
            if (fadeSpriteEmission)
                ApplySpriteVisual(deliveredPowerColor, startEmissionBlend);
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);

            pointLight.intensity = Mathf.Lerp(startIntensity, endIntensity, t);
            pointLight.range = Mathf.Lerp(startRange, endRange, t);

            if (fadeSpriteEmission)
                ApplySpriteVisual(fadeColor, Mathf.Lerp(startEmissionBlend, endEmissionBlend, t));

            yield return null;
        }

        ApplyLightState(endMw, endMw >= 1 ? deliveredPowerColor : fadeColor, endEmissionBlend);
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
        pointLight.range = baseRange * GetRangeMultiplier(mw);
        pointLight.intensity = baseIntensity * GetIntensityMultiplier(mw);
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

    private static float GetRangeMultiplier(int mw)
    {
        return mw;
    }

    private static float GetIntensityMultiplier(int mw)
    {
        if (mw <= 1)
            return 1f;

        return 1f + FirstStepIntensityBonus + (mw - 2) * AdditionalIntensityBonusPerMw;
    }
}
