using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ActivatorOutlineHighlight : MonoBehaviour
{
    private const string OutlineChildName = "Activator Outline Mesh";

    private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    [SerializeField] private Material outlineMaterial;
    [ColorUsage(false, true)]
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private float outlineWidth = 0.02f;
    [SerializeField] private bool skipEyeMeshes = true;
    [SerializeField] private bool applyStaticFlagsOnAwake = true;

    private Transform activatorRoot;
    private Material outlineMaterialInstance;
    private readonly List<OutlineEntry> outlineEntries = new List<OutlineEntry>();
    private readonly List<Transform> dynamicRoots = new List<Transform>();

    private sealed class OutlineEntry
    {
        public MeshFilter SourceFilter;
        public MeshRenderer OutlineRenderer;
    }

    private void Awake()
    {
        activatorRoot = transform;

        if (applyStaticFlagsOnAwake)
            ApplyActivatorStaticFlags();
    }

    private void Start()
    {
        StartCoroutine(InitializeOutlinesAfterStaticBatch());
    }

    private IEnumerator InitializeOutlinesAfterStaticBatch()
    {
        yield return null;

        outlineMaterialInstance = CreateOutlineMaterialInstance();
        ApplyMaterialProperties();
        BuildOutlineRenderers();
        SetOutlineVisible(false);
    }

    private void OnDestroy()
    {
        if (outlineMaterialInstance != null)
            Destroy(outlineMaterialInstance);
    }

    private void Update()
    {
        if (outlineEntries.Count == 0)
            return;

        ActivatorHoverTracker.EnsureExists();
        bool hovered = ActivatorHoverTracker.HoveredActivator == activatorRoot;
        SetOutlineVisible(hovered);
    }

    private void ApplyActivatorStaticFlags()
    {
        dynamicRoots.Clear();
        CollectDynamicRoots();

        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform current = transforms[i];
            if (current == null)
                continue;

            if (current.name == OutlineChildName || current.name.StartsWith(OutlineChildName))
            {
                current.gameObject.isStatic = false;
                continue;
            }

            if (IsUnderDynamicRoot(current))
            {
                current.gameObject.isStatic = false;
                continue;
            }

            // Mesh renderers stay dynamic so outline duplicates match the visible mesh
            // (static batching moves the draw away from the transform).
            if (current.GetComponent<MeshRenderer>() != null)
            {
                current.gameObject.isStatic = false;
                continue;
            }

            current.gameObject.isStatic = true;
        }
    }

    private void CollectDynamicRoots()
    {
        if (GetComponent<Lever>() != null)
            AddDynamicRootByName("Switch Lever");

        if (GetComponent<ButtonDevice>() != null)
            AddDynamicRootByName("Skull Button");

        if (GetComponent<PressurePlate>() != null)
            AddDynamicRootByName("Plate");
    }

    private void AddDynamicRootByName(string objectName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == objectName)
            {
                dynamicRoots.Add(transforms[i]);
                return;
            }
        }
    }

    private bool IsUnderDynamicRoot(Transform target)
    {
        for (int i = 0; i < dynamicRoots.Count; i++)
        {
            Transform root = dynamicRoots[i];
            if (root == null)
                continue;

            if (target == root || target.IsChildOf(root))
                return true;
        }

        return false;
    }

    private Material CreateOutlineMaterialInstance()
    {
        if (outlineMaterial != null)
            return new Material(outlineMaterial);

        Shader shader = Shader.Find("Custom/ActivatorMeshOutline");
        if (shader == null)
        {
            Debug.LogError($"{nameof(ActivatorOutlineHighlight)}: shader Custom/ActivatorMeshOutline not found.", this);
            return null;
        }

        return new Material(shader);
    }

    private void ApplyMaterialProperties()
    {
        if (outlineMaterialInstance == null)
            return;

        outlineMaterialInstance.SetColor(OutlineColorId, outlineColor);
        outlineMaterialInstance.SetFloat(OutlineWidthId, outlineWidth);
    }

    private void BuildOutlineRenderers()
    {
        outlineEntries.Clear();

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer sourceRenderer = renderers[i];
            if (sourceRenderer == null || ShouldSkipRenderer(sourceRenderer))
                continue;

            MeshFilter sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
                continue;

            OutlineEntry entry = CreateLocalOutlineEntry(sourceRenderer, sourceFilter);
            if (entry != null)
                outlineEntries.Add(entry);
        }
    }

    private OutlineEntry CreateLocalOutlineEntry(MeshRenderer sourceRenderer, MeshFilter sourceFilter)
    {
        Transform outlineTransform = sourceRenderer.transform.Find(OutlineChildName);
        if (outlineTransform == null)
        {
            var outlineObject = new GameObject(OutlineChildName);
            outlineTransform = outlineObject.transform;
            outlineTransform.SetParent(sourceRenderer.transform, false);
            outlineTransform.localPosition = Vector3.zero;
            outlineTransform.localRotation = Quaternion.identity;
            outlineTransform.localScale = Vector3.one;
        }

        outlineTransform.gameObject.isStatic = false;

        MeshFilter outlineFilter = outlineTransform.GetComponent<MeshFilter>();
        if (outlineFilter == null)
            outlineFilter = outlineTransform.gameObject.AddComponent<MeshFilter>();

        MeshRenderer outlineRenderer = outlineTransform.GetComponent<MeshRenderer>();
        if (outlineRenderer == null)
            outlineRenderer = outlineTransform.gameObject.AddComponent<MeshRenderer>();

        outlineFilter.sharedMesh = sourceFilter.sharedMesh;
        ConfigureOutlineRenderer(outlineRenderer);

        return new OutlineEntry
        {
            SourceFilter = sourceFilter,
            OutlineRenderer = outlineRenderer,
        };
    }

    private void ConfigureOutlineRenderer(MeshRenderer outlineRenderer)
    {
        outlineRenderer.sharedMaterial = outlineMaterialInstance;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.allowOcclusionWhenDynamic = false;
    }

    private bool ShouldSkipRenderer(MeshRenderer renderer)
    {
        if (renderer.gameObject.name == OutlineChildName || renderer.gameObject.name.StartsWith(OutlineChildName))
            return true;

        if (skipEyeMeshes && renderer.gameObject.name == "Eyes")
            return true;

        return false;
    }

    private void SetOutlineVisible(bool visible)
    {
        for (int i = 0; i < outlineEntries.Count; i++)
        {
            OutlineEntry entry = outlineEntries[i];
            if (entry.OutlineRenderer == null)
                continue;

            if (entry.SourceFilter != null && entry.SourceFilter.sharedMesh != null)
            {
                MeshFilter outlineFilter = entry.OutlineRenderer.GetComponent<MeshFilter>();
                if (outlineFilter != null)
                    outlineFilter.sharedMesh = entry.SourceFilter.sharedMesh;
            }

            entry.OutlineRenderer.enabled = visible;
        }
    }
}
