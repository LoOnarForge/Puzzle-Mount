using UnityEngine;

public class RuneTorchVisualRandomizer : MonoBehaviour
{
    private const float SpriteSizeMultiplier = 1.98f;

    [SerializeField] private RuneTorchSpriteLibrary spriteLibrary;
    [SerializeField] private RunodeCube runodeCube;

    private void Awake()
    {
        if (spriteLibrary == null || runodeCube == null)
            return;

        ApplyRandomTorchSprite();
    }

    private void ApplyRandomTorchSprite()
    {
        Sprite sprite = spriteLibrary.GetRandomSprite();
        if (sprite == null || runodeCube.topFaceTransform == null)
            return;

        foreach (Transform child in runodeCube.topFaceTransform)
        {
            if (!child.name.Contains("Line Sprite"))
                continue;

            SpriteRenderer spriteRenderer = child.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return;

            spriteRenderer.sprite = sprite;
            spriteRenderer.size = sprite.bounds.size * SpriteSizeMultiplier;
            return;
        }
    }
}
