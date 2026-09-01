using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class DevicePowerSocket : MonoBehaviour
{
    [Header("SOCKET:")]
    [SerializeField] private int colorIndex;
    [SerializeField] private int requiredMw = 1;
   
    [Space(20)]
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
    [Space(20)]
    [SerializeField] private ForgottenGate ownerForgottenGate;
    [SerializeField] private Lever ownerLever;
    [SerializeField] private Elevator ownerElevator;
    [SerializeField] private Piston ownerPiston;
    [FormerlySerializedAs("ownerRuneTorch")]
    [SerializeField] private TorchRunode ownerTorchRunode;

    private LinePort port;
    private SpriteRenderer socketSprite;
    private MaterialPropertyBlock propBlock;
    private float powerPropagationDelay;
    private Coroutine clearPowerCoroutine;
    private int ownerIndex = -1;


    private const float EmissionStrength = 2f;
    private const float MinBrightness = 0f;
    private const float LightInfluence = 1f;


    private static readonly Color NeutralColor = Color.white;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int MinBrightnessId = Shader.PropertyToID("_MinBrightness");
    private static readonly int LightInfluenceId = Shader.PropertyToID("_LightInfluence");


    public int AllocatedMw => allocatedMw;
    public int RequiredColorIndex => colorIndex;
    public int RemainingMwNeeded => requiredMw - allocatedMw;
    public int PowerIndex => powerIndex;


    private void Awake()
    {
        port = GetComponent<LinePort>();

        if (socketSpriteObject != null)
            socketSprite = socketSpriteObject.GetComponent<SpriteRenderer>();

        powerPropagationDelay = GameConfig.Instance.PowerPropagationDelay;
        propBlock = new MaterialPropertyBlock();
        port.ParentDevicePowerSocket = this;
        port.SetPortType(PortType.Receiver);
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (socketSprite == null)
            return;

        bool powered = allocatedMw >= 1;
        ApplyVisualState(powered ? powerColor : NeutralColor, powered);
    }

    private void ApplyVisualState(Color baseColor, bool powered)
    {
        socketSprite.GetPropertyBlock(propBlock);
        propBlock.SetColor(BaseColorId, baseColor);

        if (powered)
        {
            propBlock.SetColor(EmissionColorId, powerColor);
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

    // Enables or disables the socket hierarchy root (parent one level up, or the DPS object if none).
    public static void SetSocketHierarchyActive(GameObject socketObject, bool active)
    {
        if (socketObject == null)
            return;

        Transform parent = socketObject.transform.parent;
        if (parent != null)
            parent.gameObject.SetActive(active);
        else
            socketObject.SetActive(active);
    }

    // Sets socket index, required color, and required MW from the owning device. Called once.
    public void InitialSocketConfiguration(int index, int color, int mw)
    {
        ownerIndex = index;
        colorIndex = color;
        requiredMw = mw;
        UpdateSocketStateChangeToOwner();
    }

    // Takes MW from the giver's Power Source 1 at a time until full or the pool is empty.
    public bool TryReceivePower(RunodeLine givingLine, LinePort givingPort, PowerSource source, Color color, int index)
    {
        StopClearPowerCoroutine();

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

        RefreshVisual();

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
            ResetDepoweredState();
        }

        givingLine?.RefreshFaceAndPortsStates();
        UpdateSocketStateChangeToOwner();
    }

    // Returns all held MW one at a time, waiting PowerPropagationDelay between each.
    public void ClearPower()
    {
        StopClearPowerCoroutine();

        if (allocatedMw <= 0)
        {
            if (powerSource != null)
            {
                powerSource.RemoveWaitingEntriesForSocket(this);
                powerSource.RemoveCircuitMember(this);
            }

            ResetDepoweredState();
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

        if (ownerTorchRunode != null)
        {
            ownerTorchRunode.UpdateSocketStateToOwner(ownerIndex, allocatedMw, powerSource);
            return;
        }
    }

    private void StopClearPowerCoroutine()
    {
        if (clearPowerCoroutine == null)
            return;

        StopCoroutine(clearPowerCoroutine);
        clearPowerCoroutine = null;
    }

    private void ResetDepoweredState()
    {
        powerSource = null;
        poweredByLine = null;
        givingLinePort = null;
        powerColor = Color.white;
        powerIndex = -1;
        RefreshVisual();
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
