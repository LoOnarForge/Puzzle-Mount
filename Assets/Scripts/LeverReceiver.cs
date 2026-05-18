using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to the lever object in the world.
/// Assign this to PowerReceiverBase on the ReceiverCube.
/// Player clicks to toggle. Calls Toggle() on all targets only when powered with correct color and enough power.
/// </summary>
public class LeverReceiver : MonoBehaviour
{
    [Header("REQUIREMENTS")]
    public int requiredColorIndex;
    public int minimumPower = 1;

    [Header("TARGETS")]
    public List<LeverTarget> targets = new List<LeverTarget>();

    [Header("STATE")]
    public bool isOn = false;
    public bool isPowered = false;

    private bool targetsToggled = false;
    private Color currentPowerColor = Color.white;

    private const float ColorTolerance = 0.05f;

    /// <summary>Called by PowerReceiverBase when power is connected.</summary>
    public void PassColorAndPower(Color incomingColor, int incomingPower)
    {
        bool colorMatches = ColorsMatch(incomingColor, ColorManager.Instance.GetColor(requiredColorIndex));
        bool powerSufficient = incomingPower >= minimumPower;

        if (!colorMatches || !powerSufficient)
        {
            isPowered = false;
            return;
        }

        isPowered = true;
        currentPowerColor = incomingColor;

        foreach (LeverTarget target in targets)
        {
            if (target != null)
                target.SetPowerColor(incomingColor);
        }

        if (isOn && !targetsToggled)
        {
            ToggleAllTargets();
        }
    }

    /// <summary>Called by PowerReceiverBase when power is disconnected.</summary>
    public void OnPowerLost()
    {
        isPowered = false;

        if (targetsToggled)
        {
            ToggleAllTargets();
        }
    }

    private void OnMouseDown()
    {
        isOn = !isOn;

        // TODO: trigger lever visual/animation here

        if (!isPowered) return;

        ToggleAllTargets();
    }

    private void ToggleAllTargets()
    {
        foreach (LeverTarget target in targets)
        {
            if (target != null)
                target.Toggle();
        }

        targetsToggled = !targetsToggled;
    }

    private bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < ColorTolerance &&
               Mathf.Abs(a.g - b.g) < ColorTolerance &&
               Mathf.Abs(a.b - b.b) < ColorTolerance;
    }
}
