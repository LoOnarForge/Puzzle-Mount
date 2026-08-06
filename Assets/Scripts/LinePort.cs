using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PortType
{
    Neutral,
    Receiver,
    Giver
}


[RequireComponent(typeof(BoxCollider))]
public class LinePort : MonoBehaviour
{
    [Header("STATE:")]
    [SerializeField] private RunodeLine parentLine;
    [SerializeField] private RunodeCube parentCube;

    [SerializeField] private PowerSource parentPowerSource;
    [SerializeField] private DevicePowerSocket parentDevicePowerSocket;

    [Header("DEBUGGING:")]
    [SerializeField] private PortType type = PortType.Neutral;
    [SerializeField] private bool isIncluded;
    public bool IsIncluded => isIncluded;
    public RunodeLine ParentLine => parentLine;
    public DevicePowerSocket ParentDevicePowerSocket
    {
        get => parentDevicePowerSocket;
        set => parentDevicePowerSocket = value;
    }
    [SerializeField] private bool isBlocked;
    private bool faceBlocked;
    private bool canReportConnections;

    [SerializeField] private LayerMask portTriggerLayers;
    [SerializeField] private List<LinePort> connectedPorts = new List<LinePort>();
    [SerializeField] private List<GameObject> obstructions = new List<GameObject>();


    private BoxCollider portTrigger;
    private Coroutine multipleConnectionWarningCoroutine;
    private bool multipleConnectionWarningIssued;

    private readonly Collider[] overlapResults = new Collider[16];
    private int overlapCount;


    private void Awake()
    {
        portTrigger = GetComponent<BoxCollider>();
    }

    public bool IsConnectedTo(LinePort otherPort)
    {
        return connectedPorts.Contains(otherPort);
    }

    public void SetPortType(PortType newType)
    {
        type = newType;
    }



    public void SetParentFaceBlockedState(bool blocked)
    {
        faceBlocked = blocked;
        SetBlockedPortState();
    }
    private void SetBlockedPortState()
    {
        isBlocked = faceBlocked || obstructions.Count > 0;
        // to be changed later from a list , only matters if there is 0 or 1 obstructions 
    }
    private void OverlapBoxCheck()
    {
        Vector3 center = portTrigger.transform.TransformPoint(portTrigger.center);
        Vector3 halfExtents = Vector3.Scale(
        portTrigger.size,
        portTrigger.transform.lossyScale) * 0.5f;

        overlapCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            overlapResults,
            portTrigger.transform.rotation,
            portTriggerLayers,
            QueryTriggerInteraction.Collide);
    }

    private void CheckPortObstructions()
    {
        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = overlapResults[i];
            LinePort otherPort = overlap.GetComponent<LinePort>();

            if (otherPort != null)
                continue;

            if (!obstructions.Contains(overlap.gameObject))
            {
                obstructions.Add(overlap.gameObject);

            }
        }
        SetBlockedPortState();

    }
    public void RefreshPortObstructionState()
    {
        obstructions.Clear();

        OverlapBoxCheck();
        CheckPortObstructions();
    }
    public void RefreshPortConnection()
    {
        if (faceBlocked)
        {
            RemoveAllConnections();
            return;
        }

        RemoveConnectionsThatNoLongerExist();
        AddNewConnections();
    }
   
    private void RemoveAllConnections()
    {
        for (int i = connectedPorts.Count - 1; i >= 0; i--)
        {
            LinePort connectedPort = connectedPorts[i];
            connectedPorts.RemoveAt(i);
            ReportLostConnection(connectedPort);
        }

        ResetMultipleConnectionWarningIfResolved();
    }

    private void RemoveConnectionsThatNoLongerExist()
    {
        for (int i = connectedPorts.Count - 1; i >= 0; i--)
        {
            LinePort connectedPort = connectedPorts[i];

            if (IsPortStillConnected(connectedPort))
                continue;

            connectedPorts.RemoveAt(i);
            ReportLostConnection(connectedPort);
        }

        ResetMultipleConnectionWarningIfResolved();
    }

    private bool IsPortStillConnected(LinePort connectedPort)
    {
        for (int i = 0; i < overlapCount; i++)
        {
            LinePort overlappingPort = overlapResults[i].GetComponent<LinePort>();

            if (overlappingPort == connectedPort && CanConnectTo(overlappingPort))
                return true;
        }

        return false;
    }

    private void AddNewConnections()
    {
        for (int i = 0; i < overlapCount; i++)
        {
            LinePort otherPort = overlapResults[i].GetComponent<LinePort>();

            if (otherPort == null || otherPort == this)
                continue;

            if (!CanConnectTo(otherPort))
                continue;

            if (connectedPorts.Contains(otherPort))
                continue;

            connectedPorts.Add(otherPort);
            ReportNewConnection(otherPort);
        }

        if (connectedPorts.Count > 1
            && multipleConnectionWarningCoroutine == null
            && !multipleConnectionWarningIssued)
        {
            multipleConnectionWarningCoroutine = StartCoroutine(WarnIfMultipleConnectionsPersist());
        }
    }

    private IEnumerator WarnIfMultipleConnectionsPersist()
    {
        yield return null;

        multipleConnectionWarningCoroutine = null;

        if (connectedPorts.Count <= 1 || multipleConnectionWarningIssued)
            yield break;

        multipleConnectionWarningIssued = true;

        List<string> connectedPortDescriptions = new List<string>();

        foreach (LinePort connectedPort in connectedPorts)
        {
            bool isInternal = parentCube == connectedPort.parentCube;
            connectedPortDescriptions.Add($"{connectedPort.name} ({(isInternal ? "internal" : "external")})");
        }

        Debug.LogWarning(
            $"LinePort '{name}' retained {connectedPorts.Count} connections: {string.Join(", ", connectedPortDescriptions)}.",
            this);
    }

    private void ResetMultipleConnectionWarningIfResolved()
    {
        if (connectedPorts.Count > 1)
            return;

        multipleConnectionWarningIssued = false;

        if (multipleConnectionWarningCoroutine == null)
            return;

        StopCoroutine(multipleConnectionWarningCoroutine);
        multipleConnectionWarningCoroutine = null;
    }

    private bool CanConnectTo(LinePort otherPort)
    {
        bool isInternal = parentCube == otherPort.parentCube;
        if (!isInternal)
            return true;

        return !isBlocked && !otherPort.isBlocked;
    }

    public void SetCanReportConnections(bool canReport)
    {
        canReportConnections = canReport;
    }

    // Tells fed faces they are no longer connected to the Power Source path.
    public void PropagateDisconnectFromPowerSource()
    {
        if (type != PortType.Giver)
            return;

        foreach (LinePort connectedPort in connectedPorts)
        {
            if (connectedPort.parentDevicePowerSocket != null)
                continue;

            // Skip faces this line does not power (stale links must not clear isConnected).
            if (connectedPort.parentLine.PoweredByLine != parentLine)
                continue;

            connectedPort.parentLine.DisconnectFromPowerSource();
        }
    }

    public void ReportValidConnections()
    {
        if (type != PortType.Giver)
            return;

        foreach (LinePort connectedPort in connectedPorts)
        {
            if (!parentLine.TryPowerConnectedPort(connectedPort, this))
                break;
        }
    }

    public void ReportPowerLossToConnectedPorts()
    {
        foreach (LinePort connectedPort in connectedPorts)
            connectedPort.ReportPowerLost();
    }

    private void ReportPowerLost()
    {
        if (type != PortType.Receiver)
            return;

        if (parentDevicePowerSocket != null)
        {
            parentDevicePowerSocket.ClearPower();
            return;
        }

        parentLine.PowerDownLine();
    }


    private void ReportNewConnection(LinePort otherPort)
    {
        if (canReportConnections && type == PortType.Giver)
        {
            parentLine.TryPowerConnectedPort(otherPort, this);
            return;
        }

        // Device socket ports never refresh themselves, so the Power Source port reports for them.
        if (parentPowerSource != null && type == PortType.Giver && otherPort.parentDevicePowerSocket != null)
        {
            parentPowerSource.PowerConnectedPortFromSource(otherPort, this);
            return;
        }

        if (otherPort.parentPowerSource != null)
            otherPort.parentPowerSource.PowerConnectedPortFromSource(this, otherPort);
    }

    private void ReportLostConnection(LinePort connectedPort)
    {
        if (type == PortType.Receiver)
        {
            ReportPowerLost();
            return;
        }

        if (type == PortType.Giver && parentLine != null && parentLine.PowerSource != null)
            parentLine.PowerSource.RemoveWaitingEntry(parentLine, connectedPort);
    }

    public void SetIncluded(bool included)
    {
        isIncluded = included;
    }

    public void SetPortColliderEnabled(bool enabled)
    {
        if (isIncluded)
            portTrigger.enabled = enabled;
    }
}
