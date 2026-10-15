using UnityEngine;

/// <summary>
/// Fills a zone (BoxCollider bounds) with a honeycomb of hex sprites. Behaviour and animation come later.
/// </summary>
[DisallowMultipleComponent]
public class HexMeshTest : MonoBehaviour
{
    private const string HexContainerName = "_HexGrid";

    [Header("Zone")]
    [SerializeField] private BoxCollider zoneTrigger;
    [Tooltip("Height above the bottom of the zone trigger where hexes sit.")]
    [SerializeField] private float floorInsetFromBottom = 0.1f;

    [Header("Sprite")]
    [SerializeField] private Sprite hexSprite;

    [Header("Hex shape")]
    [Tooltip("Distance from the center of the hex to any corner (tip). Tune until neighbours share edges.")]
    [SerializeField] private float hexRadius = 0.5f;

    [Tooltip("Flat top = flat edge on top/bottom of the sprite. Pointy top = tip on top/bottom. Must match your art.")]
    [SerializeField] private bool flatTop = true;

    [Tooltip("How far apart hex centers are. Below 1 = pull grid tighter; above 1 = spread out.")]
    [SerializeField] private float gridSpacingMultiplier = 1f;

    [Tooltip("Makes each sprite larger/smaller without moving grid centers. Use above 1 to close gaps from PNG padding.")]
    [SerializeField] private float spriteVisualScale = 1f;

    [Header("Optional")]
    [Tooltip("If on, scales each cell so the sprite size matches the grid math for hexRadius.")]
    [SerializeField] private bool scaleSpritesToHexRadius = true;

    [SerializeField] private bool buildOnStart = true;

    private void Awake()
    {
        if (zoneTrigger == null)
            zoneTrigger = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        if (buildOnStart)
            BuildGrid();
    }

    [ContextMenu("Rebuild Grid")]
    public void BuildGrid()
    {
        if (zoneTrigger == null)
        {
            Debug.LogWarning($"{nameof(HexMeshTest)}: assign a BoxCollider on zoneTrigger.", this);
            return;
        }

        if (hexSprite == null)
        {
            Debug.LogWarning($"{nameof(HexMeshTest)}: assign hexSprite.", this);
            return;
        }

        Transform root = EnsureHexContainer();
        ClearChildren(root);

        Bounds zoneBounds = zoneTrigger.bounds;
        float floorY = zoneBounds.min.y + floorInsetFromBottom;
        root.position = new Vector3(zoneBounds.center.x, floorY, zoneBounds.center.z);
        root.rotation = Quaternion.identity;

        float gridR = GetGridRadius();
        if (gridR <= 0f)
            return;

        float sqrt3 = Mathf.Sqrt(3f);
        float hexStepX = flatTop ? sqrt3 * gridR : 1.5f * gridR;
        float hexStepZ = flatTop ? 1.5f * gridR : sqrt3 * gridR;

        int colRange = Mathf.CeilToInt(zoneBounds.extents.x / hexStepX) + 2;
        int rowRange = Mathf.CeilToInt(zoneBounds.extents.z / hexStepZ) + 2;

        for (int row = -rowRange; row <= rowRange; row++)
        {
            for (int col = -colRange; col <= colRange; col++)
            {
                Vector2 hexXZ = GetHoneycombOffset(col, row);
                Vector3 worldCenter = root.TransformPoint(new Vector3(hexXZ.x, 0f, hexXZ.y));

                if (!HexFitsInsideZone(worldCenter, GetGridRadius(), zoneBounds))
                    continue;

                GameObject cell = new GameObject($"Hex_{col}_{row}");
                cell.transform.SetParent(root, false);
                cell.transform.localPosition = new Vector3(hexXZ.x, 0f, hexXZ.y);
                cell.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                SpriteRenderer spriteRenderer = cell.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = hexSprite;

                if (scaleSpritesToHexRadius)
                    FitSpriteToHexRadius(cell.transform, spriteRenderer);
            }
        }
    }

    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        Transform root = transform.Find(HexContainerName);
        if (root != null)
            ClearChildren(root);
    }

    private Transform EnsureHexContainer()
    {
        Transform existing = transform.Find(HexContainerName);
        if (existing != null)
            return existing;

        GameObject container = new GameObject(HexContainerName);
        container.transform.SetParent(transform, false);
        return container.transform;
    }

    private bool HexFitsInsideZone(Vector3 worldCenter, float radius, Bounds zoneBounds)
    {
        Vector2 half = GetHexHalfExtents(radius);
        return worldCenter.x - half.x >= zoneBounds.min.x && worldCenter.x + half.x <= zoneBounds.max.x &&
               worldCenter.z - half.y >= zoneBounds.min.z && worldCenter.z + half.y <= zoneBounds.max.z;
    }

    private float GetGridRadius()
    {
        if (hexSprite == null || hexRadius <= 0f)
            return 0f;

        float baseRadius = scaleSpritesToHexRadius ? hexRadius : DeriveRadiusFromSprite(hexSprite);
        return baseRadius * gridSpacingMultiplier;
    }

    private float GetSpriteFitRadius()
    {
        if (hexRadius <= 0f)
            return 0f;

        float baseRadius = scaleSpritesToHexRadius ? hexRadius : DeriveRadiusFromSprite(hexSprite);
        return baseRadius * spriteVisualScale;
    }

    private float DeriveRadiusFromSprite(Sprite sprite)
    {
        Vector2 size = sprite.bounds.size;
        if (flatTop)
            return size.y * 0.5f;
        return size.x * 0.5f;
    }

    private Vector2 GetHoneycombOffset(int column, int row)
    {
        float r = GetGridRadius();
        float sqrt3 = Mathf.Sqrt(3f);

        if (flatTop)
        {
            float x = sqrt3 * r * (column + (row & 1) * 0.5f);
            float z = 1.5f * r * row;
            return new Vector2(x, z);
        }

        float px = 1.5f * r * column;
        float pz = sqrt3 * r * (row + (column & 1) * 0.5f);
        return new Vector2(px, pz);
    }

    private Vector2 GetHexHalfExtents(float radius)
    {
        float sqrt3 = Mathf.Sqrt(3f);
        if (flatTop)
            return new Vector2(sqrt3 * radius * 0.5f, radius);
        return new Vector2(radius, sqrt3 * radius * 0.5f);
    }

    private void FitSpriteToHexRadius(Transform cell, SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer.sprite == null)
            return;

        cell.localScale = Vector3.one;

        Vector2 targetSize = GetHexWorldSize(GetSpriteFitRadius());
        Vector2 currentSize = spriteRenderer.sprite.bounds.size;
        if (currentSize.x <= 0f || currentSize.y <= 0f)
            return;

        float scaleX = targetSize.x / currentSize.x;
        float scaleY = targetSize.y / currentSize.y;
        float uniform = Mathf.Min(scaleX, scaleY);
        cell.localScale = new Vector3(uniform, uniform, 1f);
    }

    private Vector2 GetHexWorldSize(float radius)
    {
        float sqrt3 = Mathf.Sqrt(3f);
        if (flatTop)
            return new Vector2(sqrt3 * radius, 2f * radius);
        return new Vector2(2f * radius, sqrt3 * radius);
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        BoxCollider trigger = zoneTrigger != null ? zoneTrigger : GetComponent<BoxCollider>();
        if (trigger == null)
            return;

        Bounds zoneBounds = trigger.bounds;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
        Gizmos.DrawWireCube(zoneBounds.center, zoneBounds.size);

        float floorY = zoneBounds.min.y + floorInsetFromBottom;
        Gizmos.color = new Color(0f, 0.7f, 1f, 0.5f);
        Vector3 floorCenter = new Vector3(zoneBounds.center.x, floorY, zoneBounds.center.z);
        Gizmos.DrawLine(floorCenter + Vector3.left * 0.5f, floorCenter + Vector3.right * 0.5f);

        if (hexRadius <= 0f)
            return;

        float r = GetGridRadius();
        int preview = 3;
        for (int row = -preview; row <= preview; row++)
        {
            for (int col = -preview; col <= preview; col++)
            {
                Vector2 hexXZ = GetHoneycombOffset(col, row);
                Vector3 center = floorCenter + new Vector3(hexXZ.x, 0f, hexXZ.y);
                if (!HexFitsInsideZone(center, r, zoneBounds))
                    continue;
                DrawHexGizmo(center, r * spriteVisualScale, flatTop);
            }
        }
    }

    private static void DrawHexGizmo(Vector3 center, float radius, bool flatTopOrientation)
    {
        Vector3[] corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float deg = flatTopOrientation ? i * 60f : 90f + i * 60f;
            float rad = deg * Mathf.Deg2Rad;
            corners[i] = center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
        }

        for (int i = 0; i < 6; i++)
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 6]);
    }
#endif
}
