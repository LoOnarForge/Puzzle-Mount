using UnityEngine;
using UnityEngine.InputSystem;

/// Handles Leya's flight using CharacterController for aerial inspection.
/// Features smoothed acceleration and deceleration for bird-like flight.
[RequireComponent(typeof(CharacterController))]
public class LeyasCamera : MonoBehaviour
{
    [Header("TARGET")]
    public Transform timTransform;

    [Header("FLIGHT SETTINGS")]
    public float baseMoveSpeed = 12f;
    public float acceleration = 5f;
    public float deceleration = 3f;
    public float lookSensitivity = 0.15f;
    public float maxRadius = 15f;
    public float maxHeightOffset = 10f;
    public float fov = 80f;

    [Header("UI")]
    public Sprite inspectionVignette;

    private CharacterController controller;
    private float yaw;
    private float pitch;
    private bool isActive = false;
    private MenuManager menuManager;
    private Vector3 currentVelocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        menuManager = FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
        
        Camera cam = GetComponent<Camera>();
        if (cam != null) cam.fieldOfView = fov;

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
        currentVelocity = Vector3.zero;

        // Cursor control commented out for testing as requested
        // Cursor.lockState = CursorLockMode.Locked;
        // Cursor.visible = false;
        
        if (menuManager != null) menuManager.SetInspectionModeUI(true);
    }

    // Deactivates Leya and restores cursor state.
    public void Deactivate()
    {
        isActive = false;
        gameObject.SetActive(false);
        
        // Cursor control commented out for testing as requested
        // Cursor.lockState = CursorLockMode.None;
        // Cursor.visible = true;
        
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
        Vector3 targetDir = Vector3.zero;

        if (Keyboard.current[Key.W].isPressed) targetDir += transform.forward;
        if (Keyboard.current[Key.S].isPressed) targetDir -= transform.forward;
        if (Keyboard.current[Key.A].isPressed) targetDir -= transform.right;
        if (Keyboard.current[Key.D].isPressed) targetDir += transform.right;
        if (Keyboard.current[Key.Q].isPressed) targetDir += Vector3.down;
        if (Keyboard.current[Key.E].isPressed) targetDir += Vector3.up;

        Vector3 targetVelocity = targetDir.normalized * baseMoveSpeed;

        // Apply bird-like acceleration/deceleration
        float accelRate = (targetDir.sqrMagnitude > 0.001f) ? acceleration : deceleration;
        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, accelRate * Time.deltaTime);

        if (currentVelocity.sqrMagnitude > 0.001f)
        {
            Vector3 movement = currentVelocity * Time.deltaTime;

            if (timTransform != null)
            {
                // Horizontal clamping logic
                Vector3 currentHorizontalOffset = transform.position - timTransform.position;
                currentHorizontalOffset.y = 0;
                
                if (currentHorizontalOffset.magnitude >= maxRadius)
                {
                    Vector3 normal = currentHorizontalOffset.normalized;
                    Vector3 horizontalMove = new Vector3(movement.x, 0, movement.z);
                    if (Vector3.Dot(horizontalMove, normal) > 0)
                    {
                        Vector3 projected = Vector3.ProjectOnPlane(horizontalMove, normal);
                        movement.x = projected.x;
                        movement.z = projected.z;
                        
                        // Kill velocity component pointing out of bounds
                        Vector3 horizontalVel = new Vector3(currentVelocity.x, 0, currentVelocity.z);
                        Vector3 projectedVel = Vector3.ProjectOnPlane(horizontalVel, normal);
                        currentVelocity.x = projectedVel.x;
                        currentVelocity.z = projectedVel.z;
                    }
                }

                // Vertical clamping logic
                float nextY = transform.position.y + movement.y;
                if (nextY > timTransform.position.y + maxHeightOffset && movement.y > 0)
                {
                    movement.y = 0;
                    currentVelocity.y = 0;
                }
                else if (nextY < timTransform.position.y && movement.y < 0)
                {
                    movement.y = 0;
                    currentVelocity.y = 0;
                }
            }

            controller.Move(movement);
        }
    }
}
