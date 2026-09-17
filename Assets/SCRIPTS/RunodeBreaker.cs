using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RunodeBreaker : MonoBehaviour
{
    private const float DefaultCubeSize = 1f;
    private const int DefaultGridResolution = 5;
    private const float DefaultDebrisLifetime = 3f;
    private const float DefaultExplosionForce = 1.5f;
    private const float DefaultMinFadeStartTime = 1.5f;
    private const float DefaultFadeDuration = 1f;

    [Header("VISUAL SOURCE")]
    [SerializeField] private MeshRenderer cubeMeshRenderer;

    [Header("BREAK SETTINGS")]
    [SerializeField] private float cubeSize = DefaultCubeSize;
    [SerializeField] private int gridResolution = DefaultGridResolution;
    [SerializeField] private float debrisLifetime = DefaultDebrisLifetime;
    [SerializeField] private float explosionForce = DefaultExplosionForce;
    [SerializeField] private float minFadeStartTime = DefaultMinFadeStartTime;
    [SerializeField] private float fadeDuration = DefaultFadeDuration;

    [Header("DEBUG")]
    [SerializeField] private bool breakOnKeyPress;
    [SerializeField] private Key debugBreakKey = Key.B;

    private RunodeCube runodeCube;
    private bool isBreaking;

    private void Awake()
    {
        runodeCube = GetComponent<RunodeCube>();

        if (cubeMeshRenderer == null)
            cubeMeshRenderer = GetComponentInChildren<MeshRenderer>();
    }

    private void Update()
    {
        if (!breakOnKeyPress || isBreaking)
            return;

        if (Keyboard.current != null && Keyboard.current[debugBreakKey].wasPressedThisFrame)
            Break();
    }

    // Breaks this runode into a grid of physics debris chunks.
    public void Break()
    {
        if (isBreaking)
            return;

        if (RunodeDebrisPool.Instance == null)
        {
            Debug.LogError($"{nameof(RunodeBreaker)} requires a {nameof(RunodeDebrisPool)} in the scene.", this);
            return;
        }

        isBreaking = true;
        StartCoroutine(BreakRoutine());
    }

    private IEnumerator BreakRoutine()
    {
        PowerDownAllLines();
        DisableRunodePhysicsAndColliders();
        HideRunodeVisuals();

        Material debrisMaterial = cubeMeshRenderer != null ? cubeMeshRenderer.sharedMaterial : null;
        Vector3 cubeCenter = transform.position;
        float cellSize = cubeSize / gridResolution;
        float halfCube = cubeSize * 0.5f;
        float halfCell = cellSize * 0.5f;

        for (int x = 0; x < gridResolution; x++)
        {
            for (int y = 0; y < gridResolution; y++)
            {
                for (int z = 0; z < gridResolution; z++)
                {
                    Vector3 localOffset = new Vector3(
                        x * cellSize + halfCell - halfCube,
                        y * cellSize + halfCell - halfCube,
                        z * cellSize + halfCell - halfCube);

                    Vector3 worldPosition = cubeCenter + localOffset;
                    Vector3 forceDirection = (worldPosition - cubeCenter).normalized;
                    if (forceDirection.sqrMagnitude < 0.001f)
                        forceDirection = Vector3.up;

                    Vector3 explosion = forceDirection * explosionForce + Vector3.up * (explosionForce * 0.25f);

                    RunodeDebrisChunk chunk = RunodeDebrisPool.Instance.Get();
                    if (chunk == null)
                        continue;

                    chunk.transform.SetParent(null, true);
                    chunk.Activate(
                        worldPosition,
                        cellSize,
                        debrisMaterial,
                        explosion,
                        debrisLifetime,
                        minFadeStartTime,
                        fadeDuration);
                }
            }
        }

        if (runodeCube != null)
            RunodeCube.RefreshConnectionsNearPoint(transform.position);

        Destroy(gameObject);
        yield break;
    }

    private void PowerDownAllLines()
    {
        if (runodeCube == null)
            return;

        RunodeLine[] lines = runodeCube.GetLines();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].IsPowered)
                lines[i].PowerDownLine();
        }
    }

    private void DisableRunodePhysicsAndColliders()
    {
        RunodeMovement movement = GetComponent<RunodeMovement>();
        if (movement != null)
            movement.enabled = false;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;

        Collider[] colliders = GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private void HideRunodeVisuals()
    {
        if (cubeMeshRenderer != null)
            cubeMeshRenderer.enabled = false;

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
            spriteRenderers[i].enabled = false;

        Transform visualRoot = transform.childCount > 0 ? transform.GetChild(0) : null;
        if (visualRoot != null)
        {
            MeshRenderer[] meshRenderers = visualRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
                meshRenderers[i].enabled = false;
        }
    }
}
