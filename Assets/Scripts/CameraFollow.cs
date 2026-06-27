using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public bool findPlayerAutomatically = true;

    [Header("UNIFIED CAMERA SETTINGS")]
    [Tooltip("Adjusts speed, damping, and mouse sensitivity together (0 = Lazy, 1 = Snappy)")]
    [Range(0.01f, 1f)] public float cameraResponsiveness = 0.5f;
    
    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0, 5, -8);
    public bool constrainY = true;
    public float fixedYPosition = 10f;
    
    [Header("Look At Settings")]
    public bool lookAtTarget = true;
    public Vector3 lookAtOffset = Vector3.up;
    
    [Header("Manual Control")]
    public Key clockwiseKey = Key.E;
    public Key counterClockwiseKey = Key.Q;
    public Key resetKey = Key.R;

    [Header("Mouse Interaction")]
    public float swipeThreshold = 30f;
    
    private Vector3 velocity = Vector3.zero;
    private Camera cameraComponent;
    private CharacterMovement tim;
    
    // Preset camera angles (North, East, South, West)
    private Vector3[] presetOffsets = new Vector3[]
    {
        new Vector3(0, 5, -8),    // North view (default)
        new Vector3(8, 5, 0),     // East view  
        new Vector3(0, 5, 8),     // South view
        new Vector3(-8, 5, 0)     // West view
    };
    
    private int currentAngleIndex = 0;
    private bool isInViewMode = false;
    
    private Vector2 mouseStartPos;
    private bool mouseActive = false;
    private float targetOrbitYaw = 0f;
    private float currentOrbitYaw = 0f;
    private float orbitVelocity = 0f;
    private float lastRotationTime = 0f;
    private const float ROTATION_COOLDOWN = 0.1f;

    // Unified helper properties based on cameraResponsiveness
    private float DampingTime => Mathf.Lerp(0.35f, 0.02f, cameraResponsiveness);
    private float OrbitSensitivity => Mathf.Lerp(0.05f, 0.6f, cameraResponsiveness);
    private float FollowSpeed => Mathf.Lerp(5f, 25f, cameraResponsiveness);
    private float RotationLerpSpeed => Mathf.Lerp(2f, 20f, cameraResponsiveness);

    public bool IsInViewMode => isInViewMode;
    public int CurrentAngleIndex => currentAngleIndex;
    
    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        offset = presetOffsets[0];
    }
    
    private void Start()
    {
        tim = FindFirstObjectByType<CharacterMovement>();
        if (findPlayerAutomatically && target == null) FindPlayerTarget();
    }
    
    private void FindPlayerTarget()
    {
        if (tim != null) target = tim.transform;
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        HandleInput();
        
        if (isInViewMode)
        {
            UpdateViewMode();
        }
        else
        {
            UpdateCameraPosition();
        }
        
        if (lookAtTarget)
        {
            UpdateCameraRotation();
        }
    }
    
    private void HandleInput()
    {
        if (Keyboard.current != null && Keyboard.current[resetKey].wasPressedThisFrame)
        {
            ResetToDefault();
        }

        if (Mouse.current != null)
        {
            if (Mouse.current.middleButton.wasPressedThisFrame)
            {
                mouseStartPos = Mouse.current.position.ReadValue();
                mouseActive = true;
            }

            if (mouseActive && Mouse.current.middleButton.isPressed)
            {
                Vector2 currentMousePos = Mouse.current.position.ReadValue();
                float deltaX = currentMousePos.x - mouseStartPos.x;

                if (!isInViewMode && Time.time - lastRotationTime > ROTATION_COOLDOWN)
                {
                    if (Mathf.Abs(deltaX) > swipeThreshold)
                    {
                        if (deltaX > 0) CycleClockwise();
                        else CycleCounterClockwise();
                        mouseStartPos = currentMousePos;
                    }
                }
            }

            if (Keyboard.current != null && Keyboard.current.altKey.isPressed && Mouse.current.middleButton.isPressed)
            {
                if (!isInViewMode) EnterViewMode();
            }
            else if (isInViewMode)
            {
                ExitViewMode();
            }

            if (Mouse.current.middleButton.wasReleasedThisFrame)
            {
                mouseActive = false;
            }
        }

        if (!isInViewMode && Time.time - lastRotationTime > ROTATION_COOLDOWN)
        {
            if (Keyboard.current != null && Keyboard.current[clockwiseKey].wasPressedThisFrame)
            {
                CycleClockwise();
            }
            else if (Keyboard.current != null && Keyboard.current[counterClockwiseKey].wasPressedThisFrame)
            {
                CycleCounterClockwise();
            }
        }
    }

    private void EnterViewMode()
    {
        isInViewMode = true;
        targetOrbitYaw = currentAngleIndex * 90f;
        currentOrbitYaw = targetOrbitYaw;
        if (tim != null) tim.SetMovementEnabled(false);
    }

    private void ExitViewMode()
    {
        isInViewMode = false;
        float normalizedYaw = (currentOrbitYaw % 360 + 360) % 360;
        currentAngleIndex = Mathf.RoundToInt(normalizedYaw / 90f) % 4;
        offset = presetOffsets[currentAngleIndex];
        lastRotationTime = Time.time;
        if (tim != null) tim.SetMovementEnabled(true);
    }

    private void UpdateViewMode()
    {
        if (Mouse.current != null)
        {
            float deltaX = Mouse.current.delta.x.ReadValue();
            targetOrbitYaw += deltaX * OrbitSensitivity;
        }

        currentOrbitYaw = Mathf.SmoothDampAngle(currentOrbitYaw, targetOrbitYaw, ref orbitVelocity, 0.08f);
        float rad = currentOrbitYaw * Mathf.Deg2Rad;
        
        float dist = new Vector2(presetOffsets[0].x, presetOffsets[0].z).magnitude;
        Vector3 orbitOffset = new Vector3(Mathf.Sin(rad) * dist, presetOffsets[0].y, Mathf.Cos(rad) * dist);
        
        transform.position = Vector3.Lerp(transform.position, target.position + orbitOffset, FollowSpeed * Time.deltaTime);
    }

    private void UpdateCameraPosition()
    {
        Vector3 targetPosition = CalculateTargetPosition();
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, DampingTime);
    }

    private void CycleClockwise()
    {
        currentAngleIndex = (currentAngleIndex + 1) % presetOffsets.Length;
        offset = presetOffsets[currentAngleIndex];
        lastRotationTime = Time.time;
    }
    
    private void CycleCounterClockwise()
    {
        currentAngleIndex = (currentAngleIndex - 1 + presetOffsets.Length) % presetOffsets.Length;
        offset = presetOffsets[currentAngleIndex];
        lastRotationTime = Time.time;
    }
    
    private void ResetToDefault()
    {
        currentAngleIndex = 0;
        offset = presetOffsets[0];
        lastRotationTime = Time.time;
    }
    
    private Vector3 CalculateTargetPosition()
    {
        Vector3 targetPos = target.position + offset;
        if (constrainY) targetPos.y = fixedYPosition;
        return targetPos;
    }
    
    private void UpdateCameraRotation()
    {
        Vector3 lookAtPos = target.position + lookAtOffset;
        Vector3 direction = (lookAtPos - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, RotationLerpSpeed * Time.deltaTime);
        }
    }
}