using UnityEngine;

public class RunodeLine : MonoBehaviour
{
    [Header("Ports (Assign in Inspector)")]
    [SerializeField] private LinePort[] ports;
    [SerializeField] private ObstructionPort obstructionPort;
    [SerializeField] private SpriteRenderer lineSprite;

    [Header("State (Read-Only)")]
    [SerializeField] private bool isPowered;
    public bool IsFaceObstructed => isObstructed;


    [SerializeField] private bool isObstructed;

    private float darkenSpeed = 15f;


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

        lineSprite.color = Color.Lerp(lineSprite.color, Color.white, Time.deltaTime * darkenSpeed);

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
            if (port != null) port.SetFaceBlocked(true);
        }
    }

    private void ActivateLinePorts()
    {
        if (ports == null) return;
        foreach (var port in ports)
        {
            if (port != null) port.SetFaceBlocked(false);
        }
    }

    // Refreshes the current face obstruction state.
    public void RefreshFaceObstructionState()
    {

        if (obstructionPort == null || !obstructionPort.isActiveAndEnabled)
            return;

        obstructionPort.RefreshObstructionState();

        if (isObstructed == obstructionPort.IsObstructed)
            return;

        if (obstructionPort.IsObstructed)
            FaceObstructed();
        else
            FaceCleared();
    }

    public void RefreshAllActivePortObstructions()
    {


        if (ports == null)
            return;

        foreach (LinePort port in ports)
        {
            if (port != null && port.isActiveAndEnabled)
                port.RefreshPortObstructionState();
        }
    }

    // Refreshes every active port on this face using already refreshed obstruction state.
    public void RefreshAllActivePortConnections()
    {

        if (ports == null)
            return;

        foreach (LinePort port in ports)
        {
            if (port != null && port.isActiveAndEnabled)
                port.RefreshPortConnection();
        }
    }

    // Refreshes the face obstruction and then all active port connections.
    public void CheckPortConnections()
    {
        RefreshFaceObstructionState();

        RefreshAllActivePortObstructions();
        RefreshAllActivePortConnections();
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
