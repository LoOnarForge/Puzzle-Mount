using System.Collections;
using UnityEngine;

public class DevicePowerSocket : MonoBehaviour
{
    private const float EmissionStrength = 2f;
    private const float PoweredEmissionMultiplier = 1f;
    private const float MinBrightness = 0f;
    private const float LightInfluence = 1f;

    private static readonly Color NeutralColor = Color.white;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int MinBrightnessId = Shader.PropertyToID("_MinBrightness");
    private static readonly int LightInfluenceId = Shader.PropertyToID("_LightInfluence");

    [Header("SOCKET:")]
    [SerializeField] private int colorIndex;
    [SerializeField] private int requiredMw = 1;

    [Header("VISUAL:")]
    [SerializeField] private GameObject socketSpriteObject;

    [Space(20)]
    [Header("STATE:")]
    [SerializeField] private int allocatedMw;
    [SerializeField] private PowerSource powerSource;
    [SerializeField] private RunodeLine poweredByLine;
    [SerializeField] private LinePort givingLinePort;
    [SerializeField] private int powerIndex = -1;
    [SerializeField] private Color powerColor;

    private LinePort port;
    private SpriteRenderer socketSprite;
    private MaterialPropertyBlock propBlock;
    private float powerPropagationDelay;
    private Coroutine clearPowerCoroutine;
    private int ownerIndex = -1;

    public int AllocatedMw => allocatedMw;
    public int RemainingMwNeeded => requiredMw - allocatedMw;
    public int PowerIndex => powerIndex;

    [Space (20)]
    [SerializeField] private ForgottenGate ownerForgottenGate;
    [SerializeField] private Lever ownerLever;
    [SerializeField] private Elevator ownerElevator;
    [SerializeField] private Piston ownerPiston;


    private void Awake()
    {
        port = GetComponent<LinePort>();

        if (socketSpriteObject != null)
            socketSprite = socketSpriteObject.GetComponent<SpriteRenderer>();

        Debug.Assert(port != null, $"{nameof(DevicePowerSocket)} on {name} requires a {nameof(LinePort)} on the same object.", this);
        Debug.Assert(socketSprite != null, $"{nameof(DevicePowerSocket)} on {name} requires a {nameof(SpriteRenderer)} on the assigned socket sprite object.", this);
        Debug.Assert(AssignAnOwner(), $"{nameof(DevicePowerSocket)} on {name} requires an owner device.", this);
        Debug.Assert(GameConfig.Instance != null, $"{nameof(DevicePowerSocket)} on {name} requires a {nameof(GameConfig)} in the scene.", this);

        powerPropagationDelay = GameConfig.Instance.PowerPropagationDelay;
        propBlock = new MaterialPropertyBlock();
        port.ParentDevicePowerSocket = this;
        port.SetPortType(PortType.Receiver);
        ApplyDepoweredVisual();
    }

    // Colors the socket sprite with the delivered power color once at least 1 MW is held.
    public void ApplyPoweredVisual()
    {
        if (socketSprite == null || allocatedMw < 1)
            return;

        ApplyVisualState(powerColor, true);
    }

    // Resets the socket sprite to white when the socket has no power.
    public void ApplyDepoweredVisual()
    {
        if (socketSprite == null)
            return;

        ApplyVisualState(NeutralColor, false);
    }

    private void ApplyVisualState(Color baseColor, bool powered)
    {
        socketSprite.GetPropertyBlock(propBlock);
        propBlock.SetColor(BaseColorId, baseColor);

        if (powered)
        {
            propBlock.SetColor(EmissionColorId, powerColor * PoweredEmissionMultiplier);
            propBlock.SetFloat(EmissionStrengthId, EmissionStrength);
        }
        else
        {
            propBlock.SetColor(EmissionColorId, Color.black);
            propBlock.SetFloat(EmissionStrengthId, 0f);
        }

        propBlock.SetFloat(MinBrightnessId, MinBrightness);
        propBlock.SetFloat(LightInfluenceId, LightInfluence);
        socketSprite.SetPropertyBlock(propBlock);
    }

    // Sets socket index, required color, and required MW from the owning device. Called once.
    public void InitialSocketConfiguration(int index, int color, int mw)
    {
        ownerIndex = index;
        colorIndex = color;
        requiredMw = mw;
        UpdateSocketStateChangeToOwner();
    }

    // True when the Power Source colour matches this socket's required colour.
    public bool CheckIfCorrectPowerColor(PowerSource source)
    {
        return source != null && source.ColorIndex == colorIndex;
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
                givingLinePort = givingPort;
                powerColor = color;
                powerIndex = index;
            }
        }

        if (allocatedMw >= 1)
            ApplyPoweredVisual();

        UpdateSocketStateChangeToOwner();
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
            givingLinePort = null;
            powerColor = Color.white;
            powerIndex = -1;
            ApplyDepoweredVisual();
        }

        givingLine?.RefreshFaceAndPortsStates();
        UpdateSocketStateChangeToOwner();
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
            givingLinePort = null;
            powerColor = Color.white;
            powerIndex = -1;
            ApplyDepoweredVisual();
            UpdateSocketStateChangeToOwner();
            return;
        }

        poweredByLine = null;
        givingLinePort = null;
        UpdateSocketStateChangeToOwner();

        if (powerSource != null)
        {
            powerSource.RemoveWaitingEntriesForSocket(this);
            powerSource.RemoveCircuitMember(this);
        }

        clearPowerCoroutine = StartCoroutine(ClearPowerSequence());
    }

    // Tells the owning device that this socket's power state changed.
    private void UpdateSocketStateChangeToOwner()
    {
        if (ownerIndex < 0)
            return;

        if (ownerForgottenGate != null)
        {
            ownerForgottenGate.UpdateSocketStateToOwner(ownerIndex, allocatedMw, powerSource);
            return;
        }

        if (ownerLever != null)
        {
            ownerLever.UpdateSocketStateToOwner(ownerIndex, allocatedMw, powerSource);
            return;
        }

        if (ownerElevator != null)
        {
            ownerElevator.UpdateSocketStateToOwner(ownerIndex, allocatedMw, powerSource);
            return;
        }

        if (ownerPiston != null)
        {
            ownerPiston.UpdateSocketStateToOwner(ownerIndex, allocatedMw, powerSource);
            return;
        }
    }

    private bool AssignAnOwner()
    {
        return ownerForgottenGate != null || ownerLever != null || ownerElevator != null || ownerPiston != null;
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
