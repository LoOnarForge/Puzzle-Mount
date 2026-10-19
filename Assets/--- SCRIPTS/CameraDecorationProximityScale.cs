using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class CameraDecorationProximityScale : MonoBehaviour
{
    private const float AlphaRestoreEpsilon = 0.001f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] private LayerMask decorationLayers;
    [SerializeField] [Range(0f, 1f)] private float hiddenAlpha = 0f;
    [SerializeField] [Min(0.01f)] private float transitionSeconds = 0.25f;

    private readonly Dictionary<MeshRenderer, FadeState> fadeStates = new Dictionary<MeshRenderer, FadeState>();
    private readonly List<MeshRenderer> finishedRestore = new List<MeshRenderer>();
    private readonly List<MeshRenderer> fadeUpdateKeys = new List<MeshRenderer>();

    private struct FadeState
    {
        public Color FullBaseColor;
        public float CurrentAlpha;
        public int OverlapCount;
        public MaterialPropertyBlock PropertyBlock;
    }

    private void Awake()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        Collider collider = GetComponent<Collider>();
        collider.isTrigger = true;

        if (decorationLayers.value == 0)
            decorationLayers = LayerMask.GetMask("Decorations");
    }

    private void Update()
    {
        if (fadeStates.Count == 0)
            return;

        finishedRestore.Clear();
        fadeUpdateKeys.Clear();
        fadeUpdateKeys.AddRange(fadeStates.Keys);

        for (int i = 0; i < fadeUpdateKeys.Count; i++)
        {
            MeshRenderer renderer = fadeUpdateKeys[i];
            if (!fadeStates.TryGetValue(renderer, out FadeState state))
                continue;

            if (renderer == null)
            {
                finishedRestore.Add(renderer);
                continue;
            }

            float targetAlpha = state.OverlapCount > 0 ? hiddenAlpha : state.FullBaseColor.a;
            float alphaRange = Mathf.Abs(state.FullBaseColor.a - hiddenAlpha);
            float maxStep = alphaRange / transitionSeconds * Time.deltaTime;
            state.CurrentAlpha = Mathf.MoveTowards(state.CurrentAlpha, targetAlpha, maxStep);

            ApplyBaseColorAlpha(renderer, state.FullBaseColor, state.CurrentAlpha, state.PropertyBlock);
            fadeStates[renderer] = state;

            if (state.OverlapCount <= 0 && Mathf.Abs(state.CurrentAlpha - state.FullBaseColor.a) <= AlphaRestoreEpsilon)
            {
                renderer.SetPropertyBlock(null);
                finishedRestore.Add(renderer);
            }
        }

        for (int i = 0; i < finishedRestore.Count; i++)
            fadeStates.Remove(finishedRestore[i]);
    }

    private void OnDisable()
    {
        foreach (KeyValuePair<MeshRenderer, FadeState> entry in fadeStates)
        {
            if (entry.Key != null)
                entry.Key.SetPropertyBlock(null);
        }

        fadeStates.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryResolveDecorationRenderer(other, out MeshRenderer renderer, out Color fullBaseColor))
            return;

        if (fadeStates.TryGetValue(renderer, out FadeState state))
        {
            state.OverlapCount++;
            fadeStates[renderer] = state;
            return;
        }

        fadeStates[renderer] = new FadeState
        {
            FullBaseColor = fullBaseColor,
            CurrentAlpha = fullBaseColor.a,
            OverlapCount = 1,
            PropertyBlock = new MaterialPropertyBlock(),
        };
    }

    private void OnTriggerExit(Collider other)
    {
        if (!TryResolveDecorationRenderer(other, out MeshRenderer renderer, out _))
            return;

        if (!fadeStates.TryGetValue(renderer, out FadeState state))
            return;

        state.OverlapCount = Mathf.Max(0, state.OverlapCount - 1);
        fadeStates[renderer] = state;
    }

    private bool TryResolveDecorationRenderer(Collider other, out MeshRenderer renderer, out Color fullBaseColor)
    {
        renderer = null;
        fullBaseColor = Color.white;

        if (other == null)
            return false;

        if ((decorationLayers.value & (1 << other.gameObject.layer)) == 0)
            return false;

        renderer = other.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = other.GetComponentInParent<MeshRenderer>();

        if (renderer == null || renderer.sharedMaterial == null)
            return false;

        if (renderer.sharedMaterial.HasProperty(BaseColorId))
            fullBaseColor = renderer.sharedMaterial.GetColor(BaseColorId);
        else
            fullBaseColor = renderer.sharedMaterial.color;

        return true;
    }

    private static void ApplyBaseColorAlpha(
        MeshRenderer renderer,
        Color fullBaseColor,
        float alpha,
        MaterialPropertyBlock propertyBlock)
    {
        Color color = fullBaseColor;
        color.a = alpha;
        propertyBlock.SetColor(BaseColorId, color);
        renderer.SetPropertyBlock(propertyBlock);
    }
}
