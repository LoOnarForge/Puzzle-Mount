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
    [SerializeField] private RunodeLine parentLine;
    [SerializeField] private RunodeCube parentCube;

    [SerializeField] private PowerSource parentPowerSource;

    [Header("DEBUGGING:")]
    [SerializeField] private PortType type = PortType.Neutral;
    [SerializeField] private bool isIncluded;
    public bool IsIncluded => isIncluded;
    [SerializeField] private bool isBlocked;
    private bool faceBlocked;

    [SerializeField] private LayerMask portTriggerLayers;
    [SerializeField] private List<LinePort> connectedPorts = new List<LinePort>();
    [SerializeField] private List<GameObject> obstructions = new List<GameObject>();


    private BoxCollider portTrigger;
    private readonly Collider[] overlapResults = new Collider[16];
    private int overlapCount;


    private void Awake()
    {
        portTrigger = GetComponent<BoxCollider>();
    }

    public void SetPortType(PortType newType)
    {
        type = newType;
    }

    public void SetPortColliderEnabled(bool enabled)
    {
        if (isIncluded)
            portTrigger.enabled = enabled;
    }

    public void SetParentFaceBlockedState(bool blocked)
    {
        faceBlocked = blocked;
        SetBlockedPortState();
    }
    private void SetBlockedPortState()
    {
        isBlocked = faceBlocked || obstructions.Count > 0;
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
            connectedPorts.RemoveAt(i);
            ReportLostConnection();
        }
    }

    private void RemoveConnectionsThatNoLongerExist()
    {
        for (int i = connectedPorts.Count - 1; i >= 0; i--)
        {
            LinePort connectedPort = connectedPorts[i];

            if (IsPortStillConnected(connectedPort))
                continue;

            connectedPorts.RemoveAt(i);
            ReportLostConnection();
        }
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
        List<LinePort> viablePorts = new List<LinePort>();
        List<string> viablePortDescriptions = new List<string>();

        for (int i = 0; i < overlapCount; i++)
        {
            LinePort otherPort = overlapResults[i].GetComponent<LinePort>();

            if (otherPort == null || otherPort == this)
                continue;

            if (!CanConnectTo(otherPort))
                continue;

            bool isInternal = parentCube == otherPort.parentCube;
            viablePorts.Add(otherPort);
            viablePortDescriptions.Add($"{otherPort.name} ({(isInternal ? "internal" : "external")})");

            if (connectedPorts.Contains(otherPort))
                continue;

            connectedPorts.Add(otherPort);
            ReportNewConnection(otherPort);
        }

        if (viablePorts.Count > 1)
        {
            Debug.LogWarning(
                $"LinePort '{name}' found {viablePorts.Count} viable connections: {string.Join(", ", viablePortDescriptions)}.",
                this);
        }
    }

    private bool CanConnectTo(LinePort otherPort)
    {
        bool isInternal = parentCube == otherPort.parentCube;
        if (!isInternal)
            return true;

        return !isBlocked && !otherPort.isBlocked;
    }

    private void ReportNewConnection(LinePort otherPort)
    {
        if (type == PortType.Giver)
        {
            parentLine.TryToGivePower();
            return;
        }

        if (otherPort.parentPowerSource != null)
            otherPort.parentPowerSource.PowerRunodeLine(parentLine, this);
    }

    private void ReportLostConnection()
    {
        if (type == PortType.Receiver)
            parentLine.PowerDownLine();
    }

    // Tries to give power through this port's connected ports.
    public void TryToGivePower()
    {
        if (type != PortType.Giver)
            return;

        foreach (LinePort receivingPort in connectedPorts)
            parentLine.GivePowerTo(receivingPort.parentLine, receivingPort);
    }

    // Stops this port from providing power to connected lines.
    public void StopGivingPower()
    {
        foreach (LinePort receivingPort in connectedPorts)
        {
            if (receivingPort.type == PortType.Receiver)
                receivingPort.parentLine.PowerDownLine();
        }
    }

    public void SetIncluded(bool included)
    {
        isIncluded = included;
    }
}
