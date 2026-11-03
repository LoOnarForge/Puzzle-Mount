using UnityEngine;

public class FrameRateDisplay : MonoBehaviour
{
    [SerializeField] private bool capAt60Fps;
    [SerializeField] private bool capAt40Fps;
    [SerializeField] private bool capAt50Fps;
    [SerializeField] private bool capAt30Fps;
    [SerializeField] private bool capAt15Fps;

    private const float UpdateInterval = 0.1f;
    private const float ResolutionUpdateInterval = 5f;
    private const int FpsFontSize = 36;
    private const int ResolutionFontSize = FpsFontSize * 2 / 3;
    private const float FpsLabelHeight = 50f;
    private const int UncappedFrameRate = -1;
    private const int Cap60Fps = 60;
    private const int Cap40Fps = 40;
    private const int Cap50Fps = 50;
    private const int Cap30Fps = 30;
    private const int Cap15Fps = 15;

    private float intervalTimer;
    private float resolutionTimer;
    private int framesInInterval;
    private string displayText = "0 FPS";
    private string resolutionText = "0 x 0";
    private GUIStyle labelStyle;
    private GUIStyle resolutionLabelStyle;

    private void Awake()
    {
        ApplyFrameRateCap();
        RefreshResolutionText();
    }

    private void Update()
    {
        intervalTimer += Time.unscaledDeltaTime;
        framesInInterval++;

        if (intervalTimer >= UpdateInterval)
        {
            displayText = $"{framesInInterval / intervalTimer:0} FPS";
            intervalTimer = 0f;
            framesInInterval = 0;
        }

        resolutionTimer += Time.unscaledDeltaTime;
        if (resolutionTimer >= ResolutionUpdateInterval)
        {
            RefreshResolutionText();
            resolutionTimer = 0f;
        }

        if (Application.targetFrameRate != GetTargetFrameRate())
            ApplyFrameRateCap();
    }

    private void RefreshResolutionText()
    {
        resolutionText = $"{Screen.width} x {Screen.height}";
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            ApplyFrameRateCap();
    }

    private void OnGUI()
    {
        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = FpsFontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        if (resolutionLabelStyle == null)
        {
            resolutionLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ResolutionFontSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        const float x = 10f;
        const float y = 10f;
        GUI.Label(new Rect(x, y, 220f, FpsLabelHeight), displayText, labelStyle);
        GUI.Label(new Rect(x, y + FpsLabelHeight, 220f, ResolutionFontSize + 8f), resolutionText, resolutionLabelStyle);
    }

    private int GetTargetFrameRate()
    {
        if (capAt60Fps)
            return Cap60Fps;
        if (capAt50Fps)
            return Cap50Fps;
        if (capAt40Fps)
            return Cap40Fps;
        if (capAt30Fps)
            return Cap30Fps;
        if (capAt15Fps)
            return Cap15Fps;
        return UncappedFrameRate;
    }

    private void ApplyFrameRateCap()
    {
        Application.targetFrameRate = GetTargetFrameRate();
    }
}
