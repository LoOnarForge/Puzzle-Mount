using UnityEngine;


/// Attach to a GameObject with a Light component.
/// Controlled by a LeverReceiver. Toggles the light on and off.
/// Takes on the color of the connected power source.

public class LightController : MonoBehaviour, ILeverTarget
{
    [Header("SETTINGS")]
    public bool isOnAtStart = false;

    private Light targetLight;
    private bool isOn;
    private Color powerColor = Color.white;

    private void Awake()
    {
        targetLight = GetComponent<Light>();
        isOn = isOnAtStart;

        if (targetLight != null)
            targetLight.enabled = isOn;
    }

    /// Receives the power color from the lever.
    public void SetPowerColor(Color color)
    {
        powerColor = color;
    }

    /// Flips the light on or off and applies the power color.
    public void Toggle()
    {
        isOn = !isOn;

        if (targetLight == null) return;

        targetLight.enabled = isOn;
        targetLight.color = powerColor;
    }
}
