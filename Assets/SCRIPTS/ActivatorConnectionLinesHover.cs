using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRendererGenerator))]
public class ActivatorConnectionLinesHover : MonoBehaviour
{
    private Transform activatorRoot;
    private readonly List<LineVisualState> lineVisuals = new List<LineVisualState>();

    private sealed class LineVisualState
    {
        public LineRenderer LineRenderer;
        public ActivatorConnectionLineImpulse LineImpulse;
        public Gradient BaseGradient;
        public float BaseWidthMultiplier;
        public float BaseStartWidth;
        public float BaseEndWidth;
    }

    private void Awake()
    {
        activatorRoot = ResolveActivatorRoot();
        ActivatorHoverTracker.EnsureExists();
        RefreshLineVisuals();
        ApplyAllLineVisuals(false);
    }

    private void Update()
    {
        bool hovered = ActivatorHoverTracker.HoveredActivator == activatorRoot;

        for (int i = 0; i < lineVisuals.Count; i++)
        {
            LineVisualState state = lineVisuals[i];
            if (state.LineImpulse != null)
            {
                state.LineImpulse.SetLoopWhileRequested(hovered);
                if (hovered)
                    state.LineImpulse.StartCycleIfIdle();
            }

            ApplyLineVisual(state, hovered);
        }
    }

    public void RefreshLineVisuals()
    {
        CacheLineVisuals();
        ApplyAllLineVisuals(false);
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
        LineRendererGenerator generator = activatorRoot.GetComponent<LineRendererGenerator>();

        LineRenderer[] lines = activatorRoot.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (line == null)
                continue;

            ActivatorConnectionLineImpulse impulse = line.GetComponent<ActivatorConnectionLineImpulse>();

            var state = new LineVisualState
            {
                LineRenderer = line,
                LineImpulse = impulse,
                BaseGradient = CopyGradient(line.colorGradient),
                BaseWidthMultiplier = line.widthMultiplier,
                BaseStartWidth = line.startWidth,
                BaseEndWidth = line.endWidth,
            };

            if (impulse != null)
            {
                if (generator != null)
                    impulse.ConfigureFromGenerator(generator.CometPrefab, generator.BeamSpeedMin, generator.BeamSpeedMax);
                impulse.CachePathFromLineRenderer();
            }

            lineVisuals.Add(state);
        }
    }

    private void ApplyAllLineVisuals(bool hovered)
    {
        for (int i = 0; i < lineVisuals.Count; i++)
            ApplyLineVisual(lineVisuals[i], hovered);
    }

    private void ApplyLineVisual(LineVisualState state, bool hovered)
    {
        if (state.LineRenderer == null)
            return;

        bool beamPlaying = state.LineImpulse != null && state.LineImpulse.IsRunning;
        bool visible = hovered || beamPlaying;
        state.LineRenderer.enabled = visible;
        if (!visible)
            return;

        if (state.LineImpulse != null)
            state.LineImpulse.SetExternalAlphaScale(1f);

        if (state.LineImpulse == null)
        {
            state.LineRenderer.colorGradient = state.BaseGradient;
            state.LineRenderer.widthMultiplier = state.BaseWidthMultiplier;
        }

        state.LineRenderer.startWidth = state.BaseStartWidth;
        state.LineRenderer.endWidth = state.BaseEndWidth;
    }

    private static Gradient CopyGradient(Gradient source)
    {
        var copy = new Gradient();
        copy.SetKeys(source.colorKeys, source.alphaKeys);
        return copy;
    }
}
