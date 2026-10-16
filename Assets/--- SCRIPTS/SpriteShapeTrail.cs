using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SpriteShapeTrail : MonoBehaviour
{
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");

    private const float TeleportDistanceThreshold = 0.5f;

    [SerializeField] private SpriteRenderer sourceSprite;
    [SerializeField] private float spawnDistance = 0.025f;
    [SerializeField] private float lifetime = 0.75f;
    [SerializeField] private int sortingOrderOffset = -1;

    private readonly List<TrailStamp> stamps = new List<TrailStamp>();
    private Vector3 lastSpawnPosition;
    private bool emitting = true;

    private struct TrailStamp
    {
        public SpriteRenderer Renderer;
        public Material Material;
        public Color SpawnColor;
        public float SpawnTime;
    }

    private void Awake()
    {
        if (sourceSprite == null)
            sourceSprite = GetComponent<SpriteRenderer>();

        lastSpawnPosition = transform.position;
    }

    // Stops or resumes spawning. Stamps already placed keep fading out.
    public void SetEmitting(bool value)
    {
        emitting = value;
    }

    // Removes all trail stamps immediately.
    public void Clear()
    {
        for (int i = stamps.Count - 1; i >= 0; i--)
            DestroyStamp(stamps[i]);

        stamps.Clear();
        lastSpawnPosition = transform.position;
    }

    // Call after the object is moved instantly so the trail does not fill the jump.
    public void ResetSpawnPosition()
    {
        lastSpawnPosition = transform.position;
    }

    private void LateUpdate()
    {
        if (sourceSprite == null)
            return;

        if (emitting && sourceSprite.enabled)
            TrySpawnAlongPath();

        UpdateStamps();
    }

    private void TrySpawnAlongPath()
    {
        Vector3 current = transform.position;
        float distanceMoved = Vector3.Distance(current, lastSpawnPosition);

        if (distanceMoved > TeleportDistanceThreshold)
        {
            lastSpawnPosition = current;
            return;
        }

        while (distanceMoved >= spawnDistance)
        {
            float step = spawnDistance / distanceMoved;
            Vector3 spawnPosition = Vector3.Lerp(lastSpawnPosition, current, step);
            SpawnStamp(spawnPosition);
            lastSpawnPosition = spawnPosition;
            current = transform.position;
            distanceMoved = Vector3.Distance(current, lastSpawnPosition);
        }
    }

    private void SpawnStamp(Vector3 position)
    {
        GameObject stampObject = new GameObject($"{name} Trail Stamp");
        stampObject.transform.SetPositionAndRotation(position, transform.rotation);
        stampObject.transform.localScale = transform.lossyScale;

        SpriteRenderer stampRenderer = stampObject.AddComponent<SpriteRenderer>();
        Material stampMaterial = new Material(sourceSprite.sharedMaterial);

        stampRenderer.sprite = sourceSprite.sprite;
        stampRenderer.material = stampMaterial;
        stampRenderer.flipX = sourceSprite.flipX;
        stampRenderer.flipY = sourceSprite.flipY;
        stampRenderer.sortingLayerID = sourceSprite.sortingLayerID;
        stampRenderer.sortingOrder = sourceSprite.sortingOrder + sortingOrderOffset;

        Color spawnColor = sourceSprite.color;
        stampRenderer.color = spawnColor;
        ApplyColor(stampMaterial, spawnColor);

        stamps.Add(new TrailStamp
        {
            Renderer = stampRenderer,
            Material = stampMaterial,
            SpawnColor = spawnColor,
            SpawnTime = Time.time
        });
    }

    private void UpdateStamps()
    {
        for (int i = stamps.Count - 1; i >= 0; i--)
        {
            TrailStamp stamp = stamps[i];

            if (stamp.Renderer == null)
            {
                stamps.RemoveAt(i);
                continue;
            }

            float age = Time.time - stamp.SpawnTime;
            float fade = 1f - Mathf.Clamp01(age / lifetime);
            Color fadedColor = stamp.SpawnColor * fade;

            stamp.Renderer.color = fadedColor;
            ApplyColor(stamp.Material, fadedColor);

            if (age >= lifetime)
            {
                DestroyStamp(stamp);
                stamps.RemoveAt(i);
            }
        }
    }

    private static void ApplyColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty(ColorPropertyId))
            material.SetColor(ColorPropertyId, color);

        if (material.HasProperty(BaseColorPropertyId))
            material.SetColor(BaseColorPropertyId, color);
    }

    private static void DestroyStamp(TrailStamp stamp)
    {
        if (stamp.Material != null)
            Destroy(stamp.Material);

        if (stamp.Renderer != null)
            Destroy(stamp.Renderer.gameObject);
    }

    private void OnDisable()
    {
        Clear();
    }
}
