using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class RuntimeGraphicsSettings
{
    public const int ResolutionPresetCount = 5;
    public const float ShadowDistanceMin = 0f;
    public const float ShadowDistanceMax = 120f;
    public const float RenderScaleMin = 0.25f;
    public const float RenderScaleMax = 1f;

    private static readonly (int width, int height, string label)[] ResolutionPresets =
    {
        (0, 0, "Desktop"),
        (1920, 1080, "1920×1080"),
        (1600, 900, "1600×900"),
        (1280, 720, "1280×720"),
        (960, 540, "960×540"),
    };

    private static int desktopWidth;
    private static int desktopHeight;
    private static bool initialized;
    private static UniversalRenderPipelineAsset urpAsset;
    private static float defaultShadowDistance = 50f;
    private static float defaultRenderScale = 1f;

    public static bool ShadowsEnabled { get; private set; } = true;
    public static int ActiveResolutionPreset { get; private set; }
    public static float ShadowDistance { get; private set; } = 50f;
    public static float RenderScale { get; private set; } = 1f;

    public static bool ResolutionPresetsWorkInEditor =>
#if UNITY_EDITOR
        false;
#else
        true;
#endif

    public static string GetResolutionPresetLabel(int index)
    {
        if (index < 0 || index >= ResolutionPresetCount)
            return string.Empty;

        if (index == 0 && initialized)
            return $"{ResolutionPresets[0].label} ({desktopWidth}×{desktopHeight})";

        return ResolutionPresets[index].label;
    }

    public static void EnsureInitialized()
    {
        if (initialized)
            return;

        desktopWidth = Display.main.systemWidth;
        desktopHeight = Display.main.systemHeight;
        if (desktopWidth <= 0 || desktopHeight <= 0)
        {
            Resolution res = Screen.currentResolution;
            desktopWidth = res.width;
            desktopHeight = res.height;
        }

        CachePipelineAsset();
        if (urpAsset != null)
        {
            defaultShadowDistance = urpAsset.shadowDistance;
            defaultRenderScale = urpAsset.renderScale;
            ShadowDistance = defaultShadowDistance;
            RenderScale = defaultRenderScale;
        }

        initialized = true;
        ApplyAll();
    }

    public static void SetShadowsEnabled(bool enabled)
    {
        ShadowsEnabled = enabled;
        ApplyShadows();
    }

    public static void SetShadowDistance(float distance)
    {
        ShadowDistance = Mathf.Clamp(distance, ShadowDistanceMin, ShadowDistanceMax);
        ApplyShadowDistance();
    }

    public static void SetRenderScale(float scale)
    {
        RenderScale = Mathf.Clamp(scale, RenderScaleMin, RenderScaleMax);
        ApplyRenderScale();
    }

    public static void SetResolutionPreset(int presetIndex)
    {
        if (presetIndex < 0 || presetIndex >= ResolutionPresetCount)
            return;

        ActiveResolutionPreset = presetIndex;
        ApplyResolution();
    }

    public static void ApplyAll()
    {
        ApplyShadows();
        ApplyShadowDistance();
        ApplyRenderScale();
        ApplyResolution();
    }

    private static void CachePipelineAsset()
    {
        if (urpAsset != null)
            return;

        urpAsset = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        if (urpAsset == null)
            urpAsset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
    }

    private static void ApplyShadows()
    {
        QualitySettings.shadows = ShadowsEnabled ? UnityEngine.ShadowQuality.All : UnityEngine.ShadowQuality.Disable;

        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (Light light in lights)
        {
            if (light == null)
                continue;

            light.shadows = ShadowsEnabled ? LightShadows.Soft : LightShadows.None;
        }

        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        foreach (Camera camera in cameras)
        {
            if (camera == null || !camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                continue;

            cameraData.renderShadows = ShadowsEnabled;
        }
    }

    private static void ApplyShadowDistance()
    {
        CachePipelineAsset();
        if (urpAsset != null)
            urpAsset.shadowDistance = ShadowDistance;
    }

    private static void ApplyRenderScale()
    {
        CachePipelineAsset();
        if (urpAsset != null)
            urpAsset.renderScale = RenderScale;
    }

    private static void ApplyResolution()
    {
#if UNITY_EDITOR
        return;
#endif
        int width = ActiveResolutionPreset == 0 ? desktopWidth : ResolutionPresets[ActiveResolutionPreset].width;
        int height = ActiveResolutionPreset == 0 ? desktopHeight : ResolutionPresets[ActiveResolutionPreset].height;
        Screen.SetResolution(width, height, Screen.fullScreenMode);
    }
}
