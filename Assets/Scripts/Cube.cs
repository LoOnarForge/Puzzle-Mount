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
        // Register with CubeManager
        if (CubeManager.Instance != null)
        {
            CubeManager.Instance.RegisterCube(this);
        }
        
        // Force perfect grid alignment on start
        SnapToGrid();
    }
    
    private void OnDestroy()
    {
        // Unregister from CubeManager
        if (CubeManager.Instance != null)
        {
            CubeManager.Instance.UnregisterCube(this);
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
            currentPos.y, // Keep Y natural - let physics handle it
            Mathf.Round(currentPos.z)
        );
        
        // CRITICAL: Direct transform assignment ensures perfect X,Z alignment
        transform.position = gridPos;
        transform.rotation = Quaternion.identity;
    }
    
    /// <summary>
    /// Calculate push direction based on pusher's position relative to this cube
    /// This prevents side-pushing by always pushing away from the pusher
    /// </summary>
    public Vector3 GetRelativePushDirection(Transform pusherTransform)
    {
        Vector3 relativePosition = transform.position - pusherTransform.position;
        
        // Determine strongest axis and push away from pusher
        if (Mathf.Abs(relativePosition.x) > Mathf.Abs(relativePosition.z))
        {
            // Pusher is on left/right side - push horizontally away
            return new Vector3(Mathf.Sign(relativePosition.x), 0, 0);
        }
        else
        {
            // Pusher is on front/back side - push forward/backward away
            return new Vector3(0, 0, Mathf.Sign(relativePosition.z));
        }
    }
    
    /// <summary>
    /// Push the cube or stack from Tim's level upward - called by Tim
    /// </summary>
    public bool TryPush(Vector3 direction)
    {
        if (isMoving) return false;
        
        // Convert to pure grid direction
        Vector3 pushDir = GetGridDirection(direction);
        if (pushDir == Vector3.zero) return false;
        
        // Get stack from Tim's level upward for this cube
        Cube[] stackFromLevel = new Cube[0];
        if (CubeManager.Instance != null)
        {
            // Find Tim's position - we need to get it from CharacterMovement
            CharacterMovement tim = FindObjectOfType<CharacterMovement>();
            if (tim != null)
            {
                stackFromLevel = CubeManager.Instance.GetStackFromTimLevel(tim.transform.position);
            }
        }
        
        // If no level-based stack found, just push this cube alone
        if (stackFromLevel.Length == 0)
        {
            stackFromLevel = new Cube[] { this };
        }
        
        Debug.Log($"[Cube] Attempting to push stack from Tim's level: {stackFromLevel.Length} cubes");
        
        // Check if all positions in the stack's destination are clear
        bool allPositionsClear = true;
        Vector3[] targetPositions = new Vector3[stackFromLevel.Length];
        
        for (int i = 0; i < stackFromLevel.Length; i++)
        {
            targetPositions[i] = stackFromLevel[i].transform.position + pushDir;
            if (!IsPositionClear(targetPositions[i]))
            {
                allPositionsClear = false;
                Debug.Log($"[Cube] Position blocked for cube {stackFromLevel[i].name} at {targetPositions[i]}");
                break;
            }
        }
        
        if (!allPositionsClear) return false;
        
        // Push all cubes in the level-based stack
        for (int i = 0; i < stackFromLevel.Length; i++)
        {
            stackFromLevel[i].StartCoroutine(stackFromLevel[i].MoveTo(targetPositions[i], pushDir));
        }
        
        Debug.Log($"[Cube] Successfully started pushing {stackFromLevel.Length} cubes from Tim's level");
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
    /// Check if a grid position is completely clear of obstacles (including other cubes in stacks)
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
            
            // Check if this collider belongs to a cube that's part of our stack
            Cube otherCube = col.GetComponent<Cube>();
            if (otherCube != null && CubeManager.Instance != null)
            {
                Cube[] ourStack = CubeManager.Instance.GetEntireStackForCube(this);
                bool isPartOfOurStack = System.Array.Exists(ourStack, cube => cube == otherCube);
                
                // If it's part of our stack, it's okay to overlap during movement
                if (isPartOfOurStack) continue;
            }
            
            // Any other solid collider blocks movement
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