using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public bool findPlayerAutomatically = true;
    
    [Header("Follow Settings")]
    public Vector3 offset = new Vector3(0, 5, -8);
    public float followSpeed = 5f;
    public float rotationSpeed = 2f;
    
    [Header("Camera Constraints")]
    public bool constrainY = true;
    public float fixedYPosition = 10f;
    public bool smoothDamping = true;
    public float dampingTime = 0.3f;
    
    [Header("Look At Settings")]
    public bool lookAtTarget = true;
    public Vector3 lookAtOffset = Vector3.up;
    
    [Header("Manual Control")]
    public Key clockwiseKey = Key.X;
    public Key counterClockwiseKey = Key.Z;
    public Key resetKey = Key.R;
    
    private Vector3 velocity = Vector3.zero;
    private Camera cameraComponent;
    
    // Preset camera angles (North, East, South, West)
    private Vector3[] presetOffsets = new Vector3[]
    {
        new Vector3(0, 5, -8),    // North view (default)
        new Vector3(8, 5, 0),     // East view  
        new Vector3(0, 5, 8),     // South view
        new Vector3(-8, 5, 0)     // West view
    };
    
    private int currentAngleIndex = 0;
    
    private void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        // Set initial offset to first preset (North view)
        offset = presetOffsets[0];
    }
    
    private void Start()
    {
        if (findPlayerAutomatically && target == null)
        {
            FindPlayerTarget();
        }
        
        if (target == null)
        {
            Debug.LogWarning("CameraFollow: No target found! Please assign a target or ensure a CharacterMovement exists in the scene.");
            enabled = false;
        }
    }
    
    private void FindPlayerTarget()
    {
        CharacterMovement player = FindFirstObjectByType<CharacterMovement>();
        if (player != null)
        {
            target = player.transform;
            Debug.Log("CameraFollow: Automatically found player target.");
        }
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        HandleInput();
        UpdateCameraPosition();
        
        if (lookAtTarget)
        {
            UpdateCameraRotation();
        }
    }
    
    private void UpdateCameraPosition()
    {
        Vector3 targetPosition = CalculateTargetPosition();
        
        if (smoothDamping)
        {
            transform.position = Vector3.SmoothDamp(
                transform.position, 
                targetPosition, 
                ref velocity, 
                dampingTime
            );
        }
        else
        {
            transform.position = Vector3.Lerp(
                transform.position, 
                targetPosition, 
                followSpeed * Time.deltaTime
            );
        }
    }
    
    private void HandleInput()
    {
        // Only allow camera rotation when Tim is stationary
        bool isTimStationary = IsTimStationary();
        
        // Check for reset key (always works)
        if (Keyboard.current != null && Keyboard.current[resetKey].wasPressedThisFrame)
        {
            ResetToDefault();
        }
        
        // Check for clockwise rotation (X key) - only when stationary
        if (isTimStationary && Keyboard.current != null && Keyboard.current[clockwiseKey].wasPressedThisFrame)
        {
            CycleClockwise();
        }
        
        // Check for counterclockwise rotation (Z key) - only when stationary
        if (isTimStationary && Keyboard.current != null && Keyboard.current[counterClockwiseKey].wasPressedThisFrame)
        {
            CycleCounterClockwise();
        }
    }
    
    private bool IsTimStationary()
    {
        CharacterMovement tim = FindFirstObjectByType<CharacterMovement>();
        if (tim == null) return true; // Default to allowing rotation if no Tim found
        
        // Use the public property to check if Tim is stationary
        return tim.IsStationary;
    }
    
    private void CycleClockwise()
    {
        currentAngleIndex = (currentAngleIndex + 1) % presetOffsets.Length;
        offset = presetOffsets[currentAngleIndex];
    }
    
    private void CycleCounterClockwise()
    {
        currentAngleIndex = (currentAngleIndex - 1 + presetOffsets.Length) % presetOffsets.Length;
        offset = presetOffsets[currentAngleIndex];
    }
    
    private void ResetToDefault()
    {
        currentAngleIndex = 0;
        offset = presetOffsets[0]; // North view
    }
    
    private Vector3 CalculateTargetPosition()
    {
        Vector3 targetPosition = target.position + offset;
        
        if (constrainY)
        {
            targetPosition.y = fixedYPosition;
        }
        
        return targetPosition;
    }
    
    private void UpdateCameraRotation()
    {
        Vector3 lookAtPosition = target.position + lookAtOffset;
        Vector3 direction = (lookAtPosition - transform.position).normalized;
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                rotationSpeed * Time.deltaTime
            );
        }
    }
    
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
    
    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }
    
    public void SetFollowSpeed(float newSpeed)
    {
        followSpeed = Mathf.Max(0f, newSpeed);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;
        
        // Draw connection to target
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, target.position);
        
        // Draw target position
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(target.position, 0.5f);
        
        // Draw offset visualization
        Vector3 targetPos = CalculateTargetPosition();
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(targetPos, 0.3f);
        
        // Draw look at position
        if (lookAtTarget)
        {
            Vector3 lookPos = target.position + lookAtOffset;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(lookPos, 0.2f);
            Gizmos.DrawLine(transform.position, lookPos);
        }
    }
}