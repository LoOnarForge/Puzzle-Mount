using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SlipperySurfaceZone : MonoBehaviour
{
    private const string FloorSpriteObjectPrefix = "_ZoneFloorSprite";
    private const float CellHalfExtent = 0.5f;
    private const float FloorSpriteLift = 0.02f;

    [SerializeField] private BoxCollider zoneTrigger;
    [SerializeField] private ParticleSystem zoneParticleSystem;
    [SerializeField] private Sprite floorMarkerSprite;
    [SerializeField] private Sprite floorMarkerSprite2;
    [SerializeField] private Color floorMarkerColor = Color.white;
    [SerializeField] private Color floorMarkerColor2 = Color.white;
    [SerializeField] [Range(0.1f, 10f)] private float floorMarkerPulseCycleSeconds = 3f;

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

    private void OnValidate()
    {
#if UNITY_EDITOR
        QueueEditorFloorRebuild();
#endif
    }

    private void Awake()
    {
        ApplyEvenSizeColliderCenterOffset();
        EnsureZoneParticleSystem();
        SyncZoneParticleSystem();
    }

    private void Start()
    {
        RebuildFloorMarkers();
        SyncZoneParticleSystem();
        RestartZoneParticleSystem();
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
        SyncZoneParticleSystem();
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

        UpdateFloorMarkerPulse();
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

        foreach (RunodeMovement cube in UnityEngine.Object.FindObjectsByType<RunodeMovement>())
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

    // Per axis: even size -> 0.5 local center; odd size -> 0. Keeps box + sprites on the 1m grid.
    private void ApplyEvenSizeColliderCenterOffset()
    {
        Vector3 size = zoneTrigger.size;
        Vector3 center = zoneTrigger.center;

        center.x = Mathf.RoundToInt(size.x) % 2 == 0 ? 0.5f : 0f;
        center.z = Mathf.RoundToInt(size.z) % 2 == 0 ? 0.5f : 0f;

        zoneTrigger.center = center;
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
        if (isRebuildingFloorMarkers || zoneTrigger == null)
            return;

        isRebuildingFloorMarkers = true;

        ApplyEvenSizeColliderCenterOffset();

        DestroyFloorMarkerObjects();

        if (floorMarkerSprite == null)
        {
            isRebuildingFloorMarkers = false;
            return;
        }

        Bounds bounds = zoneTrigger.bounds;
        float floorY = bounds.min.y + FloorSpriteLift;
        float zoneWidth = bounds.size.x;
        float zoneDepth = bounds.size.z;
        Vector3 zoneCenter = new Vector3(bounds.center.x, floorY, bounds.center.z);
        Quaternion floorRotation = Quaternion.Euler(90f, 0f, 0f);

        AddFloorMarkerSpriteLayer(floorMarkerSprite, floorMarkerColor, 0, zoneWidth, zoneDepth, zoneCenter, floorRotation);

        if (floorMarkerSprite2 != null)
            AddFloorMarkerSpriteLayer(floorMarkerSprite2, floorMarkerColor2, 1, zoneWidth, zoneDepth, zoneCenter, floorRotation);

        isRebuildingFloorMarkers = false;
    }

    private void EnsureZoneParticleSystem()
    {
        if (zoneParticleSystem != null && zoneParticleSystem.gameObject.scene.IsValid())
        {
            zoneParticleSystem.gameObject.SetActive(true);
            return;
        }

        ParticleSystem childSystem = GetComponentInChildren<ParticleSystem>(true);
        if (childSystem != null)
        {
            zoneParticleSystem = childSystem;
            zoneParticleSystem.gameObject.SetActive(true);
            return;
        }

        if (zoneParticleSystem == null)
            return;

        GameObject instance = Instantiate(zoneParticleSystem.gameObject, transform);
        instance.SetActive(true);
        zoneParticleSystem = instance.GetComponent<ParticleSystem>();
    }

    private void SyncZoneParticleSystem()
    {
        if (zoneParticleSystem == null || zoneTrigger == null)
            return;

        Transform particleTransform = zoneParticleSystem.transform;
        particleTransform.SetParent(transform, true);
        particleTransform.position = zoneTrigger.transform.TransformPoint(zoneTrigger.center);

        ParticleSystem.ShapeModule shape = zoneParticleSystem.shape;
        shape.scale = GetParticleBoxScale(particleTransform);

        ParticleSystem.TriggerModule trigger = zoneParticleSystem.trigger;
        trigger.SetCollider(0, zoneTrigger);
    }

    private Vector3 GetParticleBoxScale(Transform particleTransform)
    {
        Vector3 zoneSize = zoneTrigger.size;
        Quaternion zoneToParticle = Quaternion.Inverse(particleTransform.rotation) * zoneTrigger.transform.rotation;
        Vector3 axisX = zoneToParticle * Vector3.right;
        Vector3 axisY = zoneToParticle * Vector3.up;
        Vector3 axisZ = zoneToParticle * Vector3.forward;

        Vector3 shapeScale = new Vector3(
            Mathf.Abs(axisX.x) * zoneSize.x + Mathf.Abs(axisY.x) * zoneSize.y + Mathf.Abs(axisZ.x) * zoneSize.z,
            Mathf.Abs(axisX.y) * zoneSize.x + Mathf.Abs(axisY.y) * zoneSize.y + Mathf.Abs(axisZ.y) * zoneSize.z,
            Mathf.Abs(axisX.z) * zoneSize.x + Mathf.Abs(axisY.z) * zoneSize.y + Mathf.Abs(axisZ.z) * zoneSize.z);

        Vector3 particleScale = particleTransform.localScale;
        shapeScale.x = DivideByScale(shapeScale.x, particleScale.x);
        shapeScale.y = DivideByScale(shapeScale.y, particleScale.y);
        shapeScale.z = DivideByScale(shapeScale.z, particleScale.z);
        return shapeScale;
    }

    private static float DivideByScale(float size, float scale)
    {
        if (Mathf.Abs(scale) < 0.0001f)
            return size;

        return size / scale;
    }

    private void RestartZoneParticleSystem()
    {
        if (zoneParticleSystem == null)
            return;

        zoneParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        zoneParticleSystem.Play(true);
    }

    private void UpdateFloorMarkerPulse()
    {
        if (floorMarkerSprite == null)
            return;

        Color layer1Color = floorMarkerColor;
        layer1Color.a = EvaluatePulsingAlpha();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith(FloorSpriteObjectPrefix))
                continue;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null)
                continue;

            if (renderer.sortingOrder >= 1)
                renderer.color = floorMarkerColor2;
            else
                renderer.color = layer1Color;
        }
    }

    private void AddFloorMarkerSpriteLayer(
        Sprite sprite,
        Color color,
        int sortingOrder,
        float targetWidth,
        float targetDepth,
        Vector3 worldPosition,
        Quaternion worldRotation)
    {
        if (sprite == null)
            return;

        GameObject layer = new GameObject(FloorSpriteObjectPrefix + sortingOrder);
        layer.transform.SetParent(transform, false);
        layer.transform.SetPositionAndRotation(worldPosition, worldRotation);
        layer.transform.localScale = Vector3.one;

        SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = new Vector2(targetWidth, targetDepth);
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
    }

    private void DestroyFloorMarkerObjects()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == "_SlipperyFloorMarkers" || child.name == "FloorMarker" || child.name.StartsWith(FloorSpriteObjectPrefix))
                DestroyUnityObject(child.gameObject);
        }
    }

    private float EvaluatePulsingAlpha()
    {
        float baseAlpha = floorMarkerColor.a;
        if (baseAlpha <= 0f || floorMarkerPulseCycleSeconds <= 0f)
            return baseAlpha;

        float cycleT = Mathf.Repeat(Time.time, floorMarkerPulseCycleSeconds) / floorMarkerPulseCycleSeconds;
        float phaseT = cycleT < 0.5f ? cycleT * 2f : (cycleT - 0.5f) * 2f;

        if (cycleT < 0.5f)
            return Mathf.Lerp(baseAlpha, 0f, phaseT);

        return Mathf.Lerp(0f, baseAlpha, phaseT);
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
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
