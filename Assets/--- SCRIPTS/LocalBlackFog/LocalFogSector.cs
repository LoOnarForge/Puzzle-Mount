using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class LocalFogSector : MonoBehaviour
{
    [SerializeField] private LayerMask contentLayers = ~0;

    private BoxCollider zoneCollider;
    private LocalFogManager manager;
    private Renderer[] renderers = System.Array.Empty<Renderer>();
    private Light[] lights = System.Array.Empty<Light>();

    public IReadOnlyList<Renderer> ContentRenderers => renderers;
    public IReadOnlyList<Light> ContentLights => lights;

    public void Initialize(LocalFogManager owner)
    {
        manager = owner;
    }

    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        if (zoneCollider == null)
            return false;

        Vector3 localPoint = zoneCollider.transform.InverseTransformPoint(worldPoint);
        Vector3 delta = localPoint - zoneCollider.center;
        Vector3 halfExtents = zoneCollider.size * 0.5f;

        return Mathf.Abs(delta.x) <= halfExtents.x
            && Mathf.Abs(delta.y) <= halfExtents.y
            && Mathf.Abs(delta.z) <= halfExtents.z;
    }

    public void CollectContent()
    {
        var rendererSet = new HashSet<Renderer>();
        var lightSet = new HashSet<Light>();

        Bounds sectorBounds = zoneCollider.bounds;
        int layerMask = contentLayers.value;

        Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer renderer = allRenderers[i];
            if (renderer == null)
                continue;

            if (((1 << renderer.gameObject.layer) & layerMask) == 0)
                continue;

            if (renderer.transform.IsChildOf(transform))
                continue;

            if (renderer.GetComponentInParent<CharacterMovement>() != null)
                continue;

            if (renderer.GetComponent<Camera>() != null)
                continue;

            if (!sectorBounds.Intersects(renderer.bounds))
                continue;

            rendererSet.Add(renderer);
        }

        Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        for (int i = 0; i < allLights.Length; i++)
        {
            Light light = allLights[i];
            if (light == null)
                continue;

            if (((1 << light.gameObject.layer) & layerMask) == 0)
                continue;

            if (light.transform.IsChildOf(transform))
                continue;

            if (light.GetComponentInParent<CharacterMovement>() != null)
                continue;

            if (!sectorBounds.Contains(light.transform.position))
                continue;

            lightSet.Add(light);
        }

        renderers = new List<Renderer>(rendererSet).ToArray();
        lights = new List<Light>(lightSet).ToArray();
    }

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider>();
        zoneCollider.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (manager == null || other.GetComponentInParent<CharacterMovement>() == null)
            return;

        manager.NotifyTimEntered(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (manager == null || other.GetComponentInParent<CharacterMovement>() == null)
            return;

        manager.NotifyTimExited(this);
    }
}
