using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages all cubes in the scene, handles stacking detection, selection, and future power connections
/// </summary>
public class CubeManager : MonoBehaviour
{
    [Header("Stack Detection Settings")]
    public float stackDetectionDistance = 3f; // Increased to handle larger gaps between cubes
    public LayerMask cubeLayerMask = -1;
    
    [Header("Selection")]
    public float maxSelectionDistance = 3f; // Cancel selection when Tim moves too far
    [Header("Debug Visualization")]
    public bool showDebugRays = false; // Disabled by default
    public bool verboseLogging = false; // Disabled by default
    
    [Header("Debug Information - READ ONLY")]
    [SerializeField] private int totalCubesTracked;
    [SerializeField] private int totalStacksDetected;
    [SerializeField] private PowerCube currentlyTargetedCube;
    [SerializeField] private int currentStackSize;
    [SerializeField] private bool canPushCurrentStack;
    [SerializeField] private PowerCube[] currentStack;
    [SerializeField] private int selectedCubeIndex;
    [SerializeField] private PowerCube selectedCube;
    
    // Singleton instance
    public static CubeManager Instance { get; private set; }
    
    // Cube tracking
    private HashSet<PowerCube> allCubes = new HashSet<PowerCube>();
    private Dictionary<PowerCube, PowerCube[]> stackCache = new Dictionary<PowerCube, PowerCube[]>();
    private Dictionary<PowerCube, Material> originalMaterials = new Dictionary<PowerCube, Material>();
    
    // Selection system
    private PowerCube targetedBaseCube;
    private PowerCube[] currentStackArray;
    private int currentSelectedIndex = 0;
    
    // Visual highlighting
    private PowerCube lastHighlightedCube;
    
    private void Awake()
    {
        // Singleton setup - prefer existing scene instance over programmatically created ones
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[CubeManager] Singleton instance set to: {gameObject.name}");
        }
        else
        {
            Debug.Log($"[CubeManager] Duplicate instance found: {gameObject.name}, destroying it. Active instance: {Instance.gameObject.name}");
            Destroy(gameObject);
        }
    }
    
    [Header("Debug Tools")]
    [Space]
    [SerializeField] private bool forceRefreshCubes;
    
    private void Start()
    {
        // Find all existing cubes in scene
        RefreshAllCubes();
    }
    
    private void Update()
    {
        // Debug tool to force refresh
        if (forceRefreshCubes)
        {
            forceRefreshCubes = false;
            RefreshAllCubes();
        }
    }
    
    /// <summary>
    /// Register a cube with the manager
    /// </summary>
    public void RegisterCube(PowerCube cube)
    {
        if (cube != null && allCubes.Add(cube))
        {
            // Store original material for later restoration
            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            if (cubeRenderer != null && cubeRenderer.material != null)
            {
                originalMaterials[cube] = cubeRenderer.material;
            }
            
            Debug.Log($"[CubeManager] Registered cube: {cube.name}");
            InvalidateStackCache();
            UpdateDebugInfo();
        }
    }
    
    /// <summary>
    /// Unregister a cube from the manager
    /// </summary>
    public void UnregisterCube(PowerCube cube)
    {
        if (cube != null && allCubes.Remove(cube))
        {
            Debug.Log($"[CubeManager] Unregistered cube: {cube.name}");
            InvalidateStackCache();
            UpdateDebugInfo();
        }
    }
    
    /// <summary>
    /// Find all cubes in scene and register them
    /// </summary>
    public void RefreshAllCubes()
    {
        allCubes.Clear();
        PowerCube[] sceneCubes = FindObjectsByType<PowerCube>(FindObjectsSortMode.None);
        
        foreach (PowerCube cube in sceneCubes)
        {
            allCubes.Add(cube);
        }
        
        InvalidateStackCache();
        UpdateDebugInfo();
        Debug.Log($"[CubeManager] Refreshed - Found {allCubes.Count} cubes in scene");
        
        // Only test stack detection if verbose logging is enabled and we're testing
        if (verboseLogging && forceRefreshCubes)
        {
            Debug.Log("=== STACK DETECTION TEST ===");
            foreach (PowerCube cube in allCubes)
            {
                Debug.Log($"[CubeManager] Testing cube: {cube.name} at position {cube.transform.position}");
                PowerCube[] stack = GetStackAbove(cube);
                Debug.Log($"[CubeManager] Cube {cube.name} has stack of {stack.Length} cubes:");
                for (int i = 0; i < stack.Length; i++)
                {
                    Debug.Log($"  [{i}] {stack[i].name} at {stack[i].transform.position}");
                }
            }
            Debug.Log("=== END STACK DETECTION TEST ===");
        }
    }
    
    /// <summary>
    /// Get the complete stack above a base cube (including the base cube itself)
    /// Only returns meaningful results when called on the bottom cube of a stack
    /// </summary>
    public PowerCube[] GetStackAbove(PowerCube baseCube)
    {
        if (baseCube == null) return new PowerCube[0];
        
        // Check cache first
        if (stackCache.TryGetValue(baseCube, out PowerCube[] cachedStack))
        {
            return cachedStack;
        }
        
        // Find the actual bottom cube first (cube with lowest Y position in this column)
        PowerCube actualBaseCube = FindBottomCubeInColumn(baseCube);
        
        // If this isn't the bottom cube, return empty array to avoid duplicate stacks
        if (actualBaseCube != baseCube)
        {
            stackCache[baseCube] = new PowerCube[0];
            return new PowerCube[0];
        }
        
        // Calculate stack using position-based detection from the actual base
        List<PowerCube> stack = new List<PowerCube> { actualBaseCube };
        Vector3 basePos = actualBaseCube.transform.position;
        
        if (verboseLogging)
        {
            Debug.Log($"[CubeManager] Starting stack detection from BOTTOM cube: {actualBaseCube.name} at position {basePos}");
        }
        
        // Find all cubes that are vertically aligned with the base cube
        List<PowerCube> candidateCubes = new List<PowerCube>();
        foreach (PowerCube cube in allCubes)
        {
            if (cube == actualBaseCube) continue;
            
            Vector3 cubePos = cube.transform.position;
            
            // Check if cube is roughly aligned horizontally (X and Z within tolerance)
            float alignmentTolerance = 0.1f;
            bool isAligned = Mathf.Abs(cubePos.x - basePos.x) < alignmentTolerance && 
                           Mathf.Abs(cubePos.z - basePos.z) < alignmentTolerance;
            
            // Check if cube is above the base cube
            bool isAbove = cubePos.y > basePos.y;
            
            if (isAligned && isAbove)
            {
                candidateCubes.Add(cube);
                if (verboseLogging)
                {
                    Debug.Log($"[CubeManager] Found candidate cube above: {cube.name} at {cubePos}");
                }
            }
        }
        
        // Sort candidates by Y position (bottom to top)
        candidateCubes.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        
        // Add candidates to stack
        foreach (PowerCube candidate in candidateCubes)
        {
            stack.Add(candidate);
            if (verboseLogging)
            {
                Debug.Log($"[CubeManager] Added to stack: {candidate.name}. Stack size now: {stack.Count}");
            }
        }
        
        PowerCube[] stackArray = stack.ToArray();
        stackCache[actualBaseCube] = stackArray;
        
        if (verboseLogging)
        {
            Debug.Log($"[CubeManager] Final stack for {actualBaseCube.name}: {stackArray.Length} cubes");
        }
        
        return stackArray;
    }
    
    /// <summary>
    /// Find the bottom cube in the same vertical column as the given cube
    /// </summary>
    private PowerCube FindBottomCubeInColumn(PowerCube referenceCube)
    {
        if (referenceCube == null) return null;
        
        Vector3 refPos = referenceCube.transform.position;
        PowerCube bottomCube = referenceCube;
        float lowestY = refPos.y;
        
        foreach (PowerCube cube in allCubes)
        {
            Vector3 cubePos = cube.transform.position;
            
            // Check if cube is in the same column (X and Z alignment)
            float alignmentTolerance = 0.1f;
            bool isAligned = Mathf.Abs(cubePos.x - refPos.x) < alignmentTolerance && 
                           Mathf.Abs(cubePos.z - refPos.z) < alignmentTolerance;
            
            if (isAligned && cubePos.y < lowestY)
            {
                bottomCube = cube;
                lowestY = cubePos.y;
            }
        }
        
        return bottomCube;
    }
    
    /// <summary>
    /// Set the cube that Tim's raycast hit as targeted, and highlight the cube at Tim's level
    /// This ensures the cube at Tim's level is highlighted, not necessarily the bottom cube
    /// </summary>
    public void SetTargetedCubeAtTimLevel(PowerCube hitCube, Vector3 timPosition)
    {
        if (hitCube == null)
        {
            SetTargetedCube(null);
            return;
        }
        
        // Find the cube at Tim's level in the same stack
        PowerCube[] stackFromTimLevel = GetStackFromTimLevel(timPosition);
        if (stackFromTimLevel.Length > 0)
        {
            PowerCube cubeAtTimLevel = stackFromTimLevel[0]; // First cube is at Tim's level
            
            // Set this cube as targeted for highlighting
            if (targetedBaseCube != cubeAtTimLevel)
            {
                targetedBaseCube = cubeAtTimLevel;
                int oldIndex = currentSelectedIndex;
                currentSelectedIndex = 0; // Start selection at Tim's level cube
                currentStackArray = stackFromTimLevel; // Use the level-based stack
                
                Debug.Log($"[SELECTION] RESET: {oldIndex} -> 0 (new stack: {cubeAtTimLevel.name})");
                // Debug.Log($"[CubeManager] Targeted cube at Tim's level: {cubeAtTimLevel.name}, Stack size from level: {currentStackArray.Length}");
                
                UpdateVisualHighlights();
                UpdateDebugInfo();
            }
        }
        else
        {
            // Fallback to original behavior if no level-based stack found
            SetTargetedCube(hitCube);
        }
    }
    
    /// <summary>
    /// Set the currently targeted base cube and calculate its stack
    /// </summary>
    public void SetTargetedCube(PowerCube baseCube)
    {
        if (targetedBaseCube != baseCube)
        {
            targetedBaseCube = baseCube;
            int oldIndex = currentSelectedIndex;
            currentSelectedIndex = 0; // Reset selection to bottom cube
            
            Debug.Log($"[SELECTION] RESET: {oldIndex} -> 0 (new base: {baseCube?.name})");
            
            if (baseCube != null)
            {
                currentStackArray = GetStackAbove(baseCube);
                // Debug.Log($"[CubeManager] Targeted cube: {baseCube.name}, Stack size: {currentStackArray.Length}");
            }
            else
            {
                currentStackArray = new PowerCube[0];
                // Debug.Log("[CubeManager] No cube targeted");
            }
            
            UpdateVisualHighlights();
            UpdateDebugInfo();
        }
    }
    
    /// <summary>
    /// Auto-select the cube closest to Tim's level
    /// </summary>
    public void AutoSelectCubeAtTimLevel(Vector3 timPosition)
    {
        PowerCube[] stackFromLevel = GetStackFromTimLevel(timPosition);
        if (stackFromLevel.Length > 0)
        {
            // Set the cube at Tim's level as the targeted cube
            SetTargetedCube(stackFromLevel[0]);
            Debug.Log($"[CubeManager] Auto-selected cube at Tim's level: {stackFromLevel[0].name}");
        }
    }
    
    /// <summary>
    /// Check if Tim is too far from selected cube and cancel selection if needed
    /// </summary>
    public void CheckSelectionDistance(Vector3 timPosition)
    {
        if (targetedBaseCube != null)
        {
            float distance = Vector3.Distance(timPosition, targetedBaseCube.transform.position);
            if (distance > maxSelectionDistance)
            {
                Debug.Log($"[CubeManager] Tim moved too far ({distance:F1}m) from selected cube. Canceling selection.");
                SetTargetedCube(null);
            }
        }
    }
    
    /// <summary>
    /// Get the currently targeted cube (for push validation)
    /// </summary>
    public PowerCube GetTargetedCube()
    {
        return targetedBaseCube;
    }
    
    /// <summary>
    /// Get the currently selected cube from the stack (for rotation)
    /// </summary>
    public PowerCube GetSelectedCube()
    {
        if (currentStackArray != null && currentSelectedIndex >= 0 && currentSelectedIndex < currentStackArray.Length)
        {
            return currentStackArray[currentSelectedIndex];
        }
        return null;
    }
    
    /// <summary>
    /// Cycle selection through bottom 3 cubes in stack (Tab key functionality)
    /// </summary>
    public void CycleSelection()
    {
        if (currentStackArray == null || currentStackArray.Length == 0) return;
        
        int maxSelectableIndex = Mathf.Min(2, currentStackArray.Length - 1); // Max index 2 (3rd cube) or stack length - 1
        int oldIndex = currentSelectedIndex;
        currentSelectedIndex = (currentSelectedIndex + 1) % (maxSelectableIndex + 1);
        
        Debug.Log($"[SELECTION] TAB: {oldIndex} -> {currentSelectedIndex} (cube: {GetSelectedCube()?.name})");
        UpdateVisualHighlights();
        UpdateDebugInfo();
    }
    
    /// <summary>
    /// Update visual highlights for targeted and selected cubes
    /// </summary>
    private void UpdateVisualHighlights()
    {
        // Visual highlights now handled by OnDrawGizmos - just store selected cube
    }
    
    /// <summary>
    /// Check if Tim is positioned to push the cube (uses same logic as CharacterMovement)
    /// </summary>
    private bool IsTimInPushablePosition(PowerCube cube)
    {
        CharacterMovement tim = FindFirstObjectByType<CharacterMovement>();
        if (tim == null || cube == null) return false;
        
        Vector3 cubeCenter = cube.transform.position;
        Vector3 timPos = tim.transform.position;
        Vector3 localOffset = timPos - cubeCenter;
        
        float absX = Mathf.Abs(localOffset.x);
        float absZ = Mathf.Abs(localOffset.z);
        
        // Use same strict tolerance as CharacterMovement.IsProperlyAlignedToPush
        float faceTolerance = 0.4f;
        
        bool isAlignedToFace = false;
        if (absX > absZ) // Approaching from X direction (left/right faces)
        {
            isAlignedToFace = Mathf.Abs(localOffset.z) < faceTolerance;
        }
        else // Approaching from Z direction (front/back faces)
        {
            isAlignedToFace = Mathf.Abs(localOffset.x) < faceTolerance;
        }
        
        return isAlignedToFace;
    }
    
    /// <summary>
    /// Get stack from Tim's level upward (for level-based pushing)
    /// Tim raycast hits at 0.8m above his feet, so find cube at that exact level
    /// </summary>
    public PowerCube[] GetStackFromTimLevel(Vector3 timPosition)
    {
        // Calculate Tim's detection level (0.8m above his feet)
        float detectionHeight = 0.8f;
        float timDetectionLevel = timPosition.y + detectionHeight;
        
        // Find cube at Tim's detection level first
        PowerCube cubeAtTimLevel = null;
        float minDistance = float.MaxValue;
        
        foreach (PowerCube cube in allCubes)
        {
            Vector3 cubePos = cube.transform.position;
            
            // Check if cube is at roughly Tim's detection level (within 0.5m vertically)
            if (Mathf.Abs(cubePos.y - timDetectionLevel) < 0.5f)
            {
                float horizontalDistance = Vector2.Distance(
                    new Vector2(cubePos.x, cubePos.z), 
                    new Vector2(timPosition.x, timPosition.z)
                );
                
                if (horizontalDistance < minDistance)
                {
                    minDistance = horizontalDistance;
                    cubeAtTimLevel = cube;
                }
            }
        }
        
        if (cubeAtTimLevel == null) return new PowerCube[0];
        
        // Now get all cubes from this level upward in the same column
        List<PowerCube> stackFromLevel = new List<PowerCube> { cubeAtTimLevel };
        Vector3 basePos = cubeAtTimLevel.transform.position;
        
        // Find cubes above this level only (ignore cubes below Tim's level)
        List<PowerCube> cubesAbove = new List<PowerCube>();
        foreach (PowerCube cube in allCubes)
        {
            if (cube == cubeAtTimLevel) continue;
            
            Vector3 cubePos = cube.transform.position;
            
            // Check alignment and if above Tim's detection level
            float alignmentTolerance = 0.1f;
            bool isAligned = Mathf.Abs(cubePos.x - basePos.x) < alignmentTolerance && 
                           Mathf.Abs(cubePos.z - basePos.z) < alignmentTolerance;
            bool isAboveTimLevel = cubePos.y > timDetectionLevel; // Key change: above Tim's level, not cube's level
            
            if (isAligned && isAboveTimLevel)
            {
                cubesAbove.Add(cube);
            }
        }
        
        // Sort by height and add to stack
        cubesAbove.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        stackFromLevel.AddRange(cubesAbove);
        
        Debug.Log($"[CubeManager] Found stack from Tim's level (Y={timDetectionLevel:F1}): {stackFromLevel.Count} cubes starting with {cubeAtTimLevel.name}");
        return stackFromLevel.ToArray();
    }
    /// <summary>
    /// Get the entire stack for any cube in the stack (for legacy compatibility)
    /// </summary>
    public PowerCube[] GetEntireStackForCube(PowerCube anyCube)
    {
        if (anyCube == null) return new PowerCube[0];
        
        // Find the bottom cube of this stack
        PowerCube bottomCube = FindBottomCubeInColumn(anyCube);
        
        // Get the full stack from the bottom cube
        return GetStackAbove(bottomCube);
    }
    
    /// <summary>
    /// Check if the current stack can be pushed (3 cubes or fewer)
    /// </summary>
    public bool CanPushCurrentStack()
    {
        return currentStackArray != null && currentStackArray.Length <= 3;
    }
    
   
    
    /// <summary>
    /// Get the number of cubes in current stack  
    /// </summary>
    public int GetCurrentStackSize()
    {
        return currentStackArray?.Length ?? 0;
    }
    
    /// <summary>
    /// Clear all cached stack calculations (call when cubes move)
    /// </summary>
    public void InvalidateStackCache()
    {
        stackCache.Clear();
    }
    
    /// <summary>
    /// Update debug information shown in inspector
    /// </summary>
    private void UpdateDebugInfo()
    {
        totalCubesTracked = allCubes.Count;
        totalStacksDetected = stackCache.Count;
        currentlyTargetedCube = targetedBaseCube;
        currentStackSize = GetCurrentStackSize();
        canPushCurrentStack = CanPushCurrentStack();
        currentStack = currentStackArray;
        selectedCubeIndex = currentSelectedIndex;
        selectedCube = GetSelectedCube();
    }
    
    /// <summary>
    /// Debug method to visualize stacks in scene view
    /// </summary>
    private void OnDrawGizmos()
    {
        if (currentStackArray != null && currentStackArray.Length > 1)
        {
            Gizmos.color = Color.cyan;
            
            for (int i = 0; i < currentStackArray.Length - 1; i++)
            {
                if (currentStackArray[i] != null && currentStackArray[i + 1] != null)
                {
                    Vector3 bottom = currentStackArray[i].transform.position + Vector3.up * 0.5f;
                    Vector3 top = currentStackArray[i + 1].transform.position - Vector3.up * 0.5f;
                    Gizmos.DrawLine(bottom, top);
                }
            }
        }
        
        // Highlight selected cube
        if (selectedCube != null)
        {
            // Check if Tim can push this cube
            bool canPush = IsTimInPushablePosition(selectedCube);
            
            // Green for pushable, yellow for selectable only
            Gizmos.color = canPush ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(selectedCube.transform.position, Vector3.one * 1.1f);
        }
    }
}