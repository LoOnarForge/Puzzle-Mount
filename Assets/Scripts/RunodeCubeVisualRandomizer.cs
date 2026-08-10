using UnityEngine;

public class RunodeCubeVisualRandomizer : MonoBehaviour
{
    [SerializeField] private Mesh mesh01;
    [SerializeField] private Mesh mesh02;
    [SerializeField] private Mesh mesh03;
    [SerializeField] private Mesh mesh04;
    [SerializeField] private Mesh mesh05;

    [SerializeField] private Material material01;
    [SerializeField] private Material material02;
    [SerializeField] private Material material03;

    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private RunodeLineSpriteLibrary lineSpriteLibrary;

    private void Awake()
    {
        Mesh[] meshes = { mesh01, mesh02, mesh03, mesh04, mesh05 };
        Material[] materials = { material01, material02, material03 };

        if (meshFilter != null)
        {
            Mesh chosenMesh = meshes[Random.Range(0, meshes.Length)];
            if (chosenMesh != null)
                meshFilter.sharedMesh = chosenMesh;

            Vector3 restingLocalPosition = meshFilter.transform.localPosition;
            Quaternion rotation = Quaternion.Euler(
                Random.Range(1, 4) * 90f,
                Random.Range(1, 4) * 90f,
                Random.Range(1, 4) * 90f);

            meshFilter.transform.localRotation = rotation;
            meshFilter.transform.localPosition = rotation * restingLocalPosition;
        }

        if (meshRenderer != null)
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
