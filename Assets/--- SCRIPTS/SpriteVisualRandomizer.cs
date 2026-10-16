using UnityEngine;

public class SpriteVisualRandomizer : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapStId = Shader.PropertyToID("_BaseMap_ST");

    [SerializeField] private MeshRenderer quadRenderer;
    [SerializeField] private Color tint = Color.white;
    [SerializeField] private Vector2 tiling = Vector2.one;

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        ApplyVisuals();
    }

    private void OnValidate()
    {
        ApplyVisuals();
    }

    private void ApplyVisuals()
    {
        if (quadRenderer == null)
            return;

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        quadRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, tint);
        propertyBlock.SetVector(BaseMapStId, new Vector4(tiling.x, tiling.y, 0f, 0f));
        quadRenderer.SetPropertyBlock(propertyBlock);
    }
}
