using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunodeLine : MonoBehaviour
{
    [SerializeField] private SpriteRenderer lineSprite;

    [Space(20)]
    [Header("STATE:")]
    [SerializeField] private bool isPowered;
    [SerializeField] private bool isConnected;
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

    public bool IsPowered => isPowered;
    public int PowerIndex => powerIndex;
    public PowerSource PowerSource => powerSource;
    public RunodeLine PoweredByLine => poweredByLine;

    // Set false to log short circuits without halting propagation (testing).
    private const bool HaltAllPropagationOnShortCircuit = false;

    private static bool propagationHalted;

    public static bool IsPropagationHalted => propagationHalted;

    private const float powerColorDuration = 0.07f;
    private const float powerDecolorDuration = 0.07f;
    private float darkeningSpeed = 20f;

    private readonly List<LinePort> linePorts = new List<LinePort>();

    private Coroutine colorPowerLineCoroutine;
    private Coroutine powerUpSequenceCoroutine;
    private Coroutine decolorPowerLineCoroutine;
    private Coroutine depowerSequenceCoroutine;
    private Coroutine darkenSpriteLineCoroutine;
    private Coroutine brightenSpriteLineCoroutine;

    private void Awake()
    {
        propagationHalted = false;

        Debug.Assert(lineSprite != null, $"{nameof(RunodeLine)} on {name} requires a line sprite.", this);
        Debug.Assert(obstructionPort != null, $"{nameof(RunodeLine)} on {name} requires an obstruction port.", this);
        Debug.Assert(upPort != null && rightPort != null && downPort != null && leftPort != null,
            $"{nameof(RunodeLine)} on {name} requires all four line ports.", this);

        linePorts.Add(upPort);
        linePorts.Add(rightPort);
        linePorts.Add(downPort);
        linePorts.Add(leftPort);
    }


    // True when this line may propagate power downstream on its circuit path.
    public bool CanPropagatePower()
    {
        return isConnected;
    }

    public void PowerUpLine(PowerSource source, RunodeLine givingLine, LinePort targetPort, Color color, int index)
    {
        if (propagationHalted)
            return;

        if (isPowered)
        {
            RunodeCube cube = GetComponentInParent<RunodeCube>();
            string cubeName = cube != null ? cube.gameObject.name.ToUpper() : name.ToUpper();
            string existingSourceName = powerSource != null ? powerSource.gameObject.name.ToUpper() : "UNKNOWN";
            string incomingSourceName = source != null ? source.gameObject.name.ToUpper() : "UNKNOWN";

            ReportShortCircuit(existingSourceName, incomingSourceName, cubeName);
            return;
        }

        isPowered = true;
        isConnected = true;
        powerSource = source;
        poweredByLine = givingLine;
        receiverPort = targetPort;
        powerColor = color;
        powerIndex = index;
        
        foreach (LinePort port in linePorts)
            port.SetCanReportConnections(false);

        SetInitialReceiverAndGiverPorts(targetPort);
        StopBrightenSpriteLineCoroutine();
        StopPowerCoroutines();
        colorPowerLineCoroutine = StartCoroutine(ColorPowerLine(color));
        powerUpSequenceCoroutine = StartCoroutine(PowerUpSequence());
    }
    public void PowerDownLine()
    {
        RevokeConnection();
        isPowered = false;
        
        foreach (LinePort port in linePorts)
            port.SetCanReportConnections(false);

        powerSource.RemoveWaitingEntriesForLine(this);
        powerSource.RemoveCircuitMember(this);
        powerSource.ReturnMW();

        receiverPort.SetPortType(PortType.Neutral);
        powerSource = null;
        poweredByLine = null;
        receiverPort = null;
        powerColor = Color.white;
        powerIndex = -1;
        StopPowerCoroutines();
        decolorPowerLineCoroutine = StartCoroutine(DecolorPowerLine());
        depowerSequenceCoroutine = StartCoroutine(DepowerSequence());
    }

    private void ReportShortCircuit(string existingSourceName, string incomingSourceName, string cubeName)
    {
        Debug.Log(
            $"SHORT CIRCUIT DETECTED BETWEEN {existingSourceName} AND {incomingSourceName} ON {cubeName}.",
            this);

        if (HaltAllPropagationOnShortCircuit)
            propagationHalted = true;

        // TriggerShortCircuitFinalEvent();
    }

    private void SetInitialReceiverAndGiverPorts(LinePort targetPort)
    {
        foreach (LinePort port in linePorts)
        {
            if (!port.IsIncluded)
                continue;

            port.SetPortType(port == targetPort ? PortType.Receiver : PortType.Giver);
        }
    }


    public bool NewValidConnection(RunodeLine targetLine, LinePort targetPort, LinePort sourcePort)
    {
        if (propagationHalted)
            return false;

        if (powerSource == null || !isConnected)
            return false;

        if (!powerSource.TakeMW())
        {
            powerSource.AddWaitingEntry(this, targetLine, targetPort, sourcePort, powerIndex + 1);
            return false;
        }

        targetLine.PowerUpLine(powerSource, this, targetPort, powerColor, powerIndex + 1);
        powerSource.AddCircuitMember(targetLine);
        return true;
    }

    // Instantly marks this line and downstream faces as disconnected from the live circuit path.
    public void RevokeConnection()
    {
        if (!isConnected)
            return;

        isConnected = false;

        foreach (LinePort port in linePorts)
        {
            if (port.IsIncluded)
                port.PropagateRevokeToFedLines();
        }
    }

    private void SetAllPortsNeutral()
    {
        foreach (LinePort port in linePorts)
        {
            if (port.IsIncluded)
                port.SetPortType(PortType.Neutral);
        }
    }


    private IEnumerator PowerUpSequence()
    {
        yield return new WaitForSeconds(powerColorDuration);

        foreach (LinePort port in linePorts)
        {
            if (port.IsIncluded)
            {
                port.SetCanReportConnections(true);
                port.ReportValidConnections();
            }
        }

        powerUpSequenceCoroutine = null;
    }

    private IEnumerator DepowerSequence()
    {
        yield return new WaitForSeconds(powerDecolorDuration);

        foreach (LinePort port in linePorts)
        {
            if (port.IsIncluded)
            {
                port.SetCanReportConnections(false);
                port.ReportPowerLossToConnectedPorts();
            }
        }

        SetAllPortsNeutral();
        depowerSequenceCoroutine = null;
    }

    private IEnumerator ColorPowerLine(Color targetColor)
    {
        Color startingColor = lineSprite.color;
        float elapsedTime = 0f;

        while (elapsedTime < powerColorDuration)
        {
            elapsedTime += Time.deltaTime;
            lineSprite.color = Color.Lerp(startingColor, targetColor, elapsedTime / powerColorDuration);
            yield return null;
        }

        lineSprite.color = isFaceBlocked ? Color.black : targetColor;
        colorPowerLineCoroutine = null;
    }

    private IEnumerator DecolorPowerLine()
    {
        Color startingColor = lineSprite.color;
        float elapsedTime = 0f;

        while (elapsedTime < powerDecolorDuration)
        {
            elapsedTime += Time.deltaTime;
            lineSprite.color = Color.Lerp(startingColor, isFaceBlocked ? Color.black : Color.white, elapsedTime / powerDecolorDuration);
            yield return null;
        }

        lineSprite.color = isFaceBlocked ? Color.black : Color.white;
        decolorPowerLineCoroutine = null;
    }

    public void FaceObstructed()
    {
        isFaceBlocked = true;
        StopBrightenSpriteLineCoroutine();
        darkenSpriteLineCoroutine = StartCoroutine(DarkenSpriteLine());
        SetPortBlockedState(true);
    }

    public void FaceCleared()
    {
        isFaceBlocked = false;
        StopDarkenSpriteLineCoroutine();
        brightenSpriteLineCoroutine = StartCoroutine(BrightenSpriteLine());
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
        darkenSpriteLineCoroutine = null;
    }

    private IEnumerator BrightenSpriteLine()
    {
        while (lineSprite.color != Color.white)
        {
            lineSprite.color = Color.Lerp(lineSprite.color, Color.white, Time.deltaTime * darkeningSpeed);
            yield return null;
        }

        lineSprite.color = Color.white;
        brightenSpriteLineCoroutine = null;
    }

    private void StopPowerCoroutines()
    {
        if (colorPowerLineCoroutine != null)
        {
            StopCoroutine(colorPowerLineCoroutine);
            colorPowerLineCoroutine = null;
        }

        if (powerUpSequenceCoroutine != null)
        {
            StopCoroutine(powerUpSequenceCoroutine);
            powerUpSequenceCoroutine = null;
        }

        if (decolorPowerLineCoroutine != null)
        {
            StopCoroutine(decolorPowerLineCoroutine);
            decolorPowerLineCoroutine = null;
        }

        if (depowerSequenceCoroutine != null)
        {
            StopCoroutine(depowerSequenceCoroutine);
            depowerSequenceCoroutine = null;
        }
    }

    private void StopDarkenSpriteLineCoroutine()
    {
        if (darkenSpriteLineCoroutine == null)
            return;

        StopCoroutine(darkenSpriteLineCoroutine);
        darkenSpriteLineCoroutine = null;
    }

    private void StopBrightenSpriteLineCoroutine()
    {
        if (brightenSpriteLineCoroutine == null)
            return;

        StopCoroutine(brightenSpriteLineCoroutine);
        brightenSpriteLineCoroutine = null;
    }

    private void SetPortBlockedState(bool blocked)
    {
        foreach (LinePort port in linePorts)
        {
            port.SetParentFaceBlockedState(blocked);
            port.SetPortColliderEnabled(!blocked);
        }
    }

    public void RefreshFaceAndPortsStates()
    {
        RefreshFaceObstructionState();
        RefreshPortObstructions();
        RefreshPortConnections();

        foreach (LinePort port in linePorts)
        {
            if (port.isActiveAndEnabled)
                port.ReportValidConnections();
        }
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
