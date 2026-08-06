using UnityEngine;
using UnityEngine.InputSystem;

/// Manages visual feedback for Runode cubes with smooth transitions for both in-range and out-of-range states.
public class RunodeHighlighter : MonoBehaviour
{
    [Header("HIGHLIGHT SETTINGS")]
    [ColorUsage(true, true)]
    public Color inRangeColor = new Color(1.5f, 1.5f, 1.5f, 1.0f);
    [ColorUsage(true, true)]
    public Color outOfRangeColor = new Color(1.2f, 1.2f, 1.2f, 1.0f);
    [ColorUsage(true, true)]
    public Color pushableColor = new Color(0.8f, 1.5f, 0.8f, 1.0f);
    public float highlightUpSpeed = 15f;
    public float highlightDownSpeed = 5f;

    [Header("POWER LINE DARKENING:")]
    public bool darkenLinesOnHighlight = true;
    public Color lineDarkenedColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);

    [Header("REFERENCES")]
    public MeshRenderer cubeRenderer;
    public GameObject selectionFrame; 
    public TimCubeController timCubeController;

    [Header("FACE DECALS")]
    public GameObject[] faceDecals; // Order: 0:Top, 1:Bottom, 2:North, 3:South, 4:East, 5:West

    [Header("LINE SPRITES")]
    public SpriteRenderer[] lineSprites; // Order: 0:Top, 1:Bottom, 2:North, 3:South, 4:East, 5:West

    private RunodeCube cube;
    private MaterialPropertyBlock cubePropBlock;
    private MaterialPropertyBlock linePropBlock;

    private Color restingColor = Color.white;
    private Color currentColor = Color.white;
    private Color currentLineColor = Color.white;
    
    private bool isHovered = false;
    private bool isInRange = false;
    private bool isPushable = false;
    private int activeDecalIndex = -1;
    private bool colorNeedsUpdate = false;
    private bool lineColorNeedsUpdate = false;

    private static RunodeHighlighter currentHovered;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        cubePropBlock = new MaterialPropertyBlock();
        linePropBlock = new MaterialPropertyBlock();
        
        cube = GetComponent<RunodeCube>();
        if (cubeRenderer == null) cubeRenderer = GetComponentInChildren<MeshRenderer>();
        if (timCubeController == null)
            timCubeController = FindAnyObjectByType<TimCubeController>();
        if (selectionFrame != null) selectionFrame.SetActive(false);

        ClearAllDecals();
    }

    private void Start()
    {
        // Read after VisualRandomizer Awake so highlight restores the chosen material tint.
        if (cubeRenderer != null && cubeRenderer.sharedMaterial != null && cubeRenderer.sharedMaterial.HasProperty(BaseColorId))
            restingColor = cubeRenderer.sharedMaterial.GetColor(BaseColorId);

        currentColor = restingColor;
    }

    private void Update()
    {
        // 1. Cube Highlight Transition
        if (colorNeedsUpdate)
        {
            Color target;
            bool lightingUp;

            if (!isHovered)
            {
                target = isPushable ? pushableColor : restingColor;
                lightingUp = isPushable;
            }
            else
            {
                target = isInRange ? inRangeColor : outOfRangeColor;
                lightingUp = isInRange;
            }

            float speed = lightingUp ? highlightUpSpeed : highlightDownSpeed;
            currentColor = Color.Lerp(currentColor, target, Time.deltaTime * speed);
        
            if (cubeRenderer != null)
            {
                cubeRenderer.GetPropertyBlock(cubePropBlock);
                cubePropBlock.SetColor(BaseColorId, currentColor);
                cubeRenderer.SetPropertyBlock(cubePropBlock);
            }

            if (ColorsApproximatelyEqual(currentColor, target))
            {
                currentColor = target;
                colorNeedsUpdate = false;
            }
        }

        // 2. Selection Frame Logic (Occluded gizmo)
        if (selectionFrame != null)
        {
            bool shouldBeActive = isHovered && isInRange;
            if (selectionFrame.activeSelf != shouldBeActive)
                selectionFrame.SetActive(shouldBeActive);
        }

        // 3. Decal Logic
        if (faceDecals != null)
        {
            for (int i = 0; i < faceDecals.Length; i++)
            {
                if (faceDecals[i] == null) continue;
                bool shouldBeActive = isHovered && isInRange && i == activeDecalIndex;
                if (faceDecals[i].activeSelf != shouldBeActive)
                    faceDecals[i].SetActive(shouldBeActive);
            }
        }

        // 4. Power Line Darkening Logic (Layered)
        if (darkenLinesOnHighlight)
        {
            bool shouldDarken = isHovered && isInRange;
            if (shouldDarken && lineColorNeedsUpdate)
            {
                currentLineColor = Color.Lerp(currentLineColor, lineDarkenedColor, Time.deltaTime * highlightUpSpeed);
                UpdateHighlightLayerOnFaces(currentLineColor);

                if (ColorsApproximatelyEqual(currentLineColor, lineDarkenedColor))
                {
                    currentLineColor = lineDarkenedColor;
                    lineColorNeedsUpdate = false;
                }
            }
            else if (!shouldDarken && currentLineColor != Color.white)
            {
                currentLineColor = Color.white;
                lineColorNeedsUpdate = true;
                ClearHighlightLayerOnFaces();
            }
        }
    }

    private void UpdateHighlightLayerOnFaces(Color highlightColor)
    {
        if (lineSprites == null) return;

        linePropBlock.Clear();
        linePropBlock.SetColor("_Color", highlightColor);

        foreach (var sr in lineSprites)
        {
            if (sr != null) sr.SetPropertyBlock(linePropBlock);
        }
    }

    private void ClearHighlightLayerOnFaces()
    {
        if (lineSprites == null) return;

        foreach (var sr in lineSprites)
        {
            if (sr != null) sr.SetPropertyBlock(null);
        }
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f
            && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
    }

    public void SetHighlight(bool rotatable, int faceIndex = -1)
    {
        isHovered = true;
        isInRange = rotatable;
        activeDecalIndex = faceIndex;
        colorNeedsUpdate = true;
        lineColorNeedsUpdate = true;
    }

    public void ClearHighlight()
    {
        isHovered = false;
        activeDecalIndex = -1;
        ClearAllDecals();
        ClearHighlightLayerOnFaces();
        currentLineColor = Color.white;
        lineColorNeedsUpdate = true;
        colorNeedsUpdate = true;
    }

    private void LateUpdate()
    {
        TimCubeController controller = timCubeController;
        if (controller == null)
        {
            if (isHovered) ClearHighlight();
            if (isPushable) { isPushable = false; colorNeedsUpdate = true; }
            return;
        }

        RunodeMovement hitCube = controller.MouseHitCube;
        if (hitCube != null && hitCube.gameObject == gameObject)
        {
            if (currentHovered != null && currentHovered != this)
                currentHovered.ClearHighlight();

            bool rotatable = controller.IsCubeRotatable(hitCube, controller.MouseHitPoint);

            int faceIndex = -1;
            bool mousePressed = Mouse.current != null && Mouse.current.leftButton.isPressed;
            if (cube != null && !mousePressed)
                faceIndex = cube.GetFaceIndexFromPoint(controller.MouseHitPoint);
            else if (isHovered)
                faceIndex = activeDecalIndex;

            SetHighlight(rotatable, faceIndex);
            currentHovered = this;
        }
        else
        {
            if (isHovered) ClearHighlight();
            if (currentHovered == this) currentHovered = null;
        }

        RunodeMovement pushable = controller.PushableRunode;
        bool wasPushable = isPushable;
        isPushable = pushable != null && pushable.gameObject == gameObject;
        if (isPushable != wasPushable) colorNeedsUpdate = true;
    }

    private void ClearAllDecals()
    {
        if (faceDecals == null) return;
        foreach (var decal in faceDecals)
        {
            if (decal != null) decal.SetActive(false);
        }
    }
}
