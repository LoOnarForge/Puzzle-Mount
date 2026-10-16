using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ActivatorConnectionLinesHover))]
public class LineRendererGenerator : MonoBehaviour
{
    [SerializeField] private GameObject lineRendererPrefab;
    [SerializeField] private Material editorPreviewMaterial;
    [SerializeField] private Material beamLineMaterial;
    [SerializeField] private GameObject cometPrefab;
    [Min(0f)] [SerializeField] private float beamSpeedMin = 2f;
    [Min(0f)] [SerializeField] private float beamSpeedMax = 3f;
    [SerializeField] private bool avoidObstaclesWhenGenerating = true;
    [SerializeField] private LayerMask pathObstacleLayers;
    [Min(0.01f)] [SerializeField] private float pathClearanceRadius = 0.25f;
    [SerializeField] private List<LineRendererGeneratorLinkEntry> links = new List<LineRendererGeneratorLinkEntry>();

    public GameObject LineRendererPrefab => lineRendererPrefab;
    public Material EditorPreviewMaterial => editorPreviewMaterial;
    public Material BeamLineMaterial => beamLineMaterial;
    public GameObject CometPrefab => cometPrefab;
    public float BeamSpeedMin => beamSpeedMin;
    public float BeamSpeedMax => beamSpeedMax;
    public bool AvoidObstaclesWhenGenerating => avoidObstaclesWhenGenerating;
    public LayerMask PathObstacleLayers => pathObstacleLayers;
    public float PathClearanceRadius => pathClearanceRadius;
    public IReadOnlyList<LineRendererGeneratorLinkEntry> Links => links;

    public List<LineRendererGeneratorLinkEntry> LinksEditable => links;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ConfigureAllLinkImpulses();
    }

    // Pushes generator settings (including beam material) to all link lines.
    public void ConfigureAllLinkImpulses()
    {
        ActivatorConnectionLineImpulse[] impulses = GetComponentsInChildren<ActivatorConnectionLineImpulse>(true);
        for (int i = 0; i < impulses.Length; i++)
        {
            ActivatorConnectionLineImpulse impulse = impulses[i];
            if (impulse == null)
                continue;

            impulse.ConfigureFromGenerator(CometPrefab, beamLineMaterial, beamSpeedMin, beamSpeedMax);
        }
    }
}

[Serializable]
public class LineRendererGeneratorLinkEntry
{
    public string deviceName;
    public GameObject device;
    [Range(0, 10)] public int zigzagAmount = 0;
    public int routeVariantIndex = 0;
}
