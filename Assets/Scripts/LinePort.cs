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

public class LinePort : MonoBehaviour
{
    [SerializeField] private RunodeLine parentLine;
    [SerializeField] private PortLocation location;

    [Header("State (Read-Only)")]
    [SerializeField] private List<LinePort> connectedPorts = new List<LinePort>();
    [SerializeField] private List<GameObject> obstructions = new List<GameObject>();

    public PortType Type { get; private set; } = PortType.Neutral;
    public PortLocation Location => location;
    public RunodeLine ParentLine => parentLine;
    public List<LinePort> ConnectedPorts => connectedPorts;
    public List<GameObject> Obstructions => obstructions;
    public bool IsConnected => connectedPorts.Count > 0;
    public bool IsObstructed => obstructions.Count > 0;

    [SerializeField] private LayerMask portTriggerLayers;

    public void SetRole(PortType newType)
    {
        Type = newType;
    }

    public void ResetType()
    {
        Type = PortType.Neutral;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (parentLine == null) return;

        if ((portTriggerLayers & (1 << other.gameObject.layer)) == 0) return;

        LinePort otherPort = other.GetComponent<LinePort>();
        if (otherPort != null && otherPort.parentLine != null)
        {
            if (!connectedPorts.Contains(otherPort))
                connectedPorts.Add(otherPort);
        }
        else
        {
            if (!obstructions.Contains(other.gameObject))
                obstructions.Add(other.gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentLine == null) return;

        if ((portTriggerLayers & (1 << other.gameObject.layer)) == 0) return;

        LinePort otherPort = other.GetComponent<LinePort>();
        if (otherPort != null)
        {
            connectedPorts.Remove(otherPort);
        }
        else
        {
            obstructions.Remove(other.gameObject);
        }
    }
}
