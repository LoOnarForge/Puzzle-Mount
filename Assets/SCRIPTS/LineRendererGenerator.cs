using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ActivatorConnectionLinesHover))]
public class LineRendererGenerator : MonoBehaviour
{
    [SerializeField] private GameObject lineRendererPrefab;
    [SerializeField] private GameObject cometPrefab;
    [Min(0f)] [SerializeField] private float beamSpeedMin = 2f;
    [Min(0f)] [SerializeField] private float beamSpeedMax = 3f;
    [SerializeField] private List<LineRendererGeneratorLinkEntry> links = new List<LineRendererGeneratorLinkEntry>();

    public GameObject LineRendererPrefab => lineRendererPrefab;
    public GameObject CometPrefab => cometPrefab;
    public float BeamSpeedMin => beamSpeedMin;
    public float BeamSpeedMax => beamSpeedMax;
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
