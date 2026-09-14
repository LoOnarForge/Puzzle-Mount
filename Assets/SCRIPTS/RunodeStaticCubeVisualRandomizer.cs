using UnityEngine;

public class RunodeStaticCubeVisualRandomizer : MonoBehaviour
{
    [SerializeField] private Mesh[] meshes;
    [Space(10)]
    [SerializeField] private Material[] materials;
    [Space(10)]
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;

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
    }
}
