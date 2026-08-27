using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class ShaftZigzagLineTest : MonoBehaviour
{
    private const float EndInset = 0.05f;
    private const float MinLength = 0.01f;
    private const float LengthChangeThreshold = 0.001f;
    private const float MaxSidewaysOffset = 0.35f;

    [Header("Anchors")]
    [SerializeField] private Transform baseAnchor;
    [SerializeField] private Transform faceAnchor;

    [Header("Path")]
    [Tooltip("Shortest straight leg length in metres (forward toward platform, or 90° sideways).")]
    [SerializeField] private float minStepLength = 0.25f;
    [Tooltip("Longest straight leg length in metres (forward toward platform, or 90° sideways).")]
    [SerializeField] private float maxStepLength = 0.55f;

    [Header("Line")]
    [SerializeField] private Sprite lineSprite;
    [SerializeField] private float lineWidth = 0.06f;
    [SerializeField] private float emissionStrength = 2f;
    [SerializeField] private int cornerVertices = 4;

    [Header("Motion")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float trailLength = 1.5f;
    [SerializeField] private float sampleSpacing = 0.05f;
    [SerializeField] private bool newPathEachCycle = true;

    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");
    private static readonly int CrossSectionOnlyId = Shader.PropertyToID("_CrossSectionOnly");

    private LineRenderer lineRenderer;
    private Material runtimeMaterial;
    private readonly List<Vector3> pathPoints = new List<Vector3>();
    private readonly List<float> cumulativeDistances = new List<float>();
    private readonly List<Vector3> visiblePoints = new List<Vector3>();

    private float lastPathLength = -1f;
    private float lastMinStepLength = -1f;
    private float lastMaxStepLength = -1f;
    private Vector3 lastBasePosition;
    private Vector3 lastFacePosition;
    private Vector3 currentBasePoint;
    private Vector3 currentFacePoint;
    private float currentLength;
    private float totalPathLength;
    private float headDistance;
    private int pathSeed;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        EnsureMaterial();
        ApplyLineSettings();
        pathSeed = Random.Range(0, int.MaxValue);
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }

    private void OnValidate()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer == null)
            return;

        EnsureMaterial();
        ApplyLineSettings();
        ClampStepLengths();
    }

    private void ClampStepLengths()
    {
        minStepLength = Mathf.Max(minStepLength, MinLength);
        maxStepLength = Mathf.Max(maxStepLength, minStepLength);
    }

    private void LateUpdate()
    {
        if (baseAnchor == null || faceAnchor == null)
        {
            lineRenderer.enabled = false;
            return;
        }

        currentBasePoint = baseAnchor.position;
        currentFacePoint = faceAnchor.position;
        currentLength = Vector3.Distance(currentBasePoint, currentFacePoint);

        if (currentLength < MinLength)
        {
            lineRenderer.enabled = false;
            headDistance = 0f;
            return;
        }

        lineRenderer.enabled = true;

        bool pathRebuilt = false;
        if (ShouldRebuildPath(currentBasePoint, currentFacePoint, currentLength))
        {
            BuildPath(currentBasePoint, currentFacePoint, currentLength);
            lastPathLength = currentLength;
            lastBasePosition = currentBasePoint;
            lastFacePosition = currentFacePoint;
            lastMinStepLength = minStepLength;
            lastMaxStepLength = maxStepLength;
            headDistance = 0f;
            pathRebuilt = true;
        }

        if (!pathRebuilt)
            AdvanceHead();

        ApplyVisibleSegmentToLine();
    }

    private void AdvanceHead()
    {
        if (totalPathLength <= MinLength)
            return;

        headDistance += moveSpeed * Time.deltaTime;

        if (headDistance < totalPathLength)
            return;

        headDistance = 0f;

        if (!newPathEachCycle)
            return;

        pathSeed = Random.Range(0, int.MaxValue);
        BuildPath(currentBasePoint, currentFacePoint, currentLength);
    }

    private void EnsureMaterial()
    {
        if (runtimeMaterial == null)
        {
            Shader shader = Shader.Find("Custom/ShaftSnakeLine");
            if (shader == null)
                return;

            runtimeMaterial = new Material(shader);
            lineRenderer.material = runtimeMaterial;
        }

        ApplyMaterialProperties();
    }

    private void ApplyMaterialProperties()
    {
        if (runtimeMaterial == null)
            return;

        if (lineSprite != null)
        {
            runtimeMaterial.SetTexture(MainTexId, lineSprite.texture);

            Rect rect = lineSprite.textureRect;
            Texture2D texture = lineSprite.texture;
            runtimeMaterial.SetTextureScale(MainTexId, new Vector2(rect.width / texture.width, rect.height / texture.height));
            runtimeMaterial.SetTextureOffset(MainTexId, new Vector2(rect.x / texture.width, rect.y / texture.height));
        }

        runtimeMaterial.SetFloat(EmissionStrengthId, emissionStrength);
        runtimeMaterial.SetFloat(CrossSectionOnlyId, 1f);
    }

    private void ApplyLineSettings()
    {
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.widthMultiplier = lineWidth;
        lineRenderer.numCornerVertices = cornerVertices;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.textureScale = Vector2.one;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        ApplyMaterialProperties();
    }

    private bool ShouldRebuildPath(Vector3 basePoint, Vector3 facePoint, float length)
    {
        if (lastPathLength < 0f)
            return true;

        if (Mathf.Abs(length - lastPathLength) > LengthChangeThreshold)
            return true;

        if ((basePoint - lastBasePosition).sqrMagnitude > LengthChangeThreshold * LengthChangeThreshold)
            return true;

        if ((facePoint - lastFacePosition).sqrMagnitude > LengthChangeThreshold * LengthChangeThreshold)
            return true;

        if (!Mathf.Approximately(minStepLength, lastMinStepLength))
            return true;

        if (!Mathf.Approximately(maxStepLength, lastMaxStepLength))
            return true;

        return false;
    }

    private float RandomStepLength()
    {
        return Random.Range(minStepLength, maxStepLength);
    }

    private void BuildPath(Vector3 basePoint, Vector3 facePoint, float length)
    {
        pathPoints.Clear();

        Vector3 forward = (facePoint - basePoint) / length;
        Vector3 right = Vector3.Cross(forward, GetShaftUp(forward)).normalized;
        Vector3 up = Vector3.Cross(right, forward).normalized;

        Random.InitState(pathSeed);

        Vector3 position = basePoint + forward * EndInset;
        float forwardProgress = EndInset;
        pathPoints.Add(position);

        int safety = 0;
        while (forwardProgress < length - EndInset && safety < 512)
        {
            safety++;

            float forwardStep = Mathf.Min(RandomStepLength(), length - EndInset - forwardProgress);
            if (forwardStep <= MinLength)
                break;

            position += forward * forwardStep;
            forwardProgress += forwardStep;
            pathPoints.Add(position);

            if (forwardProgress >= length - EndInset - MinLength)
                break;

            Vector3 lateral = PickLateralDirection(right, up);
            float lateralStep = RandomStepLength();
            Vector3 axisPoint = basePoint + forward * forwardProgress;
            Vector3 next = position + lateral * lateralStep;
            position = ClampInsideTunnel(next, axisPoint, forward, MaxSidewaysOffset);
            pathPoints.Add(position);
        }

        pathPoints.Add(facePoint - forward * EndInset);
        BuildCumulativeDistances();
    }

    private void BuildCumulativeDistances()
    {
        cumulativeDistances.Clear();
        cumulativeDistances.Add(0f);
        totalPathLength = 0f;

        for (int i = 1; i < pathPoints.Count; i++)
        {
            totalPathLength += Vector3.Distance(pathPoints[i - 1], pathPoints[i]);
            cumulativeDistances.Add(totalPathLength);
        }
    }

    private void ApplyVisibleSegmentToLine()
    {
        if (totalPathLength <= MinLength)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        float tailDistance = Mathf.Max(0f, headDistance - trailLength);
        float spacing = Mathf.Max(sampleSpacing, MinLength);

        visiblePoints.Clear();
        visiblePoints.Add(SamplePathAtDistance(tailDistance));

        float distance = tailDistance + spacing;
        while (distance < headDistance)
        {
            visiblePoints.Add(SamplePathAtDistance(distance));
            distance += spacing;
        }

        visiblePoints.Add(SamplePathAtDistance(headDistance));

        lineRenderer.positionCount = visiblePoints.Count;
        for (int i = 0; i < visiblePoints.Count; i++)
            lineRenderer.SetPosition(i, visiblePoints[i]);
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
            if (segmentLength <= MinLength)
                return pathPoints[i];

            float t = (distance - segmentStart) / segmentLength;
            return Vector3.Lerp(pathPoints[i - 1], pathPoints[i], t);
        }

        return pathPoints[pathPoints.Count - 1];
    }

    private static Vector3 PickLateralDirection(Vector3 right, Vector3 up)
    {
        switch (Random.Range(0, 4))
        {
            case 0: return right;
            case 1: return -right;
            case 2: return up;
            default: return -up;
        }
    }

    private static Vector3 ClampInsideTunnel(Vector3 point, Vector3 axisPoint, Vector3 forward, float tunnelHalfWidth)
    {
        Vector3 offset = point - axisPoint;
        offset -= forward * Vector3.Dot(offset, forward);

        if (offset.sqrMagnitude > tunnelHalfWidth * tunnelHalfWidth && offset.sqrMagnitude > MinLength)
            offset = offset.normalized * tunnelHalfWidth;

        return axisPoint + offset;
    }

    private static Vector3 GetShaftUp(Vector3 forward)
    {
        if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f)
            return Vector3.forward;

        return Vector3.up;
    }
}
