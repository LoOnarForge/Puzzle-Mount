using UnityEngine;
using UnityEngine.InputSystem;

/// Handles Leya's flight using CharacterController for aerial inspection.
/// Features smoothed acceleration and deceleration and organic Lens Breathing for a living feel.
[RequireComponent(typeof(CharacterController))]
public class LeyasCamera : MonoBehaviour
{
    [Header("TARGET")]
    public Transform timTransform;

    [Header("FLIGHT SETTINGS")]
    public float baseMoveSpeed = 12f;
    public float accelerationTime = 0.5f;
    public float decelerationTime = 0.5f;
    public float lookSensitivity = 0.15f;
    public float maxRadius = 15f;
    public float maxHeightOffset = 10f;
    public float baseFov = 80f;

    [Header("BIRD FEEL - BREATHING")]
    [Range(0f, 2f)] public float idleDelay = 0.5f;
    [Range(0f, 5f)] public float rampUpTime = 2.0f;
    [Range(0f, 5f)] public float fovPulseAmplitude = 1.5f;
    [Range(0f, 10f)] public float fovPulseFrequency = 1.2f;

    private CharacterController controller;
    private Camera cam;
    private float yaw;
    private float pitch;
    private bool isActive = false;
    private MenuManager menuManager;
    private Vector3 currentVelocity;

    private float idleTimer;
    private float internalTime;
    private float currentRamp;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = GetComponent<Camera>();
        menuManager = FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
        
        if (cam != null) cam.fieldOfView = baseFov;

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
        
        ResetIdleState();

        if (menuManager != null) menuManager.SetInspectionModeUI(true);
    }

    // Deactivates Leya and restores cursor state.
    public void Deactivate()
    {
        isActive = false;
        gameObject.SetActive(false);
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

        if (mouseDelta.sqrMagnitude > 0.001f)
        {
            idleTimer = 0f;
        }

        yaw += mouseDelta.x * lookSensitivity;
        pitch -= mouseDelta.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMovement()
    {
        Vector3 targetDir = Vector3.zero;
        bool hasInput = false;

        Vector3 forward = transform.forward;
        forward.y = 0;
        if (forward.sqrMagnitude > 0.001f) forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0;
        if (right.sqrMagnitude > 0.001f) right.Normalize();

        if (Keyboard.current[Key.W].isPressed) { targetDir += forward; hasInput = true; }
        if (Keyboard.current[Key.S].isPressed) { targetDir -= forward; hasInput = true; }
        if (Keyboard.current[Key.A].isPressed) { targetDir -= right; hasInput = true; }
        if (Keyboard.current[Key.D].isPressed) { targetDir += right; hasInput = true; }
        if (Keyboard.current[Key.Q].isPressed) { targetDir += Vector3.down; hasInput = true; }
        if (Keyboard.current[Key.E].isPressed) { targetDir += Vector3.up; hasInput = true; }

        if (hasInput)
        {
            idleTimer = 0f;
        }
        else
        {
            idleTimer += Time.deltaTime;
        }

        Vector3 targetVelocity = targetDir.normalized * baseMoveSpeed;

        if (targetDir.sqrMagnitude > 0.001f)
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, (baseMoveSpeed / accelerationTime) * Time.deltaTime);
        }
        else
        {
            currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, (baseMoveSpeed / decelerationTime) * Time.deltaTime);
        }

        Vector3 movement = currentVelocity * Time.deltaTime;

        // Apply Organic Idle Effects
        if (idleTimer > idleDelay)
        {
            internalTime += Time.deltaTime;
            currentRamp = Mathf.MoveTowards(currentRamp, 1.0f, Time.deltaTime / rampUpTime);

            // Lens Breathing (FOV Pulse)
            if (cam != null)
            {
                float fovOffset = Mathf.Sin(internalTime * fovPulseFrequency) * fovPulseAmplitude * currentRamp;
                cam.fieldOfView = baseFov + fovOffset;
            }
        }
        else
        {
            currentRamp = 0f;
            if (cam != null) cam.fieldOfView = baseFov;
        }

        if (movement.sqrMagnitude > 0.000001f)
        {
            if (timTransform != null)
            {
                ApplyClamping(ref movement);
            }
            controller.Move(movement);
        }
    }

    private void ApplyClamping(ref Vector3 movement)
    {
        // Horizontal clamping
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
                
                Vector3 horizontalVel = new Vector3(currentVelocity.x, 0, currentVelocity.z);
                Vector3 projectedVel = Vector3.ProjectOnPlane(horizontalVel, normal);
                currentVelocity.x = projectedVel.x;
                currentVelocity.z = projectedVel.z;
            }
        }

        // Vertical clamping
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

    private void ResetIdleState()
    {
        idleTimer = 0f;
        internalTime = 0f;
        currentRamp = 0f;
        if (cam != null) cam.fieldOfView = baseFov;
    }
}
