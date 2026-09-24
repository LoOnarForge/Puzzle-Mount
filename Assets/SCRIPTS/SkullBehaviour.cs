using UnityEngine;
using UnityEngine.Serialization;

public class SkullBehaviour : MonoBehaviour
{
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Renderer emissionRenderer;

    [ColorUsage(true, true)]
    [FormerlySerializedAs("emissionColor")]
    [SerializeField] private Color defaultColor = Color.white;

    [ColorUsage(true, true)]
    [SerializeField] private Color openColor = Color.white;

    [ColorUsage(true, true)]
    [SerializeField] private Color closedColor = Color.white;

    // Absorbed into defaultColor (HDR) — kept so existing prefab intensity values migrate once.
    [SerializeField] [HideInInspector] private float emissionIntensity = 1f;

    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        MigrateLegacyIntensity();
    }

    private void Start()
    {
        ApplyEmission(defaultColor);
    }

    private void OnValidate()
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        MigrateLegacyIntensity();
        ApplyEmission(defaultColor);
    }

    private void MigrateLegacyIntensity()
    {
        if (Mathf.Approximately(emissionIntensity, 1f))
            return;

        defaultColor *= emissionIntensity;
        emissionIntensity = 1f;
    }

    private void ApplyEmission(Color color)
    {
        if (emissionRenderer == null)
            return;

        emissionRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(EmissionColorId, color);
        emissionRenderer.SetPropertyBlock(propertyBlock);
    }
}
