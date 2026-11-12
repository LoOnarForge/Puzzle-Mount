using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(60)]
public class FloorOfShame : MonoBehaviour
{
    private const int SkullPrefabSlotCount = 6;
    private const string SkullSpawnPointNamePrefix = "Skull Spawn point (";

    [SerializeField] [Range(0f, 5f)] private float platformSpawnDelaySeconds;
    [SerializeField] private GameObject platformPrefab;
    [SerializeField] private GameObject skullPrefab0;
    [SerializeField] private GameObject skullPrefab1;
    [SerializeField] private GameObject skullPrefab2;
    [SerializeField] private GameObject skullPrefab3;
    [SerializeField] private GameObject skullPrefab4;
    [SerializeField] private GameObject skullPrefab5;

    private CharacterMovement tim;
    private bool spawnedForCurrentFall;
    private float platformSpawnAtTime = -1f;
    private Transform spawnedSkull;
    private readonly List<Transform> skullSpawnPointBuffer = new List<Transform>(8);

    private void OnValidate()
    {
        platformSpawnDelaySeconds = Mathf.Clamp(Mathf.Round(platformSpawnDelaySeconds * 10f) / 10f, 0f, 5f);
    }

    private void Start()
    {
        tim = Object.FindAnyObjectByType<CharacterMovement>();
    }

    private void Update()
    {
        if (!Application.isPlaying || tim == null)
            return;

        if (tim.IsDead)
            return;

        if (!tim.IsAirborneDeathPending)
        {
            if (tim.IsGrounded)
            {
                spawnedForCurrentFall = false;
                platformSpawnAtTime = -1f;
                spawnedSkull = null;
            }

            return;
        }

        if (spawnedForCurrentFall)
            return;

        if (platformSpawnAtTime < 0f)
            platformSpawnAtTime = Time.time + platformSpawnDelaySeconds;

        if (Time.time < platformSpawnAtTime)
            return;

        SpawnShameSetup();
        spawnedForCurrentFall = true;
    }

    private void SpawnShameSetup()
    {
        if (platformPrefab == null)
            return;

        float floorY = transform.position.y;
        Vector3 timPosition = tim.transform.position;
        Vector3 platformPosition = new Vector3(timPosition.x, floorY, timPosition.z);

        GameObject platformInstance = Instantiate(platformPrefab, platformPosition, Quaternion.identity);
        Transform platformRoot = platformInstance.transform;

        GameObject skullPrefab = PickRandomSkullPrefab();
        if (skullPrefab == null)
            return;

        if (!TryPickRandomSkullSpawnPoint(platformRoot, out Vector3 skullPosition))
            return;

        GameObject skullInstance = Instantiate(skullPrefab, skullPosition, Quaternion.identity);
        spawnedSkull = skullInstance.transform;
    }

    private void LateUpdate()
    {
        PointSkullAtFollowTarget();
    }

    private void PointSkullAtFollowTarget()
    {
        if (!Application.isPlaying || spawnedSkull == null || tim == null || !spawnedForCurrentFall)
            return;

        Transform followTarget = tim.CameraFollowTarget;
        if (followTarget == null)
            return;

        spawnedSkull.rotation = Quaternion.LookRotation(
            followTarget.position - spawnedSkull.position,
            Vector3.up);
    }

    private bool TryPickRandomSkullSpawnPoint(Transform platformRoot, out Vector3 skullWorldPosition)
    {
        skullWorldPosition = default;
        if (platformRoot == null)
            return false;

        skullSpawnPointBuffer.Clear();
        foreach (Transform child in platformRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == platformRoot)
                continue;

            if (child.name.StartsWith(SkullSpawnPointNamePrefix))
                skullSpawnPointBuffer.Add(child);
        }

        if (skullSpawnPointBuffer.Count == 0)
            return false;

        Transform spawnPoint = skullSpawnPointBuffer[Random.Range(0, skullSpawnPointBuffer.Count)];
        skullWorldPosition = spawnPoint.position;
        return true;
    }

    private GameObject PickRandomSkullPrefab()
    {
        int index = Random.Range(0, SkullPrefabSlotCount);
        return GetSkullPrefabAt(index);
    }

    private GameObject GetSkullPrefabAt(int index)
    {
        switch (index)
        {
            case 0: return skullPrefab0;
            case 1: return skullPrefab1;
            case 2: return skullPrefab2;
            case 3: return skullPrefab3;
            case 4: return skullPrefab4;
            case 5: return skullPrefab5;
            default: return null;
        }
    }
}
