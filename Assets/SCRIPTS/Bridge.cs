using System.Collections.Generic;
using UnityEngine;

public class Bridge : MonoBehaviour
{
    private const int SocketIndex = 0;
    private const float GridUnit = 1f;
    private const float PlatformDepthInCenterSpan = 1f;
    private const float ColliderDisableDelay = 0.2f;
    private const float SettleDurationShare = 0.5f;
    private const float GrowEasePower = 2f;
    private const float ShrinkEasePower = 2.5f;
    private const float MinPopSpeed = 0.01f;
    private const float SamePositionLimit = 0.0001f;

    [System.Serializable]
    private class SocketSlot
    {
        public GameObject powerSocket;
        public int requiredColorIndex;
        public int requiredMw = 1;
        public int allocatedMw;
        public PowerSource poweringSource;
    }

    private class GapCube
    {
        public Transform visual;
        public int columnFromStart;
        public float delay;
        public Vector3 startScale;
    }

    [SerializeField] private SocketSlot socket = new SocketSlot();
    [SerializeField] private Transform startPlatform;
    [SerializeField] private Transform endPlatform;
    [SerializeField] private GameObject gapColliderObject;
    [SerializeField] private GameObject cubePrefab;

    [SerializeField, Range(1f, 40f)] private float cubePopSpeed = 10f;
    [SerializeField, Range(0f, 50f)] private float overshootPercent = 10f;
    [SerializeField, Range(0f, 1f)] private float columnWaveDelay = 0.04f;
    [SerializeField, Range(0f, 0.5f)] private float cubeRandomDelay = 0.03f;

    [SerializeField] private bool isPowered;

    private readonly List<GapCube> gapCubes = new List<GapCube>();

    private BoxCollider gapCollider;
    private Coroutine sequenceRoutine;
    private Vector3 lengthDirection;
    private Vector3 widthDirection;
    private int lengthCount;
    private int widthCount;
    private int sequenceId;
    private bool hasValidLayout;
    private bool isBridgeOpen;
    private bool isSequenceRunning;
    private bool isClosing;

    private void Awake()
    {
        hasValidLayout = TryBuildLayout();

        if (hasValidLayout)
        {
            ApplyColliderShape();
            SetGapColliderEnabled(false);
        }

        InitialSocketConfiguration();
    }

    // Called by a DevicePowerSocket when its power state changes.
    public void UpdateSocketStateToOwner(int ownerIndex, int allocatedMw, PowerSource poweringSource)
    {
        if (ownerIndex != SocketIndex)
            return;

        socket.allocatedMw = allocatedMw;
        socket.poweringSource = poweringSource;
        RefreshBridgePower();
    }

    // Called by a powered Lever when the player operates it.
    public void OnLeverOperated()
    {
        if (!hasValidLayout || !isPowered || isSequenceRunning)
            return;

        if (isBridgeOpen)
            BeginClose();
        else
            BeginOpen();
    }

    // Claims the assigned socket once and pushes required color and MW to it.
    private void InitialSocketConfiguration()
    {
        if (socket.powerSocket == null)
        {
            Debug.Assert(false, $"{nameof(Bridge)} on {name} requires a power socket.", this);
            return;
        }

        DevicePowerSocket device = socket.powerSocket.GetComponent<DevicePowerSocket>();
        Debug.Assert(device != null, $"{nameof(Bridge)} on {name} requires a {nameof(DevicePowerSocket)} on the assigned power socket.", this);
        if (device == null)
            return;

        device.SetBridgeOwner(this);
        device.InitialSocketConfiguration(SocketIndex, socket.requiredColorIndex, socket.requiredMw);
    }

    private void RefreshBridgePower()
    {
        bool poweredNow = socket.allocatedMw >= socket.requiredMw;
        bool lostPower = isPowered && !poweredNow;
        isPowered = poweredNow;

        if (lostPower)
            BeginClose();
    }

    private bool TryBuildLayout()
    {
        Debug.Assert(startPlatform != null, $"{nameof(Bridge)} on {name} requires a start platform.", this);
        Debug.Assert(endPlatform != null, $"{nameof(Bridge)} on {name} requires an end platform.", this);
        Debug.Assert(gapColliderObject != null, $"{nameof(Bridge)} on {name} requires a gap collider object.", this);
        Debug.Assert(cubePrefab != null, $"{nameof(Bridge)} on {name} requires a cube prefab.", this);

        if (startPlatform == null || endPlatform == null || gapColliderObject == null || cubePrefab == null)
            return false;

        Debug.Assert((transform.lossyScale - Vector3.one).sqrMagnitude < SamePositionLimit, $"{nameof(Bridge)} on {name} must stay at scale 1,1,1.", this);
        Debug.Assert((startPlatform.position - transform.position).sqrMagnitude < SamePositionLimit, $"{nameof(Bridge)} on {name} start platform must sit on the bridge object.", this);
        Debug.Assert(Mathf.Abs(startPlatform.position.y - endPlatform.position.y) < 0.01f, $"{nameof(Bridge)} on {name} start and end must be at the same height.", this);

        gapCollider = gapColliderObject.GetComponent<BoxCollider>();
        Debug.Assert(gapCollider != null, $"{nameof(Bridge)} on {name} gap collider object needs a {nameof(BoxCollider)}.", this);
        if (gapCollider == null)
            return false;

        Vector3 offset = endPlatform.position - startPlatform.position;
        offset.y = 0f;

        if (offset.sqrMagnitude < SamePositionLimit)
        {
            Debug.Assert(false, $"{nameof(Bridge)} on {name} end platform must be away from the start.", this);
            return false;
        }

        lengthDirection = offset.normalized;
        widthDirection = Vector3.Cross(Vector3.up, lengthDirection).normalized;

        int centerSteps = Mathf.RoundToInt(offset.magnitude / GridUnit);
        lengthCount = centerSteps - Mathf.RoundToInt(PlatformDepthInCenterSpan);
        widthCount = MeasureWidthCount();

        Debug.Assert(lengthCount >= 1, $"{nameof(Bridge)} on {name} gap is shorter than 1 metre.", this);
        Debug.Assert(widthCount >= 1, $"{nameof(Bridge)} on {name} start platform width is under 1 metre.", this);

        return lengthCount >= 1 && widthCount >= 1;
    }

    private int MeasureWidthCount()
    {
        Vector3 localX = startPlatform.TransformDirection(Vector3.right);
        Vector3 localZ = startPlatform.TransformDirection(Vector3.forward);
        bool lengthUsesLocalX = Mathf.Abs(Vector3.Dot(localX, lengthDirection)) > Mathf.Abs(Vector3.Dot(localZ, lengthDirection));
        Vector3 widthVector = lengthUsesLocalX
            ? startPlatform.TransformVector(Vector3.forward)
            : startPlatform.TransformVector(Vector3.right);

        return Mathf.RoundToInt(widthVector.magnitude);
    }

    private void ApplyColliderShape()
    {
        Vector3 center = (startPlatform.position + endPlatform.position) * 0.5f;
        center.y = startPlatform.position.y;

        Transform colliderTransform = gapCollider.transform;
        colliderTransform.SetPositionAndRotation(center, Quaternion.LookRotation(lengthDirection, Vector3.up));
        colliderTransform.localScale = Vector3.one;
        gapCollider.center = Vector3.zero;
        gapCollider.size = new Vector3(widthCount * GridUnit, GridUnit, lengthCount * GridUnit);
    }

    private void SetGapColliderEnabled(bool enabled)
    {
        gapCollider.enabled = enabled;
    }

    private void BeginOpen()
    {
        isSequenceRunning = true;
        isClosing = false;
        sequenceId++;

        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);

        sequenceRoutine = StartCoroutine(OpenSequence(sequenceId));
    }

    private void BeginClose()
    {
        if (isClosing || (!isSequenceRunning && !isBridgeOpen))
            return;

        isClosing = true;
        isSequenceRunning = true;
        sequenceId++;

        if (sequenceRoutine != null)
            StopCoroutine(sequenceRoutine);

        sequenceRoutine = StartCoroutine(CloseSequence(sequenceId));
    }

    private IEnumerator OpenSequence(int id)
    {
        SpawnGapCubes();
        ApplyColliderShape();
        SetGapColliderEnabled(true);

        float growDuration = 1f / Mathf.Max(cubePopSpeed, MinPopSpeed);
        float settleDuration = growDuration * SettleDurationShare;
        float overshootScale = 1f + overshootPercent * 0.01f;
        float longestDelay = AssignDelays(fromEnd: false);
        float totalDuration = longestDelay + growDuration + settleDuration;
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            if (id != sequenceId)
                yield break;

            elapsed += Time.deltaTime;

            for (int i = 0; i < gapCubes.Count; i++)
            {
                GapCube cube = gapCubes[i];
                if (cube.visual == null)
                    continue;

                float scale = SampleOpenScale(elapsed - cube.delay, growDuration, settleDuration, overshootScale);
                cube.visual.localScale = Vector3.one * scale;
            }

            yield return null;
        }

        if (id != sequenceId)
            yield break;

        for (int i = 0; i < gapCubes.Count; i++)
        {
            if (gapCubes[i].visual != null)
                gapCubes[i].visual.localScale = Vector3.one;
        }

        isBridgeOpen = true;
        isSequenceRunning = false;
        sequenceRoutine = null;
    }

    private IEnumerator CloseSequence(int id)
    {
        float shrinkDuration = 1f / Mathf.Max(cubePopSpeed, MinPopSpeed);

        for (int i = 0; i < gapCubes.Count; i++)
        {
            GapCube cube = gapCubes[i];
            cube.startScale = cube.visual != null ? cube.visual.localScale : Vector3.zero;
        }

        float longestDelay = AssignDelays(fromEnd: true);
        float totalDuration = Mathf.Max(ColliderDisableDelay, longestDelay + shrinkDuration);
        float elapsed = 0f;
        bool colliderDisabled = false;

        while (elapsed < totalDuration)
        {
            if (id != sequenceId)
                yield break;

            elapsed += Time.deltaTime;

            if (!colliderDisabled && elapsed >= ColliderDisableDelay)
            {
                SetGapColliderEnabled(false);
                colliderDisabled = true;
            }

            for (int i = 0; i < gapCubes.Count; i++)
            {
                GapCube cube = gapCubes[i];
                if (cube.visual == null)
                    continue;

                float time = elapsed - cube.delay;
                if (time <= 0f)
                    continue;

                float t = Mathf.Clamp01(time / shrinkDuration);
                float curved = 1f - Mathf.Pow(t, ShrinkEasePower);
                cube.visual.localScale = cube.startScale * curved;
            }

            yield return null;
        }

        if (id != sequenceId)
            yield break;

        SetGapColliderEnabled(false);
        ClearCubes();
        isBridgeOpen = false;
        isClosing = false;
        isSequenceRunning = false;
        sequenceRoutine = null;
    }

    private float AssignDelays(bool fromEnd)
    {
        float longestDelay = 0f;
        int lastColumn = lengthCount - 1;

        for (int i = 0; i < gapCubes.Count; i++)
        {
            GapCube cube = gapCubes[i];
            int column = fromEnd ? lastColumn - cube.columnFromStart : cube.columnFromStart;
            float delay = column * columnWaveDelay;

            if (cubeRandomDelay > 0f)
                delay += Random.Range(0f, cubeRandomDelay);

            cube.delay = delay;

            if (delay > longestDelay)
                longestDelay = delay;
        }

        return longestDelay;
    }

    private void SpawnGapCubes()
    {
        ClearCubes();

        for (int column = 0; column < lengthCount; column++)
        {
            for (int across = 0; across < widthCount; across++)
            {
                GameObject cubeObject = Instantiate(cubePrefab, GetCubePosition(column, across), transform.rotation, transform);
                cubeObject.transform.localScale = Vector3.zero;
                RemoveColliders(cubeObject);

                gapCubes.Add(new GapCube
                {
                    visual = cubeObject.transform,
                    columnFromStart = column
                });
            }
        }
    }

    private Vector3 GetCubePosition(int column, int across)
    {
        float along = (column + 1) * GridUnit;
        float side = (across - (widthCount - 1) * 0.5f) * GridUnit;
        Vector3 position = startPlatform.position + lengthDirection * along + widthDirection * side;
        position.y = startPlatform.position.y;
        return position;
    }

    private static void RemoveColliders(GameObject cubeObject)
    {
        Collider[] colliders = cubeObject.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
            Destroy(colliders[i]);
    }

    private void ClearCubes()
    {
        for (int i = 0; i < gapCubes.Count; i++)
        {
            if (gapCubes[i].visual != null)
                Destroy(gapCubes[i].visual.gameObject);
        }

        gapCubes.Clear();
    }
}
