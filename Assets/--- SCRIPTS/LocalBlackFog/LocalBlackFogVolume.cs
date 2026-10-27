using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class LocalBlackFogVolume : MonoBehaviour
{
    [SerializeField] private Vector3 boxDimensions = new Vector3(10f, 6f, 10f);
    [SerializeField] [Min(0f)] private float fogDensity = 12f;
    [SerializeField] [Range(0.001f, 0.5f)] private float edgeSoftness = 0.08f;

    [Header("Clears around the walker (Tim)")]
    [SerializeField] private Transform referenceTransform;
    [SerializeField] [Min(0f)] private float clearRadius = 2.5f;
    [SerializeField] [Min(0f)] private float obscureRadius = 12f;

    private CharacterMovement cachedTim;

    public Vector3 BoxDimensions => boxDimensions;
    public float FogDensity => fogDensity;
    public float EdgeSoftness => edgeSoftness;
    public float ClearRadius => clearRadius;
    public float ObscureRadius => obscureRadius;
    public Matrix4x4 WorldToLocalMatrix => transform.worldToLocalMatrix;
    public Matrix4x4 LocalToWorldMatrix => transform.localToWorldMatrix;

    public Vector3 ReferenceWorldPosition
    {
        get
        {
            if (referenceTransform != null)
                return referenceTransform.position;

            if (cachedTim == null)
                cachedTim = Object.FindFirstObjectByType<CharacterMovement>();

            if (cachedTim != null)
                return cachedTim.transform.position;

            return transform.position;
        }
    }

    private void OnEnable()
    {
        LocalBlackFogRenderPass.Register(this);
        cachedTim = null;
    }

    private void OnDisable() => LocalBlackFogRenderPass.Unregister(this);

    private void Update()
    {
        transform.localScale = boxDimensions;
    }

    private void OnValidate()
    {
        boxDimensions.x = Mathf.Max(0.01f, boxDimensions.x);
        boxDimensions.y = Mathf.Max(0.01f, boxDimensions.y);
        boxDimensions.z = Mathf.Max(0.01f, boxDimensions.z);
        clearRadius = Mathf.Max(0.5f, clearRadius);
        obscureRadius = Mathf.Max(clearRadius + 0.01f, obscureRadius);
        transform.localScale = boxDimensions;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0f, 0f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

        Vector3 refPos = ReferenceWorldPosition;
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(refPos, clearRadius);
        Gizmos.color = new Color(0f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(refPos, obscureRadius);
    }
#endif
}
