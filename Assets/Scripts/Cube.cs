using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class Cube : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    
    private Rigidbody rb;
    private bool isMoving = false;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Use built-in physics for gravity, but lock X/Z movement
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
    }
    
    private void Start()
    {
        // Force perfect grid alignment on start
        SnapToGrid();
    }
    
    /// <summary>
    /// Force cube to exact integer grid coordinates (X,Z only - Y stays natural)
    /// </summary>
    private void SnapToGrid()
    {
        Vector3 currentPos = transform.position;
        Vector3 gridPos = new Vector3(
            Mathf.Round(currentPos.x),
            currentPos.y, // Keep Y natural - let physics handle it
            Mathf.Round(currentPos.z)
        );
        
        // CRITICAL: Direct transform assignment ensures perfect X,Z alignment
        transform.position = gridPos;
        transform.rotation = Quaternion.identity;
    }
    
    /// <summary>
    /// Push the cube in a direction - called by Tim
    /// </summary>
    public bool TryPush(Vector3 direction)
    {
        if (isMoving) return false;
        
        // Convert to pure grid direction
        Vector3 pushDir = GetGridDirection(direction);
        if (pushDir == Vector3.zero) return false;
        
        // Calculate exact target position
        Vector3 targetPos = transform.position + pushDir;
        
        // Check if destination is completely clear
        if (!IsPositionClear(targetPos)) return false;
        
        // Start precise movement
        StartCoroutine(MoveTo(targetPos, pushDir));
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
    private System.Collections.IEnumerator MoveTo(Vector3 targetPosition, Vector3 direction)
    {
        isMoving = true;
        Vector3 startPos = transform.position;
        
        // Temporarily unlock the movement axis
        RigidbodyConstraints oldConstraints = rb.constraints;
        if (direction == Vector3.right || direction == Vector3.left)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        }
        else if (direction == Vector3.forward || direction == Vector3.back)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotation;
        }
        
        // Preserve natural Y position during horizontal movement
        targetPosition.y = startPos.y;
        
        float elapsed = 0f;
        float moveTime = 1f / moveSpeed;
        
        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / moveTime);
            
            // Direct transform interpolation for horizontal movement
            Vector3 currentPos = Vector3.Lerp(startPos, targetPosition, progress);
            currentPos.y = transform.position.y; // Let physics handle Y
            transform.position = currentPos;
            yield return null;
        }
        
        // CRITICAL: Force exact final X,Z position
        Vector3 finalPos = targetPosition;
        finalPos.y = transform.position.y; // Preserve physics Y
        transform.position = finalPos;
        
        // Re-lock all horizontal movement
        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        
        isMoving = false;
    }
}