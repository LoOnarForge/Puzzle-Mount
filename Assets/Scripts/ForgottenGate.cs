using System.Collections;
using UnityEngine;

public class ForgottenGate : MonoBehaviour
{
    private const int SocketCount = 4;

    [System.Serializable]
    private class SocketSlot
    {
        public GameObject socketObject;
        public bool isEnabled;
        public int requiredColorIndex;
        public int requiredMw = 1;
        public int allocatedMw;
        public PowerSource poweringSource;
    }

    [SerializeField] private SocketSlot[] sockets = new SocketSlot[SocketCount]
    {
        new SocketSlot(),
        new SocketSlot(),
        new SocketSlot(),
        new SocketSlot()
    };

    [SerializeField] private SpriteRenderer portalSurface;
    [SerializeField] private float pulseSpeed = 2f;
    [SerializeField] private float minAlpha = 0.2f;
    [SerializeField] private float maxAlpha = 0.9f;
    [SerializeField] private float scalePulseAmount = 0.06f;
    [SerializeField] private float bloomMin = 0.75f;
    [SerializeField] private float bloomMax = 1.25f;

    [SerializeField] private bool isGateActive;
    [SerializeField] private bool levelCompleted;

    private Coroutine portalPulseCoroutine;
    private Collider portalTrigger;
    private CharacterController timController;
    private Vector3 portalBaseScale;
    private Color portalBaseColor;

    private void Awake()
    {
        CachePortalDefaults();

        Debug.Assert(portalSurface != null, $"{nameof(ForgottenGate)} on {name} requires a portal surface.", this);
        Debug.Assert(portalTrigger != null, $"{nameof(ForgottenGate)} on {name} requires a collider on the portal surface.", this);
        Debug.Assert(sockets != null && sockets.Length == SocketCount, $"{nameof(ForgottenGate)} on {name} requires {SocketCount} socket slots.", this);
        
        SetPortalHidden();
        InitialSocketConfiguration();
    }

    private void Start()
    {
        CharacterMovement tim = FindAnyObjectByType<CharacterMovement>();
        if (tim != null)
            timController = tim.GetComponent<CharacterController>();
    }

    private void Update()
    {
        TryDetectTimInPortal();
    }

    // Called by a DevicePowerSocket when its power state changes.
    public void UpdateSocketStateToOwner(int ownerIndex, int allocatedMw, PowerSource poweringSource)
    {
        if (ownerIndex < 0 || ownerIndex >= sockets.Length)
            return;

        sockets[ownerIndex].allocatedMw = allocatedMw;
        sockets[ownerIndex].poweringSource = poweringSource;
        RefreshGateState();
    }

    // Claims assigned sockets once and pushes required color and MW to each.
    private void InitialSocketConfiguration()
    {
        for (int i = 0; i < sockets.Length; i++)
        {
            SocketSlot slot = sockets[i];

            if (slot.isEnabled)
                Debug.Assert(slot.socketObject != null, $"{nameof(ForgottenGate)} on {name} has an enabled socket with no socket object assigned.", this);

            if (slot.socketObject == null)
                continue;

            if (!slot.isEnabled)
            {
                DevicePowerSocket.SetSocketHierarchyActive(slot.socketObject, false);
                slot.allocatedMw = 0;
                slot.poweringSource = null;
                continue;
            }

            DevicePowerSocket device = slot.socketObject.GetComponent<DevicePowerSocket>();
            Debug.Assert(device != null, $"{nameof(ForgottenGate)} on {name} socket object requires a {nameof(DevicePowerSocket)}.", this);

            DevicePowerSocket.SetSocketHierarchyActive(slot.socketObject, true);
            device.InitialSocketConfiguration(i, slot.requiredColorIndex, slot.requiredMw);
        }

        RefreshGateState();
    }

    private void RefreshGateState()
    {
        bool wasGateActive = isGateActive;
        bool hasEnabledSocket = false;
        bool allPowered = true;

        foreach (SocketSlot slot in sockets)
        {
            if (!slot.isEnabled || slot.socketObject == null)
                continue;

            hasEnabledSocket = true;

            if (slot.allocatedMw < slot.requiredMw)
                allPowered = false;
        }

        isGateActive = hasEnabledSocket && allPowered;

        if (isGateActive && !wasGateActive)
            StartPortalPulse();
        else if (!isGateActive && wasGateActive)
            StopPortalPulse();
    }

    private void CachePortalDefaults()
    {
        if (portalSurface == null)
            return;

        portalBaseScale = portalSurface.transform.localScale;
        portalBaseColor = portalSurface.color;
        portalTrigger = portalSurface.GetComponent<Collider>();

        if (portalTrigger != null)
            portalTrigger.isTrigger = true;
    }

    private void SetPortalHidden()
    {
        if (portalSurface == null)
            return;

        portalSurface.transform.localScale = portalBaseScale;
        portalSurface.color = portalBaseColor;
        portalSurface.gameObject.SetActive(false);
    }

    private void StartPortalPulse()
    {
        if (portalSurface == null)
            return;

        if (portalPulseCoroutine != null)
            StopCoroutine(portalPulseCoroutine);

        portalSurface.gameObject.SetActive(true);
        portalSurface.enabled = true;
        portalPulseCoroutine = StartCoroutine(PortalPulseLoop());
    }

    private void StopPortalPulse()
    {
        if (portalPulseCoroutine != null)
        {
            StopCoroutine(portalPulseCoroutine);
            portalPulseCoroutine = null;
        }

        SetPortalHidden();
    }

    private void TryDetectTimInPortal()
    {
        if (levelCompleted || portalTrigger == null || timController == null)
            return;

        if (!portalSurface.gameObject.activeInHierarchy)
            return;

        if (!portalTrigger.bounds.Intersects(timController.bounds))
            return;

        levelCompleted = true;
        Debug.Log("Level completed.");
    }

    private IEnumerator PortalPulseLoop()
    {
        while (true)
        {
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(minAlpha, maxAlpha, pulse);
            float bloom = Mathf.Lerp(bloomMin, bloomMax, pulse);
            float scaleMultiplier = 1f + Mathf.Lerp(-scalePulseAmount, scalePulseAmount, pulse);

            Color color = portalBaseColor;
            color.r = Mathf.Min(color.r * bloom, 1f);
            color.g = Mathf.Min(color.g * bloom, 1f);
            color.b = Mathf.Min(color.b * bloom, 1f);
            color.a = alpha;

            portalSurface.color = color;
            portalSurface.transform.localScale = portalBaseScale * scaleMultiplier;

            yield return null;
        }
    }
}
