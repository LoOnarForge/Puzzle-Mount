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
    }
}
