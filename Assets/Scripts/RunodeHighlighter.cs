using UnityEngine;

/// Manages visual feedback for Runode cubes with smooth transitions.
public class RunodeHighlighter : MonoBehaviour
{
    [Header("HIGHLIGHT SETTINGS")]
    [ColorUsage(true, true)]
    public Color cubeHighlightColor = new Color(1.5f, 1.5f, 1.5f, 1.0f);
    public float transitionSpeed = 15f;
    
    [Header("REFERENCES")]
    public MeshRenderer cubeRenderer;

    private MaterialPropertyBlock cubePropBlock;
    private Color currentCubeColor = Color.white;
    private bool isHighlighted = false;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        cubePropBlock = new MaterialPropertyBlock();
        if (cubeRenderer == null) cubeRenderer = GetComponentInChildren<MeshRenderer>();
    }

    private void Update()
    {
        // Smooth Cube Transition
        Color targetCube = isHighlighted ? cubeHighlightColor : Color.white;
        currentCubeColor = Color.Lerp(currentCubeColor, targetCube, Time.deltaTime * transitionSpeed);
        
        if (cubeRenderer != null)
        {
            cubeRenderer.GetPropertyBlock(cubePropBlock);
            cubePropBlock.SetColor(BaseColorId, currentCubeColor);
            cubeRenderer.SetPropertyBlock(cubePropBlock);
        }
    }

    public void SetHighlight(bool rotatable)
    {
        isHighlighted = rotatable;
    }

    public void ClearHighlight()
    {
        isHighlighted = false;
    }
}
