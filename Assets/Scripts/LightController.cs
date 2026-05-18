using UnityEngine;

/// <summary>
/// Attach to a GameObject with a Light component.
/// Controlled by a LeverReceiver. Toggles the light on and off.
/// Takes on the color of the connected power source.
/// </summary>
public class LightController : LeverTarget
{
    [Header("REFERENCES")]
    public Light targetLight;

    private bool isOn = false;
    private Color powerColor = Color.white;

    private void Awake()
    {
        if (targetLight != null)
            targetLight.enabled = false;
    }

    /// <summary>Receives the power color from the lever.</summary>
    public override void SetPowerColor(Color color)
    {
        powerColor = color;
    }

    /// <summary>Flips the light on or off and applies the power color.</summary>
    public override void Toggle()
    {
        isOn = !isOn;

        if (targetLight == null) return;

        targetLight.enabled = isOn;
        targetLight.color = powerColor;
    }
}
