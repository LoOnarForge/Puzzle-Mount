using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class ForgottenGate : MonoBehaviour
{
    private const int SocketCount = 4;
    private const float MinPortalScaleDuration = 0.01f;

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

    [FormerlySerializedAs("portalSurface")]
    [SerializeField] private GameObject portalObject;
    [SerializeField] private float portalScaleDuration = 1f;

    [SerializeField] private bool isGateActive;
    [SerializeField] private bool levelCompleted;

    private Coroutine portalScaleCoroutine;
    private Collider portalTrigger;
    private CharacterController timController;
    private Vector3 portalOriginalLocalScale;

    private void Awake()
    {
        CachePortalDefaults();

        Debug.Assert(portalObject != null, $"{nameof(ForgottenGate)} on {name} requires a portal object.", this);
        Debug.Assert(portalTrigger != null, $"{nameof(ForgottenGate)} on {name} requires a collider on the portal object.", this);
        Debug.Assert(sockets != null && sockets.Length == SocketCount, $"{nameof(ForgottenGate)} on {name} requires {SocketCount} socket slots.", this);

        SetPortalHidden();
        InitialSocketConfiguration();
    }

    private void OnValidate()
    {
        portalScaleDuration = Mathf.Max(MinPortalScaleDuration, portalScaleDuration);
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
            StartPortalScaleIn();
        else if (!isGateActive && wasGateActive)
            StopPortalScaleIn();
    }

    private void CachePortalDefaults()
    {
        if (portalObject == null)
            return;

        portalOriginalLocalScale = portalObject.transform.localScale;
        portalTrigger = portalObject.GetComponent<Collider>();
        if (portalTrigger == null)
            portalTrigger = portalObject.GetComponentInChildren<Collider>();

        if (portalTrigger != null)
            portalTrigger.isTrigger = true;
    }

    private void SetPortalHidden()
    {
        if (portalObject == null)
            return;

        portalObject.transform.localScale = Vector3.zero;
        portalObject.SetActive(false);
    }

    private void StartPortalScaleIn()
    {
        if (portalObject == null)
            return;

        if (portalScaleCoroutine != null)
            StopCoroutine(portalScaleCoroutine);

        portalObject.SetActive(true);
        portalObject.transform.localScale = Vector3.zero;
        portalScaleCoroutine = StartCoroutine(PortalScaleInRoutine());
    }

    private void StopPortalScaleIn()
    {
        if (portalScaleCoroutine != null)
        {
            StopCoroutine(portalScaleCoroutine);
            portalScaleCoroutine = null;
        }

        SetPortalHidden();
    }

    private void TryDetectTimInPortal()
    {
        if (levelCompleted || portalTrigger == null || timController == null)
            return;

        if (!portalObject.activeInHierarchy)
            return;

        if (!portalTrigger.bounds.Intersects(timController.bounds))
            return;

        levelCompleted = true;
        Debug.Log("Level completed.");
    }

    private IEnumerator PortalScaleInRoutine()
    {
        float elapsed = 0f;

        while (elapsed < portalScaleDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / portalScaleDuration);
            float eased = EaseOutCubic(t);
            portalObject.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, portalOriginalLocalScale, eased);
            yield return null;
        }

        portalObject.transform.localScale = portalOriginalLocalScale;
        portalScaleCoroutine = null;
    }

    private static float EaseOutCubic(float t)
    {
        float oneMinusT = 1f - t;
        return 1f - oneMinusT * oneMinusT * oneMinusT;
    }
}
