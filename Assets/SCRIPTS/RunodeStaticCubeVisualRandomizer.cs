using UnityEngine;

public class RunodeStaticCubeVisualRandomizer : MonoBehaviour
{
    [SerializeField] private bool isRandomMesh = true;
    [SerializeField] private bool isRandomMaterial = true;
    [Space(10)]
    [SerializeField] private Mesh[] meshes;
    [Space(10)]
    [SerializeField] private Material[] materials;
    [Space(10)]
    [SerializeField] private MeshFilter meshFilter;

    private void Awake()
    {
        if (isRandomMesh && meshFilter != null && meshes != null && meshes.Length > 0)
        {
            Transform meshTransform = meshFilter.transform;
            Vector3 restingLocalPosition = meshTransform.localPosition;
            Vector3 restingLocalScale = meshTransform.localScale;
            Mesh restingMesh = meshFilter.sharedMesh;
            Vector3 targetVolumeCenter = GetTargetVolumeCenter(
                meshTransform, restingLocalPosition, restingLocalScale, restingMesh);

            Mesh chosenMesh = meshes[Random.Range(0, meshes.Length)];
            if (chosenMesh != null)
                meshFilter.sharedMesh = chosenMesh;

            Mesh meshForOffset = chosenMesh != null ? chosenMesh : restingMesh;
            Vector3 meshCenterLocal = meshForOffset != null ? meshForOffset.bounds.center : Vector3.zero;

            Quaternion rotation = Quaternion.Euler(
                Random.Range(0, 4) * 90f,
                Random.Range(0, 4) * 90f,
                Random.Range(0, 4) * 90f);
            Vector3 compensatedLocalScale = GetCompensatedLocalScale(restingLocalScale, rotation);

            meshTransform.localRotation = rotation;
            meshTransform.localScale = compensatedLocalScale;
            meshTransform.localPosition = targetVolumeCenter
                - rotation * Vector3.Scale(compensatedLocalScale, meshCenterLocal);
        }

        if (isRandomMaterial && meshFilter != null && materials != null && materials.Length > 0)
        {
            MeshRenderer meshRenderer = meshFilter.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                Material chosenMaterial = materials[Random.Range(0, materials.Length)];
                if (chosenMaterial != null)
                    meshRenderer.sharedMaterial = chosenMaterial;
            }
        }
    }

    private static Vector3 GetTargetVolumeCenter(
        Transform meshTransform,
        Vector3 restingLocalPosition,
        Vector3 restingLocalScale,
        Mesh restingMesh)
    {
        BoxCollider ownBox = meshTransform.GetComponent<BoxCollider>();
        if (ownBox != null)
            return restingLocalPosition + Vector3.Scale(restingLocalScale, ownBox.center);

        Transform parent = meshTransform.parent;
        if (parent != null)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform sibling = parent.GetChild(i);
                if (sibling == meshTransform)
                    continue;

                BoxCollider siblingBox = sibling.GetComponent<BoxCollider>();
                if (siblingBox == null)
                    continue;

                return sibling.localPosition + Vector3.Scale(sibling.localScale, siblingBox.center);
            }
        }

        Vector3 meshCenterLocal = restingMesh != null ? restingMesh.bounds.center : Vector3.zero;
        return restingLocalPosition + Vector3.Scale(restingLocalScale, meshCenterLocal);
    }

    private static Vector3 GetCompensatedLocalScale(Vector3 restingLocalScale, Quaternion rotation)
    {
        Vector3 compensated = Vector3.zero;
        Vector3[] localAxes = { Vector3.right, Vector3.up, Vector3.forward };

        for (int j = 0; j < 3; j++)
        {
            Vector3 worldDir = rotation * localAxes[j];
            int dominantAxis = DominantAxis(worldDir);
            compensated[j] = restingLocalScale[dominantAxis];
        }

        return compensated;
    }

    private static int DominantAxis(Vector3 direction)
    {
        float absX = Mathf.Abs(direction.x);
        float absY = Mathf.Abs(direction.y);
        float absZ = Mathf.Abs(direction.z);

        if (absX >= absY && absX >= absZ)
            return 0;
        if (absY >= absX && absY >= absZ)
            return 1;
        return 2;
    }
}