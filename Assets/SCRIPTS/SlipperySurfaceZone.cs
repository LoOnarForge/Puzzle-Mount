using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class SlipperySurfaceZone : MonoBehaviour
{
    private const string FloorMarkersChildName = "_SlipperyFloorMarkers";
    private const float CellHalfExtent = 0.5f;
    private const float FloorSpriteLift = 0.02f;

    [SerializeField] private BoxCollider zoneTrigger;
    [SerializeField] private Sprite floorMarkerSprite;

    private Transform floorMarkersRoot;
    private bool isRebuildingFloorMarkers;

#if UNITY_EDITOR
    private bool editorFloorRebuildQueued;
#endif

    private readonly HashSet<RunodeMovement> runodesInTrigger = new HashSet<RunodeMovement>();
    private readonly Dictionary<RunodeMovement, RunodeTrackState> trackByRunode = new Dictionary<RunodeMovement, RunodeTrackState>();
    private readonly HashSet<RunodeMovement> slidingStackBottoms = new HashSet<RunodeMovement>();

    private struct RunodeTrackState
    {
        public bool WasMoving;
        public Vector3 MoveStartGrid;
        public bool HasMoveStart;
    }

    private void Reset()
    {
        zoneTrigger = GetComponent<BoxCollider>();
        ConfigureTrigger();
    }

    private void Awake()
    {
        if (zoneTrigger == null)
            zoneTrigger = GetComponent<BoxCollider>();
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        if (zoneTrigger == null)
            zoneTrigger = GetComponent<BoxCollider>();

#if UNITY_EDITOR
        QueueEditorFloorRebuild();
#endif
    }

    private void Start()
    {
        RebuildFloorMarkers();
    }

#if UNITY_EDITOR
    private void QueueEditorFloorRebuild()
    {
        if (editorFloorRebuildQueued)
            return;

        editorFloorRebuildQueued = true;
        UnityEditor.EditorApplication.delayCall += RunQueuedEditorFloorRebuild;
    }

    private void RunQueuedEditorFloorRebuild()
    {
        editorFloorRebuildQueued = false;
        if (this == null)
            return;

        RebuildFloorMarkers();
    }
#endif

    private void Update()
    {
        foreach (RunodeMovement runode in runodesInTrigger)
        {
            if (runode == null)
                continue;

            UpdateRunodeTracking(runode);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        RunodeMovement runode = other.GetComponentInParent<RunodeMovement>();
        if (runode != null)
            runodesInTrigger.Add(runode);
    }

    private void OnTriggerExit(Collider other)
    {
        RunodeMovement runode = other.GetComponentInParent<RunodeMovement>();
        if (runode == null)
            return;

        runodesInTrigger.Remove(runode);
        trackByRunode.Remove(runode);
    }

    private void ConfigureTrigger()
    {
        if (zoneTrigger != null)
            zoneTrigger.isTrigger = true;
    }

    private void UpdateRunodeTracking(RunodeMovement runode)
    {
        if (!trackByRunode.TryGetValue(runode, out RunodeTrackState state))
            state = default;

        bool isMoving = runode.isMovingPos;

        if (isMoving && !state.HasMoveStart)
        {
            state.MoveStartGrid = SnapHorizontal(runode.transform.position);
            state.HasMoveStart = true;
        }
        else if (isMoving && state.HasMoveStart)
        {
            TryEnableSlipEasingForPushStep(runode, state);
        }
        else if (state.WasMoving && !isMoving)
        {
            TryStartSlideAfterPush(runode, state);

            if (!slidingStackBottoms.Contains(runode))
                ResetSlipState(runode);

            state.HasMoveStart = false;
        }

        state.WasMoving = isMoving;
        trackByRunode[runode] = state;
    }

    // Linear push lerp for Tim's step when that step's target cell is on this zone (removes entry ease-out).
    private void TryEnableSlipEasingForPushStep(RunodeMovement runode, RunodeTrackState state)
    {
        if (slidingStackBottoms.Contains(runode))
            return;

        RunodeMovement columnBottom = GetColumnBottom(runode);
        if (runode != columnBottom || columnBottom.GetStack().Length != 1)
            return;

        Vector3 direction = CardinalDirection(runode.transform.position - state.MoveStartGrid);
        if (direction == Vector3.zero)
            return;

        Vector3 stepTarget = state.MoveStartGrid + direction;
        if (ContainsGridPosition(stepTarget))
        {
            runode.isSlipping = true;
            runode.slipSpeedMultiplier = 1f;
        }
    }

    private void TryStartSlideAfterPush(RunodeMovement runode, RunodeTrackState state)
    {
        RunodeMovement columnBottom = GetColumnBottom(runode);
        if (runode != columnBottom || columnBottom.GetStack().Length != 1 || slidingStackBottoms.Contains(runode))
            return;

        Vector3 endGrid = SnapHorizontal(runode.transform.position);
        if (!ContainsGridPosition(endGrid) || !state.HasMoveStart)
            return;

        Vector3 direction = CardinalDirection(endGrid - state.MoveStartGrid);
        if (direction == Vector3.zero)
            return;

        StartCoroutine(SlideStack(runode, direction));
    }

    private IEnumerator SlideStack(RunodeMovement stackBottom, Vector3 direction)
    {
        slidingStackBottoms.Add(stackBottom);
        stackBottom.isSlipping = true;
        stackBottom.slipSpeedMultiplier = 1f;

        int cellsMoved = 0;
        int expectedCells = CountZoneStepsUntilExit(SnapHorizontal(stackBottom.transform.position), direction);
        if (expectedCells < 1)
            expectedCells = 1;

        while (ContainsGridPosition(SnapHorizontal(stackBottom.transform.position)))
        {
            Vector3 nextCell = SnapHorizontal(stackBottom.transform.position) + direction;

            if (!stackBottom.CanPushSingle(direction))
                break;

            bool nextStillInZone = ContainsGridPosition(nextCell);

            stackBottom.slipSpeedMultiplier = GetSlipSpeedMultiplier(cellsMoved, expectedCells);
            stackBottom.PushSingle(direction);

            yield return WaitUntilRunodeIdle(stackBottom);

            cellsMoved++;

            if (!nextStillInZone)
                break;
        }

        ResetSlipState(stackBottom);
        slidingStackBottoms.Remove(stackBottom);
    }

    private static float GetSlipSpeedMultiplier(int cellsMovedSoFar, int expectedCellsInTrip)
    {
        if (expectedCellsInTrip <= 1)
            return 1f;

        float tripProgress = (float)cellsMovedSoFar / (expectedCellsInTrip - 1);
        return Mathf.Lerp(1f, 0.5f, tripProgress);
    }

    private static void ResetSlipState(RunodeMovement runode)
    {
        if (runode == null)
            return;

        runode.isSlipping = false;
        runode.slipSpeedMultiplier = 1f;
    }

    private int CountZoneStepsUntilExit(Vector3 startGrid, Vector3 direction)
    {
        const int maxZoneSteps = 64;
        int steps = 0;
        Vector3 grid = startGrid;

        while (steps < maxZoneSteps)
        {
            Vector3 next = grid + direction;
            steps++;

            if (!ContainsGridPosition(next))
                break;

            grid = next;
        }

        return steps;
    }

    private static RunodeMovement GetColumnBottom(RunodeMovement runode)
    {
        RunodeMovement bottom = runode;
        Vector3 basePos = runode.transform.position;

        foreach (RunodeMovement cube in Object.FindObjectsByType<RunodeMovement>())
        {
            if (cube == null || cube == runode)
                continue;

            Vector3 cubePos = cube.transform.position;
            bool aligned = Mathf.Abs(cubePos.x - basePos.x) < 0.1f && Mathf.Abs(cubePos.z - basePos.z) < 0.1f;
            if (aligned && cubePos.y < bottom.transform.position.y)
                bottom = cube;
        }

        return bottom;
    }

    private static IEnumerator WaitUntilRunodeIdle(RunodeMovement runode)
    {
        while (runode != null && runode.isMovingPos)
            yield return null;
    }

    private bool ContainsGridPosition(Vector3 gridPosition)
    {
        if (zoneTrigger == null)
            return false;

        Bounds b = zoneTrigger.bounds;
        float x = gridPosition.x;
        float z = gridPosition.z;

        return x + CellHalfExtent > b.min.x && x - CellHalfExtent < b.max.x &&
               z + CellHalfExtent > b.min.z && z - CellHalfExtent < b.max.z;
    }

    private void RebuildFloorMarkers()
    {
        if (isRebuildingFloorMarkers || zoneTrigger == null || floorMarkerSprite == null)
            return;

        isRebuildingFloorMarkers = true;

        Transform markersRoot = EnsureSingleFloorMarkersRoot();
        ClearChildren(markersRoot);

        Bounds b = zoneTrigger.bounds;
        float floorY = b.min.y + FloorSpriteLift;
        float spriteScale = 1f / Mathf.Max(floorMarkerSprite.bounds.size.x, 0.001f);

        int minX = Mathf.FloorToInt(b.min.x + CellHalfExtent);
        int maxX = Mathf.FloorToInt(b.max.x - CellHalfExtent);
        int minZ = Mathf.FloorToInt(b.min.z + CellHalfExtent);
        int maxZ = Mathf.FloorToInt(b.max.z - CellHalfExtent);

        var placedCells = new HashSet<long>();

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                long cellKey = CellKey(x, z);
                if (!placedCells.Add(cellKey))
                    continue;

                Vector3 cell = new Vector3(x, b.max.y, z);
                if (!ContainsGridPosition(cell))
                    continue;

                GameObject marker = new GameObject($"FloorMarker_{x}_{z}");
                marker.transform.SetParent(markersRoot, false);
                marker.transform.SetPositionAndRotation(new Vector3(x, floorY, z), Quaternion.Euler(90f, 0f, 0f));
                marker.transform.localScale = new Vector3(spriteScale, spriteScale, 1f);

                SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = floorMarkerSprite;
            }
        }

        isRebuildingFloorMarkers = false;
    }

    private Transform EnsureSingleFloorMarkersRoot()
    {
        Transform keep = null;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.name != FloorMarkersChildName)
                continue;

            if (keep == null)
                keep = child;
            else
                DestroyUnityObject(child.gameObject);
        }

        if (keep == null)
        {
            keep = new GameObject(FloorMarkersChildName).transform;
            keep.SetParent(transform, false);
        }

        floorMarkersRoot = keep;
        return keep;
    }

    private static long CellKey(int x, int z)
    {
        return ((long)x << 32) ^ (uint)z;
    }

    private static void DestroyUnityObject(Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private static void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyUnityObject(parent.GetChild(i).gameObject);
    }

    private static Vector3 SnapHorizontal(Vector3 position)
    {
        return new Vector3(Mathf.Round(position.x), position.y, Mathf.Round(position.z));
    }

    private static Vector3 CardinalDirection(Vector3 delta)
    {
        delta.y = 0f;
        if (delta.sqrMagnitude < 0.01f)
            return Vector3.zero;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.z))
            return delta.x > 0f ? Vector3.right : Vector3.left;

        return delta.z > 0f ? Vector3.forward : Vector3.back;
    }
}
