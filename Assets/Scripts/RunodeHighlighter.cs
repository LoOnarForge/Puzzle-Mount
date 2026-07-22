using UnityEngine;
using System.Collections.Generic;

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
    public float transitionSpeed = 15f;
    
    [Header("POWER LINE DARKENING:")]
    public bool darkenLinesOnHighlight = true;
    public Color lineDarkenedColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);

    [Header("REFERENCES")]
    public MeshRenderer cubeRenderer;
    public GameObject selectionFrame; 
    public TimCubeController timCubeController;

    [Header("FACE DECALS")]
    public GameObject[] faceDecals; // Order: 0:Top, 1:Bottom, 2:North, 3:South, 4:East, 5:West

    private RunodePower powerSystem;
    private MaterialPropertyBlock cubePropBlock;
    private MaterialPropertyBlock linePropBlock;

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

    private List<SpriteRenderer> cachedLineRenderers = new List<SpriteRenderer>();
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        cubePropBlock = new MaterialPropertyBlock();
        linePropBlock = new MaterialPropertyBlock();
        
        powerSystem = GetComponent<RunodePower>();
        if (cubeRenderer == null) cubeRenderer = GetComponentInChildren<MeshRenderer>();
        if (selectionFrame != null) selectionFrame.SetActive(false);

        // Ensure all decals are off initially
        ClearAllDecals();

        // Cache the line renderers once
        SpriteRenderer[] allSprites = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in allSprites)
        {
            if (sr.name.StartsWith("Power Line Sprite"))
                cachedLineRenderers.Add(sr);
        }
    }

    private void Update()
    {
        // 1. Cube Highlight Transition
        if (colorNeedsUpdate)
        {
            Color target;
            if (!isHovered) target = isPushable ? pushableColor : Color.white;
            else target = isInRange ? inRangeColor : outOfRangeColor;

            currentColor = Color.Lerp(currentColor, target, Time.deltaTime * transitionSpeed);
        
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
        if (darkenLinesOnHighlight && powerSystem != null)
        {
            bool shouldDarken = isHovered && isInRange;
            if (shouldDarken && lineColorNeedsUpdate)
            {
                currentLineColor = Color.Lerp(currentLineColor, lineDarkenedColor, Time.deltaTime * transitionSpeed);
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
                UpdateHighlightLayerOnFaces(Color.white);
            }
        }
    }

    private void UpdateHighlightLayerOnFaces(Color highlightColor)
    {
        foreach (var face in GetComponentsInChildren<RunodeFace>())
        {
            face.highlightColorLayer = highlightColor;
            face.UpdateSpriteVisuals();
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
            if (powerSystem != null)
                faceIndex = powerSystem.GetFaceIndexFromPoint(controller.MouseHitPoint);

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
