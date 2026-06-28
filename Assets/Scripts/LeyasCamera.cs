using UnityEngine;
using UnityEngine.InputSystem;

/// Handles Leya's flight using CharacterController for non-intrusive collision.
[RequireComponent(typeof(CharacterController))]
public class LeyasCamera : MonoBehaviour
{
    [Header("TARGET")]
    public Transform timTransform;

    [Header("FLIGHT SETTINGS")]
    public float baseMoveSpeed = 8f;
    public float lookSensitivity = 0.15f;
    public float maxRadius = 15f;

    [Header("UI")]
    public Sprite inspectionVignette;

    private CharacterController controller;
    private float yaw;
    private float pitch;
    private bool isActive = false;
    private MenuManager menuManager;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        menuManager = FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
        gameObject.SetActive(false);
    }

    // Activates Leya at the specified position and rotation.
    public void Activate(Vector3 startPosition, Quaternion startRotation)
    {
        isActive = true;
        gameObject.SetActive(true);
        
        transform.position = startPosition;
        transform.rotation = startRotation;
        
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (menuManager != null) menuManager.SetInspectionModeUI(true);
    }

    // Deactivates Leya and restores cursor state.
    public void Deactivate()
    {
        isActive = false;
        gameObject.SetActive(false);
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (menuManager != null) menuManager.SetInspectionModeUI(false);
    }

    private void Update()
    {
        if (!isActive || Keyboard.current == null || Mouse.current == null) return;

        HandleRotation();
        HandleMovement();
    }

    private void HandleRotation()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        yaw += mouseDelta.x * lookSensitivity;
        pitch -= mouseDelta.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMovement()
    {
        Vector3 direction = Vector3.zero;

        if (Keyboard.current[Key.W].isPressed) direction += transform.forward;
        if (Keyboard.current[Key.S].isPressed) direction -= transform.forward;
        if (Keyboard.current[Key.A].isPressed) direction -= transform.right;
        if (Keyboard.current[Key.D].isPressed) direction += transform.right;

        // Vertical movement with Q and E
        if (Keyboard.current[Key.Q].isPressed) direction += Vector3.down;
        if (Keyboard.current[Key.E].isPressed) direction += Vector3.up;

        if (direction.sqrMagnitude > 0.001f)
        {
            Vector3 movement = direction.normalized * baseMoveSpeed * Time.deltaTime;

            if (timTransform != null)
            {
                Vector3 currentOffset = transform.position - timTransform.position;
                if (currentOffset.magnitude >= maxRadius)
                {
                    Vector3 normal = currentOffset.normalized;
                    if (Vector3.Dot(movement, normal) > 0)
                    {
                        movement = Vector3.ProjectOnPlane(movement, normal);
                    }
                }
            }

            controller.Move(movement);
        }
    }
}
