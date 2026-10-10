using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bridge : MonoBehaviour
{
    private const int SocketCount = 2;
    private const float GridUnit = 1f;
    private const float PlatformDepthInCenterSpan = 1f;
    private const float ColliderDisableDelay = 0.2f;
    private const float SettleDurationShare = 0.5f;
    private const float GrowEasePower = 2f;
    private const float ShrinkEasePower = 2.5f;
    private const float MinPopSpeed = 0.01f;
    private const float SamePositionLimit = 0.0001f;
    private const float VisibleCubeScaleThreshold = 0.01f;

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

    private class GapCube
    {
        public Transform visual;
        public int columnFromStart;
        public float delay;
        public Vector3 startScale;
    }

    private class GapRow
    {
        public BoxCollider box;
        public int columnFromStart;
        public float delay;
    }

    [SerializeField] private bool isSelfPowered;

    [SerializeField] private SocketSlot[] sockets = new SocketSlot[SocketCount]
    {
        new SocketSlot(),
        new SocketSlot()
    };

    [SerializeField, Range(1, 4)] private int bridgeWidth = 2;
    [SerializeField] private Transform startPlatform;
    [SerializeField] private Transform endPlatform;
    [SerializeField] private GameObject cubePrefab;

    [SerializeField, Range(1f, 40f)] private float cubePopSpeed = 10f;
    [SerializeField, Range(0f, 50f)] private float overshootPercent = 10f;
    [SerializeField, Range(0f, 1f)] private float columnWaveDelay = 0.04f;
    [SerializeField, Range(0f, 0.5f)] private float cubeRandomDelay = 0.03f;

    [SerializeField] private bool isPowered;

    private readonly List<GapCube> gapCubes = new List<GapCube>();
    private readonly List<GapRow> gapRows = new List<GapRow>();

    private Coroutine sequenceRoutine;
    private Vector3 lengthDirection;
    private int lengthCount;
    private int widthCount;
    private int sequenceId;
    private bool hasValidLayout;
    private bool isBridgeOpen;
    private bool isSequenceRunning;
    private bool isClosing;
    private bool activatorDemandsOpen;

    private void Awake()
    {
        hasValidLayout = TryBuildLayout();
        InitialSocketConfiguration();
    }

    // Called by a DevicePowerSocket when its power state changes.
    public void UpdateSocketStateToOwner(int ownerIndex, int allocatedMw, PowerSource poweringSource)
    {
        if (ownerIndex < 0 || ownerIndex >= sockets.Length)
            return;

        sockets[ownerIndex].allocatedMw = allocatedMw;
        sockets[ownerIndex].poweringSource = poweringSource;
        RefreshBridgeState();
    }

    // Called by a powered Lever when the player operates it.
    public void OnLeverOperated()
    {
        if (!hasValidLayout || (!isSelfPowered && !isPowered) || isSequenceRunning)
            return;

        ApplyOpenState(!isBridgeOpen);
    }

    // Reverses settled state or in-progress motion — used by pressure plates and buttons.
    public void ApplyActivatorReverse()
    {
        ApplyOpenState(!IsEffectivelyOpenOrOpening());
    }

    // Drives toward explicit open or closed (also used internally after reverse).
    public void ApplyOpenState(bool isOpen)
    {
        activatorDemandsOpen = isOpen;

        if (!hasValidLayout)
            return;

        if (isOpen)
        {
            if (!isSelfPowered && !isPowered)
                return;

            if (isBridgeOpen)
                return;

            if (isSequenceRunning && !isClosing)
                return;

            BeginOpen();
            return;
        }

        if (!IsEffectivelyOpenOrOpening())
            return;

        ForceBeginClose();
    }

    // Claims assigned sockets once and pushes required color and MW to each.
    private void InitialSocketConfiguration()
    {
        if (isSelfPowered)
        {
            isPowered = true;
            return;
        }

        for (int i = 0; i < sockets.Length; i++)
        {
            SocketSlot slot = sockets[i];

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
            Debug.Assert(device != null, $"{nameof(Bridge)} on {name} requires a {nameof(DevicePowerSocket)} on socket {(i + 1):D2}.", this);
            if (device == null)
                continue;

            DevicePowerSocket.SetSocketHierarchyActive(slot.socketObject, true);
            device.SetBridgeOwner(this);
            device.InitialSocketConfiguration(i, slot.requiredColorIndex, slot.requiredMw);
        }

        RefreshBridgeState();
    }

    private void RefreshBridgeState()
    {
        if (isSelfPowered)
        {
            isPowered = true;
            return;
        }

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

        bool poweredNow = hasEnabledSocket && allPowered;
        bool lostPower = isPowered && !poweredNow;
        isPowered = poweredNow;

        if (lostPower)
            ForceBeginClose();
    }

    private bool TryBuildLayout()
    {
        Debug.Assert(startPlatform != null, $"{nameof(Bridge)} on {name} requires a start platform.", this);
        Debug.Assert(endPlatform != null, $"{nameof(Bridge)} on {name} requires an end platform.", this);
        Debug.Assert(cubePrefab != null, $"{nameof(Bridge)} on {name} requires a cube prefab.", this);

        if (startPlatform == null || endPlatform == null || cubePrefab == null)
            return false;

        Vector3 offset = endPlatform.position - startPlatform.position;
        offset.y = 0f;

        if (offset.sqrMagnitude < SamePositionLimit)
            return false;

        lengthDirection = offset.normalized;

        int centerSteps = Mathf.RoundToInt(offset.magnitude / GridUnit);
        lengthCount = centerSteps - Mathf.RoundToInt(PlatformDepthInCenterSpan);
        widthCount = bridgeWidth;

        return lengthCount >= 1 && widthCount >= 1;
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

    private void ForceBeginClose()
    {
        if (!hasValidLayout)
            return;

        if (!isSequenceRunning && !isBridgeOpen && !HasPartialGapBridge())
            return;

        if (isClosing && isSequenceRunning)
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

        float growDuration = 1f / Mathf.Max(cubePopSpeed, MinPopSpeed);
        float settleDuration = growDuration * SettleDurationShare;
        float overshootScale = 1f + overshootPercent * 0.01f;
        float longestDelay = AssignDelays(fromEnd: false);
        float totalDuration = longestDelay + growDuration + settleDuration;
        float elapsed = 0f;
        EnableReadyRows(elapsed);

        while (elapsed < totalDuration)
        {
            if (id != sequenceId)
                yield break;

            if (!activatorDemandsOpen)
                yield break;

            elapsed += Time.deltaTime;
            EnableReadyRows(elapsed);

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

        if (!activatorDemandsOpen)
            yield break;

        EnableReadyRows(totalDuration);

        for (int i = 0; i < gapCubes.Count; i++)
        {
            if (gapCubes[i].visual != null)
                gapCubes[i].visual.localScale = Vector3.one;
        }

        isBridgeOpen = true;
        isSequenceRunning = false;
        isClosing = false;
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

        int leadingColumn = GetLeadingVisibleColumn();
        float longestDelay = AssignCloseDelays(leadingColumn);
        float totalDuration = longestDelay + shrinkDuration;
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            if (id != sequenceId)
                yield break;

            elapsed += Time.deltaTime;
            DisableReadyRows(elapsed);

            for (int i = 0; i < gapCubes.Count; i++)
            {
                GapCube cube = gapCubes[i];
                if (cube.visual == null)
                    continue;

                if (cube.startScale.x <= VisibleCubeScaleThreshold)
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

        DisableReadyRows(totalDuration);
        ClearCubes();
        isBridgeOpen = false;
        isClosing = false;
        isSequenceRunning = false;
        sequenceRoutine = null;
    }

    private static float SampleOpenScale(float time, float growDuration, float settleDuration, float overshootScale)
    {
        if (time <= 0f)
            return 0f;

        if (time < growDuration)
        {
            float growT = time / growDuration;
            float eased = 1f - Mathf.Pow(1f - growT, GrowEasePower);
            return Mathf.Lerp(0f, overshootScale, eased);
        }

        float settleTime = time - growDuration;
        if (settleTime < settleDuration)
        {
            float settleT = settleDuration > 0f ? settleTime / settleDuration : 1f;
            float eased = Mathf.Pow(settleT, GrowEasePower);
            return Mathf.Lerp(overshootScale, 1f, eased);
        }

        return 1f;
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

        SyncRowDelaysToColumnCubes();

        return longestDelay;
    }

    // Close wave runs from the current growth front back toward the start (matches lever when fully open).
    private float AssignCloseDelays(int leadingColumn)
    {
        float longestDelay = 0f;

        for (int i = 0; i < gapCubes.Count; i++)
        {
            GapCube cube = gapCubes[i];
            int column = leadingColumn - cube.columnFromStart;
            if (column < 0)
                column = 0;

            float delay = column * columnWaveDelay;

            if (cubeRandomDelay > 0f)
                delay += Random.Range(0f, cubeRandomDelay);

            cube.delay = delay;

            if (delay > longestDelay)
                longestDelay = delay;
        }

        SyncRowDelaysToColumnCubes();

        return longestDelay;
    }

    private int GetLeadingVisibleColumn()
    {
        int leadingColumn = 0;

        for (int i = 0; i < gapCubes.Count; i++)
        {
            GapCube cube = gapCubes[i];
            if (cube.startScale.x <= VisibleCubeScaleThreshold)
                continue;

            if (cube.columnFromStart > leadingColumn)
                leadingColumn = cube.columnFromStart;
        }

        return leadingColumn;
    }

    private bool IsEffectivelyOpenOrOpening()
    {
        if (isSequenceRunning)
            return !isClosing;

        if (isBridgeOpen)
            return true;

        return HasPartialGapBridge();
    }

    private bool HasPartialGapBridge()
    {
        for (int i = 0; i < gapCubes.Count; i++)
        {
            Transform visual = gapCubes[i].visual;
            if (visual == null)
                continue;

            if (visual.localScale.x > VisibleCubeScaleThreshold)
                return true;
        }

        return false;
    }

    // Row colliders use the earliest cube delay in that column so they match the visual wave.
    private void SyncRowDelaysToColumnCubes()
    {
        for (int r = 0; r < gapRows.Count; r++)
        {
            GapRow row = gapRows[r];
            float columnDelay = float.MaxValue;
            bool found = false;

            for (int c = 0; c < gapCubes.Count; c++)
            {
                GapCube cube = gapCubes[c];
                if (cube.columnFromStart != row.columnFromStart)
                    continue;

                found = true;
                if (cube.delay < columnDelay)
                    columnDelay = cube.delay;
            }

            if (found)
                row.delay = columnDelay;
        }
    }

    private void SpawnGapCubes()
    {
        ClearCubes();

        for (int column = 0; column < lengthCount; column++)
        {
            SpawnRowCollider(column);

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

    private void SpawnRowCollider(int column)
    {
        GameObject rowObject = new GameObject("Bridge Row");
        rowObject.layer = LayerMask.NameToLayer("Devices");
        rowObject.transform.SetParent(transform, false);
        rowObject.transform.SetPositionAndRotation(GetRowColliderPosition(column), Quaternion.LookRotation(lengthDirection, Vector3.up));
        rowObject.transform.localScale = Vector3.one;

        BoxCollider box = rowObject.AddComponent<BoxCollider>();
        box.size = new Vector3(widthCount * GridUnit, GridUnit, GridUnit);
        box.enabled = false;

        gapRows.Add(new GapRow
        {
            box = box,
            columnFromStart = column
        });
    }

    private Vector3 GetRowColliderPosition(int column)
    {
        float along = (column + 1) * GridUnit;
        float side = (widthCount - 1) * 0.5f * GridUnit;
        Vector3 position = startPlatform.position + lengthDirection * along + transform.right * side;
        position.y = startPlatform.position.y;
        return position;
    }

    private void EnableReadyRows(float elapsed)
    {
        for (int i = 0; i < gapRows.Count; i++)
        {
            GapRow row = gapRows[i];
            if (row.box == null || row.box.enabled)
                continue;

            if (elapsed >= row.delay)
                row.box.enabled = true;
        }
    }

    private void DisableReadyRows(float elapsed)
    {
        for (int i = 0; i < gapRows.Count; i++)
        {
            GapRow row = gapRows[i];
            if (row.box == null || !row.box.enabled)
                continue;

            if (elapsed >= row.delay + ColliderDisableDelay)
                row.box.enabled = false;
        }
    }

    private Vector3 GetCubePosition(int column, int across)
    {
        float along = (column + 1) * GridUnit;
        Vector3 position = startPlatform.position + lengthDirection * along + transform.right * (across * GridUnit);
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

        for (int i = 0; i < gapRows.Count; i++)
        {
            if (gapRows[i].box != null)
                Destroy(gapRows[i].box.gameObject);
        }

        gapRows.Clear();
    }
}
