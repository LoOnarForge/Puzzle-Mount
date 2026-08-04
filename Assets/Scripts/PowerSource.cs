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

    private const int FirstReceiverPowerIndex = 1;

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

    // Powers whatever owns the connected port (face or device socket) directly from this Power Source.
    public void PowerConnectedPortFromSource(LinePort targetPort, LinePort sourcePort)
    {
        GivePowerToConnectedPort(null, targetPort, sourcePort, FirstReceiverPowerIndex);
    }

    // Single give path: allocates MW to the owner of the target port, or creates a waiting entry.
    public bool GivePowerToConnectedPort(RunodeLine givingLine, LinePort targetPort, LinePort sourcePort, int receiverPowerIndex)
    {
        DevicePowerSocket targetSocket = targetPort.ParentDevicePowerSocket;
        int mwStillNeeded;

        if (targetSocket != null)
        {
            if (!targetSocket.CanReceivePowerFrom(this, givingLine))
                return true;

            targetSocket.TryReceivePower(givingLine, sourcePort, this, circuitColor, receiverPowerIndex);

            if (targetSocket.AllocatedMw > 0)
                AddCircuitMember(targetSocket);

            mwStillNeeded = targetSocket.RemainingMwNeeded;
        }
        else
        {
            RunodeLine targetLine = targetPort.ParentLine;

            if (TakeMW())
            {
                targetLine.PowerUpLine(this, givingLine, targetPort, circuitColor, receiverPowerIndex);
                AddCircuitMember(targetLine);
            }

            mwStillNeeded = targetLine.RemainingMwNeeded;
        }

        if (mwStillNeeded > 0)
        {
            AddWaitingEntry(givingLine, targetPort, sourcePort, receiverPowerIndex, mwStillNeeded);
            return false;
        }

        RemoveWaitingEntry(givingLine, targetPort);
        return true;
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

    // Adds a failed transfer for the owner of the receiving port and immediately attempts arbitration.
    private void AddWaitingEntry(RunodeLine givingLine, LinePort receivingPort, LinePort sourcePort, int powerIndex, int mwNeeded)
    {
        DevicePowerSocket receivingSocket = receivingPort.ParentDevicePowerSocket;
        RunodeLine receivingLine = receivingSocket != null ? null : receivingPort.ParentLine;

        if (HasWaitingEntry(givingLine, receivingLine, receivingSocket))
            return;

        WaitingEntry entry = new WaitingEntry
        {
            givingLine = givingLine,
            receivingLine = receivingLine,
            receivingSocket = receivingSocket,
            receivingPort = receivingPort,
            sourcePort = sourcePort,
            powerIndex = powerIndex,
            mwNeeded = mwNeeded
        };

        waitingEntries.Add(entry);
        ArbitrateBetweenMembersAndWaiters(entry);
    }

    // Removes the waiting entry between this giver and the owner of the receiving port.
    public void RemoveWaitingEntry(RunodeLine givingLine, LinePort receivingPort)
    {
        DevicePowerSocket receivingSocket = receivingPort.ParentDevicePowerSocket;
        RunodeLine receivingLine = receivingSocket != null ? null : receivingPort.ParentLine;

        for (int i = waitingEntries.Count - 1; i >= 0; i--)
        {
            WaitingEntry entry = waitingEntries[i];

            if (entry.givingLine == givingLine
                && entry.receivingLine == receivingLine
                && entry.receivingSocket == receivingSocket)
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
        CircuitMember victim = null;
        int victimPowerIndex = -1;

        foreach (CircuitMember member in circuitMembers)
        {
            if (GetMemberAllocatedMw(member) <= 0)
                continue;

            int memberPowerIndex = GetMemberPowerIndex(member);

            if (victimPowerIndex < 0 || memberPowerIndex >= victimPowerIndex)
            {
                victimPowerIndex = memberPowerIndex;
                victim = member;
            }
        }

        if (victim == null || victimPowerIndex <= waitingEntry.powerIndex)
            return;

        ReleaseOneMwFromMember(victim);
    }

    private static int GetMemberAllocatedMw(CircuitMember member)
    {
        if (member.socket != null)
            return member.socket.AllocatedMw;

        return member.line != null ? member.line.AllocatedMw : 0;
    }

    private static int GetMemberPowerIndex(CircuitMember member)
    {
        return member.socket != null ? member.socket.PowerIndex : member.line.PowerIndex;
    }

    private static void ReleaseOneMwFromMember(CircuitMember member)
    {
        if (member.socket != null)
        {
            member.socket.ReleaseOneMw();
            return;
        }

        member.line.ReleaseOneMw();
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
            PowerConnectedPortFromSource(entry.receivingPort, entry.sourcePort);
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
        int mwStillNeeded = entry.receivingSocket != null
            ? entry.receivingSocket.RemainingMwNeeded
            : (entry.receivingLine != null ? entry.receivingLine.RemainingMwNeeded : 0);

        if (mwStillNeeded <= 0)
            return false;

        if (entry.sourcePort == null || !entry.sourcePort.IsConnectedTo(entry.receivingPort))
            return false;

        if (entry.givingLine == null)
            return true;

        return entry.givingLine.IsPowered
            && entry.givingLine.IsConnectedToPowerSource()
            && entry.givingLine.PowerSource == this;
    }

    private bool HasWaitingEntry(RunodeLine givingLine, RunodeLine receivingLine, DevicePowerSocket receivingSocket)
    {
        foreach (WaitingEntry entry in waitingEntries)
        {
            if (entry.givingLine == givingLine
                && entry.receivingLine == receivingLine
                && entry.receivingSocket == receivingSocket)
                return true;
        }

        return false;
    }

    // Registers a powered face as a consumer of this Power Source.
    public void AddCircuitMember(RunodeLine line)
    {
        AddCircuitMember(line, null);
    }

    // Registers a device socket holding MW from this Power Source.
    public void AddCircuitMember(DevicePowerSocket socket)
    {
        AddCircuitMember(null, socket);
    }

    // Removes a face that no longer consumes MW from this Power Source.
    public void RemoveCircuitMember(RunodeLine line)
    {
        RemoveCircuitMember(line, null);
    }

    // Removes a device socket that no longer holds MW from this Power Source.
    public void RemoveCircuitMember(DevicePowerSocket socket)
    {
        RemoveCircuitMember(null, socket);
    }

    private void AddCircuitMember(RunodeLine line, DevicePowerSocket socket)
    {
        if (line == null && socket == null)
            return;

        if (HasCircuitMember(line, socket))
            return;

        circuitMembers.Add(new CircuitMember { line = line, socket = socket });
    }

    private void RemoveCircuitMember(RunodeLine line, DevicePowerSocket socket)
    {
        if (line == null && socket == null)
            return;

        for (int i = circuitMembers.Count - 1; i >= 0; i--)
        {
            CircuitMember member = circuitMembers[i];

            if (member.line == line && member.socket == socket)
                circuitMembers.RemoveAt(i);
        }
    }

    private bool HasCircuitMember(RunodeLine line, DevicePowerSocket socket)
    {
        foreach (CircuitMember member in circuitMembers)
        {
            if (member.line == line && member.socket == socket)
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
