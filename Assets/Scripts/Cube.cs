using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class Cube : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    
    [Header("Gravity")]
    public float gravityMultiplier = 2f;
    
    private Rigidbody rb;
    private bool isMoving = false;
    private bool isFalling = false;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // CRITICAL: Force kinematic setup to prevent physics interference
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.None;
        
        // Don't set velocity on kinematic rigidbodies - Unity 6 warns against this
    }
    
    private void Start()
    {
        // Force perfect grid alignment on start
        SnapToGrid();
    }
    
    private void Update()
    {
        // Handle gravity manually - only when not moving
        if (!isMoving && !isFalling)
        {
            CheckGravity();
        }
    }
    
    /// <summary>
    /// Force cube to exact integer grid coordinates (X,Z only - Y stays natural)
    /// </summary>
    private void SnapToGrid()
    {
        Vector3 currentPos = transform.position;
        Vector3 gridPos = new Vector3(
            Mathf.Round(currentPos.x),
            currentPos.y, // Keep Y natural - don't force to integers
            Mathf.Round(currentPos.z)
        );
        
        // CRITICAL: Direct transform assignment ensures perfect X,Z alignment
        transform.position = gridPos;
        transform.rotation = Quaternion.identity;
        
        // Don't try to set velocity on kinematic rigidbodies
    }
    
    /// <summary>
    /// Check if cube should fall due to gravity and let it settle naturally
    /// </summary>
    private void CheckGravity()
    {
        // Cast ray from bottom edge of cube to check if grounded
        Vector3 bottomCenter = transform.position + Vector3.down * 0.5f; // Cube bottom edge
        float rayDistance = 0.1f; // Small buffer distance
        
        bool isGrounded = Physics.Raycast(bottomCenter, Vector3.down, rayDistance);
        
        if (!isGrounded)
        {
            // Use custom gravity system for better control
            StartCoroutine(FallWithCustomGravity());
        }
    }
    
    /// <summary>
    /// Apply custom gravity with adjustable speed
    /// </summary>
    private System.Collections.IEnumerator FallWithCustomGravity()
    {
        isFalling = true;
        Vector3 velocity = Vector3.zero;
        float customGravity = Physics.gravity.y * gravityMultiplier;
        
        while (true)
        {
            // Apply custom gravity acceleration
            velocity.y += customGravity * Time.fixedDeltaTime;
            
            // Calculate next position
            Vector3 nextPos = transform.position + velocity * Time.fixedDeltaTime;
            
            // Check for ground collision
            float rayDistance = 0.6f;
            if (Physics.Raycast(transform.position, Vector3.down, rayDistance))
            {
                // Found ground - settle and stop falling
                break;
            }
            
            // Move to next position
            transform.position = nextPos;
            
            yield return new WaitForFixedUpdate();
        }
        
        // Snap to grid and finish falling
        SnapToGrid();
        isFalling = false;
    }
    
    /// <summary>
    /// Push the cube in a direction - called by Tim
    /// </summary>
    public bool TryPush(Vector3 direction)
    {
        if (isMoving || isFalling) return false;
        
        // Convert to pure grid direction
        Vector3 pushDir = GetGridDirection(direction);
        if (pushDir == Vector3.zero) return false;
        
        // Calculate exact target position
        Vector3 targetPos = transform.position + pushDir;
        
        // Check if destination is completely clear
        if (!IsPositionClear(targetPos)) return false;
        
        // Start precise movement
        StartCoroutine(MoveTo(targetPos));
        Debug.Log($"Cube pushed! Moving to {targetPos}");
        return true;
    }
    
    /// <summary>
    /// Convert any direction to exact grid direction (no diagonals)
    /// </summary>
    private Vector3 GetGridDirection(Vector3 direction)
    {
        direction.y = 0; // Remove vertical component
        direction.Normalize();
        
        // Force to strongest cardinal direction only
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            return direction.x > 0 ? Vector3.right : Vector3.left;
        }
        else if (Mathf.Abs(direction.z) > 0.1f)
        {
            return direction.z > 0 ? Vector3.forward : Vector3.back;
        }
        
        return Vector3.zero;
    }
    
    /// <summary>
    /// Check if a grid position is completely clear of obstacles
    /// </summary>
    private bool IsPositionClear(Vector3 position)
    {
        // Use OverlapBox for precise collision checking
        Bounds checkBounds = new Bounds(position, Vector3.one * 0.9f);
        Collider[] overlapping = Physics.OverlapBox(checkBounds.center, checkBounds.extents);
        
        foreach (Collider col in overlapping)
        {
            // Ignore self and triggers
            if (col.gameObject == gameObject || col.isTrigger) continue;
            
            // Any solid collider blocks movement
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Move cube to target position with perfect grid precision (X,Z only)
    /// </summary>
    private System.Collections.IEnumerator MoveTo(Vector3 targetPosition)
    {
        isMoving = true;
        Vector3 startPos = transform.position;
        
        // Preserve natural Y position during horizontal movement
        targetPosition.y = startPos.y;
        
        float elapsed = 0f;
        float moveTime = 1f / moveSpeed;
        
        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / moveTime);
            
            // Direct transform interpolation - no physics
            Vector3 currentPos = Vector3.Lerp(startPos, targetPosition, progress);
            transform.position = currentPos;
            yield return null;
        }
        
        // CRITICAL: Force exact final X,Z position, keep Y natural
        Vector3 finalPos = targetPosition;
        finalPos.y = transform.position.y; // Preserve current Y
        transform.position = finalPos;
        transform.rotation = Quaternion.identity;
        
        isMoving = false;
        
        // Check for gravity after movement completes
        CheckGravity();
    }
    
    /// <summary>
    /// Force cube back to grid if it somehow gets misaligned
    /// </summary>
    private void OnValidate()
    {
        // Only snap in play mode and if rigidbody is set up
        if (Application.isPlaying && rb != null)
        {
            SnapToGrid();
        }
    }
}