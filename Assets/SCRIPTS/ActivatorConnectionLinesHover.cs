using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRendererGenerator))]
public class ActivatorConnectionLinesHover : MonoBehaviour
{
    [SerializeField] private float fadeInSpeed = 5f;
    [SerializeField] private float fadeOutSpeed = 3f;
    [SerializeField] private float hideDelayBase = 1f;
    [SerializeField] private float hideDelayPerMeter = 0.25f;

    private Transform activatorRoot;
    private readonly List<LineVisualState> lineVisuals = new List<LineVisualState>();
    private bool wasHovered;

    private sealed class LineVisualState
    {
        public LineRenderer LineRenderer;
        public ActivatorConnectionLineImpulse LineImpulse;
        public Gradient BaseGradient;
        public float BaseWidthMultiplier;
        public float BaseStartWidth;
        public float BaseEndWidth;
        public float PathMeters;
        public float DisplayAlpha;
        public float HideHoldTimer;
        public bool LineImpulseEnabled;
    }

    private void Awake()
    {
        activatorRoot = ResolveActivatorRoot();
        ActivatorHoverTracker.EnsureExists();
        RefreshLineVisuals();
        ApplyAllLineVisuals();
        SetAllLineImpulsesEnabled(false);
    }

    private void Update()
    {
        bool hovered = ActivatorHoverTracker.HoveredActivator == activatorRoot;

        for (int i = 0; i < lineVisuals.Count; i++)
        {
            LineVisualState state = lineVisuals[i];
            if (hovered)
            {
                state.HideHoldTimer = 0f;
            }
            else if (wasHovered)
            {
                state.HideHoldTimer = GetHideDelaySeconds(state.PathMeters);
            }
            else if (state.HideHoldTimer > 0f)
            {
                state.HideHoldTimer -= Time.deltaTime;
            }

            float targetAlpha = hovered || state.HideHoldTimer > 0f ? 1f : 0f;
            float speed = targetAlpha > state.DisplayAlpha ? fadeInSpeed : fadeOutSpeed;
            state.DisplayAlpha = Mathf.MoveTowards(state.DisplayAlpha, targetAlpha, speed * Time.deltaTime);

            ApplyLineVisual(state);

            if (state.LineImpulse != null)
                state.LineImpulse.SetExternalAlphaScale(state.DisplayAlpha);

            bool shouldRunImpulse = state.DisplayAlpha > 0.01f;
            if (shouldRunImpulse == state.LineImpulseEnabled)
                continue;

            state.LineImpulseEnabled = shouldRunImpulse;
            if (state.LineImpulse != null)
                state.LineImpulse.enabled = shouldRunImpulse;
        }

        wasHovered = hovered;
    }

    private float GetHideDelaySeconds(float pathMeters)
    {
        return hideDelayBase + hideDelayPerMeter * pathMeters;
    }

    public void RefreshLineVisuals()
    {
        CacheLineVisuals();
        ApplyAllLineVisuals();
    }

    private Transform ResolveActivatorRoot()
    {
        Lever lever = GetComponentInParent<Lever>();
        if (lever != null)
            return lever.transform;

        ButtonDevice button = GetComponentInParent<ButtonDevice>();
        if (button != null)
            return button.transform;

        PressurePlate plate = GetComponentInParent<PressurePlate>();
        if (plate != null)
            return plate.transform;

        return transform;
    }

    private void CacheLineVisuals()
    {
        lineVisuals.Clear();
        LineRenderer[] lines = activatorRoot.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null)
                continue;

            ActivatorConnectionCometPathDriver legacyComet = line.GetComponent<ActivatorConnectionCometPathDriver>();
            if (legacyComet != null)
                legacyComet.enabled = false;

            ActivatorConnectionLineImpulse impulse = line.GetComponent<ActivatorConnectionLineImpulse>();

            var state = new LineVisualState
            {
                LineRenderer = line,
                LineImpulse = impulse,
                BaseGradient = CopyGradient(line.colorGradient),
                BaseWidthMultiplier = line.widthMultiplier,
                BaseStartWidth = line.startWidth,
                BaseEndWidth = line.endWidth,
                PathMeters = MeasureLinePathMeters(line),
                DisplayAlpha = 0f,
                HideHoldTimer = 0f,
                LineImpulseEnabled = false
            };

            if (impulse != null)
                impulse.CachePathFromLineRenderer();

            lineVisuals.Add(state);
        }
    }

    private static float MeasureLinePathMeters(LineRenderer line)
    {
        if (line == null || line.positionCount < 2)
            return 0f;

        float length = 0f;
        Vector3 previous = line.GetPosition(0);
        for (int i = 1; i < line.positionCount; i++)
        {
            Vector3 current = line.GetPosition(i);
            length += Vector3.Distance(previous, current);
            previous = current;
        }

        return length;
    }

    private void ApplyAllLineVisuals()
    {
        for (int i = 0; i < lineVisuals.Count; i++)
            ApplyLineVisual(lineVisuals[i]);
    }

    private void ApplyLineVisual(LineVisualState state)
    {
        if (state.LineRenderer == null)
            return;

        float alpha = state.DisplayAlpha;
        bool visible = alpha > 0.001f;
        state.LineRenderer.enabled = visible;
        if (!visible)
            return;

        if (state.LineImpulse == null)
        {
            state.LineRenderer.colorGradient = ScaleGradientAlpha(state.BaseGradient, alpha);
            state.LineRenderer.widthMultiplier = state.BaseWidthMultiplier * alpha;
        }

        state.LineRenderer.startWidth = state.BaseStartWidth * alpha;
        state.LineRenderer.endWidth = state.BaseEndWidth * alpha;
    }

    private void SetAllLineImpulsesEnabled(bool enabled)
    {
        for (int i = 0; i < lineVisuals.Count; i++)
        {
            LineVisualState state = lineVisuals[i];
            state.LineImpulseEnabled = enabled;
            if (state.LineImpulse != null)
                state.LineImpulse.enabled = enabled;
        }
    }

    private static Gradient CopyGradient(Gradient source)
    {
        var copy = new Gradient();
        copy.SetKeys(source.colorKeys, source.alphaKeys);
        return copy;
    }

    private static Gradient ScaleGradientAlpha(Gradient baseGradient, float alphaScale)
    {
        var scaled = new Gradient();
        GradientAlphaKey[] alphaKeys = baseGradient.alphaKeys;
        var scaledAlphas = new GradientAlphaKey[alphaKeys.Length];
        for (int i = 0; i < alphaKeys.Length; i++)
            scaledAlphas[i] = new GradientAlphaKey(alphaKeys[i].alpha * alphaScale, alphaKeys[i].time);

        scaled.SetKeys(baseGradient.colorKeys, scaledAlphas);
        return scaled;
    }
}
