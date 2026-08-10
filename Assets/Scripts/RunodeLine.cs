using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RunodeLine : MonoBehaviour
{
    [SerializeField] private PowerLineVisuals lineVisuals;

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


    private const bool HaltAllPropagationOnShortCircuit = false;
    private const int RequiredMwAmount = 1;

    private readonly List<LinePort> linePorts = new List<LinePort>();

    public bool IsPowered => isPowered;
    public int PowerIndex => powerIndex;
    public int AllocatedMw => isPowered ? RequiredMwAmount : 0;
    public int RemainingMwNeeded => isPowered ? 0 : RequiredMwAmount;
    public PowerSource PowerSource => powerSource;
    public RunodeLine PoweredByLine => poweredByLine;


    private static bool propagationHalted;
    public static bool IsPropagationHalted => propagationHalted;

    private float powerPropagationDelay;

    private Coroutine powerUpSequenceCoroutine;
    private Coroutine depowerSequenceCoroutine;

    private void Awake()
    {
        propagationHalted = false;

        Debug.Assert(lineVisuals != null, $"{nameof(RunodeLine)} on {name} requires line visuals.", this);
        Debug.Assert(obstructionPort != null, $"{nameof(RunodeLine)} on {name} requires an obstruction port.", this);
        Debug.Assert(upPort != null && rightPort != null && downPort != null && leftPort != null,
            $"{nameof(RunodeLine)} on {name} requires all four line ports.", this);
        Debug.Assert(GameConfig.Instance != null, $"{nameof(RunodeLine)} on {name} requires a {nameof(GameConfig)} in the scene.", this);

        powerPropagationDelay = GameConfig.Instance.PowerPropagationDelay;

        linePorts.Add(upPort);
        linePorts.Add(rightPort);
        linePorts.Add(downPort);
        linePorts.Add(leftPort);
    }


    public bool IsConnectedToPowerSource()
    {
        return isConnected;
    }

    public void PowerUpLine(PowerSource source, RunodeLine givingLine, LinePort targetPort, Color color, int index)
    {
        if (propagationHalted)
            return;

        if (isPowered)
        {
            ReportShortCircuit(source);
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
        {
            port.SetCanReportConnections(false);
        }

        SetInitialReceiverAndGiverPorts(targetPort);
        StopPropagationCoroutines();

        lineVisuals.PowerUp(color, powerPropagationDelay);
        powerUpSequenceCoroutine = StartCoroutine(PowerUpSequence());
    }

    public void PowerDownLine()
    {
        DisconnectFromPowerSource();
        isPowered = false;

        foreach (LinePort port in linePorts)
        {
            port.SetCanReportConnections(false);
        }

        powerSource.RemoveWaitingEntriesForLine(this);
        powerSource.RemoveCircuitMember(this);
        powerSource.ReturnMW();

        receiverPort.SetPortType(PortType.Neutral);
        powerSource = null;
        poweredByLine = null;
        receiverPort = null;
        powerColor = Color.white;
        powerIndex = -1;
        StopPropagationCoroutines();

        lineVisuals.PowerDown(powerPropagationDelay);
        depowerSequenceCoroutine = StartCoroutine(DepowerSequence());
    }

    // Returns this face's single MW by depowering, then lets its giver re-evaluate propagation.
    public void ReleaseOneMw()
    {
        RunodeLine poweringLine = poweredByLine;

        PowerDownLine();
        poweringLine?.RefreshFaceAndPortsStates();
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

    // Gives power into whatever owns the target port (face or device socket).
    public bool TryPowerConnectedPort(LinePort targetPort, LinePort sourcePort)
    {
        if (propagationHalted)
            return false;

        if (powerSource == null || !isConnected)
            return false;

        return powerSource.GivePowerToConnectedPort(this, targetPort, sourcePort, powerIndex + 1);
    }

    // Clears isConnected and tells downstream faces they lost the Power Source path.
    public void DisconnectFromPowerSource()
    {
        if (!isConnected)
            return;

        isConnected = false;

        foreach (LinePort port in linePorts)
        {
            if (port.IsIncluded)
                port.PropagateDisconnectFromPowerSource();
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
        yield return new WaitForSeconds(powerPropagationDelay);

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
        yield return new WaitForSeconds(powerPropagationDelay);

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
    // Marks the face as obstructed: disables its ports and darkens its line sprite.
    public void FaceObstructed()
    {
        isFaceBlocked = true;
        lineVisuals.FaceObstructed();
        SetPortBlockedState(true);
    }

    // Marks the face as cleared: re-enables its ports and brightens its line sprite back.
    public void FaceCleared()
    {
        isFaceBlocked = false;
        lineVisuals.FaceCleared();
        SetPortBlockedState(false);
    }

    private void StopPropagationCoroutines()
    {
        if (powerUpSequenceCoroutine != null)
        {
            StopCoroutine(powerUpSequenceCoroutine);
            powerUpSequenceCoroutine = null;
        }

        if (depowerSequenceCoroutine != null)
        {
            StopCoroutine(depowerSequenceCoroutine);
            depowerSequenceCoroutine = null;
        }
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
        if (isFaceBlocked && isConnected)
            DisconnectFromPowerSource();

        foreach (LinePort port in linePorts)
        {
            if (port.isActiveAndEnabled)
                port.RefreshPortConnection();
        }
    }

    private void ReportShortCircuit(PowerSource incomingSource)
    {
        RunodeCube cube = GetComponentInParent<RunodeCube>();
        string cubeName = cube != null ? cube.gameObject.name.ToUpper() : name.ToUpper();
        string existingSourceName = powerSource != null ? powerSource.gameObject.name.ToUpper() : "UNKNOWN";
        string incomingSourceName = incomingSource != null ? incomingSource.gameObject.name.ToUpper() : "UNKNOWN";

        Debug.Log(
            $"SHORT CIRCUIT DETECTED BETWEEN {existingSourceName} AND {incomingSourceName} ON {cubeName}.",
            this);

        if (HaltAllPropagationOnShortCircuit)
            propagationHalted = true;

        // TriggerShortCircuitFinalEvent(); 
    }

}
