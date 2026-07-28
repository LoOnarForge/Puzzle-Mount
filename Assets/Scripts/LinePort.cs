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

    public void SetFaceBlocked(bool blocked)
    {
        faceBlocked = blocked;
        ApplyBlockedState();
    }

    public void RefreshPortState()
    {
        RefreshPortObstructionState();
        RefreshPortConnection();
    }

    public void RefreshPortObstructionState()
    {
        Debug.Log($"Refresh {RunodeCube.CurrentRefreshId} port obstruction pass | port={name} | parentCube={(parentCube == null ? "None" : parentCube.name)} | portPosition={transform.position} | portLayer={gameObject.layer}");

        obstructions.Clear();
        directlyBlocked = false;

        OverlapBox();
        CheckForPortObstructions();
    }

    public void RefreshPortConnection()
    {
        Debug.Log($"Refresh {RunodeCube.CurrentRefreshId} port connection pass | port={name} | parentCube={(parentCube == null ? "None" : parentCube.name)} | blocked={isBlocked}");

        connectedPorts.Clear();

        if (isBlocked)
        {
            Debug.Log($"Refresh {RunodeCube.CurrentRefreshId} LinePort connection skipped | port={name} | blocked=True");
            return;
        }

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

        Debug.Log($"Refresh {RunodeCube.CurrentRefreshId} LinePort query | port={name} | center={center} | halfExtents={halfExtents} | rotation={portTrigger.transform.rotation} | count={overlapCount}");
        for (int i = 0; i < overlapCount; i++)
        {
            Collider collider = overlapResults[i];
            Debug.Log($"Refresh {RunodeCube.CurrentRefreshId} LinePort hit | port={name} | collider={collider.name} | instanceId={collider.GetInstanceID()} | root={collider.transform.root.name} | layer={collider.gameObject.layer} | isTrigger={collider.isTrigger} | boundsCenter={collider.bounds.center} | boundsSize={collider.bounds.size}");
        }
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
                Debug.Log($"Refresh {RunodeCube.CurrentRefreshId} LinePort obstruction | port={name} | parentCube={(parentCube == null ? "None" : parentCube.name)} | collider={overlap.name} | root={overlap.transform.root.name} | instanceId={overlap.GetInstanceID()}");
            }
        }
        directlyBlocked = obstructions.Count > 0;
        ApplyBlockedState();
        Debug.Log($"Refresh {RunodeCube.CurrentRefreshId} LinePort obstruction state | port={name} | parentCube={(parentCube == null ? "None" : parentCube.name)} | faceBlocked={faceBlocked} | directlyBlocked={directlyBlocked} | blocked={isBlocked} | objects={obstructions.Count}");
    }

    private void ApplyBlockedState()
    {
        isBlocked = faceBlocked || directlyBlocked;
    }


    // Finds the first valid port and reports how many valid candidates were found.
    private LinePort ChooseValidPort()
    {
        LinePort firstValidPort = null;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = overlapResults[i];
            LinePort otherPort = overlap.GetComponent<LinePort>();

            if (otherPort == null || otherPort == this)
                continue;

            bool canConnect = CanConnectTo(otherPort);
            Debug.Log($"Refresh {RunodeCube.CurrentRefreshId} LinePort candidate | port={name} | parentCube={(parentCube == null ? "None" : parentCube.name)} | candidate={otherPort.name} | candidateParentCube={(otherPort.parentCube == null ? "None" : otherPort.parentCube.name)} | sameCube={parentCube == otherPort.parentCube} | selfBlocked={isBlocked} | otherBlocked={otherPort.isBlocked} | accepted={canConnect}");

            if (!canConnect)
                continue;

            firstValidPort ??= otherPort;
        }

        Debug.Log($"Refresh {RunodeCube.CurrentRefreshId} LinePort selected | port={name} | selected={(firstValidPort == null ? "None" : firstValidPort.name)}");
        return firstValidPort;
    }

    // Determines whether the overlapping port is a valid connection candidate.
    private bool CanConnectTo(LinePort otherPort)
    {
        if (otherPort == null || otherPort == this)
            return false;

        return !isBlocked && !otherPort.isBlocked;
    }

    // Sends the accepted connection to this port's parent line.
    private void ReportConnection(LinePort otherPort)
    {
        if (parentLine == null)
            return;

        Debug.Log($"Refresh {RunodeCube.CurrentRefreshId} LinePort report | {name} -> {otherPort.name}");
        parentLine.OnPortConnected(this, otherPort);
    }
}
