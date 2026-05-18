using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Attach to the lever object in the world.
/// Assign this to PowerReceiverBase on the ReceiverCube.
/// Player clicks to toggle. Only fires events when powered with correct color and enough power.
/// </summary>
public class LeverReceiver : MonoBehaviour
{
    [Header("REQUIREMENTS")]
    public int requiredColorIndex;
    public int minimumPower = 1;

    [Header("STATE")]
    public bool isOn = false;
    public bool isPowered = false;

    [Header("EVENTS")]
    public UnityEvent OnSwitchedOn;
    public UnityEvent OnSwitchedOff;

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

        if (isOn)
        {
            OnSwitchedOn?.Invoke();
        }
    }

    /// <summary>Called by PowerReceiverBase when power is disconnected.</summary>
    public void OnPowerLost()
    {
        if (isPowered && isOn)
        {
            OnSwitchedOff?.Invoke();
        }

        isPowered = false;
    }

    private void OnMouseDown()
    {
        isOn = !isOn;

        // TODO: trigger lever visual/animation here

        if (!isPowered) return;

        if (isOn)
        {
            OnSwitchedOn?.Invoke();
        }
        else
        {
            OnSwitchedOff?.Invoke();
        }
    }

    private bool ColorsMatch(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < ColorTolerance &&
               Mathf.Abs(a.g - b.g) < ColorTolerance &&
               Mathf.Abs(a.b - b.b) < ColorTolerance;
    }
}
