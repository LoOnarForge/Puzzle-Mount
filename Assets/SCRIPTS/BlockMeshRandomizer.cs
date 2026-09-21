using UnityEngine;

public enum BlockMeshRotationMode
{
    None,
    YAxisOnly,
    FullCube
}

public class BlockMeshRandomizer : MonoBehaviour
{
    private const int RotationStepCount = 4;
    private const float DegreesPerRotationStep = 90f;

    [SerializeField] private bool isRandomMesh = true;
    [SerializeField] private bool isRandomMaterial = true;
    [SerializeField] private bool isRandomRotation = true;
    [SerializeField] private bool isUniformForAll = true;
    [SerializeField] private BlockMeshRotationMode rotationMode = BlockMeshRotationMode.FullCube;
    [Space(10)]
    [SerializeField] private Mesh[] meshes;
    [Space(10)]
    [SerializeField] private Material[] materials;
    [Space(10)]
    [SerializeField] private MeshFilter[] meshFilters;

    private void Awake()
    {
        if (meshFilters == null || meshFilters.Length == 0)
            return;

        if (isUniformForAll)
        {
            Mesh meshChoice = PickRandomMesh();
            Material materialChoice = PickRandomMaterial();
            Quaternion rotationOffset = PickRandomRotationOffset();

            for (int i = 0; i < meshFilters.Length; i++)
                ApplyToMeshFilter(meshFilters[i], meshChoice, materialChoice, rotationOffset);
        }
        else
        {
            for (int i = 0; i < meshFilters.Length; i++)
            {
                ApplyToMeshFilter(
                    meshFilters[i],
                    PickRandomMesh(),
                    PickRandomMaterial(),
                    PickRandomRotationOffset());
            }
        }
    }

    private void ApplyToMeshFilter(
        MeshFilter targetFilter,
        Mesh meshChoice,
        Material materialChoice,
        Quaternion rotationOffset)
    {
        if (targetFilter == null)
            return;

        Transform bodyTransform = targetFilter.transform;

        if (isRandomMesh && meshChoice != null)
            targetFilter.sharedMesh = meshChoice;

        if (isRandomRotation && rotationMode != BlockMeshRotationMode.None)
            bodyTransform.localRotation = bodyTransform.localRotation * rotationOffset;

        if (!isRandomMaterial || materialChoice == null)
            return;

        MeshRenderer meshRenderer = targetFilter.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
            meshRenderer.sharedMaterial = materialChoice;
    }

    private Mesh PickRandomMesh()
    {
        if (!isRandomMesh || meshes == null || meshes.Length == 0)
            return null;

        Mesh chosenMesh = meshes[Random.Range(0, meshes.Length)];
        return chosenMesh != null ? chosenMesh : null;
    }

    private Material PickRandomMaterial()
    {
        if (!isRandomMaterial || materials == null || materials.Length == 0)
            return null;

        Material chosenMaterial = materials[Random.Range(0, materials.Length)];
        return chosenMaterial != null ? chosenMaterial : null;
    }

    private Quaternion PickRandomRotationOffset()
    {
        if (!isRandomRotation || rotationMode == BlockMeshRotationMode.None)
            return Quaternion.identity;

        if (rotationMode == BlockMeshRotationMode.YAxisOnly)
            return GetRandomYRotationOffset();

        return GetRandomRotationOffset();
    }

    private static Quaternion GetRandomRotationOffset()
    {
        return Quaternion.Euler(
            Random.Range(0, RotationStepCount) * DegreesPerRotationStep,
            Random.Range(0, RotationStepCount) * DegreesPerRotationStep,
            Random.Range(0, RotationStepCount) * DegreesPerRotationStep);
    }

    private static Quaternion GetRandomYRotationOffset()
    {
        return Quaternion.Euler(0f, Random.Range(0, RotationStepCount) * DegreesPerRotationStep, 0f);
    }
}