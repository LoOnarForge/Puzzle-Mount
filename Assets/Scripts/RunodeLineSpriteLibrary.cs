using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "RunodeLineSpriteLibrary",
    menuName = "Puzzle Mount/Runode Line Sprite Library")]
public class RunodeLineSpriteLibrary : ScriptableObject
{
    [Header("Horizontal (─) / Vertical (│, rotated 90°)")]
    public List<Sprite> horizontalSprites = new();

    [Header("Corner (┘)")]
    public List<Sprite> cornerSprites = new();

    [Header("T-Junction (┴)")]
    public List<Sprite> tSectionSprites = new();

    [Header("Cross (┼)")]
    public List<Sprite> crossSprites = new();

    public List<Sprite> GetSpritesForType(RunodeLineType type)
    {
        switch (type)
        {
            case RunodeLineType.Horizontal:
            case RunodeLineType.Vertical:
                return horizontalSprites;
            case RunodeLineType.CornerLeftTop:
            case RunodeLineType.CornerTopRight:
            case RunodeLineType.CornerRightBottom:
            case RunodeLineType.CornerBottomLeft:
                return cornerSprites;
            case RunodeLineType.TSectionLeft:
            case RunodeLineType.TSectionTop:
            case RunodeLineType.TSectionRight:
            case RunodeLineType.TSectionBottom:
                return tSectionSprites;
            case RunodeLineType.Cross:
                return crossSprites;
            default:
                return null;
        }
    }

    public Sprite GetRandomSpriteForType(RunodeLineType type)
    {
        List<Sprite> sprites = GetSpritesForType(type);
        if (sprites == null || sprites.Count == 0)
            return null;

        return sprites[Random.Range(0, sprites.Count)];
    }

    public static float GetRotationForType(RunodeLineType type)
    {
        switch (type)
        {
            case RunodeLineType.Vertical:
                return 90f;
            case RunodeLineType.CornerLeftTop:
            case RunodeLineType.TSectionLeft:
                return 0f;
            case RunodeLineType.CornerTopRight:
            case RunodeLineType.TSectionTop:
                return 270f;
            case RunodeLineType.CornerRightBottom:
            case RunodeLineType.TSectionRight:
                return 180f;
            case RunodeLineType.CornerBottomLeft:
            case RunodeLineType.TSectionBottom:
                return 90f;
            default:
                return 0f;
        }
    }
}
