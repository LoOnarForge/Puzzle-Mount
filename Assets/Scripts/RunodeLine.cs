using UnityEngine;

public class RunodeLine : MonoBehaviour
{
    [Header("Ports (Assign in Inspector)")]
    [SerializeField] private LinePort[] ports;

    [Header("State (Read-Only)")]
    [SerializeField] private bool isPowered;

    public bool IsPowered => isPowered;
    public LinePort[] Ports => ports;

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
}
