using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody))]
public class RunodeDebrisChunk : MonoBehaviour
{
    private const float MinFadeDuration = 0.05f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    [SerializeField] private MeshRenderer meshRenderer;

    private Rigidbody rb;
    private Material fadeMaterial;
    private Coroutine fadeCoroutine;
    private Vector3 activeScale;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
    }

    // Activates a pooled chunk at the given world position with physics and a randomized fade-out.
    public void Activate(
        Vector3 worldPosition,
        float size,
        Material material,
        Vector3 explosionForce,
        float debrisLifetime,
        float fadeDuration,
        float timingRandomSpread)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        transform.position = worldPosition;
        activeScale = Vector3.one * size;
        transform.localScale = activeScale;
        transform.rotation = Random.rotation;

        SetupFadeMaterial(material);

        gameObject.SetActive(true);

        rb.isKinematic = false;
        rb.useGravity = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(explosionForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * explosionForce.magnitude * 0.25f, ForceMode.Impulse);

        fadeCoroutine = StartCoroutine(FadeOutRoutine(debrisLifetime, fadeDuration, timingRandomSpread));
    }

    // Returns the chunk to an inactive pooled state.
    public void Deactivate(Transform poolRoot)
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        CleanupFadeMaterial();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.useGravity = false;

        transform.localScale = Vector3.one;
        transform.SetParent(poolRoot, false);
        gameObject.SetActive(false);
    }

    private void SetupFadeMaterial(Material sourceMaterial)
    {
        CleanupFadeMaterial();

        if (meshRenderer == null || sourceMaterial == null)
            return;

        fadeMaterial = new Material(sourceMaterial);
        ConfigureMaterialForFade(fadeMaterial);
        SetMaterialAlpha(1f);
        meshRenderer.material = fadeMaterial;
    }

    private void CleanupFadeMaterial()
    {
        if (fadeMaterial == null)
            return;

        Destroy(fadeMaterial);
        fadeMaterial = null;
    }

    private IEnumerator FadeOutRoutine(float debrisLifetime, float fadeDuration, float timingRandomSpread)
    {
        float chunkFadeDuration = fadeDuration + Random.Range(-timingRandomSpread, timingRandomSpread);
        chunkFadeDuration = Mathf.Max(MinFadeDuration, chunkFadeDuration);

        float disappearTime = debrisLifetime + Random.Range(-timingRandomSpread, timingRandomSpread);
        disappearTime = Mathf.Max(disappearTime, chunkFadeDuration);

        float fadeStartDelay = disappearTime - chunkFadeDuration;
        float elapsed = 0f;

        while (elapsed < fadeStartDelay)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        float fadeElapsed = 0f;
        while (fadeElapsed < chunkFadeDuration)
        {
            fadeElapsed += Time.deltaTime;
            float t = fadeElapsed / chunkFadeDuration;

            transform.localScale = Vector3.Lerp(activeScale, Vector3.zero, t);
            SetMaterialAlpha(Mathf.Lerp(1f, 0f, t));

            yield return null;
        }

        if (RunodeDebrisPool.Instance != null)
            RunodeDebrisPool.Instance.Release(this);
    }

    private void SetMaterialAlpha(float alpha)
    {
        if (fadeMaterial == null)
            return;

        if (fadeMaterial.HasProperty(BaseColorId))
        {
            Color color = fadeMaterial.GetColor(BaseColorId);
            color.a = alpha;
            fadeMaterial.SetColor(BaseColorId, color);
            return;
        }

        if (fadeMaterial.HasProperty(ColorId))
        {
            Color color = fadeMaterial.GetColor(ColorId);
            color.a = alpha;
            fadeMaterial.SetColor(ColorId, color);
        }
    }

    private static void ConfigureMaterialForFade(Material material)
    {
        if (material.HasProperty(SurfaceId))
            material.SetFloat(SurfaceId, 1f);

        if (material.HasProperty(BlendId))
            material.SetFloat(BlendId, 0f);

        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);
        material.SetInt(DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt(ZWriteId, 0);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }
}
