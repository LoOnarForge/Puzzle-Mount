using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Test harness for slippery-zone arrow drift visuals. Assign the zone BoxCollider and arrow sprites, then play.
/// </summary>
[DisallowMultipleComponent]
public class SlipperyZoneArrowEffectTest : MonoBehaviour
{
    private const float MinExtent = 0.01f;
    private const float DirectionEpsilon = 0.0001f;
    private const float BottomWallClearance = 0.1f;

    [Header("Zone")]
    [SerializeField] private BoxCollider zoneTrigger;

    [Header("Arrow sprites (up to 4)")]
    [SerializeField] private Sprite[] arrowSprites = new Sprite[4];

    [Header("Spawn")]
    [SerializeField] [Min(0)] private int maxActiveArrows = 12;
    [SerializeField] [Min(0f)] private float spawnIntervalSeconds = 0.45f;
    [SerializeField] [Min(0f)] private float edgeInset = 0.15f;

    [Header("Speed (units / second)")]
    [SerializeField] [Min(0f)] private float minSpeed = 0.15f;
    [SerializeField] [Min(0f)] private float maxSpeed = 0.45f;

    [Header("Uniform scale")]
    [SerializeField] [Min(0f)] private float minScale = 0.35f;
    [SerializeField] [Min(0f)] private float maxScale = 0.85f;

    [Header("Color & alpha over travel (0 = spawn, 1 = despawn)")]
    [SerializeField] private Gradient colorOverLifetime = CreateDefaultGradient();

    private Transform poolRoot;
    private readonly List<ArrowParticle> active = new List<ArrowParticle>();
    private readonly Stack<ArrowParticle> pool = new Stack<ArrowParticle>();
    private float spawnTimer;

    private struct ArrowParticle
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
        public Vector3 WorldDirection;
        public float Speed;
        public float TravelDistance;
        public float Traveled;
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
        if (zoneTrigger == null || !HasAnyArrowSprite())
            return;

        float dt = Time.deltaTime;
        spawnTimer += dt;
        while (spawnTimer >= spawnIntervalSeconds && active.Count < maxActiveArrows)
        {
            spawnTimer -= spawnIntervalSeconds;
            if (!TrySpawnOne())
                break;
        }

        for (int i = active.Count - 1; i >= 0; i--)
        {
            ArrowParticle particle = active[i];
            particle.Traveled += particle.Speed * dt;

            Vector3 pos = particle.Root.transform.position + particle.WorldDirection * (particle.Speed * dt);
            particle.Root.transform.position = pos;

            float t = particle.TravelDistance > DirectionEpsilon
                ? Mathf.Clamp01(particle.Traveled / particle.TravelDistance)
                : 1f;

            particle.Renderer.color = colorOverLifetime.Evaluate(t);

            if (t >= 1f)
            {
                ReleaseAt(i);
            }
            else
            {
                active[i] = particle;
            }
        }
    }

    private bool TrySpawnOne()
    {
        if (!TryGetSpawn(out Vector3 spawnWorld, out Vector3 worldDirection, out float travelDistance))
            return false;

        Sprite sprite = PickRandomSprite();
        if (sprite == null)
            return false;

        float speed = Random.Range(GetMinSpeed(), GetMaxSpeed());
        if (speed <= DirectionEpsilon || travelDistance <= DirectionEpsilon)
            return false;

        float scale = Random.Range(GetMinScale(), GetMaxScale());
        ArrowParticle particle = Rent();
        particle.WorldDirection = worldDirection;
        particle.Speed = speed;
        particle.TravelDistance = travelDistance;
        particle.Traveled = 0f;

        Transform t = particle.Root.transform;
        t.position = spawnWorld;
        t.rotation = RotationForDirection(worldDirection);
        t.localScale = Vector3.one * scale;

        particle.Renderer.sprite = sprite;
        particle.Renderer.color = colorOverLifetime.Evaluate(0f);
        particle.Root.SetActive(true);

        active.Add(particle);
        return true;
    }

    private bool TryGetSpawn(out Vector3 spawnWorld, out Vector3 worldDirection, out float travelDistance)
    {
        spawnWorld = default;
        worldDirection = default;
        travelDistance = 0f;

        Vector3 localCenter = zoneTrigger.center;
        Vector3 half = zoneTrigger.size * 0.5f;
        float minX = localCenter.x - half.x + edgeInset;
        float maxX = localCenter.x + half.x - edgeInset;
        float minZ = localCenter.z - half.z + edgeInset;
        float maxZ = localCenter.z + half.z - edgeInset;
        if (maxX - minX < MinExtent || maxZ - minZ < MinExtent)
            return false;

        float spawnLocalY = localCenter.y - half.y + BottomWallClearance;
        Vector3 localSpawn = new Vector3(
            Random.Range(minX, maxX),
            spawnLocalY,
            Random.Range(minZ, maxZ));

        Vector2 dir2 = Random.insideUnitCircle;
        if (dir2.sqrMagnitude <= DirectionEpsilon)
            dir2 = Vector2.right;
        dir2.Normalize();

        Vector3 localDir = new Vector3(dir2.x, 0f, dir2.y);
        float travelForward = MaxTravelInLocalBox(localSpawn, localDir, localCenter, half, edgeInset);
        float travelBack = MaxTravelInLocalBox(localSpawn, -localDir, localCenter, half, edgeInset);
        if (travelForward <= edgeInset)
            return false;

        float startBack = travelBack > edgeInset
            ? Random.Range(0f, Mathf.Min(travelBack * 0.5f, travelForward * 0.25f))
            : 0f;

        localSpawn -= localDir * startBack;
        travelDistance = travelForward + startBack;

        spawnWorld = zoneTrigger.transform.TransformPoint(localSpawn);
        worldDirection = zoneTrigger.transform.TransformDirection(localDir).normalized;
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

    /// <summary>
    /// Sprites are authored with the arrow pointing up (+Y). Movement uses the same horizontal direction as transform.up.
    /// </summary>
    private static Quaternion RotationForDirection(Vector3 worldDirection)
    {
        return Quaternion.LookRotation(-Vector3.up, worldDirection);
    }

    private Sprite PickRandomSprite()
    {
        int count = 0;
        for (int i = 0; i < arrowSprites.Length; i++)
        {
            if (arrowSprites[i] != null)
                count++;
        }

        if (count == 0)
            return null;

        int pick = Random.Range(0, count);
        for (int i = 0; i < arrowSprites.Length; i++)
        {
            if (arrowSprites[i] == null)
                continue;

            if (pick == 0)
                return arrowSprites[i];
            pick--;
        }

        return null;
    }

    private bool HasAnyArrowSprite()
    {
        if (arrowSprites == null)
            return false;

        for (int i = 0; i < arrowSprites.Length; i++)
        {
            if (arrowSprites[i] != null)
                return true;
        }

        return false;
    }

    private float GetMinSpeed() => Mathf.Min(minSpeed, maxSpeed);
    private float GetMaxSpeed() => Mathf.Max(minSpeed, maxSpeed);
    private float GetMinScale() => Mathf.Min(minScale, maxScale);
    private float GetMaxScale() => Mathf.Max(minScale, maxScale);

    private void EnsurePoolRoot()
    {
        if (poolRoot != null)
            return;

        GameObject root = new GameObject("_SlipperyArrowEffectPool");
        root.transform.SetParent(transform, false);
        poolRoot = root.transform;
    }

    private ArrowParticle Rent()
    {
        if (pool.Count > 0)
            return pool.Pop();

        GameObject go = new GameObject("Arrow");
        go.transform.SetParent(poolRoot, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.drawMode = SpriteDrawMode.Simple;

        return new ArrowParticle
        {
            Root = go,
            Renderer = sr
        };
    }

    private void ReleaseAt(int index)
    {
        ArrowParticle particle = active[index];
        particle.Root.SetActive(false);
        pool.Push(particle);
        active.RemoveAt(index);
    }

    private void DespawnAll()
    {
        for (int i = active.Count - 1; i >= 0; i--)
            ReleaseAt(i);
    }

    private static Gradient CreateDefaultGradient()
    {
        Gradient g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.75f, 0.9f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.75f, 0.9f, 1f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.35f, 0.2f),
                new GradientAlphaKey(0.35f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            });
        return g;
    }
}
