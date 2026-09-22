using System.Collections.Generic;
using UnityEngine;

public class PowerSource : MonoBehaviour
{
    public const int MinMaxMw = 0;
    public const int MaxMaxMw = 20;

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
    }

    [Header("POWER SOURCE:")]
    [SerializeField] private int colorIndex;
    [SerializeField] [Range(MinMaxMw, MaxMaxMw)] private int maxMW = 10;

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
    [Header("VISUAL:")]
    [SerializeField] private SpriteRenderer circuitSprite;

    [Space(20)]
    [Header("CRYSTALS:")]
    [SerializeField] private CrystalController crystalController;

    private const int FirstReceiverPowerIndex = 1;

    private Color circuitColor;

    public int ColorIndex => colorIndex;

    private void Awake()
    {
        availableMW = maxMW;
        circuitColor = GetCircuitColor();
        ApplyCircuitSpriteColor();
        RefreshCrystalController();

        upPort.SetPortType(PortType.Giver);
        rightPort.SetPortType(PortType.Giver);
        downPort.SetPortType(PortType.Giver);
        leftPort.SetPortType(PortType.Giver);
    }

    private void Start()
    {
        RefreshGiverPorts();
        RefreshCrystalController();
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
            if (targetSocket.RequiredColorIndex >= 0 && ColorIndex != targetSocket.RequiredColorIndex)
                return true;

            targetSocket.TryReceivePower(givingLine, sourcePort, this, circuitColor, receiverPowerIndex);

            if (targetSocket.AllocatedMw > 0)
                AddCircuitMember(targetSocket);

            mwStillNeeded = targetSocket.RemainingMwNeeded;
        }
        else
        {
            RunodeLine targetLine = targetPort.ParentLine;
            if (targetLine == null)
            {
                PowerSource otherSource = targetPort.ParentPowerSource;
                if (otherSource != null && otherSource != this)
                    ReportPowerSourceConnection(otherSource);

                return true;
            }

            if (TakeMW())
            {
                targetLine.PowerUpLine(this, givingLine, targetPort, circuitColor, receiverPowerIndex);
                AddCircuitMember(targetLine);
            }

            mwStillNeeded = targetLine.RemainingMwNeeded;
        }

        if (mwStillNeeded > 0)
        {
            AddWaitingEntry(givingLine, targetPort, sourcePort, receiverPowerIndex);
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

        RefreshCrystalController();
        EvaluateWaitingList();
    }

    public bool TakeMW()
    {
        if (availableMW <= 0)
            return false;

        availableMW--;
        RefreshCrystalController();
        return true;
    }

    // Adds a failed transfer for the owner of the receiving port and immediately attempts arbitration.
    private void AddWaitingEntry(RunodeLine givingLine, LinePort receivingPort, LinePort sourcePort, int powerIndex)
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
            powerIndex = powerIndex
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

    // Logs when two Power Sources are linked through overlapping ports (directly or via a Runode line).
    private void ReportPowerSourceConnection(PowerSource otherSource)
    {
        Debug.Log(
            $"POWER SOURCE CONNECTION DETECTED BETWEEN {gameObject.name.ToUpper()} AND {otherSource.gameObject.name.ToUpper()}.",
            this);
    }

    private void OnValidate()
    {
        maxMW = Mathf.Clamp(maxMW, MinMaxMw, MaxMaxMw);
        availableMW = Mathf.Clamp(availableMW, MinMaxMw, maxMW);

        circuitColor = GetCircuitColor();
        ApplyCircuitSpriteColor();

        if (!Application.isPlaying)
            RefreshCrystalController();
    }

    private Color GetCircuitColor()
    {
        ColorManager manager = ColorManager.Instance;

#if UNITY_EDITOR
        if (manager == null)
        {
            foreach (ColorManager candidate in Resources.FindObjectsOfTypeAll<ColorManager>())
            {
                if (candidate != null && candidate.gameObject.scene.IsValid())
                {
                    manager = candidate;
                    break;
                }
            }
        }
#endif

        return manager != null ? manager.GetColor(colorIndex) : Color.white;
    }

    private void ApplyCircuitSpriteColor()
    {
        if (circuitSprite == null)
            circuitSprite = GetComponentInChildren<SpriteRenderer>(true);

        if (circuitSprite != null)
            circuitSprite.color = circuitColor;
    }

    // Pushes current MW and color to the attached crystal layout.
    private void RefreshCrystalController()
    {
        if (crystalController == null)
            return;

        int availableForCrystals = Application.isPlaying ? availableMW : maxMW;
        crystalController.ApplyPowerSourceState(colorIndex, maxMW, availableForCrystals);
    }
}
