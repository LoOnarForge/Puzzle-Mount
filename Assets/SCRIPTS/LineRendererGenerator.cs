using System;
using System.Collections.Generic;
using UnityEngine;

public class LineRendererGenerator : MonoBehaviour
{
    [SerializeField] private GameObject lineRendererPrefab;
    [SerializeField] private List<LineRendererGeneratorLinkEntry> links = new List<LineRendererGeneratorLinkEntry>();

    public GameObject LineRendererPrefab => lineRendererPrefab;
    public IReadOnlyList<LineRendererGeneratorLinkEntry> Links => links;

    internal List<LineRendererGeneratorLinkEntry> LinksEditable => links;
}

[Serializable]
public class LineRendererGeneratorLinkEntry
{
    public string deviceName;
    public GameObject device;
    [Range(0, 20)] public int pivotCount = 2;
    [Range(0, 10)] public int zigzagAmount = 3;
}
