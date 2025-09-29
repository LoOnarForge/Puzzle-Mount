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
    
    [Header("Visual Selection")]
    public Material selectedCubeMaterial;
    public Material targetedCubeMaterial;
    
    [Header("Debug Information - READ ONLY")]
    [SerializeField] private int totalCubesTracked;
    [SerializeField] private int totalStacksDetected;
    [SerializeField] private Cube currentlyTargetedCube;
    [SerializeField] private int currentStackSize;
    [SerializeField] private bool canPushCurrentStack;
    [SerializeField] private Cube[] currentStack;
    [SerializeField] private int selectedCubeIndex;
    [SerializeField] private Cube selectedCube;
    
    // Singleton instance
    public static CubeManager Instance { get; private set; }
    
    // Cube tracking
    private HashSet<Cube> allCubes = new HashSet<Cube>();
    private Dictionary<Cube, Cube[]> stackCache = new Dictionary<Cube, Cube[]>();
    private Dictionary<Cube, Material> originalMaterials = new Dictionary<Cube, Material>();
    
    // Selection system
    private Cube targetedBaseCube;
    private Cube[] currentStackArray;
    private int currentSelectedIndex = 0;
    
    // Visual highlighting
    private Cube lastHighlightedCube;
    
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
    public void RegisterCube(Cube cube)
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
    public void UnregisterCube(Cube cube)
    {
        if (cube != null && allCubes.Remove(cube))
        {
            // Restore original material if we had one
            if (originalMaterials.ContainsKey(cube))
            {
                RestoreCubeMaterial(cube);
                originalMaterials.Remove(cube);
            }
            
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
        Cube[] sceneCubes = FindObjectsOfType<Cube>();
        
        foreach (Cube cube in sceneCubes)
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
            foreach (Cube cube in allCubes)
            {
                Debug.Log($"[CubeManager] Testing cube: {cube.name} at position {cube.transform.position}");
                Cube[] stack = GetStackAbove(cube);
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
    public Cube[] GetStackAbove(Cube baseCube)
    {
        if (baseCube == null) return new Cube[0];
        
        // Check cache first
        if (stackCache.TryGetValue(baseCube, out Cube[] cachedStack))
        {
            return cachedStack;
        }
        
        // Find the actual bottom cube first (cube with lowest Y position in this column)
        Cube actualBaseCube = FindBottomCubeInColumn(baseCube);
        
        // If this isn't the bottom cube, return empty array to avoid duplicate stacks
        if (actualBaseCube != baseCube)
        {
            stackCache[baseCube] = new Cube[0];
            return new Cube[0];
        }
        
        // Calculate stack using position-based detection from the actual base
        List<Cube> stack = new List<Cube> { actualBaseCube };
        Vector3 basePos = actualBaseCube.transform.position;
        
        if (verboseLogging)
        {
            Debug.Log($"[CubeManager] Starting stack detection from BOTTOM cube: {actualBaseCube.name} at position {basePos}");
        }
        
        // Find all cubes that are vertically aligned with the base cube
        List<Cube> candidateCubes = new List<Cube>();
        foreach (Cube cube in allCubes)
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
        foreach (Cube candidate in candidateCubes)
        {
            stack.Add(candidate);
            if (verboseLogging)
            {
                Debug.Log($"[CubeManager] Added to stack: {candidate.name}. Stack size now: {stack.Count}");
            }
        }
        
        Cube[] stackArray = stack.ToArray();
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
    private Cube FindBottomCubeInColumn(Cube referenceCube)
    {
        if (referenceCube == null) return null;
        
        Vector3 refPos = referenceCube.transform.position;
        Cube bottomCube = referenceCube;
        float lowestY = refPos.y;
        
        foreach (Cube cube in allCubes)
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
    /// Set the currently targeted base cube and calculate its stack
    /// </summary>
    public void SetTargetedCube(Cube baseCube)
    {
        if (targetedBaseCube != baseCube)
        {
            targetedBaseCube = baseCube;
            currentSelectedIndex = 0; // Reset selection to bottom cube
            
            if (baseCube != null)
            {
                currentStackArray = GetStackAbove(baseCube);
                Debug.Log($"[CubeManager] Targeted cube: {baseCube.name}, Stack size: {currentStackArray.Length}");
            }
            else
            {
                currentStackArray = new Cube[0];
                Debug.Log("[CubeManager] No cube targeted");
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
        Cube[] stackFromLevel = GetStackFromTimLevel(timPosition);
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
    /// Get the currently selected cube from the stack (for rotation)
    /// </summary>
    public Cube GetSelectedCube()
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
        currentSelectedIndex = (currentSelectedIndex + 1) % (maxSelectableIndex + 1);
        
        Debug.Log($"[CubeManager] Selection cycled to index {currentSelectedIndex} (cube: {GetSelectedCube()?.name})");
        UpdateVisualHighlights();
        UpdateDebugInfo();
    }
    
    /// <summary>
    /// Update visual highlights for targeted and selected cubes
    /// </summary>
    private void UpdateVisualHighlights()
    {
        // Clear previous highlight
        if (lastHighlightedCube != null)
        {
            RestoreCubeMaterial(lastHighlightedCube);
            lastHighlightedCube = null;
        }
        
        // Highlight currently selected cube
        Cube selectedCube = GetSelectedCube();
        if (selectedCube != null && selectedCubeMaterial != null)
        {
            SetCubeMaterial(selectedCube, selectedCubeMaterial);
            lastHighlightedCube = selectedCube;
        }
    }
    
    /// <summary>
    /// Set a cube's material for highlighting
    /// </summary>
    private void SetCubeMaterial(Cube cube, Material material)
    {
        if (cube != null && material != null)
        {
            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            if (cubeRenderer != null)
            {
                cubeRenderer.material = material;
            }
        }
    }
    
    /// <summary>
    /// Restore a cube's original material
    /// </summary>
    private void RestoreCubeMaterial(Cube cube)
    {
        if (cube != null && originalMaterials.TryGetValue(cube, out Material originalMaterial))
        {
            Renderer cubeRenderer = cube.GetComponent<Renderer>();
            if (cubeRenderer != null && originalMaterial != null)
            {
                cubeRenderer.material = originalMaterial;
            }
        }
    }
    
    /// <summary>
    /// Get stack from Tim's level upward (for level-based pushing)
    /// Tim is 2m tall, cubes are 1m tall, so raycast at ~0.9m to detect cube at Tim's level
    /// </summary>
    public Cube[] GetStackFromTimLevel(Vector3 timPosition)
    {
        // Raycast at Tim's eye level (slightly below 1m from ground)
        float timEyeLevel = 0.9f;
        Vector3 rayStart = new Vector3(timPosition.x, timEyeLevel, timPosition.z);
        
        // Find cube at Tim's level first
        Cube cubeAtTimLevel = null;
        float minDistance = float.MaxValue;
        
        foreach (Cube cube in allCubes)
        {
            Vector3 cubePos = cube.transform.position;
            
            // Check if cube is at roughly Tim's level (within 0.5m vertically)
            if (Mathf.Abs(cubePos.y - timEyeLevel) < 0.5f)
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
        
        if (cubeAtTimLevel == null) return new Cube[0];
        
        // Now get all cubes from this level upward in the same column
        List<Cube> stackFromLevel = new List<Cube> { cubeAtTimLevel };
        Vector3 basePos = cubeAtTimLevel.transform.position;
        
        // Find cubes above this level
        List<Cube> cubesAbove = new List<Cube>();
        foreach (Cube cube in allCubes)
        {
            if (cube == cubeAtTimLevel) continue;
            
            Vector3 cubePos = cube.transform.position;
            
            // Check alignment and if above Tim's level
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
        stackFromLevel.AddRange(cubesAbove);
        
        Debug.Log($"[CubeManager] Found stack from Tim's level: {stackFromLevel.Count} cubes starting with {cubeAtTimLevel.name}");
        return stackFromLevel.ToArray();
    }
    /// <summary>
    /// Get the entire stack for any cube in the stack (for legacy compatibility)
    /// </summary>
    public Cube[] GetEntireStackForCube(Cube anyCube)
    {
        if (anyCube == null) return new Cube[0];
        
        // Find the bottom cube of this stack
        Cube bottomCube = FindBottomCubeInColumn(anyCube);
        
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
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(selectedCube.transform.position, Vector3.one * 1.1f);
        }
    }
}