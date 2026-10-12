using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public class ActivatorConnectionCometPathDriver : MonoBehaviour
{
    private const float MinPathLength = 0.01f;
    private const float MinSegmentLength = 0.001f;
    private const float MinSpawnInterval = 0.01f;

    [SerializeField] private GameObject cometPrefab;
    [SerializeField] private float cometSpawnInterval = 2f;
    [SerializeField] private float cometSpeed = 2.5f;
    [SerializeField] private float cometDespawnDelay = 2f;

    private LineRenderer lineRenderer;
    private readonly List<Vector3> pathPoints = new List<Vector3>();
    private readonly List<float> cumulativeDistances = new List<float>();
    private readonly List<CometAgent> activeComets = new List<CometAgent>();

    private float totalPathLength;
    private Coroutine spawnRoutine;

    private sealed class CometAgent
    {
        public Transform Transform;
        public float DistanceAlongPath;
        public bool ReachedEnd;
        public bool DespawnScheduled;
    }

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        CachePathFromLineRenderer();
    }

    private void Start()
    {
        if (cometPrefab == null || totalPathLength <= MinPathLength)
            return;

        if (cometSpawnInterval < MinSpawnInterval)
            return;

        spawnRoutine = StartCoroutine(SpawnCometsOnTimer());
    }

    private void OnDisable()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    private void Update()
    {
        if (totalPathLength <= MinPathLength || cometSpeed <= 0f)
            return;

        float delta = cometSpeed * Time.deltaTime;
        for (int i = activeComets.Count - 1; i >= 0; i--)
        {
            CometAgent agent = activeComets[i];
            if (agent.Transform == null)
            {
                activeComets.RemoveAt(i);
                continue;
            }

            if (agent.ReachedEnd)
                continue;

            agent.DistanceAlongPath += delta;
            if (agent.DistanceAlongPath >= totalPathLength)
            {
                agent.DistanceAlongPath = totalPathLength;
                agent.ReachedEnd = true;
                agent.Transform.position = SamplePathAtDistance(totalPathLength);
                StopCometParticles(agent.Transform);
                if (!agent.DespawnScheduled)
                {
                    agent.DespawnScheduled = true;
                    StartCoroutine(DespawnComet(agent));
                }

                continue;
            }

            agent.Transform.position = SamplePathAtDistance(agent.DistanceAlongPath);
        }
    }

    public void Configure(GameObject prefab, float spawnIntervalSeconds, float speed)
    {
        cometPrefab = prefab;
        cometSpawnInterval = Mathf.Max(MinSpawnInterval, spawnIntervalSeconds);
        cometSpeed = Mathf.Max(0f, speed);
    }

    private void CachePathFromLineRenderer()
    {
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

    private IEnumerator SpawnCometsOnTimer()
    {
        while (true)
        {
            SpawnCometAtStart();
            yield return new WaitForSeconds(cometSpawnInterval);
        }
    }

    private void SpawnCometAtStart()
    {
        GameObject instance = Instantiate(cometPrefab, transform);
        instance.transform.position = SamplePathAtDistance(0f);

        activeComets.Add(new CometAgent
        {
            Transform = instance.transform,
            DistanceAlongPath = 0f,
            ReachedEnd = false,
            DespawnScheduled = false
        });
    }

    private static void StopCometParticles(Transform cometRoot)
    {
        if (cometRoot == null)
            return;

        ParticleSystem[] systems = cometRoot.GetComponentsInChildren<ParticleSystem>();
        for (int i = 0; i < systems.Length; i++)
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private IEnumerator DespawnComet(CometAgent agent)
    {
        if (cometDespawnDelay > 0f)
            yield return new WaitForSeconds(cometDespawnDelay);

        activeComets.Remove(agent);
        if (agent.Transform != null)
            Destroy(agent.Transform.gameObject);
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
}
