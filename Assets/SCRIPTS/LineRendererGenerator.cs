using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ActivatorConnectionLinesHover))]
public class LineRendererGenerator : MonoBehaviour
{
    [SerializeField] private GameObject lineRendererPrefab;
    [SerializeField] private GameObject cometPrefab;
    [Min(0.01f)] [SerializeField] private float cometSpawnInterval = 2f;
    [Min(0f)] [SerializeField] private float cometSpeed = 2.5f;
    [SerializeField] private List<LineRendererGeneratorLinkEntry> links = new List<LineRendererGeneratorLinkEntry>();

    public GameObject LineRendererPrefab => lineRendererPrefab;
    public GameObject CometPrefab => cometPrefab;
    public float CometSpawnInterval => cometSpawnInterval;
    public float CometSpeed => cometSpeed;
    public IReadOnlyList<LineRendererGeneratorLinkEntry> Links => links;

    public List<LineRendererGeneratorLinkEntry> LinksEditable => links;
}

[Serializable]
public class LineRendererGeneratorLinkEntry
{
    public string deviceName;
    public GameObject device;
    [Range(0, 10)] public int zigzagAmount = 0;
}
