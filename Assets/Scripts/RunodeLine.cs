using System.Collections;
using UnityEngine;

public class RunodeLine : MonoBehaviour
{
    [SerializeField] private SpriteRenderer lineSprite;

    [Space(20)]
    [SerializeField] private LinePort[] ports;
    [SerializeField] private ObstructionPort obstructionPort;
    
    [Space(20)]
    [Header("STATE:")]
    [SerializeField] private bool isPowered;
    [SerializeField] private bool isFaceBlocked;

    [SerializeField] private PowerSource powerSource;
    [SerializeField] private LinePort receiverPort;
    [SerializeField] private Color powerColor;
    [SerializeField] private int powerIndex;

    private const float PowerColorDuration = 0.2f;
    private float darkeningSpeed = 15f;


    // Powers this line from a Power Source.
    public void PowerUp(PowerSource source, LinePort receivingPort, Color color, int index)
    {
        isPowered = true;
        powerSource = source;
        receiverPort = receivingPort;
        powerColor = color;
        powerIndex = index;
        SetPortRoles(receivingPort);
        StopAllCoroutines();
        StartCoroutine(ColorPowerLine(color));
    }

    // Depowers this line after its Receiver connection is lost.
    public void ReceiverConnectionLost()
    {
        isPowered = false;
        powerSource.ReturnMW();
        powerSource.RemoveCircuitMember(this);
        ResetPortRoles();
        powerSource = null;
        receiverPort = null;
        powerColor = Color.white;
        powerIndex = 0;
        StopAllCoroutines();
        StartCoroutine(BrightenSpriteLine());
    }

    private void SetPortRoles(LinePort receivingPort)
    {
        foreach (LinePort port in ports)
        {
            if (!port.IsIncluded)
                continue;

            if (port == receivingPort)
                port.SetPortType(PortType.Receiver);
            else
                port.SetPortType(PortType.Giver);
        }
    }

    private void ResetPortRoles()
    {
        foreach (LinePort port in ports)
        {
            if (port.IsIncluded)
                port.SetPortType(PortType.Neutral);
        }
    }

    private IEnumerator ColorPowerLine(Color targetColor)
    {
        Color startingColor = lineSprite.color;
        float elapsedTime = 0f;

        while (elapsedTime < PowerColorDuration)
        {
            elapsedTime += Time.deltaTime;
            lineSprite.color = Color.Lerp(startingColor, targetColor, elapsedTime / PowerColorDuration);
            yield return null;
        }

        lineSprite.color = targetColor;
    }

    public void FaceObstructed()
    {
        isFaceBlocked = true;
        StopAllCoroutines();
        StartCoroutine(DarkenSpriteLine());
        DeactivateLinePorts();
    }
    public void FaceCleared()
    {
        isFaceBlocked = false;
        StopAllCoroutines();
        StartCoroutine(BrightenSpriteLine());
        ActivateLinePorts();
    }

    private IEnumerator DarkenSpriteLine()
    {
        while (lineSprite != null && lineSprite.color != Color.black)
        {
            lineSprite.color = Color.Lerp(lineSprite.color, Color.black, Time.deltaTime * darkeningSpeed);
            yield return null;
        }

        if (lineSprite != null)
            lineSprite.color = Color.black;
    }
    private IEnumerator BrightenSpriteLine()
    {
        while (lineSprite != null && lineSprite.color != Color.white)
        {
            lineSprite.color = Color.Lerp(lineSprite.color, Color.white, Time.deltaTime * darkeningSpeed);
            yield return null;
        }

        if (lineSprite != null)
            lineSprite.color = Color.white;
    }


    private void DeactivateLinePorts()
    {
        if (ports == null) return;
        foreach (var port in ports)
        {
            if (port != null)
            {
                port.SetParentFaceBlockedState(true);
                port.SetPortColliderEnabled(false);
            }
        }
    }
    private void ActivateLinePorts()
    {
        if (ports == null) return;
        foreach (var port in ports)
        {
            if (port != null)
            {
                port.SetParentFaceBlockedState(false);
                port.SetPortColliderEnabled(true);
            }
        }
    }

    // Refreshes the face obstruction and then all active port connections in order.

    public void RefreshFaceAndPortStates()
    {
        RefreshFaceObstructionState();
        RefreshPortObstructions();
        RefreshPortConnections();
    }

    public void RefreshFaceObstructionState()
    {
        if (obstructionPort == null || !obstructionPort.isActiveAndEnabled)
            return;

        obstructionPort.RefreshObstructionState();

        if (isFaceBlocked == obstructionPort.IsObstructed)
            return;

        if (obstructionPort.IsObstructed)
            FaceObstructed();
        else
            FaceCleared();
    }
    public void RefreshPortObstructions()
    {
        if (ports == null)
            return;

        foreach (LinePort port in ports)
        {
            if (port != null && port.isActiveAndEnabled)
                port.RefreshPortObstructionState();
        }
    }
    public void RefreshPortConnections()
    {

        if (ports == null)
            return;

        foreach (LinePort port in ports)
        {
            if (port != null && port.isActiveAndEnabled)
                port.RefreshPortConnection();
        }
    }



}
