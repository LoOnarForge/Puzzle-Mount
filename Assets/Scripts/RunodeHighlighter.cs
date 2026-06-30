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
    public float transitionSpeed = 15f;
    
    [Header("POWER LINE DARKENING:")]
    public bool darkenLinesOnHighlight = true;
    public Color lineDarkenedColor = new Color(0.2f, 0.2f, 0.2f, 1.0f);

    [Header("REFERENCES")]
    public MeshRenderer cubeRenderer;
    public GameObject selectionFrame; 

    private RunodePower powerSystem;
    private MaterialPropertyBlock cubePropBlock;
    private MaterialPropertyBlock linePropBlock;

    private Color currentColor = Color.white;
    private Color currentLineColor = Color.white;
    
    private bool isHovered = false;
    private bool isInRange = false;

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
        Color target;
        if (!isHovered) target = Color.white;
        else target = isInRange ? inRangeColor : outOfRangeColor;

        currentColor = Color.Lerp(currentColor, target, Time.deltaTime * transitionSpeed);
        
        if (cubeRenderer != null)
        {
            cubeRenderer.GetPropertyBlock(cubePropBlock);
            cubePropBlock.SetColor(BaseColorId, currentColor);
            cubeRenderer.SetPropertyBlock(cubePropBlock);
        }

        // 2. Selection Frame Logic (Occluded gizmo)
        if (selectionFrame != null)
        {
            bool shouldBeActive = isHovered && isInRange;
            if (selectionFrame.activeSelf != shouldBeActive)
                selectionFrame.SetActive(shouldBeActive);
        }

        // 3. Power Line Darkening Logic
        if (darkenLinesOnHighlight && powerSystem != null)
        {
            bool shouldDarken = isHovered && isInRange;
            if (shouldDarken)
            {
                currentLineColor = Color.Lerp(currentLineColor, lineDarkenedColor, Time.deltaTime * transitionSpeed);
                ApplyColorToAllLines(currentLineColor);
            }
            else if (currentLineColor != Color.white)
            {
                currentLineColor = Color.white;
                powerSystem.RefreshFaceVisuals();
            }
        }
    }

    private void ApplyColorToAllLines(Color color)
    {
        foreach (var sr in cachedLineRenderers)
        {
            if (sr == null) continue;
            // Using direct color assignment for Sprites as it's more reliable 
            // than MPB with the default URP sprite shader in some versions.
            sr.color = color;
        }
    }

    public void SetHighlight(bool rotatable)
    {
        isHovered = true;
        isInRange = rotatable;
    }

    public void ClearHighlight()
    {
        isHovered = false;
    }
}
