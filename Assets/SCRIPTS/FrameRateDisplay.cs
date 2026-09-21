using UnityEngine;

public class FrameRateDisplay : MonoBehaviour
{
    [SerializeField] private bool capAt60Fps;
    [SerializeField] private bool capAt30Fps;
    [SerializeField] private bool capAt15Fps;

    private const float UpdateInterval = 0.1f;
    private const int UncappedFrameRate = -1;
    private const int Cap60Fps = 60;
    private const int Cap30Fps = 30;
    private const int Cap15Fps = 15;

    private float intervalTimer;
    private int framesInInterval;
    private string displayText = "0 FPS";
    private GUIStyle labelStyle;

    private void Awake()
    {
        ApplyFrameRateCap();
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

        if (Application.targetFrameRate != GetTargetFrameRate())
            ApplyFrameRateCap();
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
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
        }

        GUI.Label(new Rect(10f, 10f, 220f, 50f), displayText, labelStyle);
    }

    private int GetTargetFrameRate()
    {
        if (capAt60Fps)
            return Cap60Fps;
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
