using UnityEngine;

public class MaterialRandomizer : MonoBehaviour
{
    public const int SlotCount = 10;

    [SerializeField] private bool applySameMaterialToAll;
    [SerializeField] private bool[] includeInPool = new bool[SlotCount];
    [SerializeField] private Material[] materials = new Material[SlotCount];
    [SerializeField] private MeshRenderer[] meshRenderers;

    private void Awake()
    {
        if (meshRenderers == null || meshRenderers.Length == 0)
            return;

        if (applySameMaterialToAll)
        {
            Material chosenMaterial = PickRandomMaterial();
            if (chosenMaterial == null)
                return;

            ApplyMaterial(chosenMaterial);
            return;
        }

        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] == null)
                continue;

            Material chosenMaterial = PickRandomMaterial();
            if (chosenMaterial != null)
                meshRenderers[i].sharedMaterial = chosenMaterial;
        }
    }

    private Material PickRandomMaterial()
    {
        int poolCount = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            if (includeInPool[i] && materials[i] != null)
                poolCount++;
        }

        if (poolCount == 0)
            return null;

        int pick = Random.Range(0, poolCount);
        for (int i = 0; i < SlotCount; i++)
        {
            if (!includeInPool[i] || materials[i] == null)
                continue;

            if (pick == 0)
                return materials[i];

            pick--;
        }

        return null;
    }

    private void ApplyMaterial(Material material)
    {
        for (int i = 0; i < meshRenderers.Length; i++)
        {
            if (meshRenderers[i] != null)
                meshRenderers[i].sharedMaterial = material;
        }
    }
}
