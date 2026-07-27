using UnityEngine;

public class RunodeLine : MonoBehaviour
{
    [Header("Ports (Assign in Inspector)")]
    [SerializeField] private LinePort[] ports;
    [SerializeField] private ObstructionPort obstructionPort;
    [SerializeField] private SpriteRenderer lineSprite;

    [Header("State (Read-Only)")]
    [SerializeField] private bool isPowered;
    [SerializeField] private bool isObstructed;

    [Header("Visuals")]
    public float darkenSpeed = 10f;
    public float brightenSpeed = 10f;


    private bool isDarkening;
    private bool isBrightening;

    private void Update()
    {
        if (isDarkening)
            DarkenSpriteLine();

        if (isBrightening)
            BrightenSpriteLine();
    }

    public void FaceObstructed()
    {
        isObstructed = true;
        isBrightening = false;
        isDarkening = true;
        DeactivateLinePorts();
    }

    public void FaceCleared()
    {
        isObstructed = false;
        isDarkening = false;
        isBrightening = true;
        ActivateLinePorts();
    }

    private void DarkenSpriteLine()
    {
        if (lineSprite == null) return;

        lineSprite.color = Color.Lerp(lineSprite.color, Color.black, Time.deltaTime * darkenSpeed);

        if (ColorsApproximatelyEqual(lineSprite.color, Color.black))
        {
            lineSprite.color = Color.black;
            isDarkening = false;
        }
    }

    private void BrightenSpriteLine()
    {
        if (lineSprite == null) return;

        lineSprite.color = Color.Lerp(lineSprite.color, Color.white, Time.deltaTime * brightenSpeed);

        if (ColorsApproximatelyEqual(lineSprite.color, Color.white))
        {
            lineSprite.color = Color.white;
            isBrightening = false;
        }
    }

    private void DeactivateLinePorts()
    {
        if (ports == null) return;
        foreach (var port in ports)
        {
            if (port != null) port.SetPortIsblocked(true);
        }
    }

    private void ActivateLinePorts()
    {
        if (ports == null) return;
        foreach (var port in ports)
        {
            if (port != null) port.SetPortIsblocked(false);
        }
    }

    // Checks every assigned port after the line finishes moving.
    public void CheckPortConnections()
    {
        if (ports == null) return;

        foreach (LinePort port in ports)
        {
            if (port != null && port.isActiveAndEnabled)
                port.RefreshPortState();
        }
    }


    public void OnPortConnected(LinePort selfPort, LinePort otherPort)
    {
    }



    public void OnPortDisconnected(LinePort selfPort, LinePort otherPort)
    {
    }

    public void OnPortObstructed(LinePort selfPort, GameObject obstructionObject)
    {
    }

    public void OnPortUnobstructed(LinePort selfPort)
    {
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f
            && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
    }
}
