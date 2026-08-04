using System.Collections.Generic;
using UnityEngine;

public class PowerSource : MonoBehaviour
{
    [System.Serializable]
    private class CircuitMember
    {
        public RunodeLine line;
        public DevicePowerSocket socket;
    }

    [System.Serializable]
    private class WaitingEntry
    {
        public RunodeLine givingLine;
        public RunodeLine receivingLine;
        public DevicePowerSocket receivingSocket;
        public LinePort receivingPort;
        public LinePort sourcePort;
        public int powerIndex;
        public int mwNeeded;
    }

    [Header("POWER SOURCE:")]
    [SerializeField] private int colorIndex;
    [SerializeField] private int maxMW = 10;

    [Space(20)]
    [Header("STATE:")]
    [SerializeField] private int availableMW;
    [SerializeField] private List<CircuitMember> circuitMembers = new List<CircuitMember>();
    [SerializeField] private List<WaitingEntry> waitingEntries = new List<WaitingEntry>();

    [Space(20)]
    [Header("POWER SOURCE PORTS:")]
    [SerializeField] private LinePort upPort;
    [SerializeField] private LinePort rightPort;
    [SerializeField] private LinePort downPort;
    [SerializeField] private LinePort leftPort;

    [Space(20)]
    [Header("VISUAL ELEMENTS:")]
    [SerializeField] private List<Renderer> colorElements = new List<Renderer>();

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock propertyBlock;
    private Color circuitColor;

    public int ColorIndex => colorIndex;

    private void Awake()
    {
        availableMW = maxMW;
        circuitColor = ColorManager.Instance.GetColor(colorIndex);

        upPort.SetPortType(PortType.Giver);
        rightPort.SetPortType(PortType.Giver);
        downPort.SetPortType(PortType.Giver);
        leftPort.SetPortType(PortType.Giver);
    }


    public void RefreshGiverPorts()
    {
        RefreshPort(upPort);
        RefreshPort(rightPort);
        RefreshPort(downPort);
        RefreshPort(leftPort);
    }

    private void RefreshPort(LinePort port)
    {
        if (port == null)
            return;

        port.RefreshPortObstructionState();
        port.RefreshPortConnection();
    }

    // Powers a face directly from this Power Source.
    public void PowerLineFromPS(RunodeLine line, LinePort receivingPort, LinePort sourcePort)
    {
        if (TakeMW())
        {
            line.PowerUpLine(this, null, receivingPort, circuitColor, 1);
            AddCircuitMember(line);
            RemoveWaitingEntriesForLine(line);
            return;
        }

        AddWaitingEntry(null, line, receivingPort, sourcePort, 1, line.RequiredMw);
    }

    // Powers a device socket directly from this Power Source.
    public void PowerSocketFromPS(DevicePowerSocket socket, LinePort receivingPort, LinePort sourcePort)
    {
        if (socket.ColorIndex != colorIndex)
            return;

        bool fullyPowered = socket.TryReceivePower(null, sourcePort, this, circuitColor, 1);

        if (fullyPowered)
        {
            RemoveWaitingEntriesForSocket(socket);
            return;
        }

        if (socket.RemainingMwNeeded > 0)
            AddWaitingEntry(null, socket, receivingPort, sourcePort, 1, socket.RemainingMwNeeded);
    }

    // Returns one MW to the shared Power Source pool.
    public void ReturnMW()
    {
        if (availableMW < maxMW)
            availableMW++;

        EvaluateWaitingList();
    }

    public bool TakeMW()
    {
        if (availableMW <= 0)
            return false;

        availableMW--;
        return true;
    }

    // Adds a failed transfer and immediately attempts arbitration.
    public void AddWaitingEntry(RunodeLine givingLine, RunodeLine receivingLine, LinePort receivingPort, LinePort sourcePort, int powerIndex, int mwNeeded)
    {
        if (HasWaitingEntry(givingLine, receivingLine))
            return;

        WaitingEntry entry = new WaitingEntry
        {
            givingLine = givingLine,
            receivingLine = receivingLine,
            receivingPort = receivingPort,
            sourcePort = sourcePort,
            powerIndex = powerIndex,
            mwNeeded = mwNeeded
        };

        waitingEntries.Add(entry);
        ArbitrateBetweenMembersAndWaiters(entry);
    }

    public void AddWaitingEntry(RunodeLine givingLine, DevicePowerSocket receivingSocket, LinePort receivingPort, LinePort sourcePort, int powerIndex, int mwNeeded)
    {
        if (HasWaitingEntry(givingLine, receivingSocket))
            return;

        WaitingEntry entry = new WaitingEntry
        {
            givingLine = givingLine,
            receivingSocket = receivingSocket,
            receivingPort = receivingPort,
            sourcePort = sourcePort,
            powerIndex = powerIndex,
            mwNeeded = mwNeeded
        };

        waitingEntries.Add(entry);
        ArbitrateBetweenMembersAndWaiters(entry);
    }

    public void RemoveWaitingEntry(RunodeLine givingLine, RunodeLine receivingLine)
    {
        for (int i = waitingEntries.Count - 1; i >= 0; i--)
        {
            WaitingEntry entry = waitingEntries[i];

            if (entry.givingLine == givingLine && entry.receivingLine == receivingLine)
                waitingEntries.RemoveAt(i);
        }
    }

    public void RemoveWaitingEntry(RunodeLine givingLine, DevicePowerSocket receivingSocket)
    {
        for (int i = waitingEntries.Count - 1; i >= 0; i--)
        {
            WaitingEntry entry = waitingEntries[i];

            if (entry.givingLine == givingLine && entry.receivingSocket == receivingSocket)
                waitingEntries.RemoveAt(i);
        }
    }

    public void RemoveWaitingEntriesForLine(RunodeLine line)

    {
        for (int i = waitingEntries.Count - 1; i >= 0; i--)
        {
            WaitingEntry entry = waitingEntries[i];

            if (entry.givingLine == line || entry.receivingLine == line)
                waitingEntries.RemoveAt(i);
        }
    }

    public void RemoveWaitingEntriesForSocket(DevicePowerSocket socket)
    {
        for (int i = waitingEntries.Count - 1; i >= 0; i--)
        {
            WaitingEntry entry = waitingEntries[i];

            if (entry.receivingSocket == socket)
                waitingEntries.RemoveAt(i);
        }
    }

    private void ArbitrateBetweenMembersAndWaiters(WaitingEntry waitingEntry)
    {
        RunodeLine victimLine = null;
        DevicePowerSocket victimSocket = null;
        int victimPowerIndex = -1;
        int victimMemberIndex = -1;

        for (int i = 0; i < circuitMembers.Count; i++)
        {
            CircuitMember member = circuitMembers[i];

            if (member.socket != null && member.socket.AllocatedMw > 0)
            {
                int socketPowerIndex = member.socket.PowerIndex;

                if (victimPowerIndex < 0
                    || socketPowerIndex > victimPowerIndex
                    || (socketPowerIndex == victimPowerIndex && i > victimMemberIndex))
                {
                    victimPowerIndex = socketPowerIndex;
                    victimMemberIndex = i;
                    victimSocket = member.socket;
                    victimLine = null;
                }

                continue;
            }

            RunodeLine line = member.line;

            if (line == null || !line.IsPowered)
                continue;

            if (victimPowerIndex < 0
                || line.PowerIndex > victimPowerIndex
                || (line.PowerIndex == victimPowerIndex && i > victimMemberIndex))
            {
                victimPowerIndex = line.PowerIndex;
                victimMemberIndex = i;
                victimLine = line;
                victimSocket = null;
            }
        }

        if (victimPowerIndex < 0 || victimPowerIndex <= waitingEntry.powerIndex)
            return;

        if (victimSocket != null)
        {
            victimSocket.ReleaseOneMw();
            return;
        }

        RunodeLine poweringLine = victimLine.PoweredByLine;
        victimLine.PowerDownLine();
        poweringLine?.RefreshFaceAndPortsStates();
    }

    private void EvaluateWaitingList()
    {
        WaitingEntry entry = FindHighestPriorityWaitingEntry();

        if (entry == null)
            return;

        waitingEntries.Remove(entry);

        if (!IsWaitingEntryValid(entry))
        {
            EvaluateWaitingList();
            return;
        }

        if (entry.givingLine == null)
        {
            if (entry.receivingSocket != null)
            {
                PowerSocketFromPS(entry.receivingSocket, entry.receivingPort, entry.sourcePort);
                return;
            }

            PowerLineFromPS(entry.receivingLine, entry.receivingPort, entry.sourcePort);
            return;
        }

        entry.sourcePort.ReportValidConnections();
    }

    private WaitingEntry FindHighestPriorityWaitingEntry()
    {
        WaitingEntry selectedEntry = null;

        foreach (WaitingEntry entry in waitingEntries)
        {
            if (selectedEntry == null || entry.powerIndex < selectedEntry.powerIndex)
                selectedEntry = entry;
        }

        return selectedEntry;
    }

    private bool IsWaitingEntryValid(WaitingEntry entry)
    {
        if (entry.receivingSocket != null)
        {
            if (entry.receivingSocket.RemainingMwNeeded <= 0)
                return false;

            if (entry.givingLine == null)
                return entry.sourcePort != null && entry.sourcePort.IsConnectedTo(entry.receivingPort);

            return entry.givingLine.IsPowered
                && entry.givingLine.IsConnectedToPowerSource()
                && entry.givingLine.PowerSource == this
                && entry.sourcePort != null
                && entry.sourcePort.IsConnectedTo(entry.receivingPort);
        }

        if (entry.receivingLine == null || entry.receivingLine.IsPowered)
            return false;

        if (entry.givingLine == null)
            return entry.sourcePort != null && entry.sourcePort.IsConnectedTo(entry.receivingPort);

        return entry.givingLine.IsPowered
            && entry.givingLine.IsConnectedToPowerSource()
            && entry.givingLine.PowerSource == this
            && entry.sourcePort != null
            && entry.sourcePort.IsConnectedTo(entry.receivingPort);
    }

    private bool HasWaitingEntry(RunodeLine givingLine, RunodeLine receivingLine)
    {
        foreach (WaitingEntry entry in waitingEntries)
        {
            if (entry.givingLine == givingLine && entry.receivingLine == receivingLine)
                return true;
        }

        return false;
    }

    private bool HasWaitingEntry(RunodeLine givingLine, DevicePowerSocket receivingSocket)
    {
        foreach (WaitingEntry entry in waitingEntries)
        {
            if (entry.givingLine == givingLine && entry.receivingSocket == receivingSocket)
                return true;
        }

        return false;
    }

    public void AddCircuitMember(RunodeLine line)
    {
        if (line == null || HasCircuitMember(line))
            return;

        circuitMembers.Add(new CircuitMember { line = line });
    }

    public void RemoveCircuitMember(RunodeLine line)
    {
        if (line == null)
            return;

        for (int i = circuitMembers.Count - 1; i >= 0; i--)
        {
            if (circuitMembers[i].line == line)
                circuitMembers.RemoveAt(i);
        }
    }

    public void AddCircuitMember(DevicePowerSocket socket)
    {
        if (socket == null || HasCircuitMember(socket))
            return;

        circuitMembers.Add(new CircuitMember { socket = socket });
    }

    public void RemoveCircuitMember(DevicePowerSocket socket)
    {
        if (socket == null)
            return;

        for (int i = circuitMembers.Count - 1; i >= 0; i--)
        {
            if (circuitMembers[i].socket == socket)
                circuitMembers.RemoveAt(i);
        }
    }

    private bool HasCircuitMember(RunodeLine line)
    {
        foreach (CircuitMember member in circuitMembers)
        {
            if (member.line == line)
                return true;
        }

        return false;
    }

    private bool HasCircuitMember(DevicePowerSocket socket)
    {
        foreach (CircuitMember member in circuitMembers)
        {
            if (member.socket == socket)
                return true;
        }

        return false;
    }

    private void OnValidate()
    {
        SetStartingColorsOnPSObject();
    }

    private void SetStartingColorsOnPSObject()
    {
        ColorManager colorManager = FindAnyObjectByType<ColorManager>();
        if (colorManager == null || colorManager.colors.Count == 0)
            return;

        int selectedColorIndex = Mathf.Clamp(colorIndex, 0, colorManager.colors.Count - 1);
        Color selectedColor = colorManager.GetColor(selectedColorIndex);

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        foreach (Renderer colorElement in colorElements)
        {
            if (colorElement == null)
                continue;

            colorElement.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorProperty, selectedColor);
            propertyBlock.SetColor(EmissionColorProperty, selectedColor * 2f);
            colorElement.SetPropertyBlock(propertyBlock);

            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer == null)
                return;

            spriteRenderer.color = selectedColor;
        }
    }
}
