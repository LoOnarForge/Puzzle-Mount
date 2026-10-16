using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns and moves sprite visuals inside a zone trigger. Intended as a child of zone prefabs.
/// </summary>
[DisallowMultipleComponent]
public class SpriteGenerator : MonoBehaviour
{
    private const float MinExtent = 0.01f;
    private const float DirectionEpsilon = 0.0001f;
    private const float MinSpawnHeightAboveBottom = 0.3f;
    private const float MaxSpawnHeightAboveBottom = 0.8f;

    [Header("Zone")]
    [SerializeField] private BoxCollider zoneTrigger;

    [Header("Sprites")]
    [SerializeField] private Sprite[] sprites;

    [Header("Direction (zone local)")]
    [SerializeField] private bool east;
    [SerializeField] private bool west;
    [SerializeField] private bool north;
    [SerializeField] private bool south;

    [Header("Spawn")]
    [SerializeField] [Min(0)] private int maxActiveSprites = 12;
    [SerializeField] [Min(0f)] private float spawnIntervalSeconds = 0.45f;
    [SerializeField] [Min(0f)] private float edgeInset = 0.15f;

    [Header("Speed (units / second)")]
    [SerializeField] private float minSpeed = 0.15f;
    [SerializeField] private float maxSpeed = 0.45f;

    [Header("Scale (uniform, random endpoints in range)")]
    [SerializeField] [Min(0f)] private float minScale = 0.35f;
    [SerializeField] [Min(0f)] private float maxScale = 0.85f;
    [SerializeField] private bool isSquashing;
    [SerializeField] [Range(0f, 100f)] private float maxSquashPercentAtPathEnd = 50f;

    [Header("Color")]
    [SerializeField] private Color primaryColor = Color.white;
    [SerializeField] private Color secondaryColor = Color.cyan;

    [Header("Appear")]
    [SerializeField] [Min(0f)] private float transitionDurationSeconds = 0.35f;

    private Transform poolRoot;
    private readonly List<SpriteInstance> active = new List<SpriteInstance>();
    private readonly Stack<SpriteInstance> pool = new Stack<SpriteInstance>();
    private float spawnTimer;

    private struct SpriteInstance
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
        public Vector3 WorldDirection;
        public float Speed;
        public float TravelDistance;
        public float Traveled;
        public float Age;
        public float StartScale;
        public float EndScale;
        public Color TargetColor;
    }

    private void Awake()
    {
        EnsurePoolRoot();
    }

    private void OnEnable()
    {
        spawnTimer = 0f;
    }

    private void OnDisable()
    {
        DespawnAll();
    }

    private void Update()
    {
        if (zoneTrigger == null || !HasAnySprite())
            return;

        float dt = Time.deltaTime;
        spawnTimer += dt;
        while (spawnTimer >= spawnIntervalSeconds && active.Count < maxActiveSprites)
        {
            spawnTimer -= spawnIntervalSeconds;
            if (!TrySpawnOne())
                break;
        }

        for (int i = active.Count - 1; i >= 0; i--)
        {
            SpriteInstance instance = active[i];
            instance.Age += dt;

            AdvanceAlongPath(ref instance, dt);
            ApplyVisuals(ref instance);

            if (instance.Traveled >= instance.TravelDistance)
                ReleaseAt(i);
            else
                active[i] = instance;
        }
    }

    private void AdvanceAlongPath(ref SpriteInstance instance, float dt)
    {
        float step = instance.Speed * dt;
        instance.Traveled += Mathf.Abs(step);

        Vector3 pos = instance.Root.transform.position;
        pos += instance.WorldDirection * step;
        instance.Root.transform.position = pos;
    }

    private void ApplyVisuals(ref SpriteInstance instance)
    {
        float appearT = transitionDurationSeconds <= DirectionEpsilon
            ? 1f
            : Mathf.Clamp01(instance.Age / transitionDurationSeconds);

        float fadeOutDistance = Mathf.Abs(instance.Speed) * transitionDurationSeconds;
        float remaining = instance.TravelDistance - instance.Traveled;
        float disappearT = fadeOutDistance <= DirectionEpsilon
            ? 1f
            : Mathf.Clamp01(remaining / fadeOutDistance);

        float visibility = appearT * disappearT;
        float scale = Mathf.Lerp(instance.StartScale, instance.EndScale, visibility);

        if (isSquashing)
        {
            float travelT = instance.TravelDistance > DirectionEpsilon
                ? Mathf.Clamp01(instance.Traveled / instance.TravelDistance)
                : 1f;
            float heightMult = Mathf.Lerp(1f, maxSquashPercentAtPathEnd / 100f, travelT);
            instance.Root.transform.localScale = new Vector3(scale, scale * heightMult, scale);
        }
        else
        {
            instance.Root.transform.localScale = Vector3.one * scale;
        }

        Color display = instance.TargetColor;
        display.a = instance.TargetColor.a * visibility;
        instance.Renderer.color = display;
    }

    private bool TrySpawnOne()
    {
        float speed = Random.Range(GetMinSpeed(), GetMaxSpeed());
        if (Mathf.Abs(speed) <= DirectionEpsilon)
            return false;

        if (!TryGetSpawn(speed, out Vector3 spawnWorld, out Vector3 worldDirection, out float travelDistance))
            return false;

        Sprite sprite = PickRandomSprite();
        if (sprite == null)
            return false;

        float startScale = Random.Range(GetMinScale(), GetMaxScale());
        float endScale = Random.Range(GetMinScale(), GetMaxScale());

        Color targetColor = Color.Lerp(primaryColor, secondaryColor, Random.value);

        SpriteInstance instance = Rent();
        instance.WorldDirection = worldDirection;
        instance.Speed = speed;
        instance.TravelDistance = travelDistance;
        instance.Traveled = 0f;
        instance.Age = 0f;
        instance.StartScale = startScale;
        instance.EndScale = endScale;
        instance.TargetColor = targetColor;

        Transform t = instance.Root.transform;
        t.position = spawnWorld;
        t.rotation = RotationForWorldDirection(worldDirection);
        t.localScale = Vector3.one * startScale;

        instance.Renderer.sprite = sprite;
        instance.Renderer.color = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
        instance.Root.SetActive(true);

        active.Add(instance);
        return true;
    }

    private bool TryGetSpawn(float speed, out Vector3 spawnWorld, out Vector3 worldDirection, out float travelDistance)
    {
        spawnWorld = default;
        worldDirection = default;
        travelDistance = 0f;

        if (!TryGetRandomLocalSpawn(out Vector3 localSpawn))
            return false;

        if (!TryPickLocalDirection(out Vector3 localDirection))
            return false;

        Vector3 localCenter = zoneTrigger.center;
        Vector3 half = zoneTrigger.size * 0.5f;
        Vector3 travelLocalDirection = speed < 0f ? -localDirection : localDirection;
        travelDistance = MaxTravelInLocalBox(localSpawn, travelLocalDirection, localCenter, half, edgeInset);
        if (travelDistance <= DirectionEpsilon)
            return false;

        spawnWorld = zoneTrigger.transform.TransformPoint(localSpawn);
        worldDirection = zoneTrigger.transform.TransformDirection(localDirection);
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= DirectionEpsilon)
            return false;

        worldDirection.Normalize();
        return true;
    }

    private static float MaxTravelInLocalBox(
        Vector3 localPos,
        Vector3 localDir,
        Vector3 localCenter,
        Vector3 half,
        float inset)
    {
        localDir.y = 0f;
        if (localDir.sqrMagnitude <= DirectionEpsilon)
            return 0f;

        localDir.Normalize();

        float tMax = float.PositiveInfinity;
        float minX = localCenter.x - half.x;
        float maxX = localCenter.x + half.x;
        float minZ = localCenter.z - half.z;
        float maxZ = localCenter.z + half.z;

        if (Mathf.Abs(localDir.x) > DirectionEpsilon)
        {
            float toMin = (minX - localPos.x) / localDir.x;
            float toMax = (maxX - localPos.x) / localDir.x;
            if (toMin > DirectionEpsilon)
                tMax = Mathf.Min(tMax, toMin);
            if (toMax > DirectionEpsilon)
                tMax = Mathf.Min(tMax, toMax);
        }

        if (Mathf.Abs(localDir.z) > DirectionEpsilon)
        {
            float toMin = (minZ - localPos.z) / localDir.z;
            float toMax = (maxZ - localPos.z) / localDir.z;
            if (toMin > DirectionEpsilon)
                tMax = Mathf.Min(tMax, toMin);
            if (toMax > DirectionEpsilon)
                tMax = Mathf.Min(tMax, toMax);
        }

        if (float.IsPositiveInfinity(tMax))
            return 0f;

        return Mathf.Max(0f, tMax - inset);
    }

    private bool TryGetRandomLocalSpawn(out Vector3 localSpawn)
    {
        localSpawn = default;

        Vector3 localCenter = zoneTrigger.center;
        Vector3 half = zoneTrigger.size * 0.5f;
        float minX = localCenter.x - half.x + edgeInset;
        float maxX = localCenter.x + half.x - edgeInset;
        float minZ = localCenter.z - half.z + edgeInset;
        float maxZ = localCenter.z + half.z - edgeInset;
        if (maxX - minX < MinExtent || maxZ - minZ < MinExtent)
            return false;

        float bottomLocalY = localCenter.y - half.y;
        float spawnLocalY = bottomLocalY + Random.Range(MinSpawnHeightAboveBottom, MaxSpawnHeightAboveBottom);
        localSpawn = new Vector3(
            Random.Range(minX, maxX),
            spawnLocalY,
            Random.Range(minZ, maxZ));
        return true;
    }

    private bool TryPickLocalDirection(out Vector3 localDirection)
    {
        localDirection = default;

        int enabledCount = CountEnabledDirections();
        if (enabledCount == 0)
            return false;

        if (enabledCount == 4)
        {
            Vector2 dir2 = Random.insideUnitCircle;
            if (dir2.sqrMagnitude <= DirectionEpsilon)
                dir2 = Vector2.right;
            dir2.Normalize();
            localDirection = new Vector3(dir2.x, 0f, dir2.y);
            return true;
        }

        int pick = Random.Range(0, enabledCount);
        if (east)
        {
            if (pick == 0)
            {
                localDirection = Vector3.right;
                return true;
            }

            pick--;
        }

        if (west)
        {
            if (pick == 0)
            {
                localDirection = Vector3.left;
                return true;
            }

            pick--;
        }

        if (north)
        {
            if (pick == 0)
            {
                localDirection = Vector3.forward;
                return true;
            }

            pick--;
        }

        if (south)
        {
            localDirection = Vector3.back;
            return true;
        }

        return false;
    }

    private int CountEnabledDirections()
    {
        int count = 0;
        if (east)
            count++;
        if (west)
            count++;
        if (north)
            count++;
        if (south)
            count++;
        return count;
    }

    private static Quaternion RotationForWorldDirection(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= DirectionEpsilon)
            return Quaternion.identity;

        worldDirection.Normalize();
        return Quaternion.LookRotation(-Vector3.up, worldDirection);
    }

    private Sprite PickRandomSprite()
    {
        if (sprites == null)
            return null;

        int count = 0;
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                count++;
        }

        if (count == 0)
            return null;

        int pick = Random.Range(0, count);
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] == null)
                continue;

            if (pick == 0)
                return sprites[i];
            pick--;
        }

        return null;
    }

    private bool HasAnySprite()
    {
        if (sprites == null)
            return false;

        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null)
                return true;
        }

        return false;
    }

    // Aligns spawn/travel bounds and colors with the zone box collider.
    public void SyncFromZone(BoxCollider zoneBox, Color primary, Color secondary)
    {
        primaryColor = primary;
        secondaryColor = secondary;
        zoneTrigger = zoneBox;
        if (zoneBox == null)
            return;

        Transform zoneTransform = zoneBox.transform;
        transform.SetPositionAndRotation(
            zoneTransform.TransformPoint(zoneBox.center),
            zoneTransform.rotation);
    }

    // Sets which zone-local directions sprites may travel.
    public void SetDirections(bool east, bool west, bool north, bool south)
    {
        this.east = east;
        this.west = west;
        this.north = north;
        this.south = south;
    }

    private float GetMinSpeed() => Mathf.Min(minSpeed, maxSpeed);
    private float GetMaxSpeed() => Mathf.Max(minSpeed, maxSpeed);
    private float GetMinScale() => Mathf.Min(minScale, maxScale);
    private float GetMaxScale() => Mathf.Max(minScale, maxScale);

    private void EnsurePoolRoot()
    {
        if (poolRoot != null)
            return;

        GameObject root = new GameObject("_SpriteGeneratorPool");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;
    }

    private SpriteInstance Rent()
    {
        if (pool.Count > 0)
            return pool.Pop();

        GameObject go = new GameObject("Sprite");
        go.transform.SetParent(poolRoot, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.drawMode = SpriteDrawMode.Simple;

        return new SpriteInstance
        {
            Root = go,
            Renderer = sr
        };
    }

    private void ReleaseAt(int index)
    {
        SpriteInstance instance = active[index];
        instance.Root.SetActive(false);
        pool.Push(instance);
        active.RemoveAt(index);
    }

    private void DespawnAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            ReleaseAt(i);
    }
}
