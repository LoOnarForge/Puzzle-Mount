using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class ActivatorConnectionLineVisual : MonoBehaviour
{
    private const float MinPathLength = 0.01f;
    private const float AnchorMoveThreshold = 0.001f;
    private const float CometSampleSpacing = 0.04f;
    private const float CometTailWidthFactor = 0.08f;
    private const int CometCornerVertices = 6;

    [Header("Anchors")]
    [SerializeField] private Transform startAnchor;
    [SerializeField] private Transform endAnchor;

    [Header("Path")]
    [SerializeField] private int pivotCount = 2;
    [SerializeField] private int zigzagAmount = 3;

    [Header("Base line")]
    [SerializeField] private Color baseLineColor = new Color(1f, 1f, 1f, 0.12f);
    [SerializeField] private float baseLineWidth = 0.035f;

    [Header("Comet line")]
    [ColorUsage(true, true)]
    [SerializeField] private Color cometColor = new Color(1.2f, 1f, 0.6f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color cometEmissionColor = new Color(1.5f, 1.1f, 0.4f, 1f);
    [SerializeField] private float cometEmissionStrength = 3f;
    [SerializeField] private float cometHeadWidth = 0.09f;
    [SerializeField] private float cometTrailLength = 1.25f;
    [SerializeField] private float cometMoveSpeed = 2.5f;

    private LineRenderer baseLineRenderer;
    private LineRenderer cometLineRenderer;
    private Material baseLineMaterial;
    private Material cometLineMaterial;

    private readonly List<Vector3> pathPoints = new List<Vector3>();
    private readonly List<float> cumulativeDistances = new List<float>();
    private readonly List<Vector3> cometVisiblePoints = new List<Vector3>();

    private float totalPathLength;
    private float headDistance;
    private Vector3 lastStartPosition;
    private Vector3 lastEndPosition;
    private bool hasCachedAnchors;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");

    private void Awake()
    {
        EnsureLineRenderers();
        EnsureMaterials();
        ApplyLineSettings();
    }

    private void OnDestroy()
    {
        if (baseLineMaterial != null)
            Destroy(baseLineMaterial);
        if (cometLineMaterial != null)
            Destroy(cometLineMaterial);
    }

    private void OnValidate()
    {
        if (baseLineRenderer == null || cometLineRenderer == null)
            EnsureLineRenderers();

        EnsureMaterials();
        ApplyLineSettings();
        ApplyMaterialColors();
    }

    private void LateUpdate()
    {
        if (startAnchor == null || endAnchor == null)
        {
            SetLinesEnabled(false);
            return;
        }

        if (ShouldRebuildPath())
            RebuildPathFromAnchors();

        if (totalPathLength <= MinPathLength)
        {
            SetLinesEnabled(false);
            return;
        }

        SetLinesEnabled(true);
        AdvanceCometHead();
        UpdateCometSegment();
    }

    // Assigns activator and device transforms used to build the shared path.
    public void SetAnchors(Transform start, Transform end)
    {
        startAnchor = start;
        endAnchor = end;
        hasCachedAnchors = false;
        RebuildPathFromAnchors();
    }

    // Rebuilds both lines from the current anchors and path settings.
    public void RebuildPathFromAnchors()
    {
        if (startAnchor == null || endAnchor == null)
            return;

        Vector3 start = startAnchor.position;
        Vector3 end = endAnchor.position;
        string routeSeedName = endAnchor.name;

        Vector3[] built = LineRendererPathBuilder.BuildPoints(start, end, routeSeedName, pivotCount, zigzagAmount);
        SetPathPoints(built);

        lastStartPosition = start;
        lastEndPosition = end;
        hasCachedAnchors = true;
        headDistance = 0f;
    }

    private void SetPathPoints(IReadOnlyList<Vector3> points)
    {
        pathPoints.Clear();
        for (int i = 0; i < points.Count; i++)
            pathPoints.Add(points[i]);

        BuildCumulativeDistances();
        ApplyBaseLinePositions();
    }

    private bool ShouldRebuildPath()
    {
        if (!hasCachedAnchors)
            return true;

        Vector3 start = startAnchor.position;
        Vector3 end = endAnchor.position;
        if ((start - lastStartPosition).sqrMagnitude > AnchorMoveThreshold * AnchorMoveThreshold)
            return true;
        if ((end - lastEndPosition).sqrMagnitude > AnchorMoveThreshold * AnchorMoveThreshold)
            return true;

        return false;
    }

    private void BuildCumulativeDistances()
    {
        cumulativeDistances.Clear();
        totalPathLength = 0f;

        if (pathPoints.Count == 0)
            return;

        cumulativeDistances.Add(0f);
        for (int i = 1; i < pathPoints.Count; i++)
        {
            totalPathLength += Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
            cumulativeDistances.Add(totalPathLength);
        }
    }

    private void ApplyBaseLinePositions()
    {
        if (baseLineRenderer == null)
            return;

        baseLineRenderer.positionCount = pathPoints.Count;
        for (int i = 0; i < pathPoints.Count; i++)
            baseLineRenderer.SetPosition(i, pathPoints[i]);
    }

    private void AdvanceCometHead()
    {
        headDistance += cometMoveSpeed * Time.deltaTime;
        if (headDistance >= totalPathLength)
            headDistance = 0f;
    }

    private void UpdateCometSegment()
    {
        if (cometLineRenderer == null || totalPathLength <= MinPathLength)
            return;

        float tailDistance = Mathf.Max(0f, headDistance - cometTrailLength);
        cometVisiblePoints.Clear();
        cometVisiblePoints.Add(SamplePathAtDistance(tailDistance));

        float distance = tailDistance + CometSampleSpacing;
        while (distance < headDistance)
        {
            cometVisiblePoints.Add(SamplePathAtDistance(distance));
            distance += CometSampleSpacing;
        }

        cometVisiblePoints.Add(SamplePathAtDistance(headDistance));

        cometLineRenderer.positionCount = cometVisiblePoints.Count;
        for (int i = 0; i < cometVisiblePoints.Count; i++)
            cometLineRenderer.SetPosition(i, cometVisiblePoints[i]);
    }

    private Vector3 SamplePathAtDistance(float distance)
    {
        distance = Mathf.Clamp(distance, 0f, totalPathLength);

        if (pathPoints.Count == 1)
            return pathPoints[0];

        for (int i = 1; i < cumulativeDistances.Count; i++)
        {
            if (distance > cumulativeDistances[i])
                continue;

            float segmentStart = cumulativeDistances[i - 1];
            float segmentLength = cumulativeDistances[i] - segmentStart;
            if (segmentLength <= MinPathLength)
                return pathPoints[i];

            float t = (distance - segmentStart) / segmentLength;
            return Vector3.Lerp(pathPoints[i - 1], pathPoints[i], t);
        }

        return pathPoints[pathPoints.Count - 1];
    }

    private void EnsureLineRenderers()
    {
        baseLineRenderer = GetOrCreateChildLineRenderer("Base Line");
        cometLineRenderer = GetOrCreateChildLineRenderer("Comet Line");
    }

    private LineRenderer GetOrCreateChildLineRenderer(string childName)
    {
        Transform existing = transform.Find(childName);
        GameObject lineObject = existing != null ? existing.gameObject : new GameObject(childName);
        if (existing == null)
            lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        if (line == null)
            line = lineObject.AddComponent<LineRenderer>();

        return line;
    }

    private void EnsureMaterials()
    {
        if (baseLineMaterial == null)
        {
            Shader shader = Shader.Find("Custom/ActivatorConnectionBaseLine");
            if (shader != null)
                baseLineMaterial = new Material(shader);
        }

        if (cometLineMaterial == null)
        {
            Shader shader = Shader.Find("Custom/Link Line Renderer");
            if (shader != null)
                cometLineMaterial = new Material(shader);
        }

        if (baseLineRenderer != null && baseLineMaterial != null)
            baseLineRenderer.material = baseLineMaterial;
        if (cometLineRenderer != null && cometLineMaterial != null)
            cometLineRenderer.material = cometLineMaterial;

        ApplyMaterialColors();
    }

    private void ApplyMaterialColors()
    {
        if (baseLineMaterial != null)
            baseLineMaterial.SetColor(ColorId, baseLineColor);

        if (cometLineMaterial != null)
        {
            cometLineMaterial.SetColor(ColorId, cometColor);
            cometLineMaterial.SetColor(EmissionColorId, cometEmissionColor);
            cometLineMaterial.SetFloat(EmissionStrengthId, cometEmissionStrength);
        }
    }

    private void ApplyLineSettings()
    {
        if (baseLineRenderer != null)
        {
            baseLineRenderer.useWorldSpace = true;
            baseLineRenderer.loop = false;
            baseLineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            baseLineRenderer.receiveShadows = false;
            baseLineRenderer.startWidth = baseLineWidth;
            baseLineRenderer.endWidth = baseLineWidth;
            baseLineRenderer.numCornerVertices = 2;
            baseLineRenderer.colorGradient = CreateSolidGradient(baseLineColor);
        }

        if (cometLineRenderer != null)
        {
            cometLineRenderer.useWorldSpace = true;
            cometLineRenderer.loop = false;
            cometLineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            cometLineRenderer.receiveShadows = false;
            cometLineRenderer.widthMultiplier = cometHeadWidth;
            cometLineRenderer.widthCurve = CreateCometWidthCurve();
            cometLineRenderer.numCornerVertices = CometCornerVertices;
            cometLineRenderer.colorGradient = CreateCometColorGradient();
        }
    }

    private static Gradient CreateSolidGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) });
        return gradient;
    }

    private Gradient CreateCometColorGradient()
    {
        Color head = cometColor;
        Color tail = cometColor;
        tail.a = 0f;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(tail, 0f), new GradientColorKey(head, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(head.a, 1f) });
        return gradient;
    }

    private static AnimationCurve CreateCometWidthCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, CometTailWidthFactor),
            new Keyframe(1f, 1f));
    }

    private void SetLinesEnabled(bool enabled)
    {
        if (baseLineRenderer != null)
            baseLineRenderer.enabled = enabled;
        if (cometLineRenderer != null)
            cometLineRenderer.enabled = enabled;
    }
}
