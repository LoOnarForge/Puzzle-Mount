using System.Collections.Generic;
using UnityEngine;

public enum PortType
{
    Neutral,
    Receiver,
    Giver
}

public enum PortLocation
{
    Up,
    Right,
    Down,
    Left
}

[RequireComponent(typeof(BoxCollider))]
public class LinePort : MonoBehaviour
{
    [SerializeField] private RunodeLine parentLine;
    [SerializeField] private RunodeCube parentCube;

    [Header("DEBUGGING:")]
    [SerializeField] private PortType type = PortType.Neutral;
    [SerializeField] private bool isIncluded;
    public bool IsIncluded => isIncluded;
    [SerializeField] private bool isBlocked;
    private bool faceBlocked;
    private bool directlyBlocked;

    [SerializeField] private LayerMask portTriggerLayers;
    [SerializeField] private List<LinePort> connectedPorts = new List<LinePort>();
    [SerializeField] private List<GameObject> obstructions = new List<GameObject>();
    [SerializeField] private PortLocation location;

    private BoxCollider portTrigger;
    private readonly Collider[] overlapResults = new Collider[16];
    private int overlapCount;

    private void Awake()
    {
        portTrigger = GetComponent<BoxCollider>();

        if (portTrigger == null)
        {
            Debug.LogError(
                $"{nameof(LinePort)} on {name} requires a {nameof(BoxCollider)}.",
                this);
        }
    }

    public void SetPortType(PortType newType)
    {
        type = newType;
    }

    public void SetIncluded(bool included)
    {
        isIncluded = included;
    }

    public void SetFaceColliderEnabled(bool enabled)
    {
        if (portTrigger == null)
            portTrigger = GetComponent<BoxCollider>();

        if (portTrigger != null)
            portTrigger.enabled = isIncluded && enabled;
    }

    public void SetFaceBlocked(bool blocked)
    {
        faceBlocked = blocked;
        ApplyBlockedState();
    }


    public void RefreshPortObstructionState()
    {

        obstructions.Clear();
        directlyBlocked = false;

        OverlapBox();
        CheckForPortObstructions();
    }

    public void RefreshPortConnection()
    {
        connectedPorts.Clear();

        if (faceBlocked)
            return;

        LinePort validPort = ChooseValidPort();
        if (validPort == null)
            return;

        connectedPorts.Add(validPort);
        ReportConnection(validPort);
    }

    // Fills the overlap buffer with colliders inside this port's BoxCollider.
    private void OverlapBox()
    {
        if (portTrigger == null)
        {
            Debug.LogError(
                $"{nameof(LinePort)} on {name} cannot refresh because its {nameof(BoxCollider)} is missing.",
                this);
            overlapCount = 0;
            return;
        }

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

    // Records every overlapping non-port object and updates the blocked state.
    private void CheckForPortObstructions()
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
        directlyBlocked = obstructions.Count > 0;
        ApplyBlockedState();

    }

    private void ApplyBlockedState()
    {
        isBlocked = faceBlocked || directlyBlocked;
    }


    // Finds the first valid port and reports how many valid candidates were found.
    private LinePort ChooseValidPort()
    {
        LinePort firstValidPort = null;

        List<LinePort> viablePorts = new List<LinePort>();
        List<string> viablePortDescriptions = new List<string>();

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = overlapResults[i];
            LinePort otherPort = overlap.GetComponent<LinePort>();

            if (otherPort == null || otherPort == this)
                continue;

            bool isInternal = parentCube == otherPort.parentCube;
            bool canConnect = CanConnectTo(otherPort);


            if (!canConnect)
                continue;

            viablePorts.Add(otherPort);
            viablePortDescriptions.Add($"{otherPort.name} ({(isInternal ? "internal" : "external")})");

            if (firstValidPort == null)
                firstValidPort = otherPort;
        }


        if (viablePorts.Count > 1)
        {
            Debug.LogWarning(
                $"LinePort '{name}' found {viablePorts.Count} viable connections: {string.Join(", ", viablePortDescriptions)}.",
                this);
        }

        return firstValidPort;
    }

    // Determines whether the overlapping port is a valid connection candidate.
    private bool CanConnectTo(LinePort otherPort)
    {
        if (otherPort == null || otherPort == this)
            return false;

        bool isInternal = parentCube == otherPort.parentCube;
        if (!isInternal)
            return true;

        return !isBlocked && !otherPort.isBlocked;
    }

    // Sends the accepted connection to this port's parent line.
    private void ReportConnection(LinePort otherPort)
    {
        if (parentLine == null)
            return;
    }
}
