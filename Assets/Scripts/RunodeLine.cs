using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunodeLine : MonoBehaviour
{
    [SerializeField] private SpriteRenderer lineSprite;

    [Space(20)]
    [Header("STATE:")]
    [SerializeField] private bool isPowered;
    [SerializeField] private bool isFaceBlocked;
    [SerializeField] private PowerSource powerSource;
    [SerializeField] private RunodeLine poweredByLine;
    [SerializeField] private LinePort receiverPort;
    [SerializeField] private Color powerColor;
    [SerializeField] private int powerIndex;

    [Space(20)]
    [Header("PORTS:")]
    [SerializeField] private LinePort upPort;
    [SerializeField] private LinePort rightPort;
    [SerializeField] private LinePort downPort;
    [SerializeField] private LinePort leftPort;
    [SerializeField] private ObstructionPort obstructionPort;

    private readonly List<LinePort> linePorts = new List<LinePort>();
    private const float PowerColorDuration = 0.05f;
    private float darkeningSpeed = 15f;

    private void Awake()
    {
        Debug.Assert(lineSprite != null, $"{nameof(RunodeLine)} on {name} requires a line sprite.", this);
        Debug.Assert(obstructionPort != null, $"{nameof(RunodeLine)} on {name} requires an obstruction port.", this);
        Debug.Assert(upPort != null && rightPort != null && downPort != null && leftPort != null,
            $"{nameof(RunodeLine)} on {name} requires all four line ports.", this);

        linePorts.Add(upPort);
        linePorts.Add(rightPort);
        linePorts.Add(downPort);
        linePorts.Add(leftPort);
    }

    public void PowerUpLine(PowerSource source, RunodeLine givingLine, LinePort targetPort, Color color, int index)
    {
        isPowered = true;
        powerSource = source;
        poweredByLine = givingLine;
        receiverPort = targetPort;
        powerColor = color;
        powerIndex = index;
        foreach (LinePort port in linePorts)
            port.SetCanReportConnections(false);

        SetAllPortsType(targetPort, PortType.Receiver);
        StopAllCoroutines();
        StartCoroutine(ColorPowerLine(color));
        StartCoroutine(PowerUpSequence());
    }
    public void PowerDownLine()
    {
        isPowered = false;
        foreach (LinePort port in linePorts)
            port.SetCanReportConnections(false);

        powerSource.ReturnMW();
        powerSource.RemoveCircuitMember(this);
        receiverPort.SetPortType(PortType.Neutral);
        powerSource = null;
        poweredByLine = null;
        receiverPort = null;
        powerColor = Color.white;
        powerIndex = 0;
        StopAllCoroutines();
        StartCoroutine(BrightenSpriteLine());
        StartCoroutine(DepowerSequence());
    }
    public void NewValidConnection(RunodeLine targetLine, LinePort targetPort)
    {
        if (!powerSource.TakeMW())
            return;

        targetLine.PowerUpLine(powerSource, this, targetPort, powerColor, powerIndex + 1);
        powerSource.AddCircuitMember(targetLine);
    }
    public void PowerDownConnectedLine(RunodeLine targetLine)
    {
        targetLine.PowerDownLine();
    }

    private void SetAllPortsType(LinePort targetPort, PortType newType)
    {
        foreach (LinePort port in linePorts)
        {
            if (!port.IsIncluded)
                continue;

            if (newType == PortType.Neutral)
            {
                port.SetPortType(PortType.Neutral);
                continue;
            }

            port.SetPortType(port == targetPort ? PortType.Receiver : PortType.Giver);
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

    private IEnumerator PowerUpSequence()
    {
        yield return new WaitForSeconds(PowerColorDuration);

        foreach (LinePort port in linePorts)
        {
            if (port.IsIncluded)
            {
                port.SetCanReportConnections(true);
                port.ReportValidConnections();
            }
        }
    }

    private IEnumerator DepowerSequence()
    {
        yield return new WaitForSeconds(PowerColorDuration);

        foreach (LinePort port in linePorts)
        {
            if (port.IsIncluded)
            {
                port.SetCanReportConnections(false);
                port.ReportConnectedReceivers();
            }
        }

        SetAllPortsType(null, PortType.Neutral);
    }

    public void FaceObstructed()
    {
        isFaceBlocked = true;
        StopAllCoroutines();
        StartCoroutine(DarkenSpriteLine());
        SetPortBlockedState(true);
    }

    public void FaceCleared()
    {
        isFaceBlocked = false;
        StopAllCoroutines();
        StartCoroutine(BrightenSpriteLine());
        SetPortBlockedState(false);
    }

    private IEnumerator DarkenSpriteLine()
    {
        while (lineSprite.color != Color.black)
        {
            lineSprite.color = Color.Lerp(lineSprite.color, Color.black, Time.deltaTime * darkeningSpeed);
            yield return null;
        }

        lineSprite.color = Color.black;
    }

    private IEnumerator BrightenSpriteLine()
    {
        while (lineSprite.color != Color.white)
        {
            lineSprite.color = Color.Lerp(lineSprite.color, Color.white, Time.deltaTime * darkeningSpeed);
            yield return null;
        }

        lineSprite.color = Color.white;
    }

    private void SetPortBlockedState(bool blocked)
    {
        foreach (LinePort port in linePorts)
        {
            port.SetParentFaceBlockedState(blocked);
            port.SetPortColliderEnabled(!blocked);
        }
    }

    public void RefreshFaceAndPortStates()
    {
        RefreshFaceObstructionState();
        RefreshPortObstructions();
        RefreshPortConnections();
    }

    public void RefreshFaceObstructionState()
    {
        if (!obstructionPort.isActiveAndEnabled)
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
        foreach (LinePort port in linePorts)
        {
            if (port.isActiveAndEnabled)
                port.RefreshPortObstructionState();
        }
    }

    public void RefreshPortConnections()
    {
        foreach (LinePort port in linePorts)
        {
            if (port.isActiveAndEnabled)
                port.RefreshPortConnection();
        }
    }
}
