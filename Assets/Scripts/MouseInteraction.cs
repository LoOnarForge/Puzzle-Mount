using UnityEngine;
using UnityEngine.InputSystem;

public class MouseInteraction : MonoBehaviour
{
    [SerializeField] private LayerMask clickLayerMask;
    [SerializeField] private float maxClickDistance = 3f;

    private Transform timTransform;

    private void Awake()
    {
        TimCubeController timCubeController = FindAnyObjectByType<TimCubeController>();
        Debug.Assert(timCubeController != null, $"{nameof(MouseInteraction)} on {name} requires a {nameof(TimCubeController)} in the scene.", this);

        if (timCubeController != null)
            timTransform = timCubeController.transform;
    }

    private void Update()
    {
        if (Mouse.current == null || Camera.main == null || timTransform == null)
            return;

        if (!Mouse.current.leftButton.wasPressedThisFrame)
            return;

        TryHandleMouseClick();
    }

    private void TryHandleMouseClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, clickLayerMask))
            return;

        if (hit.collider.GetComponentInParent<RunodeMovement>() != null)
            return;

        Lever lever = hit.collider.GetComponentInParent<Lever>();
        if (lever != null)
        {
            if (IsInClickRange(lever.transform.position))
                lever.MouseClickDetected();
            return;
        }
    }

    private bool IsInClickRange(Vector3 targetPosition)
    {
        float distance = Vector3.ProjectOnPlane(targetPosition - timTransform.position, Vector3.up).magnitude;
        return distance <= maxClickDistance;
    }
}
