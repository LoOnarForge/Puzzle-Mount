using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class ActivatorConnectionLineImpulse : MonoBehaviour
{
    private const float MinPathLength = 0.01f;
    private const float MinSegmentLength = 0.001f;
    private const float VertexEpsilon = 0.0001f;
    private const float HeadTangentStep = 0.05f;
    private const int CornerVertices = 6;

    [Header("Beam")]
    [SerializeField] private float travelSpeed = 2.5f;
    [SerializeField] private float beamLength = 1.1f;
    [SerializeField] private float fadeLength = 0.35f;
    [SerializeField] private float beamWidth = 0.03f;

    private LineRenderer lineRenderer;
    private readonly List<Vector3> pathPoints = new List<Vector3>();
    private readonly List<float> cumulativeDistances = new List<float>();
    private readonly List<Vector3> segmentPoints = new List<Vector3>();

    private Gradient beamGradient;
    private Color beamHeadColor = Color.white;
    private float beamHeadAlpha = 1f;

    private GameObject headVisualPrefab;
    private Transform headVisual;
    private float speedMin;
    private float speedMax;

    private float totalPathLength;
    private float headDistance;
    private float externalAlphaScale = 1f;
    private bool isRunning;
    private bool loopWhileRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        CaptureHeadColorFromLineRenderer();
        ApplyLineRendererSettings();
    }

    private void OnDestroy()
    {
        DestroyHeadVisual();
    }

    private void LateUpdate()
    {
        if (!isRunning || lineRenderer == null || totalPathLength <= MinPathLength || travelSpeed <= 0f)
            return;

        headDistance += travelSpeed * Time.deltaTime;

        float tailDistance = headDistance - beamLength;
        if (tailDistance >= totalPathLength - VertexEpsilon)
        {
            CompleteCycle();
            return;
        }

        DrawBeamSegment(tailDistance, headDistance);
        UpdateHeadVisual(Mathf.Min(headDistance, totalPathLength));
    }

    // Assigns head prefab and random speed range from LineRendererGenerator.
    public void ConfigureFromGenerator(GameObject prefab, float minSpeed, float maxSpeed)
    {
        headVisualPrefab = prefab;
        speedMin = Mathf.Max(0f, minSpeed);
        speedMax = Mathf.Max(speedMin, maxSpeed);
    }

    // True while the cursor is over the activator — starts the next cycle when the current one finishes.
    public void SetLoopWhileRequested(bool requested)
    {
        loopWhileRequested = requested;
    }

    // Starts a beam from the path start if idle.
    public void StartCycleIfIdle()
    {
        if (isRunning || totalPathLength <= MinPathLength)
            return;

        BeginCycle();
    }

    // Multiplies beam opacity and width without replacing the tail fade gradient.
    public void SetExternalAlphaScale(float alphaScale)
    {
        externalAlphaScale = Mathf.Clamp01(alphaScale);
        if (lineRenderer != null)
            lineRenderer.widthMultiplier = beamWidth * externalAlphaScale;
    }

    // Stores the full baked path from the line renderer (call only while positions are the full route).
    public void CachePathFromLineRenderer()
    {
        if (isRunning)
            return;

        pathPoints.Clear();
        cumulativeDistances.Clear();
        totalPathLength = 0f;

        if (lineRenderer == null || lineRenderer.positionCount <= 0)
            return;

        int count = lineRenderer.positionCount;
        for (int i = 0; i < count; i++)
            pathPoints.Add(lineRenderer.GetPosition(i));

        cumulativeDistances.Add(0f);
        for (int i = 1; i < pathPoints.Count; i++)
        {
            totalPathLength += Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
            cumulativeDistances.Add(totalPathLength);
        }
    }

    private void BeginCycle()
    {
        RollTravelSpeed();
        headDistance = 0f;
        isRunning = true;
        enabled = true;
        EnsureHeadVisual();
        UpdateHeadVisual(0f);
    }

    private void CompleteCycle()
    {
        headDistance = 0f;
        lineRenderer.positionCount = 0;
        HideHeadVisual();

        if (loopWhileRequested)
        {
            BeginCycle();
            return;
        }

        isRunning = false;
        enabled = false;
    }

    private void RollTravelSpeed()
    {
        if (speedMax <= 0f && speedMin <= 0f)
            return;

        travelSpeed = speedMax > speedMin
            ? Random.Range(speedMin, speedMax)
            : speedMin;
    }

    private void DrawBeamSegment(float tailDistance, float headDistance)
    {
        float segmentStart = Mathf.Max(0f, tailDistance);
        float segmentEnd = Mathf.Min(headDistance, totalPathLength);
        if (segmentEnd <= segmentStart + VertexEpsilon)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        BuildSegmentPoints(segmentStart, segmentEnd);
        if (segmentPoints.Count < 2)
        {
            lineRenderer.positionCount = segmentPoints.Count;
            if (segmentPoints.Count == 1)
                lineRenderer.SetPosition(0, segmentPoints[0]);
            return;
        }

        float visibleLength = segmentEnd - segmentStart;
        ApplyBeamGradient(visibleLength);

        lineRenderer.positionCount = segmentPoints.Count;
        for (int i = 0; i < segmentPoints.Count; i++)
            lineRenderer.SetPosition(i, segmentPoints[i]);
    }

    private void BuildSegmentPoints(float tailDistance, float headDistance)
    {
        segmentPoints.Clear();

        Vector3 tailPosition = SamplePathAtDistance(tailDistance);
        segmentPoints.Add(tailPosition);

        for (int i = 1; i < pathPoints.Count; i++)
        {
            float vertexDistance = cumulativeDistances[i];
            if (vertexDistance > tailDistance + VertexEpsilon && vertexDistance < headDistance - VertexEpsilon)
                segmentPoints.Add(pathPoints[i]);
        }

        Vector3 headPosition = SamplePathAtDistance(headDistance);
        if ((headPosition - segmentPoints[segmentPoints.Count - 1]).sqrMagnitude > VertexEpsilon * VertexEpsilon)
            segmentPoints.Add(headPosition);
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
            if (segmentLength <= MinSegmentLength)
                return pathPoints[i];

            float t = (distance - segmentStart) / segmentLength;
            return Vector3.Lerp(pathPoints[i - 1], pathPoints[i], t);
        }

        return pathPoints[pathPoints.Count - 1];
    }

    private void EnsureHeadVisual()
    {
        if (headVisualPrefab == null || headVisual != null)
            return;

        GameObject instance = Instantiate(headVisualPrefab, transform);
        headVisual = instance.transform;
    }

    private void UpdateHeadVisual(float headPathDistance)
    {
        if (headVisualPrefab == null)
            return;

        EnsureHeadVisual();
        if (headVisual == null)
            return;

        if (!headVisual.gameObject.activeSelf)
            headVisual.gameObject.SetActive(true);

        Vector3 headPosition = SamplePathAtDistance(headPathDistance);
        headVisual.position = headPosition;

        float backDistance = Mathf.Max(0f, headPathDistance - HeadTangentStep);
        Vector3 backPosition = SamplePathAtDistance(backDistance);
        Vector3 forward = headPosition - backPosition;
        if (forward.sqrMagnitude > VertexEpsilon)
            headVisual.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private void HideHeadVisual()
    {
        if (headVisual != null)
            headVisual.gameObject.SetActive(false);
    }

    private void DestroyHeadVisual()
    {
        if (headVisual != null)
            Destroy(headVisual.gameObject);
        headVisual = null;
    }

    private void CaptureHeadColorFromLineRenderer()
    {
        if (lineRenderer == null)
            return;

        Gradient source = lineRenderer.colorGradient;
        if (source.colorKeys.Length > 0)
            beamHeadColor = source.colorKeys[0].color;

        if (source.alphaKeys.Length > 0)
            beamHeadAlpha = source.alphaKeys[0].alpha;
    }

    private void ApplyLineRendererSettings()
    {
        if (lineRenderer == null)
            return;

        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.numCornerVertices = CornerVertices;
        lineRenderer.widthMultiplier = beamWidth;
        lineRenderer.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
    }

    private void ApplyBeamGradient(float visibleSegmentLength)
    {
        if (lineRenderer == null)
            return;

        if (beamGradient == null)
            beamGradient = new Gradient();

        float headAlpha = beamHeadAlpha * externalAlphaScale;
        Color headColor = beamHeadColor;
        headColor.a = headAlpha;

        Color tailColor = headColor;
        tailColor.a = 0f;

        float fadeSpan = Mathf.Clamp(fadeLength, 0f, visibleSegmentLength);
        float fadeEndTime = visibleSegmentLength > MinPathLength
            ? Mathf.Clamp01(fadeSpan / visibleSegmentLength)
            : 1f;

        if (fadeEndTime <= VertexEpsilon)
        {
            beamGradient.SetKeys(
                new[] { new GradientColorKey(headColor, 0f), new GradientColorKey(headColor, 1f) },
                new[] { new GradientAlphaKey(headAlpha, 0f), new GradientAlphaKey(headAlpha, 1f) });
        }
        else
        {
            beamGradient.SetKeys(
                new[] { new GradientColorKey(tailColor, 0f), new GradientColorKey(headColor, fadeEndTime), new GradientColorKey(headColor, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(headAlpha, fadeEndTime), new GradientAlphaKey(headAlpha, 1f) });
        }

        lineRenderer.colorGradient = beamGradient;
    }
}
