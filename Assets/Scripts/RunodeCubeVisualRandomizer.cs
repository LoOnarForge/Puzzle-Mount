using UnityEngine;

public class RunodeCubeVisualRandomizer : MonoBehaviour
{
    [SerializeField] private Mesh[] meshes;
    [Space(10)]
    [SerializeField] private Material[] materials;
    [Space(10)]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [Space(10)]
    [SerializeField] private RunodeLineSpriteLibrary lineSpriteLibrary;

    public RunodeLineSpriteLibrary LineSpriteLibrary => lineSpriteLibrary;

    private void Awake()
    {
        if (meshFilter != null && meshes != null && meshes.Length > 0)
        {
            Mesh chosenMesh = meshes[Random.Range(0, meshes.Length)];
            if (chosenMesh != null)
                meshFilter.sharedMesh = chosenMesh;

            meshFilter.transform.localRotation = Quaternion.Euler(
                Random.Range(0, 4) * 90f,
                Random.Range(0, 4) * 90f,
                Random.Range(0, 4) * 90f);
        }

        if (meshRenderer != null && materials != null && materials.Length > 0)
        {
            Material chosenMaterial = materials[Random.Range(0, materials.Length)];
            if (chosenMaterial != null)
                meshRenderer.sharedMaterial = chosenMaterial;
        }

        ApplyRandomLineSprites();
    }

    private void ApplyRandomLineSprites()
    {
        if (lineSpriteLibrary == null)
            return;

        RunodeCube cube = GetComponent<RunodeCube>();
        if (cube == null)
            return;

        RunodeLineType[] faceTypes =
        {
            cube.topFace, cube.bottomFace, cube.northFace,
            cube.southFace, cube.eastFace, cube.westFace
        };

        Transform[] faceTransforms =
        {
            cube.topFaceTransform, cube.bottomFaceTransform, cube.northFaceTransform,
            cube.southFaceTransform, cube.eastFaceTransform, cube.westFaceTransform
        };

        for (int i = 0; i < faceTypes.Length; i++)
        {
            RunodeLineType lineType = faceTypes[i];
            if (lineType == RunodeLineType.Empty)
                continue;

            Sprite sprite = lineSpriteLibrary.GetRandomSpriteForType(lineType);
            if (sprite == null)
                continue;

            Transform faceTransform = faceTransforms[i];
            if (faceTransform == null)
                continue;

            ApplySpriteToFace(faceTransform, sprite, lineType);
        }
    }

    private static void ApplySpriteToFace(Transform faceTransform, Sprite sprite, RunodeLineType lineType)
    {
        foreach (Transform child in faceTransform)
        {
            if (!child.name.Contains("Line Sprite"))
                continue;

            SpriteRenderer spriteRenderer = child.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                return;

            spriteRenderer.sprite = sprite;
            spriteRenderer.size = sprite.bounds.size * 1.98f;
            child.localRotation = Quaternion.Euler(0f, 0f, RunodeLineSpriteLibrary.GetRotationForType(lineType));
            return;
        }
    }
}
