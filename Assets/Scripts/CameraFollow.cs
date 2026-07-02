using UnityEngine;
using UnityEngine.InputSystem;

/// Handles Action Mode (cardinal follow) for Tim Jones.
/// Coordinates with LeyasCamera for Inspection Mode.
public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public bool findPlayerAutomatically = true;

    [Header("ACTION MODE SETTINGS")]
    [Tooltip("Adjusts speed and damping (0 = Lazy, 1 = Snappy)")]
    [Range(0.01f, 1f)] public float cameraResponsiveness = 0.5f;
    
    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0, 5, -8);
    public bool constrainY = true;
    public float fixedYPosition = 10f;
    [Tooltip("Offsets the screen position without changing the camera angle")]
    public float screenFramingOffset = 0f;
    
    private Vector3 lookAtOffset = Vector3.up * 2f;
    
    [Header("Manual Control")]
    public Key clockwiseKey = Key.E;
    public Key counterClockwiseKey = Key.Q;
    public Key resetKey = Key.R;
    public Key inspectionToggleKey = Key.Tab;
    public float rotationCooldown = 0.25f;

    [Header("INSPECTION MODE (LEYA)")]
    public LeyasCamera leyaController;
    
    public bool IsInspectionMode => isInspectionMode;
    public int CurrentAngleIndex => currentAngleIndex;

    private Vector3 velocity = Vector3.zero;
    private CharacterMovement tim;
    private Camera actionCamera;
    private float lastRotationTime;
    
    private Vector3[] presetOffsets = new Vector3[]
    {
        new Vector3(0, 7, -10),    // North 
        new Vector3(10, 7, 0),     // East  
        new Vector3(0, 7, 10),     // South 
        new Vector3(-10, 7, 0)     // West  
    };
    
    private int currentAngleIndex = 0;
    private int originalAngleIndex = 0;
    private bool isInspectionMode = false;

    private float DampingTime => Mathf.Lerp(0.35f, 0.02f, cameraResponsiveness);
    private float RotationLerpSpeed => Mathf.Lerp(2f, 20f, cameraResponsiveness);

    private void Awake()
    {
        actionCamera = GetComponent<Camera>();
        offset = presetOffsets[0];
    }
    
    private void Start()
    {
        tim = FindFirstObjectByType<CharacterMovement>();
        if (findPlayerAutomatically && target == null && tim != null) target = tim.transform;
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        HandleInput();
        
        if (!isInspectionMode)
        {
            UpdateActionMode();
        }
    }
    
    private void HandleInput()
    {
        if (Keyboard.current == null) return;

        // Toggle Inspection Mode via Leya
        if (Keyboard.current[inspectionToggleKey].wasPressedThisFrame)
        {
            ToggleInspectionMode();
        }

        if (isInspectionMode) return;

        // Action Mode Controls
        if (Keyboard.current[resetKey].wasPressedThisFrame) ResetToDefault();
        if (Keyboard.current[clockwiseKey].wasPressedThisFrame) CycleClockwise();
        if (Keyboard.current[counterClockwiseKey].wasPressedThisFrame) CycleCounterClockwise();
    }

    private void ToggleInspectionMode()
    {
        if (leyaController == null) return;

        if (!isInspectionMode)
        {
            isInspectionMode = true;
            originalAngleIndex = currentAngleIndex;
            actionCamera.enabled = false;
            
            if (tim != null) tim.SetMovementEnabled(false);
            
            leyaController.Activate(transform.position, transform.rotation);
        }
        else
        {
            isInspectionMode = false;
            
            leyaController.DeactivateWithTransition(transform.position, transform.rotation, () => {
                actionCamera.enabled = true;
                if (tim != null) tim.SetMovementEnabled(true);
            });
        }
    }

    private void UpdateActionMode()
    {
        Vector3 targetPos = target.position + offset;
        if (constrainY) targetPos.y = fixedYPosition;
        
        // Add framing offset relative to the camera's right axis to shift the camera position
        targetPos += transform.right * screenFramingOffset;
        
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, DampingTime);

        // Calculate look target with the same framing offset so the camera doesn't turn back to center Tim
        Vector3 lookAtPos = (target.position + lookAtOffset) + (transform.right * screenFramingOffset);
        Vector3 direction = (lookAtPos - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, RotationLerpSpeed * Time.deltaTime);
        }
    }

    public void CycleClockwise()
    {
        if (Time.unscaledTime < lastRotationTime + rotationCooldown) return;

        currentAngleIndex = (currentAngleIndex + 1) % presetOffsets.Length;
        offset = presetOffsets[currentAngleIndex];
        lastRotationTime = Time.unscaledTime;
    }
    
    public void CycleCounterClockwise()
    {
        if (Time.unscaledTime < lastRotationTime + rotationCooldown) return;

        currentAngleIndex = (currentAngleIndex - 1 + presetOffsets.Length) % presetOffsets.Length;
        offset = presetOffsets[currentAngleIndex];
        lastRotationTime = Time.unscaledTime;
    }
    
    public void ResetToDefault()
    {
        if (Time.unscaledTime < lastRotationTime + rotationCooldown) return;

        currentAngleIndex = 0;
        offset = presetOffsets[0];
        lastRotationTime = Time.unscaledTime;
    }
}
