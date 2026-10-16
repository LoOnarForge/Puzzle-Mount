using System.Collections.Generic;
using UnityEngine;

public class RunodeDebrisPool : MonoBehaviour
{
    public static RunodeDebrisPool Instance { get; private set; }

    [Header("DEBRIS")]
    [SerializeField] private RunodeDebrisChunk chunkPrefab;
    [SerializeField] private int prewarmCount = 375;

    [Header("BREAK EFFECT")]
    [SerializeField] private ParticleSystem breakParticlesPrefab;

    private readonly Queue<RunodeDebrisChunk> available = new Queue<RunodeDebrisChunk>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (chunkPrefab == null)
        {
            Debug.LogError($"{nameof(RunodeDebrisPool)} requires a chunk prefab.", this);
            return;
        }

        for (int i = 0; i < prewarmCount; i++)
            available.Enqueue(CreateChunk());
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // Gets an inactive debris chunk from the pool, creating one if needed.
    public RunodeDebrisChunk Get()
    {
        if (chunkPrefab == null)
            return null;

        if (available.Count == 0)
            return CreateChunk();

        return available.Dequeue();
    }

    // Spawns the break particle effect at the center of a destroyed runode.
    public void PlayBreakEffect(Vector3 worldPosition)
    {
        if (breakParticlesPrefab == null)
            return;

        ParticleSystem instance = Instantiate(breakParticlesPrefab, worldPosition, Quaternion.identity);
        instance.Play();

        ParticleSystem.MainModule main = instance.main;
        float lifetime = main.duration + main.startLifetime.constantMax;
        Destroy(instance.gameObject, lifetime);
    }

    // Returns a debris chunk to the pool.
    public void Release(RunodeDebrisChunk chunk)
    {
        if (chunk == null)
            return;

        chunk.Deactivate(transform);
        available.Enqueue(chunk);
    }

    private RunodeDebrisChunk CreateChunk()
    {
        RunodeDebrisChunk chunk = Instantiate(chunkPrefab, transform);
        chunk.Deactivate(transform);
        return chunk;
    }
}
