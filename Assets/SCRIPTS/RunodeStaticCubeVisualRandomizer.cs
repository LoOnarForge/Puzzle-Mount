using UnityEngine;

public class RunodeStaticCubeVisualRandomizer : MonoBehaviour
{
    [SerializeField] private Mesh mesh01;
    [SerializeField] private Mesh mesh02;
    [SerializeField] private Mesh mesh03;
    [SerializeField] private Mesh mesh04;
    [SerializeField] private Mesh mesh05;
    [Space(10)]
    [SerializeField] private Material material01;
    [SerializeField] private Material material02;
    [SerializeField] private Material material03;
    [Space(10)]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [Space(10)]
    [SerializeField] private RunodeLineSpriteLibrary lineSpriteLibrary;
    [SerializeField] private bool randomizeRockColor = true;
    [SerializeField] private bool alwaysUseMaterial01 = false;

    public RunodeLineSpriteLibrary LineSpriteLibrary => lineSpriteLibrary;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        Mesh[] meshes = { mesh01, mesh02, mesh03, mesh04, mesh05 };
        Material[] materials = { material01, material02, material03 };

        if (meshFilter != null)
        {
            Mesh chosenMesh = meshes[Random.Range(0, meshes.Length)];
            if (chosenMesh != null)
                meshFilter.sharedMesh = chosenMesh;
        }

        if (meshRenderer != null)
        {
            Material chosenMaterial = alwaysUseMaterial01
                ? material01
                : materials[Random.Range(0, materials.Length)];

            if (chosenMaterial != null)
                meshRenderer.sharedMaterial = chosenMaterial;

            if (randomizeRockColor && chosenMaterial != null && chosenMaterial.HasProperty(BaseColorId))
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, RandomRockColor(chosenMaterial.GetColor(BaseColorId)));
                meshRenderer.SetPropertyBlock(block);
            }
        }
    }

    private static Color RandomRockColor(Color materialBase)
    {
        float brightness = Random.Range(0.78f, 1.06f);

        Vector3 tint;
        switch (Random.Range(0, 4))
        {
            case 0:
                tint = new Vector3(1f, 1f, 1f);
                break;
            case 1:
                tint = new Vector3(1.05f, 1.02f, 0.93f);
                break;
            case 2:
                tint = new Vector3(0.93f, 1.04f, 0.95f);
                break;
            default:
                tint = new Vector3(0.93f, 0.97f, 1.05f);
                break;
        }

        return new Color(
            Mathf.Clamp01(materialBase.r * tint.x * brightness),
            Mathf.Clamp01(materialBase.g * tint.y * brightness),
            Mathf.Clamp01(materialBase.b * tint.z * brightness),
            materialBase.a);
    }
}
