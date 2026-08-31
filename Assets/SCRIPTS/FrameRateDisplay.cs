using UnityEngine;

public class FrameRateDisplay : MonoBehaviour
{
    [SerializeField] private bool capAt60Fps;

    private const float UpdateInterval = 0.1f;

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
        return capAt60Fps ? 60 : -1;
    }

    private void ApplyFrameRateCap()
    {
        Application.targetFrameRate = GetTargetFrameRate();
    }
}
