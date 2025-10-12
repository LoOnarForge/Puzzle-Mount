using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public enum RotationAxis
{
    Horizontal, // Y-axis rotation
    Vertical    // X-axis rotation
}

/// <summary>
/// Handles all of Tim's interactions with cubes: selection, pushing, rotation
/// Separated from CharacterMovement to maintain clean architecture
/// </summary>
public class TimCubeInteraction : MonoBehaviour
{
    [Header("Cube Selection:")]
    [SerializeField] private PowerCube detectedCube = null;
    [SerializeField] private PowerCube highlightedCube = null;

    [Header("Detection Settings")]
    public float detectionTolerance = 0.6f;
    public float pushRange = 1.5f;
    
    [Header("Push Delay Settings")]
    public float initialPushDelay = 0.35f;
    public float continuousPushDelay = 0.1f;

    private Transform timTransform;
    private CharacterMovement characterMovement;
    
    // Push delay tracking
    private PowerCube currentTargetCube;
    private Vector3 currentPushDirection;
    private float pushDelayTimer;
    private bool isDelayActive;
    private bool isFirstPush = true;
    private bool isPushingThisFrame;
    
    // Highlighted cube tracking (moved from CubeManager)
    private PowerCube currentlyHighlightedCube = null;
    
    // Tim's own cube selection system
    private PowerCube[] currentStack = null;
    private int selectedIndex = 0;
    private PowerCube selectedCube = null;
    
    // Public getter for highlighted cube
    public PowerCube GetHighlightedCube() { return currentlyHighlightedCube; }
    
    // Rotation tracking
    private bool isRotating = false;
    
  
    
    private void Awake()
    {
        timTransform = transform;
        characterMovement = GetComponent<CharacterMovement>();
    }
    
    private void Update()
    {
        // Reset push state at start of frame
        isPushingThisFrame = false;
        
        // Only check for cubes when grounded
        if (characterMovement.IsGrounded) 
        {
            CheckCubeInteraction(); // Unified cube detection and interaction
        }
        
        HandleCubeSelection();
        HandleCubeRotation();
        UpdatePushDelay();
        
        // ALWAYS reset push engagement when not actively pushing this frame
        if (!isPushingThisFrame)
        {
            ResetPushEngagement();
        }
    }

    /// <summary>
    /// Unified cube interaction - single raycast handles both detection and pushing
    /// </summary>
    private void CheckCubeInteraction()
    {
        // Cast ray at Tim's interaction level in facing direction
        float detectionHeight = 0.8f;
        Vector3 rayStart = timTransform.position + Vector3.up * detectionHeight;
        Vector3 rayDirection = timTransform.forward;
        
        RaycastHit hit;
        
        // Draw both raycasts for visualization (yellow for detection, red for pushing)
        Debug.DrawRay(rayStart, rayDirection * pushRange, Color.yellow, Time.deltaTime);
        Debug.DrawRay(rayStart, rayDirection * pushRange, Color.red, Time.deltaTime);
        
        if (Physics.Raycast(rayStart, rayDirection, out hit, pushRange))
        {
            PowerCube hitCube = hit.collider.GetComponent<PowerCube>();
            if (hitCube != null)
            {
                // Update debug info
                detectedCube = hitCube;
                
                HandleCubeHighlighting(hitCube);
                HandleCubePushing(hitCube);
            }
            else
            {
                detectedCube = null;
                HandleCubeHighlighting(null);
            }
        }
        else
        {
            detectedCube = null;
            HandleCubeHighlighting(null);
        }
    }

    /// <summary>
    /// Handle cube highlighting logic (lenient alignment)
    /// </summary>
    private void HandleCubeHighlighting(PowerCube hitCube)
    {
        if (hitCube != null && IsReasonablyAlignedForDetection(hitCube.transform))
        {
            // Set this cube as highlighted
            if (currentlyHighlightedCube != hitCube)
            {
                currentlyHighlightedCube = hitCube;
                
                // Update current stack and reset selection to first cube
                currentStack = GetStackFromHighlightedCube();
                selectedIndex = 0;
                selectedCube = (currentStack != null && currentStack.Length > 0) ? currentStack[0] : null;
                
                // Update inspector field to show selected cube
                highlightedCube = selectedCube;
            }
        }
        else
        {
            // Clear highlighting
            if (currentlyHighlightedCube != null)
            {
                currentlyHighlightedCube = null;
                currentStack = null;
                selectedCube = null;
                selectedIndex = 0;
                
                // Clear inspector field
                highlightedCube = null;
            }
        }
    }

    /// <summary>
    /// Handle cube pushing logic (strict alignment, requires movement input)
    /// </summary>
    private void HandleCubePushing(PowerCube hitCube)
    {
        Vector3 moveDirection = characterMovement.GetMovementDirectionExternal();
        
        // Only handle pushing if there's movement input
        if (moveDirection.magnitude < 0.1f)
        {
            return; // No movement = no pushing this frame
        }
        
        if (hitCube != null && IsProperlyAlignedToPush(hitCube.transform, timTransform.forward))
        {
            // Get stack from highlighted cube for Tim's selection
            PowerCube[] stackFromTimLevel = GetStackFromHighlightedCube();
            
            // Check if this level-based stack can be pushed (3 cubes or fewer)
            bool canPushStack = stackFromTimLevel.Length <= 3;
            if (!canPushStack)
            {
                return; // Don't set isPushingThisFrame - this prevents delay timer
            }
            
            // Tim is actively trying to push this cube
            isPushingThisFrame = true;
            
            // Use facing direction directly as push direction (same as raycast)
            Vector3 pushDirection = timTransform.forward;
            
            // Only start new engagement if this is actually a new target or significantly different direction
            bool differentTarget = currentTargetCube != hitCube;
            bool significantDirectionChange = Vector3.Angle(currentPushDirection, pushDirection) > 5f; // 5 degree tolerance
            
            if (differentTarget || significantDirectionChange)
            {
                StartNewPushEngagement(hitCube, pushDirection);
            }
            
            // Only push if delay has elapsed
            if (!isDelayActive)
            {
                Debug.Log($"[PUSH DEBUG] PUSHING! Timer: {pushDelayTimer}, Required: {(isFirstPush ? initialPushDelay : continuousPushDelay)}");
                bool pushSuccess = hitCube.TryPush(pushDirection);
                if (pushSuccess)
                {
                    // Start continuous push delay for next push
                    StartContinuousPushDelay();
                }
            }
            else
            {
                Debug.Log($"[PUSH DEBUG] Waiting for delay. Timer: {pushDelayTimer}, Required: {(isFirstPush ? initialPushDelay : continuousPushDelay)}, Remaining: {(isFirstPush ? initialPushDelay : continuousPushDelay) - pushDelayTimer}");
            }
        }
    }

    /// <summary>
    /// Check if Tim is reasonably aligned with a cube for detection/highlighting
    /// More lenient than push alignment - allows slightly off-center detection
    /// </summary>
    public bool IsReasonablyAlignedForDetection(Transform cubeTransform)
    {
        Vector3 cubeCenter = cubeTransform.position;
        Vector3 timPos = timTransform.position;
        
        // Calculate which face of the cube Tim is closest to
        Vector3 localOffset = timPos - cubeCenter;
        
        // Determine the strongest axis (which face Tim is approaching)
        float absX = Mathf.Abs(localOffset.x);
        float absZ = Mathf.Abs(localOffset.z);
        
        // More lenient tolerance for detection (can be slightly off-center)
        // Now exposed as public variable for tuning
        
        bool isAlignedToFace = false;
        
        if (absX > absZ) // Approaching from X direction (left/right faces)
        {
            isAlignedToFace = Mathf.Abs(localOffset.z) < detectionTolerance;
        }
        else // Approaching from Z direction (front/back faces)
        {
            isAlignedToFace = Mathf.Abs(localOffset.x) < detectionTolerance;
        }
        
        return isAlignedToFace;
    }
    
    /// <summary>
    /// Check if Tim is properly aligned to push a cube in the given direction
    /// Stricter tolerance than detection - requires precise face alignment
    /// </summary>
    public bool IsProperlyAlignedToPush(Transform cubeTransform, Vector3 pushDirection)
    {
        Vector3 cubeCenter = cubeTransform.position;
        Vector3 timPos = timTransform.position;
        
        // Calculate which face of the cube Tim is closest to
        Vector3 localOffset = timPos - cubeCenter;
        
        // Determine the strongest axis (which face Tim is approaching)
        float absX = Mathf.Abs(localOffset.x);
        float absZ = Mathf.Abs(localOffset.z);
        
        // Tim must be approaching from a primary face direction, not a corner/edge
        float faceTolerance = 0.4f; // Relaxed tolerance
        
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
    /// Handle Tim's own cube selection system
    /// </summary>
    private void HandleCubeSelection()
    {
        // Use new Input System's Keyboard class as fallback if Tab action isn't available
        if (UnityEngine.InputSystem.Keyboard.current?.tabKey.wasPressedThisFrame == true)
        {
            if (!characterMovement.IsGrounded) return; // Disable selection while jumping/falling
            
            CycleSelection();
        }
        // Tab action is handled by HandleTabPressed callback if available
    }
    
    /// <summary>
    /// Handle cube rotation with Q/E keys
    /// </summary>
    private void HandleCubeRotation()
    {
        if (isRotating) return; // Prevent input during rotation
        
        // Use new Input System's Keyboard class as fallback if rotation actions aren't available
        if (UnityEngine.InputSystem.Keyboard.current?.qKey.wasPressedThisFrame == true)
        {
            // Q rotates horizontally clockwise (around Y axis, positive direction)
            RotateSelectedCube(90f, RotationAxis.Horizontal);
        }
        else if (UnityEngine.InputSystem.Keyboard.current?.eKey.wasPressedThisFrame == true)
        {
            // E rotates vertically (around X axis, positive direction)
            RotateSelectedCube(90f, RotationAxis.Vertical);
        }
        // Rotation actions are handled by HandleRotateLeft/HandleRotateRight callbacks if available
    }
    
    /// <summary>
    /// Called from CharacterMovement when Tab key is pressed
    /// </summary>
    public void HandleTabPressed()
    {
        if (!characterMovement.IsGrounded) return; // Disable selection while jumping/falling
        
        CycleSelection();
    }
    
    /// <summary>
    /// Called from CharacterMovement when Q key is pressed (rotate horizontally clockwise)
    /// </summary>
    public void HandleRotateLeft()
    {
        if (isRotating) return; // Prevent input during rotation
        
        // Q rotates horizontally clockwise (around Y axis, positive direction)
        RotateSelectedCube(90f, RotationAxis.Horizontal);
    }
    
    /// <summary>
    /// Called from CharacterMovement when E key is pressed (rotate vertically)
    /// </summary>
    public void HandleRotateRight()
    {
        if (isRotating) return; // Prevent input during rotation
        
        // E rotates vertically (around X axis, positive direction)
        RotateSelectedCube(90f, RotationAxis.Vertical);
    }
    
    /// <summary>
    /// Rotate the currently selected cube around specified axis with smooth animation
    /// </summary>
    private void RotateSelectedCube(float degrees, RotationAxis axis)
    {
        if (isRotating) return; // Prevent overlapping rotations
        
        if (selectedCube != null)
        {
            // Prevent rotation while cube is being pushed/moving
            if (selectedCube.isMoving)
            {
                return;
            }
            
            StartCoroutine(SmoothRotateCube(selectedCube, degrees, axis));
        }
    }
    
    /// <summary>
    /// Smoothly rotate a cube by the specified degrees around specified axis
    /// </summary>
    private System.Collections.IEnumerator SmoothRotateCube(PowerCube cube, float degrees, RotationAxis axis)
    {
        if (cube == null) yield break;
        
        isRotating = true; // Block further rotation input
        
        // Use visual parent for rotation instead of main transform
        Transform visualTransform = cube.visualParent;
        if (visualTransform == null)
        {
            Debug.LogWarning($"[TimCubeInteraction] Cube {cube.name} has no visual parent for rotation");
            isRotating = false;
            yield break;
        }
        
        Quaternion startRotation = visualTransform.rotation;
        
        // Calculate target rotation using WORLD axes (not local)
        Quaternion deltaRotation;
        if (axis == RotationAxis.Horizontal)
        {
            // Always rotate around world Y-axis (up), regardless of cube's current orientation
            deltaRotation = Quaternion.AngleAxis(degrees, Vector3.up);
        }
        else // Vertical
        {
            // Always rotate around world X-axis (right), regardless of cube's current orientation
            deltaRotation = Quaternion.AngleAxis(degrees, Vector3.right);
        }
        
        Quaternion targetRotation = deltaRotation * startRotation;
        
        // Ensure target rotation has exact 90° increments
        Vector3 targetEuler = targetRotation.eulerAngles;
        targetEuler.x = Mathf.Round(targetEuler.x / 90f) * 90f;
        targetEuler.y = Mathf.Round(targetEuler.y / 90f) * 90f;
        targetEuler.z = Mathf.Round(targetEuler.z / 90f) * 90f;
        targetRotation = Quaternion.Euler(targetEuler);
        
        float rotationDuration = 0.3f; // Fast but visible rotation
        float elapsedTime = 0f;
        
        // Add visual feedback - scale pulse to show rotation direction and axis
        Vector3 originalScale = visualTransform.localScale;
        float pulseAmount = 1.1f; // Consistent pulse for all rotations
        
        while (elapsedTime < rotationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / rotationDuration;
            
            // Smooth rotation using ease-in-out curve
            float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
            visualTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedProgress);
            
            // Pulse scale for visual feedback (only at start)
            if (progress < 0.3f)
            {
                float scaleProgress = progress / 0.3f;
                float currentPulse = Mathf.Lerp(pulseAmount, 1f, scaleProgress);
                visualTransform.localScale = originalScale * currentPulse;
            }
            
            yield return null;
        }
        
        // Ensure exact final rotation and scale
        visualTransform.rotation = targetRotation;
        visualTransform.localScale = originalScale;
        
        isRotating = false; // Allow new rotation input
    }
    
    /// <summary>
    /// Tim's own cube selection - cycle through bottom 3 cubes in highlighted stack
    /// </summary>
    private void CycleSelection()
    {
        if (currentlyHighlightedCube == null) return;
        
        // Update current stack based on highlighted cube
        currentStack = GetStackFromHighlightedCube();
        if (currentStack == null || currentStack.Length == 0) return;
        
        int maxSelectableIndex = Mathf.Min(2, currentStack.Length - 1); // Max index 2 (3rd cube) or stack length - 1
        selectedIndex = (selectedIndex + 1) % (maxSelectableIndex + 1);
        selectedCube = currentStack[selectedIndex];
        
        // Update inspector field when selection changes
        highlightedCube = selectedCube;
    }
    
    /// <summary>
    /// Get stack from currently highlighted cube (simple version for Tim's needs)
    /// </summary>
    private PowerCube[] GetStackFromHighlightedCube()
    {
        if (currentlyHighlightedCube == null) return new PowerCube[0];
        
        // Simple stack detection - just find cubes above the highlighted cube
        List<PowerCube> stack = new List<PowerCube> { currentlyHighlightedCube };
        PowerCube[] allCubes = FindObjectsByType<PowerCube>(FindObjectsSortMode.None);
        Vector3 basePos = currentlyHighlightedCube.transform.position;
        
        foreach (PowerCube cube in allCubes)
        {
            if (cube == currentlyHighlightedCube) continue;
            
            Vector3 cubePos = cube.transform.position;
            float alignmentTolerance = 0.1f;
            bool isAligned = Mathf.Abs(cubePos.x - basePos.x) < alignmentTolerance && 
                           Mathf.Abs(cubePos.z - basePos.z) < alignmentTolerance;
            bool isAbove = cubePos.y > basePos.y;
            
            if (isAligned && isAbove)
            {
                stack.Add(cube);
            }
        }
        
        // Sort by height
        stack.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        return stack.ToArray();
    }
    
    /// <summary>
    /// Draw gizmo only for selected cube
    /// </summary>
    private void OnDrawGizmos()
    {
        // Only show gizmo for selected cube
        if (selectedCube != null)
        {
            bool canPushSelected = IsProperlyAlignedToPush(selectedCube.transform, timTransform.forward);
            Gizmos.color = canPushSelected ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(selectedCube.transform.position, selectedCube.transform.localScale * 1.1f);
        }
    }
    
    /// <summary>
    /// Check if push engagement has changed (different cube or different side)
    /// </summary>
    public bool HasPushEngagementChanged(PowerCube cube, Vector3 pushDirection)
    {
        return currentTargetCube != cube || currentPushDirection != pushDirection;
    }
    
    /// <summary>
    /// Start engagement with a new cube or from a new direction
    /// </summary>
    public void StartNewPushEngagement(PowerCube cube, Vector3 pushDirection)
    {
        currentTargetCube = cube;
        currentPushDirection = pushDirection;
        pushDelayTimer = 0f;
        isDelayActive = true;
        isFirstPush = true;
        
        Debug.Log($"[PUSH DEBUG] Starting new engagement. Timer: {pushDelayTimer}, Required delay: {initialPushDelay}, isDelayActive: {isDelayActive}");
        
        // Immediately update the timer to avoid 1-frame delay
        UpdatePushDelay();
        
        Debug.Log($"[PUSH DEBUG] After immediate update. Timer: {pushDelayTimer}, isDelayActive: {isDelayActive}");
    }
    
    /// <summary>
    /// Start continuous push delay after a successful push (keeps same engagement)
    /// </summary>
    public void StartContinuousPushDelay()
    {
        pushDelayTimer = 0f;
        isDelayActive = true;
        isFirstPush = false; // Now it's a continuous push
    }
    
    /// <summary>
    /// Reset push engagement when Tim stops actively pushing
    /// </summary>
    public void ResetPushEngagement()
    {
        currentTargetCube = null;
        currentPushDirection = Vector3.zero;
        pushDelayTimer = 0f;
        isDelayActive = false;
        isFirstPush = true;
    }
    
    /// <summary>
    /// Update the push delay timer
    /// </summary>
    public void UpdatePushDelay()
    {
        if (isDelayActive)
        {
            pushDelayTimer += Time.deltaTime;
            
            float requiredDelay = isFirstPush ? initialPushDelay : continuousPushDelay;
            
            if (pushDelayTimer >= requiredDelay)
            {
                isDelayActive = false; // Delay completed - pushing can begin
            }
        }
    }
    
    // Public getters for CharacterMovement to access push state
    public PowerCube CurrentTargetCube => currentTargetCube;
    public Vector3 CurrentPushDirection => currentPushDirection;
    public float PushDelayTimer => pushDelayTimer;
    public bool IsDelayActive => isDelayActive;
    public bool IsFirstPush => isFirstPush;
}