using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public enum RotationAxis
{
    Horizontal, // Y-axis rotation
    Vertical    // X-axis rotation
}


/// Handles all of Tim's interactions with cubes: selection, pushing, rotation
/// Separated from CharacterMovement to maintain clean architecture

public class TimCubeInteraction : MonoBehaviour
{
    [Header("CUBE SELECTION:")]
    [SerializeField] private RunodeMovement detectedCube = null;
    [SerializeField] private RunodeMovement highlightedCube = null;

    [Header("DETECTION SETTINGS:")]
    [HideInInspector] public float detectionTolerance = 0.6f;
    public float pushRange = 1.5f; 

    [Header("CUBE JUICE SETTINGS:")]
    public AnimationCurve rotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float rotationDuration = 0.2f;
    public float mouseRotationDuration = 0.12f;
    public float squashAmount = 0.15f;
    public float overshootAmount = 0.05f;

    [Header("PUSH DELAY SETTINGS:")]
    public float initialPushDelay = 0.35f;
    public float fastPushInterval = 0.1f;
    public float precisePushInterval = 0.5f;

    private bool isPreciseMode = false;

    private Transform timTransform;
    private CharacterMovement characterMovement;
    private CharacterController controller;
    private PlayerAnimator playerAnimator;
    private MenuManager _menuManager;
    
    // Push delay tracking
    private RunodeMovement currentTargetCube;
    private Vector3 currentPushDirection;
    private float pushDelayTimer;
    private bool isDelayActive;
    private bool isFirstPush = true;
    private bool isPushingThisFrame;
    
    // Highlighted cube tracking
    private RunodeMovement currentlyHighlightedCube = null;
    
    // Tim's own cube selection system
    private RunodeMovement[] currentStack = null;
    private int selectedIndex = 0;
    private RunodeMovement selectedCube = null;
    
    // Cache for performance optimization
    private RunodeMovement[] allCubesCache = null;
    private float lastCubesCacheTime = 0f;
    private const float CACHE_REFRESH_INTERVAL = 1.0f;
    
    public RunodeMovement GetHighlightedCube() { return currentlyHighlightedCube; }
    
    // Rotation tracking
    private bool isRotating = false;
    
  
    
    [Header("MOUSE ROTATION:")]
    public float mouseRotationSensitivity = 0.5f;
    private bool isMouseRotating = false;
    private RunodeMovement mouseRotTarget = null;
    private Vector2 lastMousePosition;
    private Vector2 rotationAccumulator;

    private void Awake()
    {
        timTransform = transform;
        characterMovement = GetComponent<CharacterMovement>();
        controller = GetComponent<CharacterController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        _menuManager = Object.FindFirstObjectByType<MenuManager>();
    }
    
    private void Update()
    {
        // Reset push state at start of frame
        isPushingThisFrame = false;
        
        HandleMouseRotation();

        // Only check for cubes/rotation when grounded and NOT mouse rotating
        if (characterMovement.IsGrounded && !isMouseRotating) 
        {
            CheckCubeInteraction(); // Unified cube detection and interaction
            HandleCubeRotation();
        }
        
        HandleCubeSelection();
        HandlePushModeToggle();
        UpdatePushDelay();
        UpdateGaze();
        
        // Only reset push engagement when Tim stops providing movement input
        Vector3 moveDirection = characterMovement.GetMovementDirectionExternal();
        if (moveDirection.magnitude < 0.1f)
        {
            ResetPushEngagement();
        }
    }

    private void HandleMouseRotation()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            // Increase range for testing and ignore triggers if necessary
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                RunodeMovement cube = hit.collider.GetComponentInParent<RunodeMovement>();
                if (cube != null)
                {
                    float dist = Vector3.Distance(timTransform.position, cube.transform.position);
                    // Temporarily relax distance check to confirm raycast works
                    if (dist <= pushRange + 3f)
                    {
                        isMouseRotating = true;
                        mouseRotTarget = cube;
                        lastMousePosition = Mouse.current.position.ReadValue();
                        rotationAccumulator = Vector2.zero;
                        
                        if (characterMovement != null) characterMovement.SetMovementEnabled(false);
                    }
                }
            }
        }

        if (isMouseRotating)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                Vector2 currentMousePos = Mouse.current.position.ReadValue();
                Vector2 delta = currentMousePos - lastMousePosition;
                lastMousePosition = currentMousePos;

                rotationAccumulator += delta * mouseRotationSensitivity;

                float snapThreshold = 40f; 

                // Dominant axis logic: only rotate on the axis with the most movement
                if (Mathf.Abs(rotationAccumulator.x) >= snapThreshold || Mathf.Abs(rotationAccumulator.y) >= snapThreshold)
                {
                    if (Mathf.Abs(rotationAccumulator.x) >= Mathf.Abs(rotationAccumulator.y))
                    {
                        // Horizontal movement dominant -> Rotate around Y
                        float dir = Mathf.Sign(rotationAccumulator.x);
                        RotateSelectedCube(90f * dir, RotationAxis.Horizontal, mouseRotTarget, mouseRotationDuration);
                    }
                    else
                    {
                        // Vertical movement dominant -> Rotate around X
                        // Drag Up (positive delta) = Clockwise X
                        float dir = Mathf.Sign(rotationAccumulator.y);
                        RotateSelectedCube(90f * dir, RotationAxis.Vertical, mouseRotTarget, mouseRotationDuration);
                    }
                    
                    // Reset both to prevent diagonal jitter
                    rotationAccumulator = Vector2.zero;
                }
            }
            else
            {
                isMouseRotating = false;
                mouseRotTarget = null;
                if (characterMovement != null) characterMovement.SetMovementEnabled(true);
            }
        }
    }


    /// Unified cube interaction - single raycast handles both detection and pushing

    private void CheckCubeInteraction()
    {
        // Cast ray at Tim's interaction level in facing direction
        float detectionHeight = 0.8f;
        Vector3 rayStart = timTransform.position + Vector3.up * detectionHeight;
        Vector3 rayDirection = timTransform.forward;
        
        RaycastHit hit;
        
#if UNITY_EDITOR
        // Draw debug rays only in editor for performance
        Debug.DrawRay(rayStart, rayDirection * pushRange, Color.yellow, Time.deltaTime);
        Debug.DrawRay(rayStart, rayDirection * pushRange, Color.red, Time.deltaTime);
#endif
        
        if (Physics.Raycast(rayStart, rayDirection, out hit, pushRange))
        {
            // Use GetComponentInParent to handle hits on child detectors/sprites
            RunodeMovement hitCube = hit.collider.GetComponentInParent<RunodeMovement>();
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


    /// Handle cube highlighting logic (lenient alignment)

    private void HandleCubeHighlighting(RunodeMovement hitCube)
    {
        if (hitCube != null && IsReasonablyAlignedForDetection(hitCube.transform))
        {
            if (currentlyHighlightedCube != hitCube)
            {
                RunodeMovement previouslySelectedCube = selectedCube;
                currentlyHighlightedCube = hitCube;
                
                currentStack = GetStackFromHighlightedCube(hitCube);
                
                bool selectionPreserved = false;
                if (previouslySelectedCube != null && currentStack != null)
                {
                    for (int i = 0; i < currentStack.Length; i++)
                    {
                        if (currentStack[i] == previouslySelectedCube)
                        {
                            selectedIndex = i;
                            selectedCube = previouslySelectedCube;
                            selectionPreserved = true;
                            break;
                        }
                    }
                }
                
                if (!selectionPreserved)
                {
                    selectedIndex = 0;
                    selectedCube = (currentStack != null && currentStack.Length > 0) ? currentStack[0] : null;
                }
                
                highlightedCube = selectedCube;
            }
        }
        else
        {
            if (currentlyHighlightedCube != null)
            {
                currentlyHighlightedCube = null;
                currentStack = null;
                selectedCube = null;
                selectedIndex = 0;
                highlightedCube = null;
            }
        }
    }

    private void HandleCubePushing(RunodeMovement hitCube)
    {
        Vector3 moveDirection = characterMovement.GetMovementDirectionExternal();
        
        if (moveDirection.magnitude < 0.1f)
            return;
        
        if (hitCube != null && IsProperlyAlignedToPush(hitCube.transform, timTransform.forward))
        {
            RunodeMovement[] stackFromTimLevel = GetStackFromHighlightedCube();
            
            if (stackFromTimLevel.Length > 3)
                return;
            
            isPushingThisFrame = true;
            
            Vector3 pushDirection = hitCube.GetRelativePushDirection(timTransform);
            
            if (currentTargetCube != hitCube)
            {
                StartNewPushEngagement(hitCube, pushDirection);
            }
            else
            {
                bool significantDirectionChange = currentPushDirection != pushDirection;
                if (significantDirectionChange)
                    StartNewPushEngagement(hitCube, pushDirection);
                else
                    currentPushDirection = pushDirection;
            }
            
            if (!isDelayActive)
            {
                RunodeMovement[] currentStack = GetStackFromHighlightedCube(hitCube);
                bool pushSuccess = TryPushStack(currentStack, pushDirection);
                if (pushSuccess)
                    StartContinuousPushDelay();
            }      
        }
    }


    /// Check if Tim is reasonably aligned with a cube for detection/highlighting
    /// More lenient than push alignment - allows slightly off-center detection
 
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
    
 
    /// Check if Tim is properly aligned to push a cube in the given direction
    /// Stricter tolerance than detection - requires precise face alignment
  
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
    

    /// Handle Tim's own cube selection system
 
    private void HandleCubeSelection()
    {
        if (UnityEngine.InputSystem.Keyboard.current?.tabKey.wasPressedThisFrame != true) return;
        if (!characterMovement.IsGrounded) return;
        
        CycleSelection();
    }
    
  
    /// Handle cube rotation with Q/E/R keys
 
    private void HandleCubeRotation()
    {
        if (isRotating || selectedCube == null || selectedCube.isRotating) return;
        
        bool isShiftHeld = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
        float rotationStep = isShiftHeld ? -90f : 90f;

        if (Keyboard.current?.qKey.wasPressedThisFrame == true)
        {
            RotateSelectedCube(rotationStep, RotationAxis.Horizontal);
        }
        else if (Keyboard.current?.eKey.wasPressedThisFrame == true)
        {
            RotateSelectedCube(rotationStep, RotationAxis.Vertical);
        }
        else if (Keyboard.current?.rKey.wasPressedThisFrame == true)
        {
            ResetSelectedCube();
        }
    }

    private void HandlePushModeToggle()
    {
        if (Keyboard.current != null && Keyboard.current.ctrlKey.wasPressedThisFrame)
        {
            isPreciseMode = !isPreciseMode;
            
            // Update HUD via cached MenuManager
            if (_menuManager != null)
            {
                _menuManager.SetPreciseMode(isPreciseMode);
            }
        }
    }

    /// <summary>
    /// Reset the selected cube to its base rotation (captured on Awake)
    /// </summary>
    public void ResetSelectedCube()
    {
        if (isRotating || selectedCube == null) return;
        StartCoroutine(SmoothResetRotation(selectedCube));
    }

    /// <summary>
    /// Smoothly reset a cube to its captured base rotation
    /// </summary>
    private System.Collections.IEnumerator SmoothResetRotation(RunodeMovement cube)
    {
        if (cube == null || cube.visualParent == null) yield break;

        isRotating = true;
        cube.isRotating = true;

        Transform targetTransform = cube.visualParent;
        Quaternion startRotation = targetTransform.localRotation;
        Quaternion targetRotation = cube.BaseRotation;

        float elapsedTime = 0f;
        Vector3 originalScale = targetTransform.localScale;

        while (elapsedTime < rotationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / rotationDuration;
            
            // Smoothly Slerp back to the original base rotation
            targetTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);

            // Subtle juice during reset
            float squash = Mathf.Sin(t * Mathf.PI) * (squashAmount * 0.5f);
            targetTransform.localScale = new Vector3(
                originalScale.x * (1 + squash), 
                originalScale.y * (1 - squash), 
                originalScale.z * (1 + squash)
            );
            
            yield return null;
        }

        targetTransform.localRotation = targetRotation;
        targetTransform.localScale = originalScale;

        Physics.SyncTransforms();
        cube.isRotating = false;
        isRotating = false;
        PowerManager.Instance.RequestPowerFlowCheck();
    }
    
    
    /// Called from CharacterMovement when Tab key is pressed
   
    public void HandleTabPressed()
    {
        if (!characterMovement.IsGrounded) return; // Disable selection while jumping/falling
        
        CycleSelection();
    }
    
 
    /// Called from CharacterMovement when Q key is pressed (rotate horizontally clockwise)
    
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
    private void RotateSelectedCube(float degrees, RotationAxis axis, RunodeMovement targetOverride = null, float? customDuration = null)
    {
        if (isRotating) return; // Prevent overlapping rotations
        
        RunodeMovement cubeToRotate = targetOverride != null ? targetOverride : selectedCube;
        
        if (cubeToRotate != null)
        {
            float duration = customDuration ?? rotationDuration;
            StartCoroutine(SmoothRotateCube(cubeToRotate, degrees, axis, duration));
        }
    }
    
    private System.Collections.IEnumerator SmoothRotateCube(RunodeMovement cube, float degrees, RotationAxis axis, float duration)
    {
        if (cube == null || cube.visualParent == null) yield break;
        
        isRotating = true;
        cube.isRotating = true;
        
        Transform targetTransform = cube.visualParent;
        Quaternion startRotation = targetTransform.localRotation;
        
        // Determine the world-space axis that matches the player's view
        Vector3 worldAxis;
        if (axis == RotationAxis.Horizontal)
        {
            // Dragging left/right always spins around the vertical world axis
            worldAxis = Vector3.up;
        }
        else
        {
            // Dragging up/down spins around the world axis most aligned with the camera's "Right"
            Vector3 camRight = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            if (Mathf.Abs(camRight.x) > Mathf.Abs(camRight.z))
            {
                worldAxis = Vector3.right * Mathf.Sign(camRight.x);
            }
            else
            {
                worldAxis = Vector3.forward * Mathf.Sign(camRight.z);
            }
        }
        
        // Convert the world-space axis into the coordinate space of the cube's parent
        // (This ensures it works even if the whole level/cube-root is rotated)
        Vector3 rotAxis = cube.transform.InverseTransformDirection(worldAxis);
        
        // Apply the 90-degree rotation relative to the current orientation
        Quaternion targetRotation = Quaternion.AngleAxis(degrees, rotAxis) * startRotation;
        
        // Ensure target rotation has exact 90° increments locally
        Vector3 targetEuler = targetRotation.eulerAngles;
        targetEuler.x = Mathf.Round(targetEuler.x / 90f) * 90f;
        targetEuler.y = Mathf.Round(targetEuler.y / 90f) * 90f;
        targetEuler.z = Mathf.Round(targetEuler.z / 90f) * 90f;
        targetRotation = Quaternion.Euler(targetEuler);
        
        // Calculate local overshoot for juice
        Quaternion overshootRot = Quaternion.AngleAxis(degrees + (degrees > 0 ? 5f : -5f), rotAxis) * startRotation;
        
        float elapsedTime = 0f;
        Vector3 originalScale = targetTransform.localScale;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            if (t < 0.8f)
            {
                targetTransform.localRotation = Quaternion.Slerp(startRotation, overshootRot, t / 0.8f);
            }
            else
            {
                targetTransform.localRotation = Quaternion.Slerp(overshootRot, targetRotation, (t - 0.8f) / 0.2f);
            }

            float squash = Mathf.Sin(t * Mathf.PI) * squashAmount;
            targetTransform.localScale = new Vector3(
                originalScale.x * (1 + squash), 
                originalScale.y * (1 - squash), 
                originalScale.z * (1 + squash)
            );
            
            yield return null;
        }
        
        // Final Snap and Impact Squash
        targetTransform.localRotation = targetRotation;
        
        // Impact Bounce
        float bounceTime = 0.15f;
        float bElapsed = 0;
        while (bElapsed < bounceTime)
        {
            bElapsed += Time.deltaTime;
            float bt = bElapsed / bounceTime;
            float bounceSquash = Mathf.Sin(bt * Mathf.PI) * (squashAmount * 0.5f);
            targetTransform.localScale = new Vector3(
                originalScale.x * (1 - bounceSquash), 
                originalScale.y * (1 + bounceSquash), 
                originalScale.z * (1 - bounceSquash)
            );
            yield return null;
        }
        targetTransform.localScale = originalScale;

        // Ensure triggers are updated in the physics world
        Physics.SyncTransforms();
        cube.isRotating = false;
        isRotating = false;
        PowerManager.Instance.RequestPowerFlowCheck();
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
    /// Try to push an entire stack of cubes - Tim handles the coordination
    /// </summary>
    private bool TryPushStack(RunodeMovement[] stack, Vector3 direction)
    {
        if (stack == null || stack.Length == 0) return false;
        
        foreach (var cube in stack)
        {
            if (!cube.CanPushSingle(direction))
                return false;
        }
        
        foreach (var cube in stack)
        {
            cube.PushSingle(direction);
        }
        
        return true;
    }
    
    private RunodeMovement[] GetAllCubesOptimized()
    {
        float currentTime = Time.time;
        if (allCubesCache == null || currentTime - lastCubesCacheTime > CACHE_REFRESH_INTERVAL)
        {
            allCubesCache = FindObjectsByType<RunodeMovement>(FindObjectsSortMode.None);
            lastCubesCacheTime = currentTime;
        }
        return allCubesCache;
    }
    
    private RunodeMovement[] GetStackFromHighlightedCube(RunodeMovement targetCube = null)
    {
        RunodeMovement baseCube = targetCube ?? currentlyHighlightedCube;
        if (baseCube == null) return new RunodeMovement[0];
        
        List<RunodeMovement> stack = new List<RunodeMovement> { baseCube };
        RunodeMovement[] allCubes = GetAllCubesOptimized();
        Vector3 basePos = baseCube.transform.position;
        
        foreach (RunodeMovement cube in allCubes)
        {
            if (cube == baseCube) continue;
            
            Vector3 cubePos = cube.transform.position;
            bool isAligned = Mathf.Abs(cubePos.x - basePos.x) < 0.1f && 
                             Mathf.Abs(cubePos.z - basePos.z) < 0.1f;
            bool isAbove = cubePos.y > basePos.y;
            
            if (isAligned && isAbove)
                stack.Add(cube);
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
    public bool HasPushEngagementChanged(RunodeMovement cube, Vector3 pushDirection)
    {
        return currentTargetCube != cube || currentPushDirection != pushDirection;
    }
    
    public void StartNewPushEngagement(RunodeMovement cube, Vector3 pushDirection)
    {
        currentTargetCube = cube;
        currentPushDirection = pushDirection;
        pushDelayTimer = 0f;
        isDelayActive = true;
        isFirstPush = true;
        UpdatePushDelay();        
    }
    
    public void StartContinuousPushDelay()
    {
        pushDelayTimer = 0f;
        isDelayActive = true;
        isFirstPush = false;
    }
    
    public void ResetPushEngagement()
    {
        currentTargetCube = null;
        currentPushDirection = Vector3.zero;
        pushDelayTimer = 0f;
        isDelayActive = false;
        isFirstPush = true;
    }
    
    public void UpdatePushDelay()
    {
        if (isDelayActive)
        {
            pushDelayTimer += Time.deltaTime;
            float continuousDelay = isPreciseMode ? precisePushInterval : fastPushInterval;
            float requiredDelay = isFirstPush ? initialPushDelay : continuousDelay;
            if (pushDelayTimer >= requiredDelay)
                isDelayActive = false;
        }
    }

    private void UpdateGaze()
    {
        if (playerAnimator == null) return;

        bool isMoving = !characterMovement.IsStationary;

        if (selectedCube != null && !isMoving && !isPushingThisFrame)
        {
            Vector3 targetPos = selectedCube.transform.position;
            
            // Add offset to look at top half of cube
            targetPos += Vector3.up * 0.4f;

            // Prevent looking too low (chest level)
            float minHeight = transform.position.y + 1.2f;
            if (targetPos.y < minHeight)
            {
                targetPos.y = minHeight;
            }

            playerAnimator.SetLookTarget(targetPos);
        }
        else
        {
            playerAnimator.SetLookTarget(null);
        }
    }
    
    public RunodeMovement CurrentTargetCube => currentTargetCube;
    public Vector3 CurrentPushDirection => currentPushDirection;
    public float PushDelayTimer => pushDelayTimer;
    public bool IsDelayActive => isDelayActive;
    public bool IsFirstPush => isFirstPush;
}