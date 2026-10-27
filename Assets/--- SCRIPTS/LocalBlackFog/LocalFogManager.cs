using System.Collections.Generic;
using UnityEngine;

public class LocalFogManager : MonoBehaviour
{
    [SerializeField] private List<LocalFogSector> sectors = new();

    private readonly HashSet<LocalFogSector> activeSectors = new();
    private readonly HashSet<Renderer> showRenderers = new();
    private readonly HashSet<Light> showLights = new();
    private CharacterMovement tim;

    private void Awake()
    {
        for (int i = 0; i < sectors.Count; i++)
        {
            if (sectors[i] != null)
                sectors[i].Initialize(this);
        }
    }

    private void Start()
    {
        tim = Object.FindFirstObjectByType<CharacterMovement>();

        for (int i = 0; i < sectors.Count; i++)
        {
            if (sectors[i] != null)
                sectors[i].CollectContent();
        }

        activeSectors.Clear();

        if (tim != null)
        {
            for (int i = 0; i < sectors.Count; i++)
            {
                LocalFogSector sector = sectors[i];
                if (sector != null && sector.ContainsWorldPoint(tim.transform.position))
                    activeSectors.Add(sector);
            }
        }

        ApplyVisibility();
    }

    public void NotifyTimEntered(LocalFogSector sector)
    {
        if (sector == null || !sectors.Contains(sector))
            return;

        if (!activeSectors.Add(sector))
            return;

        ApplyVisibility();
    }

    public void NotifyTimExited(LocalFogSector sector)
    {
        if (sector == null || !activeSectors.Remove(sector))
            return;

        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        showRenderers.Clear();
        showLights.Clear();

        foreach (LocalFogSector active in activeSectors)
        {
            if (active == null)
                continue;

            IReadOnlyList<Renderer> sectorRenderers = active.ContentRenderers;
            for (int i = 0; i < sectorRenderers.Count; i++)
            {
                if (sectorRenderers[i] != null)
                    showRenderers.Add(sectorRenderers[i]);
            }

            IReadOnlyList<Light> sectorLights = active.ContentLights;
            for (int i = 0; i < sectorLights.Count; i++)
            {
                if (sectorLights[i] != null)
                    showLights.Add(sectorLights[i]);
            }
        }

        var onNames = new List<string>();
        var offNames = new List<string>();

        for (int i = 0; i < sectors.Count; i++)
        {
            LocalFogSector sector = sectors[i];
            if (sector == null)
                continue;

            bool active = activeSectors.Contains(sector);
            if (active)
                onNames.Add(sector.name);
            else
                offNames.Add(sector.name);

            IReadOnlyList<Renderer> sectorRenderers = sector.ContentRenderers;
            for (int r = 0; r < sectorRenderers.Count; r++)
            {
                Renderer renderer = sectorRenderers[r];
                if (renderer != null)
                    renderer.enabled = showRenderers.Contains(renderer);
            }

            IReadOnlyList<Light> sectorLights = sector.ContentLights;
            for (int l = 0; l < sectorLights.Count; l++)
            {
                Light light = sectorLights[l];
                if (light != null)
                    light.enabled = showLights.Contains(light);
            }
        }

        Debug.Log(
            $"[LocalFog] ON: {(onNames.Count > 0 ? string.Join(", ", onNames) : "(none)")} | OFF: {(offNames.Count > 0 ? string.Join(", ", offNames) : "(none)")}",
            this);
    }
}
