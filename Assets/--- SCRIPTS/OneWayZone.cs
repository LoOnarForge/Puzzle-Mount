using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class OneWayZone : MonoBehaviour
{
    private const string FloorSpriteObjectPrefix = "_ZoneFloorSprite";
    private const float CellHalfExtent = 0.5f;
    private const float FloorSpriteLift = 0.01f;
    private const float PlayModeTriggerXZSizeReduction = 0.2f;

    private static readonly List<OneWayZone> activeZones = new List<OneWayZone>();

    [Header("ALLOWED PUSH DIRECTIONS")]
    [SerializeField] private bool isEast;
    [SerializeField] private bool isWest;
    [SerializeField] private bool isNorth;
    [SerializeField] private bool isSouth;

    private Vector3 savedTriggerSize;
    private bool hasSavedTriggerSize;
    private Bounds floorMarkerLayoutBounds;
    private bool hasFloorMarkerLayoutBounds;

    [SerializeField] private BoxCollider zoneTrigger;
    [SerializeField] private HexZone hexZone;
    [SerializeField] private ParticleSystem zoneParticleSystem;
    [SerializeField] private bool isPSActive = true;
    [FormerlySerializedAs("floorMarkerSprite")]
    [SerializeField] private Sprite frameSprite;
    [FormerlySerializedAs("floorMarkerColor")]
    [SerializeField] private Color primaryColor = Color.white;
    [FormerlySerializedAs("floorMarkerColor2")]
    [SerializeField] private Color secondaryColor = Color.white;
    [FormerlySerializedAs("floorMarkerPulseCycleSeconds")]
    [SerializeField] [Range(0.1f, 10f)] private float framePulsingTimer = 3f;

    private bool isRebuildingFloorMarkers;

#if UNITY_EDITOR
    private bool editorFloorRebuildQueued;
#endif

    private void OnEnable()
    {
        if (!activeZones.Contains(this))
            activeZones.Add(this);
    }

    private void OnDisable()
    {
        activeZones.Remove(this);
        RestoreTriggerSizeAfterPlay();
    }

    private void OnValidate()
    {
        ApplyIsPSActive();
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
        SyncHexZoneFromTrigger();
        CaptureFloorMarkerLayoutBounds();
        SyncZoneParticleSystem();
        ApplyIsPSActive();
        ApplyPlayModeTriggerInset();
    }

    private void OnDestroy()
    {
        activeZones.Remove(this);
        RestoreTriggerSizeAfterPlay();
    }

    private void Update()
    {
        UpdateFloorMarkerPulse();
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
        SyncHexZoneFromTrigger();
        SyncZoneParticleSystem();
        ApplyIsPSActive();
    }
#endif

    // Returns true when the cube starts on a one-way zone and the push direction is not allowed.
    public static bool BlocksPush(Vector3 startCell, Vector3 pushDir)
    {
        if (activeZones.Count == 0 || pushDir == Vector3.zero)
            return false;

        for (int i = 0; i < activeZones.Count; i++)
        {
            OneWayZone zone = activeZones[i];
            if (zone == null || !zone.HasAnyAllowedDirection())
                continue;

            if (zone.ContainsGridPosition(startCell) && !zone.IsPushDirectionAllowed(pushDir))
                return true;
        }

        return false;
    }

    private bool HasAnyAllowedDirection()
    {
        return isEast || isWest || isNorth || isSouth;
    }

    private bool IsPushDirectionAllowed(Vector3 pushDir)
    {
        if (pushDir == Vector3.right)
            return isEast;
        if (pushDir == Vector3.left)
            return isWest;
        if (pushDir == Vector3.forward)
            return isNorth;
        if (pushDir == Vector3.back)
            return isSouth;

        return false;
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

    private void ApplyEvenSizeColliderCenterOffset()
    {
        if (zoneTrigger == null)
            return;

        Vector3 size = zoneTrigger.size;
        Vector3 center = zoneTrigger.center;

        center.x = Mathf.RoundToInt(size.x) % 2 == 0 ? 0.5f : 0f;
        center.z = Mathf.RoundToInt(size.z) % 2 == 0 ? 0.5f : 0f;

        zoneTrigger.center = center;
    }

    private void ApplyPlayModeTriggerInset()
    {
        if (!Application.isPlaying || zoneTrigger == null)
            return;

        savedTriggerSize = zoneTrigger.size;
        hasSavedTriggerSize = true;

        Vector3 insetSize = savedTriggerSize;
        insetSize.x = Mathf.Max(insetSize.x - PlayModeTriggerXZSizeReduction, 0.01f);
        insetSize.z = Mathf.Max(insetSize.z - PlayModeTriggerXZSizeReduction, 0.01f);
        zoneTrigger.size = insetSize;
    }

    private void RestoreTriggerSizeAfterPlay()
    {
        if (!hasSavedTriggerSize || zoneTrigger == null)
            return;

        zoneTrigger.size = savedTriggerSize;
        hasSavedTriggerSize = false;
        hasFloorMarkerLayoutBounds = false;
    }

    private void CaptureFloorMarkerLayoutBounds()
    {
        if (zoneTrigger == null)
            return;

        floorMarkerLayoutBounds = zoneTrigger.bounds;
        hasFloorMarkerLayoutBounds = true;
    }

    private Bounds GetFloorMarkerLayoutBounds()
    {
        if (Application.isPlaying && hasFloorMarkerLayoutBounds)
            return floorMarkerLayoutBounds;

        return zoneTrigger.bounds;
    }

    private void RebuildFloorMarkers()
    {
        if (isRebuildingFloorMarkers || zoneTrigger == null)
            return;

        if (frameSprite == null)
        {
            DestroyEmptyRuntimeFloorSpriteObjects();
            return;
        }

        isRebuildingFloorMarkers = true;

        if (!Application.isPlaying || !hasSavedTriggerSize)
            ApplyEvenSizeColliderCenterOffset();

        DestroyFloorMarkerObjects();

        Bounds bounds = GetFloorMarkerLayoutBounds();
        float floorY = bounds.min.y + FloorSpriteLift;
        float zoneWidth = bounds.size.x;
        float zoneDepth = bounds.size.z;
        Vector3 zoneCenter = new Vector3(bounds.center.x, floorY, bounds.center.z);
        Quaternion floorRotation = Quaternion.Euler(90f, 0f, 0f);

        AddFloorMarkerSpriteLayer(frameSprite, primaryColor, 0, zoneWidth, zoneDepth, zoneCenter, floorRotation);

        DestroyEmptyRuntimeFloorSpriteObjects();

        isRebuildingFloorMarkers = false;
    }

    private void SyncHexZoneFromTrigger()
    {
        if (hexZone == null || zoneTrigger == null)
            return;

        hexZone.BuildFromZoneBounds(zoneTrigger.bounds, primaryColor, secondaryColor);
    }

    private void EnsureZoneParticleSystem()
    {
        if (zoneParticleSystem != null && zoneParticleSystem.gameObject.scene.IsValid())
        {
            ApplyIsPSActive();
            return;
        }

        ParticleSystem childSystem = GetComponentInChildren<ParticleSystem>(true);
        if (childSystem != null)
        {
            zoneParticleSystem = childSystem;
            ApplyIsPSActive();
            return;
        }

        if (zoneParticleSystem == null)
            return;

        GameObject instance = Instantiate(zoneParticleSystem.gameObject, transform);
        zoneParticleSystem = instance.GetComponent<ParticleSystem>();
        ApplyIsPSActive();
    }

    private void ApplyIsPSActive()
    {
        if (zoneParticleSystem == null)
            return;

        GameObject psObject = zoneParticleSystem.gameObject;
        if (!isPSActive)
        {
            if (Application.isPlaying)
                zoneParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            psObject.SetActive(false);
            return;
        }

        psObject.SetActive(true);

        if (Application.isPlaying)
        {
            zoneParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            zoneParticleSystem.Play(true);
        }
    }

    private void SyncZoneParticleSystem()
    {
        if (zoneParticleSystem == null || zoneTrigger == null)
            return;

        Transform particleTransform = zoneParticleSystem.transform;
        if (CanReparentTransformInEditor(particleTransform, transform))
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

    private void UpdateFloorMarkerPulse()
    {
        if (frameSprite == null)
            return;

        Color frameColor = primaryColor;
        frameColor.a = EvaluatePulsingAlpha();

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith(FloorSpriteObjectPrefix))
                continue;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null)
                continue;

            renderer.color = frameColor;
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
        if (sprite == null || targetWidth <= 0.01f || targetDepth <= 0.01f)
            return;

        string layerName = FloorSpriteObjectPrefix + sortingOrder;
        Transform existing = transform.Find(layerName);
        GameObject layer;
        SpriteRenderer renderer;

        if (existing != null)
        {
            layer = existing.gameObject;
            renderer = layer.GetComponent<SpriteRenderer>();
            if (renderer == null)
                renderer = layer.AddComponent<SpriteRenderer>();
        }
        else
        {
            layer = new GameObject(layerName);
            renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (CanReparentTransformInEditor(layer.transform, transform))
                layer.transform.SetParent(transform, false);
        }

        layer.transform.SetPositionAndRotation(worldPosition, worldRotation);
        layer.transform.localScale = Vector3.one;

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
            if (child.name == "FloorMarker" || child.name.StartsWith(FloorSpriteObjectPrefix))
                DestroyUnityObject(child.gameObject);
        }
    }

    private void DestroyEmptyRuntimeFloorSpriteObjects()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (!child.name.StartsWith(FloorSpriteObjectPrefix))
                continue;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
                continue;

            DestroyUnityObject(child.gameObject);
        }
    }

    private float EvaluatePulsingAlpha()
    {
        float baseAlpha = primaryColor.a;
        if (baseAlpha <= 0f || framePulsingTimer <= 0f)
            return baseAlpha;

        float cycleT = Mathf.Repeat(Time.time, framePulsingTimer) / framePulsingTimer;
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
        {
            Destroy(target);
            return;
        }

#if UNITY_EDITOR
        DestroyImmediate(target, true);
#else
        DestroyImmediate(target);
#endif
    }

    private static bool CanReparentTransformInEditor(Transform child, Transform newParent)
    {
        if (child.parent == newParent)
            return false;

        if (Application.isPlaying)
            return true;

#if UNITY_EDITOR
        if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(child.gameObject))
            return false;
#endif
        return true;
    }
}
