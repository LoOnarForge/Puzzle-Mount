using UnityEngine;
using UnityEngine.InputSystem;

/// Handles Leya's 6DOF flight with directional speed balancing and physical collision.
/// Uses Rigidbody linearVelocity to ensure the Sphere Collider stops at obstacles.
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class LeyasCamera : MonoBehaviour
{
    [Header("TARGET")]
    public Transform timTransform;

    [Header("FLIGHT SETTINGS")]
    public float baseMoveSpeed = 8f;
    public float lookSensitivity = 0.15f;
    public float maxRadius = 15f;
    public LayerMask collisionLayers;
    
    [Header("BIRD FEEL (BOBBING)")]
    public float bobFrequency = 2f;
    public float bobAmplitude = 1f; // 1 in inspector = 0.001 in code
    public float rotationNoiseStrength = 0.5f;

    [Header("UI")]
    public Sprite inspectionVignette;

    private Rigidbody rb;
    private float yaw;
    private float pitch;
    private bool isActive = false;
    private MenuManager menuManager;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // PHYSICAL SETUP
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearDamping = 10f; 
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        SphereCollider sphere = GetComponent<SphereCollider>();
        sphere.radius = 0.35f; // 0.7m total width

        menuManager = FindFirstObjectByType<MenuManager>(FindObjectsInactive.Include);
        
        gameObject.SetActive(false);
    }

    public void Activate(Vector3 startPosition, Quaternion startRotation)
    {
        isActive = true;
        gameObject.SetActive(true);
        
        transform.position = startPosition;
        transform.rotation = startRotation;
        
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;

        rb.linearVelocity = Vector3.zero;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (menuManager != null) menuManager.SetInspectionModeUI(true);
    }

    public void Deactivate()
    {
        isActive = false;
        gameObject.SetActive(false);
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (menuManager != null) menuManager.SetInspectionModeUI(false);
    }

    private void LateUpdate()
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
        
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);
        
        float noiseX = (Mathf.PerlinNoise(Time.time, 0) - 0.5f) * rotationNoiseStrength;
        float noiseY = (Mathf.PerlinNoise(0, Time.time) - 0.5f) * rotationNoiseStrength;
        
        transform.rotation = targetRotation * Quaternion.Euler(noiseX, noiseY, 0);
    }

    private void HandleMovement()
    {
        Vector3 movement = Vector3.zero;

        // 1. Directional Input & Speeds
        if (Keyboard.current[Key.W].isPressed) movement += transform.forward * (baseMoveSpeed * 1.2f);
        if (Keyboard.current[Key.S].isPressed) movement -= transform.forward * (baseMoveSpeed * 0.4f);
        if (Keyboard.current[Key.A].isPressed) movement -= transform.right * (baseMoveSpeed * 0.8f);
        if (Keyboard.current[Key.D].isPressed) movement += transform.right * (baseMoveSpeed * 0.8f);

        if (Keyboard.current[Key.Q].isPressed) movement += Vector3.down * (baseMoveSpeed * 0.5f);
        if (Keyboard.current[Key.E].isPressed || Keyboard.current[Key.Space].isPressed) movement += Vector3.up * (baseMoveSpeed * 0.5f);

        // 2. Bobbing (Derivative)
        float scaledAmp = bobAmplitude * 0.001f;
        float bobVelocityY = Mathf.Cos(Time.time * bobFrequency) * bobFrequency * scaledAmp;
        movement.y += bobVelocityY;

        // 3. Range Clamping
        if (timTransform != null)
        {
            Vector3 offset = transform.position - timTransform.position;
            if (offset.magnitude > maxRadius)
            {
                if (Vector3.Dot(movement, offset.normalized) > 0)
                {
                    movement = Vector3.ProjectOnPlane(movement, offset.normalized);
                }
            }
        }

        // 4. Set Physical Velocity
        rb.linearVelocity = movement;
    }
}
