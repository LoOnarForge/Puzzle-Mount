using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RuneTorchSpriteLibrary",
    menuName = "Puzzle Mount/Rune Torch Sprite Library")]
public class RuneTorchSpriteLibrary : ScriptableObject
{
    public List<Sprite> torchSprites = new();

    // Returns a random torch face sprite variation, or null if the list is empty.
    public Sprite GetRandomSprite()
    {
        if (torchSprites == null || torchSprites.Count == 0)
            return null;

        return torchSprites[Random.Range(0, torchSprites.Count)];
    }
}
