using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fills a zone with a honeycomb of hex sprites; styles, color, and fade visibility are configurable.
/// </summary>
[DisallowMultipleComponent]
public class HexZone : MonoBehaviour
{
    private const string HexContainerName = "_HexGrid";

    private enum HexVisibilityState
    {
        Hidden,
        Shown,
        FadingOut,
        FadingIn
    }

    private sealed class HexCell
    {
        public SpriteRenderer Renderer;
        public Color DisplayColor;
        public HexVisibilityState State;
        public float TransitionElapsed;
        public float ActiveTransitionDuration;
        public float WaitTimer;
    }

    private const float FloorInsetFromBottom = -0.09f;

    [Header("Zone")]
    [SerializeField] private BoxCollider zoneTrigger;

    [Header("Hex styles")]
    [SerializeField] private Sprite[] hexStyleSprites;

    [Header("Color")]
    [Tooltip("One end of the color range. Each hex picks a random color between this and Hex Color 2 (RGB and alpha).")]
    [SerializeField] private Color hexColor = Color.white;
    [Tooltip("Other end of the color range. Fades use each hex's chosen color alpha at full visibility.")]
    [SerializeField] private Color hexColor2 = Color.white;
    [Tooltip("After picking between the two colors, optional lighter/darker tweak (same hue). 0 = off.")]
    [Range(0f, 1f)]
    [SerializeField] private float colorShadeVariation = 0.35f;

    [Header("Visibility")]
    [Tooltip("Target share of hexes fully visible (0–100%).")]
    [Range(0f, 100f)]
    [SerializeField] private float maxHexPercent = 85f;
    [Tooltip("Max hexes mid-fade at once = Max Hex % × this value (e.g. 50% and 10% → 5% of grid fading). Fully hidden does not count.")]
    [Range(0f, 100f)]
    [SerializeField] private float maxPercentInTransition = 10f;
    [Space(16)]
    [Tooltip("Base seconds for each fade in or fade out.")]
    [Min(0.01f)]
    [SerializeField] private float transitionDuration = 1.5f;
    [Tooltip("Each fade can be this many seconds shorter or longer than Transition Duration (e.g. 0.5 → ±0.5s).")]
    [Min(0f)]
    [SerializeField] private float transitionDurationRandomRange = 0.5f;
    [Tooltip("Seconds a hex stays fully visible before it may fade out.")]
    [Min(0.1f)]
    [SerializeField] private float minVisibleTime = 2f;
    [Min(0.1f)]
    [SerializeField] private float maxVisibleTime = 10f;
    [Tooltip("Seconds a hex stays fully invisible before it may fade in.")]
    [Min(0.1f)]
    [SerializeField] private float minHiddenTime = 1f;
    [Min(0.1f)]
    [SerializeField] private float maxHiddenTime = 8f;

    [Header("Hex shape")]
    [Tooltip("Distance from the center of the hex to any corner (tip). Tune until neighbours share edges.")]
    [SerializeField] private float hexRadius = 0.5f;

    [Tooltip("How far apart hex centers are. Below 1 = pull grid tighter; above 1 = spread out.")]
    [SerializeField] private float gridSpacingMultiplier = 1f;

    [Tooltip("Makes each sprite larger/smaller without moving grid centers. Use above 1 to close gaps from PNG padding.")]
    [SerializeField] private float spriteVisualScale = 1f;

    [Header("Optional")]
    [Tooltip("If on, scales each cell so the sprite size matches the grid math for hexRadius.")]
    [SerializeField] private bool scaleSpritesToHexRadius = true;

    [Tooltip("When off, a parent zone must call BuildFromZoneBounds.")]
    [SerializeField] private bool buildOnStart;

    private bool hasLayoutBounds;
    private Bounds layoutBounds;

    private readonly List<HexCell> _cells = new List<HexCell>();

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

    private void Update()
    {
        if (!Application.isPlaying || _cells.Count == 0)
            return;

        float dt = Time.deltaTime;
        for (int i = 0; i < _cells.Count; i++)
            UpdateCellVisibility(_cells[i], dt);
    }

    /// <summary>
    /// Builds the hex fill from world-space zone bounds and two endpoint colors, then removes this object's zone collider.
    /// </summary>
    public void BuildFromZoneBounds(Bounds worldZoneBounds, Color colorA, Color colorB)
    {
        hexColor = colorA;
        hexColor2 = colorB;
        layoutBounds = worldZoneBounds;
        hasLayoutBounds = true;
        BuildGrid();
        RemoveLocalZoneCollider();
    }

    [ContextMenu("Rebuild Grid")]
    public void BuildGrid()
    {
        if (!TryGetLayoutBounds(out Bounds zoneBounds))
        {
            Debug.LogWarning($"{nameof(HexZone)}: no zone bounds (assign a BoxCollider or call BuildFromZoneBounds).", this);
            return;
        }

        if (!TryGetLayoutSprite(out Sprite layoutSprite))
        {
            Debug.LogWarning($"{nameof(HexZone)}: assign at least one hex style sprite.", this);
            return;
        }

        Transform root = EnsureHexContainer();
        ClearChildren(root);
        _cells.Clear();

        float floorY = zoneBounds.min.y + FloorInsetFromBottom;
        root.position = new Vector3(zoneBounds.center.x, floorY, zoneBounds.center.z);
        root.rotation = Quaternion.identity;

        float gridR = GetGridRadius(layoutSprite);
        if (gridR <= 0f)
            return;

        float sqrt3 = Mathf.Sqrt(3f);
        float hexStepX = sqrt3 * gridR;
        float hexStepZ = 1.5f * gridR;

        int colRange = Mathf.CeilToInt(zoneBounds.extents.x / hexStepX) + 2;
        int rowRange = Mathf.CeilToInt(zoneBounds.extents.z / hexStepZ) + 2;

        for (int row = -rowRange; row <= rowRange; row++)
        {
            for (int col = -colRange; col <= colRange; col++)
            {
                Vector2 hexXZ = GetHoneycombOffset(col, row, gridR);
                Vector3 worldCenter = root.TransformPoint(new Vector3(hexXZ.x, 0f, hexXZ.y));

                if (!HexFitsInsideZone(worldCenter, gridR, zoneBounds))
                    continue;

                GameObject cellObject = new GameObject($"Hex_{col}_{row}");
                cellObject.transform.SetParent(root, false);
                cellObject.transform.localPosition = new Vector3(hexXZ.x, 0f, hexXZ.y);
                cellObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                SpriteRenderer spriteRenderer = cellObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = PickRandomStyleSprite();

                if (scaleSpritesToHexRadius)
                    FitSpriteToHexRadius(cellObject.transform, spriteRenderer, layoutSprite);

                HexCell cell = new HexCell
                {
                    Renderer = spriteRenderer,
                    DisplayColor = PickRandomHexColor(hexColor, hexColor2, colorShadeVariation),
                    State = HexVisibilityState.Hidden,
                    TransitionElapsed = 0f,
                    WaitTimer = 0f
                };
                InitializeCellAsHidden(cell);
                _cells.Add(cell);
            }
        }

        ApplyInitialShownCount();
    }

    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        Transform root = transform.Find(HexContainerName);
        if (root != null)
            ClearChildren(root);
        _cells.Clear();
    }

    private bool TryGetLayoutBounds(out Bounds zoneBounds)
    {
        if (hasLayoutBounds)
        {
            zoneBounds = layoutBounds;
            return true;
        }

        if (zoneTrigger == null)
            zoneTrigger = GetComponent<BoxCollider>();

        if (zoneTrigger != null)
        {
            zoneBounds = zoneTrigger.bounds;
            return true;
        }

        zoneBounds = default;
        return false;
    }

    private void RemoveLocalZoneCollider()
    {
        if (zoneTrigger == null)
            zoneTrigger = GetComponent<BoxCollider>();

        if (zoneTrigger == null)
            return;

        if (Application.isPlaying)
            Destroy(zoneTrigger);
        else
            DestroyImmediate(zoneTrigger);

        zoneTrigger = null;
    }

    private void InitializeCellAsHidden(HexCell cell)
    {
        cell.TransitionElapsed = 0f;
        cell.ActiveTransitionDuration = transitionDuration;
        cell.State = HexVisibilityState.Hidden;
        cell.WaitTimer = PickHiddenTime();
        ApplyCellColor(cell, 0f);
    }

    private void ApplyInitialShownCount()
    {
        int targetShown = GetTargetShownCount();
        for (int i = 0; i < targetShown; i++)
        {
            if (!TryPickCellInState(HexVisibilityState.Hidden, out HexCell cell))
                break;

            cell.State = HexVisibilityState.Shown;
            ApplyCellColor(cell, 1f);
            cell.WaitTimer = PickVisibleTime();
        }
    }

    private void UpdateCellVisibility(HexCell cell, float deltaTime)
    {
        if (cell.State == HexVisibilityState.FadingOut || cell.State == HexVisibilityState.FadingIn)
        {
            UpdateCellFade(cell, deltaTime);
            return;
        }

        cell.WaitTimer -= deltaTime;
        if (cell.WaitTimer > 0f)
            return;

        if (cell.State == HexVisibilityState.Shown)
        {
            if (maxHexPercent >= 100f)
            {
                cell.WaitTimer = PickVisibleTime();
                return;
            }

            if (TryBeginFadeOut(cell))
                return;

            cell.WaitTimer = 0.25f;
            return;
        }

        if (maxHexPercent <= 0f)
            return;

        if (!CanStartFadeIn())
        {
            cell.WaitTimer = 0.25f;
            return;
        }

        BeginFadeIn(cell);
    }

    private void UpdateCellFade(HexCell cell, float deltaTime)
    {
        cell.TransitionElapsed += deltaTime;
        float duration = Mathf.Max(0.01f, cell.ActiveTransitionDuration);
        float t = Mathf.Clamp01(cell.TransitionElapsed / duration);

        if (cell.State == HexVisibilityState.FadingOut)
        {
            ApplyCellColor(cell, 1f - t);
            if (t < 1f)
                return;

            cell.State = HexVisibilityState.Hidden;
            cell.TransitionElapsed = 0f;
            ApplyCellColor(cell, 0f);
            cell.WaitTimer = PickHiddenTime();
            return;
        }

        ApplyCellColor(cell, t);
        if (t < 1f)
            return;

        cell.State = HexVisibilityState.Shown;
        cell.TransitionElapsed = 0f;
        ApplyCellColor(cell, 1f);
        cell.WaitTimer = PickVisibleTime();
    }

    private bool TryBeginFadeOut(HexCell cell)
    {
        if (!CanStartFadeOut())
            return false;

        BeginFadeOut(cell);
        return true;
    }

    private bool CanStartFadeIn()
    {
        if (CountFading() >= GetMaxTransitionCount())
            return false;

        return CountShown() + CountFadingIn() < GetTargetShownCount();
    }

    private bool CanStartFadeOut()
    {
        if (CountShown() <= 0 || CountFading() >= GetMaxTransitionCount())
            return false;

        if (CountShown() > GetTargetShownCount())
            return true;

        return CountHidden() > 0;
    }

    private void BeginFadeIn(HexCell cell)
    {
        cell.State = HexVisibilityState.FadingIn;
        cell.TransitionElapsed = 0f;
        cell.ActiveTransitionDuration = PickTransitionDuration();
        ApplyCellColor(cell, 0f);
    }

    private void BeginFadeOut(HexCell cell)
    {
        cell.State = HexVisibilityState.FadingOut;
        cell.TransitionElapsed = 0f;
        cell.ActiveTransitionDuration = PickTransitionDuration();
        ApplyCellColor(cell, 1f);
    }

    private float PickTransitionDuration()
    {
        if (transitionDurationRandomRange <= 0f)
            return transitionDuration;

        float min = transitionDuration - transitionDurationRandomRange;
        float max = transitionDuration + transitionDurationRandomRange;
        return Mathf.Max(0.01f, Random.Range(min, max));
    }

    private float PickVisibleTime()
    {
        float maxHex = GetMaxHexFraction();
        float min = minVisibleTime * maxHex;
        float max = maxVisibleTime * maxHex;
        if (max < min)
            max = min;
        return Random.Range(min, max);
    }

    private float PickHiddenTime()
    {
        float maxHex = GetMaxHexFraction();
        float min = minHiddenTime * (1f - maxHex);
        float max = maxHiddenTime * (1f - maxHex);
        if (max < min)
            max = min;
        if (max <= 0f)
            return Random.Range(minHiddenTime, maxHiddenTime);
        return Random.Range(min, max);
    }

    private float GetMaxHexFraction()
    {
        return maxHexPercent / 100f;
    }

    private int GetTargetShownCount()
    {
        return Mathf.RoundToInt(_cells.Count * GetMaxHexFraction());
    }

    private int GetMaxTransitionCount()
    {
        float capFraction = GetMaxHexFraction() * (maxPercentInTransition / 100f);
        return Mathf.RoundToInt(_cells.Count * capFraction);
    }

    private int CountShown()
    {
        int count = 0;
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].State == HexVisibilityState.Shown)
                count++;
        }

        return count;
    }

    private int CountHidden()
    {
        int count = 0;
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].State == HexVisibilityState.Hidden)
                count++;
        }

        return count;
    }

    private int CountFadingIn()
    {
        int count = 0;
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].State == HexVisibilityState.FadingIn)
                count++;
        }

        return count;
    }

    private int CountFading()
    {
        int count = 0;
        for (int i = 0; i < _cells.Count; i++)
        {
            HexVisibilityState state = _cells[i].State;
            if (state == HexVisibilityState.FadingIn || state == HexVisibilityState.FadingOut)
                count++;
        }

        return count;
    }

    private bool TryPickCellInState(HexVisibilityState state, out HexCell picked)
    {
        picked = null;
        int matchCount = 0;
        for (int i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].State != state)
                continue;

            matchCount++;
            if (Random.Range(0, matchCount) == 0)
                picked = _cells[i];
        }

        return picked != null;
    }

    private static void ApplyCellColor(HexCell cell, float visibility01)
    {
        Color c = cell.DisplayColor;
        c.a = cell.DisplayColor.a * Mathf.Clamp01(visibility01);
        cell.Renderer.color = c;
    }

    private static Color PickRandomHexColor(Color colorA, Color colorB, float shadeVariation)
    {
        Color result = Color.Lerp(colorA, colorB, Random.value);

        if (shadeVariation <= 0f)
            return result;

        Color.RGBToHSV(result, out float h, out float s, out float v);
        float shade = Random.Range(-1f, 1f) * shadeVariation;
        v = Mathf.Clamp01(v + shade * 0.55f);
        s = Mathf.Clamp01(s + shade * 0.45f);
        Color adjusted = Color.HSVToRGB(h, s, v);
        adjusted.a = result.a;
        return adjusted;
    }

    private Sprite PickRandomStyleSprite()
    {
        if (hexStyleSprites == null || hexStyleSprites.Length == 0)
            return null;

        int validCount = 0;
        for (int i = 0; i < hexStyleSprites.Length; i++)
        {
            if (hexStyleSprites[i] != null)
                validCount++;
        }

        if (validCount == 0)
            return null;

        int pick = Random.Range(0, validCount);
        for (int i = 0; i < hexStyleSprites.Length; i++)
        {
            if (hexStyleSprites[i] == null)
                continue;

            if (pick == 0)
                return hexStyleSprites[i];
            pick--;
        }

        return null;
    }

    private bool TryGetLayoutSprite(out Sprite layoutSprite)
    {
        layoutSprite = null;
        if (hexStyleSprites == null || hexStyleSprites.Length == 0)
            return false;

        for (int i = 0; i < hexStyleSprites.Length; i++)
        {
            if (hexStyleSprites[i] == null)
                continue;

            layoutSprite = hexStyleSprites[i];
            return true;
        }

        return false;
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

    private float GetGridRadius(Sprite layoutSprite)
    {
        if (layoutSprite == null || hexRadius <= 0f)
            return 0f;

        float baseRadius = scaleSpritesToHexRadius ? hexRadius : DeriveRadiusFromSprite(layoutSprite);
        return baseRadius * gridSpacingMultiplier;
    }

    private float GetSpriteFitRadius(Sprite layoutSprite)
    {
        if (hexRadius <= 0f)
            return 0f;

        float baseRadius = scaleSpritesToHexRadius ? hexRadius : DeriveRadiusFromSprite(layoutSprite);
        return baseRadius * spriteVisualScale;
    }

    private float DeriveRadiusFromSprite(Sprite sprite)
    {
        return sprite.bounds.size.y * 0.5f;
    }

    private Vector2 GetHoneycombOffset(int column, int row, float gridRadius)
    {
        float sqrt3 = Mathf.Sqrt(3f);
        float x = sqrt3 * gridRadius * (column + (row & 1) * 0.5f);
        float z = 1.5f * gridRadius * row;
        return new Vector2(x, z);
    }

    private Vector2 GetHexHalfExtents(float radius)
    {
        float sqrt3 = Mathf.Sqrt(3f);
        return new Vector2(sqrt3 * radius * 0.5f, radius);
    }

    private void FitSpriteToHexRadius(Transform cell, SpriteRenderer spriteRenderer, Sprite layoutSprite)
    {
        if (spriteRenderer.sprite == null)
            return;

        cell.localScale = Vector3.one;

        Vector2 targetSize = GetHexWorldSize(GetSpriteFitRadius(layoutSprite));
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
        return new Vector2(sqrt3 * radius, 2f * radius);
    }

    private static void ClearChildren(Transform root)
    {
        if (root == null)
            return;

        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (child == null)
                continue;

            GameObject go = child.gameObject;
            if (Application.isPlaying)
            {
                Destroy(go);
                continue;
            }

            if (!go.scene.IsValid())
                continue;

#if UNITY_EDITOR
            UnityEditor.Undo.DestroyObjectImmediate(go);
#else
            DestroyImmediate(go);
#endif
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!TryGetLayoutBounds(out Bounds zoneBounds))
            return;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
        Gizmos.DrawWireCube(zoneBounds.center, zoneBounds.size);

        float floorY = zoneBounds.min.y + FloorInsetFromBottom;
        Gizmos.color = new Color(0f, 0.7f, 1f, 0.5f);
        Vector3 floorCenter = new Vector3(zoneBounds.center.x, floorY, zoneBounds.center.z);
        Gizmos.DrawLine(floorCenter + Vector3.left * 0.5f, floorCenter + Vector3.right * 0.5f);

        if (hexRadius <= 0f || !TryGetLayoutSprite(out Sprite layoutSprite))
            return;

        float r = GetGridRadius(layoutSprite);
        int preview = 3;
        for (int row = -preview; row <= preview; row++)
        {
            for (int col = -preview; col <= preview; col++)
            {
                Vector2 hexXZ = GetHoneycombOffset(col, row, r);
                Vector3 center = floorCenter + new Vector3(hexXZ.x, 0f, hexXZ.y);
                if (!HexFitsInsideZone(center, r, zoneBounds))
                    continue;
                DrawHexGizmo(center, r * spriteVisualScale);
            }
        }
    }

    private static void DrawHexGizmo(Vector3 center, float radius)
    {
        Vector3[] corners = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            float deg = i * 60f;
            float rad = deg * Mathf.Deg2Rad;
            corners[i] = center + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
        }

        for (int i = 0; i < 6; i++)
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 6]);
    }
#endif
}
