using UnityEngine;

/// <summary>
/// Attach to the door object in the world.
/// Assign this to PowerReceiverBase on the ReceiverCube.
/// Holds its own color and power requirements.
/// Changes material color based on power state and logs received power info.
/// </summary>
public class DoorReceiver : MonoBehaviour
{
    [Header("REQUIREMENTS")]
    public Color requiredColor = Color.red;
    public int minimumPower = 1;

    [Header("REFERENCES")]
    public Renderer doorRenderer;

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");

    /// <summary>Called by PowerReceiverBase when power is connected.</summary>
    public void PassColorAndPower(Color incomingColor, int incomingPower)
    {
        bool colorMatches = ColorsMatch(incomingColor, requiredColor);
        bool powerSufficient = incomingPower >= minimumPower;

        if (!colorMatches)
        {
            Debug.Log($"[DoorReceiver] Power connected - not the correct type of power provided.");
            return;
        }

        if (!powerSufficient)
        {
            int missing = minimumPower - incomingPower;
            Debug.Log($"[DoorReceiver] Power connected - missing {missing} MW.");
            return;
        }

        if (doorRenderer != null)
        {
            doorRenderer.material.SetColor(BaseColorProperty, incomingColor);
        }

        Debug.Log($"[DoorReceiver] Requirements met | Color: {incomingColor} | Power received: {incomingPower} | Door requires: {minimumPower}");
    }

    private bool ColorsMatch(Color a, Color b)
    {
        const float tolerance = 0.05f;
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }

    /// <summary>Called by PowerReceiverBase when power is disconnected.</summary>
    public void OnPowerLost()
    {
        if (doorRenderer != null)
        {
            doorRenderer.material.SetColor(BaseColorProperty, Color.white);
        }

        Debug.Log("[DoorReceiver] Power lost - material reset to white.");
    }
}
