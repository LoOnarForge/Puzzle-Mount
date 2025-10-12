using System.Collections.Generic;
using UnityEngine;

public enum PowerLineType
{
    Empty,
    [InspectorName("─ Horizontal")]
    Horizontal,
    [InspectorName("│ Vertical")]
    Vertical,
    [InspectorName("────────")]
    Separator1,
    [InspectorName("┘ Corner Left Top")]
    CornerLeftTop,
    [InspectorName("└ Corner Top Right")]
    CornerTopRight,
    [InspectorName("┌ Corner Right Bottom")]
    CornerRightBottom,
    [InspectorName("┐ Corner Bottom Left")]
    CornerBottomLeft,
    [InspectorName("──────────")]
    Separator2,
    [InspectorName("┴ T Section Left")]
    TSectionLeft,
    [InspectorName("├ T Section Top")]
    TSectionTop,
    [InspectorName("┬ T Section Right")]
    TSectionRight,
    [InspectorName("┤ T Section Bottom")]
    TSectionBottom,
    [InspectorName("────────────")]
    Separator3,
    [InspectorName("┼ Cross")]
    Cross
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class PowerCube : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    
    [Header("Visual")]
    public Transform visualParent;
    
    public PowerLineType topFace = PowerLineType.Empty;
    public PowerLineType bottomFace = PowerLineType.Empty;
    public PowerLineType northFace = PowerLineType.Empty;
    public PowerLineType eastFace = PowerLineType.Empty;
    public PowerLineType southFace = PowerLineType.Empty;
    public PowerLineType westFace = PowerLineType.Empty;
    
    public GameObject horizontalPrefab;
    public GameObject verticalPrefab;
    public GameObject cornerTopRightPrefab;
    public GameObject cornerRightBottomPrefab;
    public GameObject cornerBottomLeftPrefab;
    public GameObject cornerLeftTopPrefab;
    public GameObject tSectionTopPrefab;
    public GameObject tSectionRightPrefab;
    public GameObject tSectionBottomPrefab;
    public GameObject tSectionLeftPrefab;
    public GameObject crossPrefab;
    
    [Header("Random Rotation")]
    public bool randomRotateOnStart = true;
    
    private Rigidbody rb;
    public bool isMoving = false;
    
    // PowerLine connection tracking
    private bool[] faceConnections = new bool[6]; // Top, Bottom, North, East, South, West
    private PowerLineState[] faceStates = new PowerLineState[6];
    
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
        
        // Random rotation before grid snap and before PowerLine prefab instantiation
        if (randomRotateOnStart)
        {
            ApplyRandomRotation();
        }
        
        // Force perfect grid alignment on start
        SnapToGrid();
    }
    
    private void OnValidate()
    {
        // Note: PowerLine prefabs are now handled by PowerCubeEditor script
    }
    
    /// <summary>
    /// Apply random rotation on at least 2 axes in 90-degree increments
    /// </summary>
    private void ApplyRandomRotation()
    {
        // Get random rotations in 90-degree increments
        float xRot = Random.Range(0, 4) * 90f;
        float yRot = Random.Range(0, 4) * 90f;
        float zRot = Random.Range(0, 4) * 90f;
        
        // Ensure at least 2 axes are rotated (not 0)
        int zeroCount = 0;
        if (xRot == 0) zeroCount++;
        if (yRot == 0) zeroCount++;
        if (zRot == 0) zeroCount++;
        
        // If more than 1 axis is zero, force rotation on random axes
        if (zeroCount > 1)
        {
            int[] axes = {0, 1, 2}; // X, Y, Z
            for (int i = 0; i < 2; i++) // Ensure 2 axes are rotated
            {
                int randomAxis = Random.Range(i, 3);
                int temp = axes[i];
                axes[i] = axes[randomAxis];
                axes[randomAxis] = temp;
                
                float randomRotation = Random.Range(1, 4) * 90f; // 90, 180, or 270
                switch (axes[i])
                {
                    case 0: xRot = randomRotation; break;
                    case 1: yRot = randomRotation; break;
                    case 2: zRot = randomRotation; break;
                }
            }
        }
        
        // Apply rotation to whole cube transform
        transform.rotation = Quaternion.Euler(xRot, yRot, zRot);
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
    }
    
    /// <summary>
    /// Rotate only the visual representation, keeping collider grid-aligned
    /// </summary>
    public void RotateVisual(float rotationY)
    {
        if (visualParent != null)
        {
            visualParent.Rotate(0, rotationY, 0);
        }
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
        
        // Don't allow pushing if cube is falling
        if (!IsGrounded()) return false;
        
        // Convert to pure grid direction
        Vector3 pushDir = GetGridDirection(direction);
        if (pushDir == Vector3.zero) return false;
        
        // Get only this cube's vertical stack (cubes directly above this one)
        PowerCube[] stackFromLevel = GetVerticalStackFromThisCube();
        
        // If no level-based stack found, just push this cube alone
        if (stackFromLevel.Length == 0)
        {
            stackFromLevel = new PowerCube[] { this };
        }
        
        // Check if all positions in the stack's destination are clear
        bool allPositionsClear = true;
        Vector3[] targetPositions = new Vector3[stackFromLevel.Length];
        
        for (int i = 0; i < stackFromLevel.Length; i++)
        {
            targetPositions[i] = stackFromLevel[i].transform.position + pushDir;
            if (!IsPositionClear(targetPositions[i]))
            {
                allPositionsClear = false;
                break;
            }
        }
        
        if (!allPositionsClear) return false;
        
        // Push all cubes in the level-based stack
        for (int i = 0; i < stackFromLevel.Length; i++)
        {
            stackFromLevel[i].StartCoroutine(stackFromLevel[i].MoveTo(targetPositions[i], pushDir));
        }
        
        return true;
    }
    
    /// <summary>
    /// Check if cube is grounded and stable for pushing
    /// </summary>
    private bool IsGrounded()
    {
        // Cast downward from cube center to check for ground
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        float rayDistance = 0.6f; // Slightly more than half cube height
        
        // Cast ray downward to detect ground or other cubes
        bool isGrounded = Physics.Raycast(rayStart, Vector3.down, rayDistance);
        
        return isGrounded;
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
            PowerCube otherCube = col.GetComponent<PowerCube>();
            if (otherCube != null && CubeManager.Instance != null)
            {
                PowerCube[] ourStack = CubeManager.Instance.GetEntireStackForCube(this);
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
    
    // PowerLine Connection System
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("PowerLines"))
        {
            HandlePowerLineConnection(other, true);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("PowerLines"))
        {
            HandlePowerLineConnection(other, false);
        }
    }
    
    private void HandlePowerLineConnection(Collider powerLineCollider, bool connecting)
    {
        // Determine which face this connection belongs to
        int faceIndex = GetFaceIndexFromCollider(powerLineCollider);
        if (faceIndex < 0) return;
        
        // Update connection state
        faceConnections[faceIndex] = connecting;
        
        // Update visual state
        UpdateFaceConnectionState(faceIndex);
    }
    
    private int GetFaceIndexFromCollider(Collider collider)
    {
        // Get face name from collider's parent
        string faceName = collider.transform.parent?.name ?? collider.name;
        
        switch (faceName)
        {
            case "Top Face": return 0;
            case "Bottom Face": return 1;
            case "North Face": return 2;
            case "East Face": return 3;
            case "South Face": return 4;
            case "West Face": return 5;
            default: return -1;
        }
    }
    
    private void UpdateFaceConnectionState(int faceIndex)
    {
        // Determine new state
        PowerLineState newState = faceConnections[faceIndex] ? 
            PowerLineState.UnpoweredConnected : 
            PowerLineState.UnpoweredUnconnected;
        
        faceStates[faceIndex] = newState;
        
        // Update visual color
        ApplyFaceColor(faceIndex, newState);
    }
    
    private void ApplyFaceColor(int faceIndex, PowerLineState state)
    {
        string[] faceNames = { "Top Face", "Bottom Face", "North Face", "East Face", "South Face", "West Face" };
        
        Transform faceTransform = transform.Find(faceNames[faceIndex]);
        if (faceTransform != null)
        {
            SpriteRenderer renderer = faceTransform.GetComponent<SpriteRenderer>();
            if (renderer != null && CubeManager.Instance != null)
            {
                CubeManager.Instance.SetPowerLineColor(renderer, state);
            }
        }
    }
    
    public void UpdateAllFaceColors()
    {
        for (int i = 0; i < 6; i++)
        {
            ApplyFaceColor(i, faceStates[i]);
        }
    }
    
    /// <summary>
    /// Get vertical stack starting from this cube upward (only cubes directly above this one)
    /// </summary>
    private PowerCube[] GetVerticalStackFromThisCube()
    {
        List<PowerCube> verticalStack = new List<PowerCube> { this };
        Vector3 basePos = transform.position;
        
        // Find all cubes in scene (inefficient but simple)
        PowerCube[] allCubes = FindObjectsByType<PowerCube>(FindObjectsSortMode.None);
        
        // Find cubes directly above this one
        List<PowerCube> cubesAbove = new List<PowerCube>();
        foreach (PowerCube cube in allCubes)
        {
            if (cube == this) continue;
            
            Vector3 cubePos = cube.transform.position;
            
            // Check alignment (same column) and if above this cube
            float alignmentTolerance = 0.1f;
            bool isAligned = Mathf.Abs(cubePos.x - basePos.x) < alignmentTolerance && 
                           Mathf.Abs(cubePos.z - basePos.z) < alignmentTolerance;
            bool isAbove = cubePos.y > basePos.y;
            
            if (isAligned && isAbove)
            {
                cubesAbove.Add(cube);
            }
        }
        
        // Sort by height and add to stack
        cubesAbove.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        verticalStack.AddRange(cubesAbove);
        
        return verticalStack.ToArray();
    }
}