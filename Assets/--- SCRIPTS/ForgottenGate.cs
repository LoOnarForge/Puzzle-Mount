using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ForgottenGate : MonoBehaviour
{
    private const int SocketCount = 4;
    private const int SpawnTransformCount = 4;
    private const int ColorPrefabCount = 6;
    private const float MinPortalTimer = 0f;
    private const float MaxPortalTimer = 5f;
    private const float MinPortalLightIntensityFraction = 0.2f;

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

    [Header("COLOR DECOR")]
    [SerializeField] private Transform[] spawnTransforms = new Transform[SpawnTransformCount];
    [SerializeField] private GameObject[] colorPrefabs = new GameObject[ColorPrefabCount];

    [FormerlySerializedAs("portalSurface")]
    [SerializeField] private GameObject portalObject;
    [SerializeField] private Light portalLight;

    [FormerlySerializedAs("portalScaleDuration")]
    [SerializeField] [Range(MinPortalTimer, MaxPortalTimer)] private float powerUpTimer = 1f;
    [SerializeField] [Range(MinPortalTimer, MaxPortalTimer)] private float depowerTimer = 1f;

    [SerializeField] private bool isGateActive;
    [SerializeField] private bool levelCompleted;

    private Coroutine portalScaleCoroutine;
    private Collider portalTrigger;
    private CharacterController timController;
    private Vector3 portalOriginalLocalScale;
    private float activePortalLightIntensity;

    private void Awake()
    {
        CachePortalDefaults();
        CachePortalLightBaseline();

        Debug.Assert(portalObject != null, $"{nameof(ForgottenGate)} on {name} requires a portal object.", this);
        Debug.Assert(portalTrigger != null, $"{nameof(ForgottenGate)} on {name} requires a collider on the portal object.", this);
        Debug.Assert(sockets != null && sockets.Length == SocketCount, $"{nameof(ForgottenGate)} on {name} requires {SocketCount} socket slots.", this);

        SetPortalHidden();
        InitialSocketConfiguration();
        SpawnColorDecor();
    }

    private void OnValidate()
    {
        powerUpTimer = Mathf.Clamp(powerUpTimer, MinPortalTimer, MaxPortalTimer);
        depowerTimer = Mathf.Clamp(depowerTimer, MinPortalTimer, MaxPortalTimer);
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

    // Spawns one decor prefab per enabled socket at a random spawn transform (ColorManager index order).
    private void SpawnColorDecor()
    {
        if (spawnTransforms == null || colorPrefabs == null)
            return;

        List<int> shuffledTransformIndices = BuildShuffledSpawnTransformIndices();
        if (shuffledTransformIndices.Count == 0)
            return;

        int transformPick = 0;

        for (int i = 0; i < sockets.Length; i++)
        {
            SocketSlot slot = sockets[i];
            if (!slot.isEnabled)
                continue;

            int colorIndex = slot.requiredColorIndex;
            if (colorIndex < 0 || colorIndex >= colorPrefabs.Length)
                continue;

            GameObject prefab = colorPrefabs[colorIndex];
            if (prefab == null)
                continue;

            Transform spawnPoint = spawnTransforms[shuffledTransformIndices[transformPick % shuffledTransformIndices.Count]];
            transformPick++;

            if (spawnPoint == null)
                continue;

            Instantiate(prefab, spawnPoint.position, spawnPoint.rotation, spawnPoint);
        }
    }

    private List<int> BuildShuffledSpawnTransformIndices()
    {
        List<int> indices = new List<int>(SpawnTransformCount);

        for (int i = 0; i < spawnTransforms.Length && i < SpawnTransformCount; i++)
        {
            if (spawnTransforms[i] != null)
                indices.Add(i);
        }

        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        return indices;
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
            StartPortalScaleOut();
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

    private void CachePortalLightBaseline()
    {
        if (portalLight == null)
            return;

        activePortalLightIntensity = portalLight.intensity;
    }

    // 0 = minimum portal light (20% of baseline), 1 = fully active portal (100% of baseline).
    private void SetPortalLightActivation(float activation)
    {
        if (portalLight == null)
            return;

        float clampedActivation = Mathf.Clamp01(activation);
        float intensityFraction = Mathf.Lerp(MinPortalLightIntensityFraction, 1f, clampedActivation);
        portalLight.intensity = activePortalLightIntensity * intensityFraction;
    }

    private void SetPortalHidden()
    {
        if (portalObject == null)
            return;

        portalObject.transform.localScale = Vector3.zero;
        portalObject.SetActive(false);
        SetPortalLightActivation(0f);
    }

    private void StartPortalScaleIn()
    {
        if (portalObject == null)
            return;

        if (portalScaleCoroutine != null)
            StopCoroutine(portalScaleCoroutine);

        portalObject.SetActive(true);

        if (powerUpTimer <= MinPortalTimer)
        {
            portalObject.transform.localScale = portalOriginalLocalScale;
            SetPortalLightActivation(1f);
            portalScaleCoroutine = null;
            return;
        }

        portalObject.transform.localScale = Vector3.zero;
        SetPortalLightActivation(0f);
        portalScaleCoroutine = StartCoroutine(PortalScaleInRoutine());
    }

    private void StartPortalScaleOut()
    {
        if (portalObject == null)
            return;

        if (portalScaleCoroutine != null)
            StopCoroutine(portalScaleCoroutine);

        if (!portalObject.activeInHierarchy)
            return;

        if (depowerTimer <= MinPortalTimer)
        {
            SetPortalHidden();
            portalScaleCoroutine = null;
            return;
        }

        portalScaleCoroutine = StartCoroutine(PortalScaleOutRoutine());
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

        while (elapsed < powerUpTimer)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / powerUpTimer);
            float eased = EaseOutCubic(t);
            portalObject.transform.localScale = Vector3.LerpUnclamped(Vector3.zero, portalOriginalLocalScale, eased);
            SetPortalLightActivation(eased);
            yield return null;
        }

        portalObject.transform.localScale = portalOriginalLocalScale;
        SetPortalLightActivation(1f);
        portalScaleCoroutine = null;
    }

    private IEnumerator PortalScaleOutRoutine()
    {
        Vector3 startScale = portalObject.transform.localScale;
        float elapsed = 0f;

        while (elapsed < depowerTimer)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / depowerTimer);
            portalObject.transform.localScale = Vector3.LerpUnclamped(startScale, Vector3.zero, t);
            SetPortalLightActivation(1f - t);
            yield return null;
        }

        SetPortalHidden();
        portalScaleCoroutine = null;
    }

    private static float EaseOutCubic(float t)
    {
        float oneMinusT = 1f - t;
        return 1f - oneMinusT * oneMinusT * oneMinusT;
    }
}
