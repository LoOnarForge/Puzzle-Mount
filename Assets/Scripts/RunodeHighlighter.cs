using UnityEngine;

/// Manages visual feedback for Runode cubes with smooth transitions for both in-range and out-of-range states.
public class RunodeHighlighter : MonoBehaviour
{
    [Header("HIGHLIGHT SETTINGS")]
    [ColorUsage(true, true)]
    public Color inRangeColor = new Color(1.5f, 1.5f, 1.5f, 1.0f);
    [ColorUsage(true, true)]
    public Color outOfRangeColor = new Color(1.2f, 1.2f, 1.2f, 1.0f);
    public float transitionSpeed = 15f;
    
    [Header("REFERENCES")]
    public MeshRenderer cubeRenderer;
    public GameObject selectionFrame; // Assign a wireframe cage object

    private MaterialPropertyBlock cubePropBlock;
    private Color currentColor = Color.white;
    private bool isHovered = false;
    private bool isInRange = false;
    private bool isSelected = false;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        cubePropBlock = new MaterialPropertyBlock();
        if (cubeRenderer == null) cubeRenderer = GetComponentInChildren<MeshRenderer>();
        if (selectionFrame != null) selectionFrame.SetActive(false);
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
