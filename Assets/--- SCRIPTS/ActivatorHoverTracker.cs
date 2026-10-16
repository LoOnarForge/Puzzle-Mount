using UnityEngine;
using UnityEngine.InputSystem;

public class ActivatorHoverTracker : MonoBehaviour
{
    public static Transform HoveredActivator { get; private set; }

    private static ActivatorHoverTracker instance;

    private TimCubeController timCubeController;

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        var trackerObject = new GameObject(nameof(ActivatorHoverTracker));
        trackerObject.AddComponent<ActivatorHoverTracker>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        timCubeController = FindAnyObjectByType<TimCubeController>();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            HoveredActivator = null;
        }
    }

    private void Update()
    {
        HoveredActivator = FindHoveredActivator();
    }

    private Transform FindHoveredActivator()
    {
        if (timCubeController == null || Mouse.current == null || Camera.main == null)
            return null;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit firstHit, 100f, timCubeController.interactionLayer))
            return TryActivatorFromRaycastAll(ray);

        if (TryGetActivator(firstHit.collider, firstHit.point, out Transform activator))
            return activator;

        return TryActivatorFromRaycastAll(ray);
    }

    private Transform TryActivatorFromRaycastAll(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, timCubeController.interactionLayer, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryGetActivator(hits[i].collider, hits[i].point, out Transform activator))
                return activator;
        }

        return null;
    }

    private bool TryGetActivator(Collider collider, Vector3 hitPoint, out Transform activator)
    {
        activator = ResolveActivatorRoot(collider);
        if (activator == null)
            return false;

        return timCubeController.CanInteractWithActivatorAtPoint(activator, hitPoint);
    }

    private static Transform ResolveActivatorRoot(Collider collider)
    {
        if (collider == null)
            return null;

        Lever lever = collider.GetComponentInParent<Lever>();
        if (lever != null)
            return lever.transform;

        ButtonDevice button = collider.GetComponentInParent<ButtonDevice>();
        if (button != null)
            return button.transform;

        PressurePlate plate = collider.GetComponentInParent<PressurePlate>();
        if (plate != null)
            return plate.transform;

        return null;
    }
}
