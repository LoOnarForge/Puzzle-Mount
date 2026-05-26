using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// A single connection path — a list of world-space points and an optional power source for color.
[System.Serializable]
public class ConnectionPath
{
    [Tooltip("World-space positions in order. First = origin, last = destination.")]
    public List<Vector3> points = new List<Vector3>();

    [Tooltip("Optional. When assigned this path inherits the source color.")]
    public PowerSource powerSource;
}

/// Standalone prefab. Animates one impulse per path — a snake that travels from the first
/// point to the last, fading at the tail and solid at the head, like a signal on a wire.
public class PathVisualisation : MonoBehaviour
{
    [Header("PATHS")]
    [Tooltip("One entry per connection to visualise. Each path animates independently.")]
    public List<ConnectionPath> paths = new List<ConnectionPath>();

    [Header("APPEARANCE")]
    public float lineWidth = 0.04f;

    [Tooltip("Peak opacity of the line. Applied after the color is resolved from any PowerSource.")]
    [Range(0f, 1f)]
    public float maxAlpha = 0.75f;

    [Header("ANIMATION")]
    [Tooltip("Speed of the impulse in world units per second.")]
    public float drawSpeed = 4f;

    [Tooltip("Length of the visible snake segment in world units.")]
    public float snakeLength = 3f;

    [Tooltip("Seconds to wait after the impulse exits before the next one fires.")]
    public float cycleDuration = 8f;

    // -------------------------------------------------------------------------

    private readonly List<LineRenderer> lineRenderers = new List<LineRenderer>();

    private void Awake()
    {
        CreateLineRenderers();
    }

    private void Start()
    {
        for (int i = 0; i < lineRenderers.Count; i++)
            StartCoroutine(AnimatePath(i));
    }

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    private void CreateLineRenderers()
    {
        lineRenderers.Clear();

        for (int i = 0; i < paths.Count; i++)
        {
            GameObject child = new GameObject($"Line_{i}");
            child.transform.SetParent(transform, worldPositionStays: false);

            LineRenderer lr = child.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr);
            lineRenderers.Add(lr);
        }
    }

    private void ConfigureLineRenderer(LineRenderer lr)
    {
        lr.useWorldSpace = true;
        lr.loop = false;
        lr.widthMultiplier = lineWidth;
        lr.numCapVertices = 3;
        lr.numCornerVertices = 3;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.positionCount = 0;
        lr.material = new Material(Shader.Find("Sprites/Default"));
    }

    // -------------------------------------------------------------------------
    // Animation
    // -------------------------------------------------------------------------

    private IEnumerator AnimatePath(int index)
    {
        // Stagger so multiple paths don't fire simultaneously.
        yield return new WaitForSeconds(Random.Range(0f, cycleDuration));

        while (true)
        {
            yield return StartCoroutine(RunSnake(index));
            yield return new WaitForSeconds(cycleDuration);
        }
    }

    /// Moves the snake from the start of the path to the end, then exits.
    private IEnumerator RunSnake(int index)
    {
        LineRenderer lr = lineRenderers[index];
        List<Vector3> pts = paths[index].points;
        Color color = GetPathColor(index);

        if (pts == null || pts.Count < 2)
            yield break;

        float[] segLengths = BuildSegmentLengths(pts, out float totalLength);

        if (totalLength <= 0f)
            yield break;

        // Gradient is fixed for the whole run: transparent at [0] (tail), opaque at [1] (head).
        ApplyGradient(lr, color);

        // headDist is allowed to exceed totalLength so the tail can naturally exit the path.
        float headDist = 0f;

        while (true)
        {
            headDist += drawSpeed * Time.deltaTime;

            float renderedHead = Mathf.Min(headDist, totalLength);
            float tailDist = Mathf.Max(0f, headDist - snakeLength);
            float renderedTail = Mathf.Min(tailDist, totalLength);

            UpdateSnake(lr, pts, segLengths, renderedTail, renderedHead);

            // Tail has exited the path — impulse is complete.
            if (tailDist >= totalLength)
                break;

            yield return null;
        }

        lr.positionCount = 0;
    }

    // -------------------------------------------------------------------------
    // Geometry
    // -------------------------------------------------------------------------

    /// Builds an array of per-segment lengths and returns the total path length.
    private float[] BuildSegmentLengths(List<Vector3> pts, out float totalLength)
    {
        float[] lengths = new float[pts.Count - 1];
        totalLength = 0f;

        for (int i = 0; i < pts.Count - 1; i++)
        {
            lengths[i] = Vector3.Distance(pts[i], pts[i + 1]);
            totalLength += lengths[i];
        }

        return lengths;
    }

    /// Writes the visible slice of the path (from tailDist to headDist) into the LineRenderer.
    private void UpdateSnake(LineRenderer lr, List<Vector3> pts, float[] segLengths, float tailDist, float headDist)
    {
        if (headDist <= tailDist)
        {
            lr.positionCount = 0;
            return;
        }

        List<Vector3> visible = new List<Vector3>();
        float accumulated = 0f;

        for (int i = 0; i < segLengths.Length; i++)
        {
            float segStart = accumulated;
            float segEnd = accumulated + segLengths[i];

            // Segment entirely behind the tail or ahead of the head — skip.
            if (segEnd <= tailDist) { accumulated = segEnd; continue; }
            if (segStart >= headDist) break;

            float clampedStart = Mathf.Max(tailDist, segStart);
            float clampedEnd = Mathf.Min(headDist, segEnd);

            float tStart = Mathf.Clamp01((clampedStart - segStart) / segLengths[i]);
            float tEnd = Mathf.Clamp01((clampedEnd - segStart) / segLengths[i]);

            // First visible point — add the tail tip.
            if (visible.Count == 0)
                visible.Add(Vector3.Lerp(pts[i], pts[i + 1], tStart));

            // Add the end of this visible segment (waypoint or interpolated head tip).
            visible.Add(Vector3.Lerp(pts[i], pts[i + 1], tEnd));

            accumulated = segEnd;
        }

        if (visible.Count < 2)
        {
            lr.positionCount = 0;
            return;
        }

        lr.positionCount = visible.Count;
        lr.SetPositions(visible.ToArray());
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// Sets the LineRenderer gradient. Index 0 = tail (transparent), index 1 = head (maxAlpha).
    /// The gradient is fixed per-run so it always reads correctly regardless of snake position.
    private void ApplyGradient(LineRenderer lr, Color color)
    {
        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f,       0f),    // tail  — fully transparent
                new GradientAlphaKey(maxAlpha,  0.3f), // ramps up in the first 30% of the snake
                new GradientAlphaKey(maxAlpha,  1f)    // head  — full opacity
            }
        );

        lr.colorGradient = gradient;
    }

    private Color GetPathColor(int index)
    {
        PowerSource src = paths[index].powerSource;
        return (src != null) ? src.powerColor : Color.white;
    }
}

