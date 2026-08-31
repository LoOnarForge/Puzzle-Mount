using UnityEngine;

public class RuneTorch : MonoBehaviour
{
    private const int SocketIndex = 0;
    private const float FirstStepIntensityBonus = 0.5f;
    private const float AdditionalIntensityBonusPerMw = 0.25f;

    [SerializeField] private GameObject powerSocketObject;
    [SerializeField] private Light pointLight;
    [SerializeField] private int maxPower = 4;

    [SerializeField] private int allocatedMw;
    [SerializeField] private PowerSource poweringSource;
    [SerializeField] private bool isLit;

    private float baseRange;
    private float baseIntensity;
    private DevicePowerSocket devicePowerSocket;
    private LinePort socketPort;
    private Color deliveredPowerColor = Color.white;

    private void Awake()
    {
        Debug.Assert(powerSocketObject != null, $"{nameof(RuneTorch)} on {name} requires a power socket object.", this);
        Debug.Assert(pointLight != null, $"{nameof(RuneTorch)} on {name} requires a point light.", this);
        Debug.Assert(maxPower >= 1, $"{nameof(RuneTorch)} on {name} requires max power of at least 1.", this);

        devicePowerSocket = powerSocketObject.GetComponent<DevicePowerSocket>();
        socketPort = powerSocketObject.GetComponent<LinePort>();
        Debug.Assert(devicePowerSocket != null, $"{nameof(RuneTorch)} on {name} requires a {nameof(DevicePowerSocket)} on the power socket object.", this);
        Debug.Assert(socketPort != null, $"{nameof(RuneTorch)} on {name} requires a {nameof(LinePort)} on the power socket object.", this);

        baseRange = pointLight.range;
        baseIntensity = pointLight.intensity;

        InitialSocketConfiguration();
        RefreshLightState();
    }

    private void LateUpdate()
    {
        RefreshSocketConnection();
        SyncLightFromSocket();
    }

    // Called by a DevicePowerSocket when its power state changes.
    public void UpdateSocketStateToOwner(int ownerIndex, int newAllocatedMw, PowerSource newPoweringSource)
    {
        if (ownerIndex != SocketIndex)
            return;

        allocatedMw = newAllocatedMw;
        poweringSource = newPoweringSource;
        deliveredPowerColor = ResolveDeliveredColor(newPoweringSource);
        RefreshLightState();
    }

    // Pushes max MW cap to the assigned socket once at startup.
    private void InitialSocketConfiguration()
    {
        if (devicePowerSocket == null)
            return;

        devicePowerSocket.InitialSocketConfiguration(SocketIndex, 0, maxPower);
    }

    // Device socket ports are not refreshed by RunodeLine; detect disconnects here.
    private void RefreshSocketConnection()
    {
        if (socketPort == null)
            return;

        socketPort.RefreshPortObstructionState();
        socketPort.RefreshPortConnection();
    }

    private void SyncLightFromSocket()
    {
        if (devicePowerSocket == null)
            return;

        int socketMw = devicePowerSocket.AllocatedMw;
        if (socketMw == allocatedMw)
            return;

        allocatedMw = socketMw;

        if (allocatedMw <= 0)
            poweringSource = null;

        RefreshLightState();
    }

    private static Color ResolveDeliveredColor(PowerSource source)
    {
        if (source == null || ColorManager.Instance == null)
            return Color.white;

        return ColorManager.Instance.GetColor(source.ColorIndex);
    }

    private void RefreshLightState()
    {
        isLit = allocatedMw >= 1;

        if (!isLit)
        {
            pointLight.enabled = false;
            return;
        }

        pointLight.enabled = true;
        pointLight.color = deliveredPowerColor;
        pointLight.range = baseRange * GetRangeMultiplier(allocatedMw);
        pointLight.intensity = baseIntensity * GetIntensityMultiplier(allocatedMw);
    }

    private static float GetRangeMultiplier(int mw)
    {
        return mw;
    }

    private static float GetIntensityMultiplier(int mw)
    {
        if (mw <= 1)
            return 1f;

        return 1f + FirstStepIntensityBonus + (mw - 2) * AdditionalIntensityBonusPerMw;
    }
}
