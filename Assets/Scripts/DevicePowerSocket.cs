using System.Collections;
using UnityEngine;

public class DevicePowerSocket : MonoBehaviour
{
    [Header("SOCKET:")]
    [SerializeField] private LinePort port;
    [SerializeField] private int colorIndex;
    [SerializeField] private int requiredMw = 1;

    [Space(20)]
    [Header("STATE:")]
    [SerializeField] private int allocatedMw;
    [SerializeField] private PowerSource powerSource;
    [SerializeField] private RunodeLine poweredByLine;
    [SerializeField] private LinePort linePort;
    [SerializeField] private int powerIndex = -1;
    [SerializeField] private Color powerColor;

    private float powerPropagationDelay;
    private Coroutine clearPowerCoroutine;

    public int ColorIndex => colorIndex;
    public int RequiredMw => requiredMw;
    public int AllocatedMw => allocatedMw;
    public int RemainingMwNeeded => requiredMw - allocatedMw;
    public int PowerIndex => powerIndex;
    public PowerSource PowerSource => powerSource;
    public RunodeLine PoweredByLine => poweredByLine;

    private void Awake()
    {
        Debug.Assert(port != null, $"{nameof(DevicePowerSocket)} on {name} requires a line port.", this);
        Debug.Assert(GameConfig.Instance != null, $"{nameof(DevicePowerSocket)} on {name} requires a {nameof(GameConfig)} in the scene.", this);

        powerPropagationDelay = GameConfig.Instance.PowerPropagationDelay;
        port.ParentDevicePowerSocket = this;
        port.SetPortType(PortType.Receiver);
    }

    // True when this socket may take MW from that Power Source and that giver.
    public bool CanReceivePowerFrom(PowerSource source, RunodeLine givingLine)
    {
        if (source == null || source.ColorIndex != colorIndex)
            return false;

        return poweredByLine == null || poweredByLine == givingLine;
    }

    // Takes MW from the giver's Power Source 1 at a time until full or the pool is empty.
    public bool TryReceivePower(RunodeLine givingLine, LinePort givingPort, PowerSource source, Color color, int index)
    {
        if (clearPowerCoroutine != null)
        {
            StopCoroutine(clearPowerCoroutine);
            clearPowerCoroutine = null;
        }

        while (allocatedMw < requiredMw)
        {
            if (!source.TakeMW())
                break;

            allocatedMw++;

            if (allocatedMw == 1)
            {
                poweredByLine = givingLine;
                powerSource = source;
                linePort = givingPort;
                powerColor = color;
                powerIndex = index;
            }
        }

        return allocatedMw >= requiredMw;
    }

    // Returns one held MW, then lets the giving face re-evaluate propagation. Used by arbitration and by staggered full clear.
    public void ReleaseOneMw()
    {
        if (allocatedMw <= 0 || powerSource == null)
            return;

        RunodeLine givingLine = poweredByLine;

        allocatedMw--;
        PowerSource source = powerSource;
        source.ReturnMW();

        if (allocatedMw <= 0)
        {
            source.RemoveCircuitMember(this);
            powerSource = null;
            poweredByLine = null;
            linePort = null;
            powerColor = Color.white;
            powerIndex = -1;
        }

        givingLine?.RefreshFaceAndPortsStates();
    }

    // Returns all held MW one at a time, waiting PowerPropagationDelay between each.
    public void ClearPower()
    {
        if (clearPowerCoroutine != null)
        {
            StopCoroutine(clearPowerCoroutine);
            clearPowerCoroutine = null;
        }

        if (allocatedMw <= 0)
        {
            if (powerSource != null)
            {
                powerSource.RemoveWaitingEntriesForSocket(this);
                powerSource.RemoveCircuitMember(this);
                powerSource = null;
            }

            poweredByLine = null;
            linePort = null;
            powerColor = Color.white;
            powerIndex = -1;
            return;
        }

        poweredByLine = null;
        linePort = null;

        if (powerSource != null)
        {
            powerSource.RemoveWaitingEntriesForSocket(this);
            powerSource.RemoveCircuitMember(this);
        }

        clearPowerCoroutine = StartCoroutine(ClearPowerSequence());
    }

    private IEnumerator ClearPowerSequence()
    {
        while (allocatedMw > 0)
        {
            ReleaseOneMw();

            if (allocatedMw > 0)
                yield return new WaitForSeconds(powerPropagationDelay);
        }

        clearPowerCoroutine = null;
    }
}
