using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to the door object in the world.
/// Assign this to PowerReceiverBase on the ReceiverCube.
/// Holds its own color and power requirements.
/// Changes material color based on power state and logs received power info.
/// Waits for connectionDelay seconds before acting on power to avoid false triggers.
/// </summary>
public class DoorReceiver : MonoBehaviour
{
    [Header("REQUIREMENTS")]
    public int requiredColorIndex;
    public int minimumPower = 1;

    [Header("TIMING")]
    public float connectionDelay = 1f;

    [Header("REFERENCES")]
    public Renderer doorRenderer;

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private Coroutine pendingConnection;

    /// <summary>Called by PowerReceiverBase when power is connected.</summary>
    public void PassColorAndPower(Color incomingColor, int incomingPower)
    {
        bool colorMatches = ColorsMatch(incomingColor, ColorManager.Instance.GetColor(requiredColorIndex));
        bool powerSufficient = incomingPower >= minimumPower;

        if (!colorMatches)
        {
            Debug.Log("[DoorReceiver] Power connected - not the correct type of power provided.");
            return;
        }

        if (!powerSufficient)
        {
            int missing = minimumPower - incomingPower;
            Debug.Log($"[DoorReceiver] Power connected - missing {missing} MW.");
            return;
        }

        if (pendingConnection == null)
        {
            pendingConnection = StartCoroutine(ActivateAfterDelay(incomingColor, incomingPower));
        }
    }

    /// <summary>Called by PowerReceiverBase when power is disconnected.</summary>
    public void OnPowerLost()
    {
        Debug.Log($"[ORDER CHECK] Frame {Time.frameCount} - Door disconnected.");

        if (pendingConnection != null)
        {
            StopCoroutine(pendingConnection);
            pendingConnection = null;
        }

        if (doorRenderer != null)
        {
            doorRenderer.material.SetColor(BaseColorProperty, Color.white);
        }
    }

    private IEnumerator ActivateAfterDelay(Color incomingColor, int incomingPower)
    {
        yield return new WaitForSeconds(connectionDelay);

        if (doorRenderer != null)
        {
            doorRenderer.material.SetColor(BaseColorProperty, incomingColor);
        }

        Debug.Log($"[DoorReceiver] Requirements met | Color: {incomingColor} | Power received: {incomingPower} | Door requires: {minimumPower}");

        pendingConnection = null;
    }

    private bool ColorsMatch(Color a, Color b)
    {
        const float tolerance = 0.05f;
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }
}
