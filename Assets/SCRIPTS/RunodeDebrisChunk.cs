using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class RunodeDebrisChunk : MonoBehaviour
{
    [SerializeField] private MeshRenderer meshRenderer;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }

    // Activates a pooled chunk at the given world position with physics.
    public void Activate(Vector3 worldPosition, float size, Material material, Vector3 explosionForce)
    {
        transform.position = worldPosition;
        transform.localScale = Vector3.one * size;
        transform.rotation = Random.rotation;

        if (meshRenderer != null && material != null)
            meshRenderer.sharedMaterial = material;

        gameObject.SetActive(true);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(explosionForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * explosionForce.magnitude * 0.25f, ForceMode.Impulse);
    }

    // Returns the chunk to an inactive pooled state.
    public void Deactivate(Transform poolRoot)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        transform.SetParent(poolRoot, false);
        gameObject.SetActive(false);
    }
}
