using UnityEngine;
using UnityEngine.InputSystem;

public enum RotationAxis
{
    Horizontal, // Y axis
    Vertical    // X axis
}

[RequireComponent(typeof(CharacterController))]
public class CharacterMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float jumpHeight = 2f;
    public float rotationSpeed = 10f;
    
    [Header("Speed Buildup")]
    public float startSpeedPercent = 40f;
    public float accelerationTime = 0.3f;
    
    [Header("Air Control")]
    public float momentumDecay = 2f;
    
    [Header("Debug - Speed Visualization")]
    [SerializeField] private float currentSpeedVisual;
    
    [Header("Cube Pushing")]
    public float pushRange = 1.5f;
    public float detectionTolerance = 0.6f;
    
    [Header("Push Delay Settings")]
    public float initialPushDelay = 0.35f;
    public float continuousPushDelay = 0.1f;
    
    [Header("Debug - Push Detection State")]
    [SerializeField] private bool isDelayActiveDebug;
    [SerializeField] private PowerCube currentTargetCubeDebug;
    [SerializeField] private Vector3 currentPushDirectionDebug;
    [SerializeField] private float pushDelayTimerDebug;
    
    // Push delay tracking
    private PowerCube currentTargetCube;
    private Vector3 currentPushDirection;
    private float pushDelayTimer;
    private bool isDelayActive;
    private bool isPushingThisFrame;
    private bool isFirstPush = true;
    
    // Hidden physics settings
    private float gravity = -20f;
    
    private CharacterController controller;
    private PlayerInput playerInput;
    private PlayerAnimator playerAnimator;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction tabAction;
    private InputAction rotateLeftAction;
    private InputAction rotateRightAction;
    
    // Rotation tracking
    private bool isRotating = false; // Prevent input during rotation
    
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isSprinting;
    private float currentSpeed;
    
    // Air control variables
    private Vector3 jumpMomentum;
    private bool wasGroundedLastFrame;
    
    // Speed buildup variables
    private float currentSpeedBuildup;
    private bool wasMovingLastFrame;
    
    public bool IsGrounded => isGrounded;
    public float CurrentSpeed => currentSpeed;
    public bool IsStationary => moveInput.magnitude < 0.1f;
    
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        playerAnimator = GetComponent<PlayerAnimator>();
        
        // Fix PlayerInput notification behavior
        if (playerInput != null)
        {
            playerInput.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
        }
        
        SetupInputActions();
    }
    
    private void SetupInputActions()
    {
        if (playerInput?.actions == null) return;
        
        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        sprintAction = playerInput.actions["Sprint"];
        
        // Try to get Tab action, create if it doesn't exist
        try
        {
            tabAction = playerInput.actions["Tab"];
        }
        catch
        {
            // Tab action doesn't exist in the input actions asset
            // For now, we'll use a fallback method in HandleCubeSelection
            tabAction = null;
        }
        
        // Try to get rotation actions (Q/E keys)
        try
        {
            rotateLeftAction = playerInput.actions["RotateLeft"];
            rotateRightAction = playerInput.actions["RotateRight"];
        }
        catch
        {
            // Rotation actions don't exist, we'll use fallback in HandleCubeRotation
            rotateLeftAction = null;
            rotateRightAction = null;
        }
    }
    
    private void OnEnable()
    {
        if (jumpAction != null)
            jumpAction.performed += OnJump;
            
        if (tabAction != null)
            tabAction.performed += OnTabPressed;
            
        if (rotateLeftAction != null)
            rotateLeftAction.performed += OnRotateLeft;
            
        if (rotateRightAction != null)
            rotateRightAction.performed += OnRotateRight;
    }
    
    private void OnDisable()
    {
        if (jumpAction != null)
            jumpAction.performed -= OnJump;
            
        if (tabAction != null)
            tabAction.performed -= OnTabPressed;
            
        if (rotateLeftAction != null)
            rotateLeftAction.performed -= OnRotateLeft;
            
        if (rotateRightAction != null)
            rotateRightAction.performed -= OnRotateRight;
    }
    
    private void Update()
    {
        // Sync debug variables with actual state
        isDelayActiveDebug = isDelayActive;
        currentTargetCubeDebug = currentTargetCube;
        currentPushDirectionDebug = currentPushDirection;
        pushDelayTimerDebug = pushDelayTimer;
        
        // Reset push state at start of frame
        isPushingThisFrame = false;
        
        CheckGroundStatus();
        ReadInput();
        HandleMovement();
        if (isGrounded) CheckCubeDetection(); // Only check for cubes when grounded
        CheckCubePushing(); // Check for pushing only when moving
        HandleCubeSelection(); // Check for Tab key input
        HandleCubeRotation(); // Check for Q/E key input
        ApplyGravity();
        UpdateAnimations();
        UpdatePushDelay();
        
        // Check for push engagement reset at end of frame
        if (!isPushingThisFrame && (currentTargetCube != null))
        {
            ResetPushEngagement();
        }
    }
    
    private void CheckGroundStatus()
    {
        wasGroundedLastFrame = isGrounded;
        isGrounded = controller.isGrounded;
        
        // Capture momentum when walking off edge (grounded → airborne)
        if (!isGrounded && wasGroundedLastFrame)
        {
            Vector3 currentMovement = GetMovementDirection();
            if (currentMovement.magnitude > 0.1f)
            {
                // Use current built-up speed for edge momentum
                jumpMomentum = currentMovement.normalized * currentSpeedBuildup;
            }
        }
        
        // Reset jump momentum and jumping animation when landing
        if (isGrounded && !wasGroundedLastFrame)
        {
            jumpMomentum = Vector3.zero;
            
            // Reset jumping animation state when landing
            if (playerAnimator != null)
            {
                playerAnimator.SetJumpingState(false);
            }
        }
    }
    
    private void ReadInput()
    {
        if (moveAction != null)
            moveInput = moveAction.ReadValue<Vector2>();
        
        if (sprintAction != null)
            isSprinting = sprintAction.IsPressed();
    }
    
    private void HandleMovement()
    {
        Vector3 moveDirection = GetMovementDirection();
        bool shouldRun = isSprinting;
        
        // When airborne, use the momentum vector directly (already has correct magnitude)
        if (!isGrounded && jumpMomentum.magnitude > 0.1f)
        {
            Vector3 movement = moveDirection; // moveDirection already contains the speed
            controller.Move(movement * Time.deltaTime);
            currentSpeed = moveDirection.magnitude; // For animation purposes
        }
        else
        {
            // Ground movement - calculate speed normally
            currentSpeed = CalculateMovementSpeed(shouldRun);
            Vector3 movement = moveDirection * currentSpeed;
            
            // Check for cube pushing when grounded and moving
            if (movement.magnitude > 0.1f)
            {
                // CheckCubePushing is now called every frame from Update()
            }
            
            controller.Move(movement * Time.deltaTime);
        }
        
        if (moveDirection != Vector3.zero)
        {
            RotatePlayer(moveDirection);
        }
    }
    
    private Vector3 GetMovementDirection()
    {
        Vector3 inputDirection = new Vector3(moveInput.x, 0, moveInput.y);
        
        if (isGrounded)
        {
            // Get camera angle and calculate movement directions based on preset angles
            CameraFollow cameraFollow = FindFirstObjectByType<CameraFollow>();
            if (cameraFollow != null)
            {
                return GetMovementDirectionForCameraAngle(cameraFollow.CurrentAngleIndex);
            }
            else
            {
                // Fallback to world directions if no camera found
                return inputDirection;
            }
        }
        else
        {
            // Use stored jump momentum when airborne
            jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, momentumDecay * Time.deltaTime);
            return jumpMomentum;
        }
    }
    
    private Vector3 GetMovementDirectionForCameraAngle(int angleIndex)
    {
        // Define forward and right directions for each camera angle
        Vector3 forward, right;
        
        switch (angleIndex)
        {
            case 0: // North view
                forward = Vector3.forward;  // North (0,0,1)
                right = Vector3.right;      // East (1,0,0)
                break;
            case 1: // East view
                forward = Vector3.left;     // West (-1,0,0)
                right = Vector3.forward;    // North (0,0,1)
                break;
            case 2: // South view
                forward = Vector3.back;     // South (0,0,-1)
                right = Vector3.left;       // West (-1,0,0)
                break;
            case 3: // West view
                forward = Vector3.right;    // East (1,0,0)
                right = Vector3.back;       // South (0,0,-1)
                break;
            default:
                forward = Vector3.forward;
                right = Vector3.right;
                break;
        }
        
        // Calculate movement direction: forward * W/S input + right * A/D input
        return forward * moveInput.y + right * moveInput.x;
    }
    
    private float CalculateMovementSpeed(bool shouldRun)
    {
        if (moveInput.magnitude < 0.1f)
        {
            wasMovingLastFrame = false;
            currentSpeedBuildup = 0f;
            return 0f;
        }
        
        float targetSpeed = shouldRun ? runSpeed : walkSpeed;
        float startSpeed = targetSpeed * (startSpeedPercent / 100f);
        
        // Start acceleration only if player was NOT moving last frame
        if (!wasMovingLastFrame)
        {
            currentSpeedBuildup = startSpeed; // Start at 40% immediately
        }
        
        // Exponential buildup to max speed
        float exponentialRate = 5f;
        float lerpRate = Time.deltaTime / accelerationTime * exponentialRate;
        currentSpeedBuildup = Mathf.Lerp(currentSpeedBuildup, targetSpeed, lerpRate);
        
        wasMovingLastFrame = true;
        return currentSpeedBuildup;
    }
    
    private void RotatePlayer(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
    
    /// <summary>
    /// Check for nearby cubes for highlighting and selection (runs every frame)
    /// Based on distance and facing direction, not movement
    /// </summary>
    private void CheckCubeDetection()
    {
        // Don't clear targeting - let the raycast below handle it
        
        // Cast ray at Tim's interaction level to detect cubes he's facing
        float detectionHeight = 0.8f;
        Vector3 rayStart = transform.position + Vector3.up * detectionHeight;
        Vector3 forward = transform.forward; // Use Tim's facing direction, not movement
        
        RaycastHit hit;
        
        // Always visible raycast for cube detection
        Debug.DrawRay(rayStart, forward * pushRange, Color.yellow);
        
        if (Physics.Raycast(rayStart, forward, out hit, pushRange))
        {
            PowerCube cube = hit.collider.GetComponent<PowerCube>();
            if (cube != null)
            {
                // Check if Tim is reasonably aligned (more lenient since this is for highlighting)
                if (IsReasonablyAlignedForDetection(cube.transform))
                {
                    // Highlight the cube at Tim's level
                    if (CubeManager.Instance != null)
                    {
                        CubeManager.Instance.SetTargetedCubeAtTimLevel(cube, transform.position);
                    }
                }
                else
                {
                    // Not aligned, clear selection
                    if (CubeManager.Instance != null)
                    {
                        CubeManager.Instance.SetTargetedCube(null);
                    }
                }
            }
            else
            {
                // Hit something but not a cube, clear selection
                if (CubeManager.Instance != null)
                {
                    CubeManager.Instance.SetTargetedCube(null);
                }
            }
        }
        else
        {
            // No raycast hit, clear selection
            if (CubeManager.Instance != null)
            {
                CubeManager.Instance.SetTargetedCube(null);
            }
        }
    }
    
    /// <summary>
    /// More lenient alignment check for cube detection/highlighting
    /// </summary>
    private bool IsReasonablyAlignedForDetection(Transform cubeTransform)
    {
        Vector3 cubeCenter = cubeTransform.position;
        Vector3 timPos = transform.position;
        
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
    /// Check if Tim is trying to push a cube and attempt to push it
    /// Only pushes when Tim is properly aligned and has movement input
    /// </summary>
    private void CheckCubePushing()
    {
        // Only check for pushing if Tim has movement input
        Vector3 moveDirection = GetMovementDirection();
        if (moveDirection.magnitude < 0.1f)
        {
            return; // No movement input - don't check for pushing
        }
        
        // Get currently targeted cube (from detection)
        PowerCube targetedCube = null;
        if (CubeManager.Instance != null)
        {
            targetedCube = CubeManager.Instance.GetTargetedCube();
        }
        
        if (targetedCube == null)
        {
            return; // No cube is currently targeted
        }
        
        // Cast ray in movement direction to verify Tim is pushing toward the cube
        Vector3 rayDirection = moveDirection.normalized;
        float detectionHeight = 0.8f;
        Vector3 rayStart = transform.position + Vector3.up * detectionHeight;
        
        RaycastHit hit;
        
        if (Physics.Raycast(rayStart, rayDirection, out hit, pushRange))
        {
            PowerCube cube = hit.collider.GetComponent<PowerCube>();
            if (cube == targetedCube)
            {
                // Check if Tim is properly aligned to push this cube (strict alignment for pushing)
                if (IsProperlyAlignedToPush(cube.transform, rayDirection))
                {
                    // Get stack from Tim's level upward for pushing
                    PowerCube[] stackFromTimLevel = new PowerCube[0];
                    if (CubeManager.Instance != null)
                    {
                        stackFromTimLevel = CubeManager.Instance.GetStackFromTimLevel(transform.position);
                    }
                    
                    // Check if this level-based stack can be pushed (3 cubes or fewer)
                    bool canPushStack = stackFromTimLevel.Length <= 3;
                    if (!canPushStack)
                    {
                        return; // Don't set isPushingThisFrame - this prevents delay timer
                    }
                    
                    // Tim is actively trying to push this cube
                    isPushingThisFrame = true;
                    
                    // Calculate push direction from relative position
                    Vector3 pushDirection = cube.GetRelativePushDirection(transform);
                    
                    // Check if this is a new push engagement or direction change
                    if (HasPushEngagementChanged(cube, pushDirection))
                    {
                        StartNewPushEngagement(cube, pushDirection);
                    }
                    
                    // Only push if delay has elapsed (or no delay needed for continued pushing)
                    if (!isDelayActive)
                    {
                        bool pushSuccess = cube.TryPush(pushDirection);
                        if (pushSuccess)
                        {
                            // Invalidate stack cache when cube moves
                            CubeManager.Instance?.InvalidateStackCache();
                            
                            // Start continuous push delay for next push
                            pushDelayTimer = 0f;
                            isDelayActive = true;
                            isFirstPush = false;
                        }
                    }
                    else
                    {
                        float requiredDelay = isFirstPush ? initialPushDelay : continuousPushDelay;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Check if Tim is properly positioned and aligned to push a cube
    /// </summary>
    private bool IsProperlyAlignedToPush(Transform cubeTransform, Vector3 pushDirection)
    {
        Vector3 cubeCenter = cubeTransform.position;
        Vector3 timPos = transform.position;
        
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
    /// Check if push engagement has changed (different cube or different side)
    /// </summary>
    private bool HasPushEngagementChanged(PowerCube cube, Vector3 pushDirection)
    {
        return currentTargetCube != cube || currentPushDirection != pushDirection;
    }
    
    /// <summary>
    /// Start engagement with a new cube or from a new direction
    /// </summary>
    private void StartNewPushEngagement(PowerCube cube, Vector3 pushDirection)
    {
        currentTargetCube = cube;
        currentPushDirection = pushDirection;
        pushDelayTimer = 0f;
        isDelayActive = true;
        isFirstPush = true;
    }
    
    /// <summary>
    /// Reset push engagement when Tim stops actively pushing
    /// </summary>
    private void ResetPushEngagement()
    {
        currentTargetCube = null;
        currentPushDirection = Vector3.zero;
        pushDelayTimer = 0f;
        isDelayActive = false;
        isFirstPush = true;
    }
    
    /// <summary>
    /// Handle cube selection cycling with Tab key
    /// </summary>
    private void HandleCubeSelection()
    {
        // Use new Input System's Keyboard class as fallback if Tab action isn't available
        if (tabAction == null)
        {
            if (UnityEngine.InputSystem.Keyboard.current?.tabKey.wasPressedThisFrame == true)
            {
                if (!isGrounded) return; // Disable selection while jumping/falling
                
                if (CubeManager.Instance != null)
                {
                    CubeManager.Instance.CycleSelection();
                }
            }
        }
        // Tab action is handled by OnTabPressed callback if available
    }
    
    /// <summary>
    /// Handle cube rotation with Q/E keys
    /// </summary>
    private void HandleCubeRotation()
    {
        if (isRotating) return; // Prevent input during rotation
        
        // Use new Input System's Keyboard class as fallback if rotation actions aren't available
        if (rotateLeftAction == null || rotateRightAction == null)
        {
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
        }
        // Rotation actions are handled by OnRotateLeft/OnRotateRight callbacks if available
    }
    
    /// <summary>
    /// Rotate the currently selected cube around specified axis with smooth animation
    /// </summary>
    private void RotateSelectedCube(float degrees, RotationAxis axis)
    {
        if (isRotating) return; // Prevent overlapping rotations
        
        if (CubeManager.Instance != null)
        {
            PowerCube selectedCube = CubeManager.Instance.GetSelectedCube();
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
    }
    
    /// <summary>
    /// Smoothly rotate a cube by the specified degrees around Y axis
    /// </summary>
    private System.Collections.IEnumerator SmoothRotateCube(PowerCube cube, float degrees, RotationAxis axis)
    {
        if (cube == null) yield break;
        
        isRotating = true; // Block further rotation input
        
        // Use visual parent for rotation instead of main transform
        Transform visualTransform = cube.visualParent;
        if (visualTransform == null)
        {
            Debug.LogWarning($"[CharacterMovement] Cube {cube.name} has no visual parent for rotation");
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
    /// Called when Q key is pressed via Input System (rotate horizontally clockwise)
    /// </summary>
    private void OnRotateLeft(InputAction.CallbackContext context)
    {
        if (isRotating) return; // Prevent input during rotation
        
        // Q rotates horizontally clockwise (around Y axis, positive direction)
        RotateSelectedCube(90f, RotationAxis.Horizontal);
    }
    
    /// <summary>
    /// Called when E key is pressed via Input System (rotate vertically)
    /// </summary>
    private void OnRotateRight(InputAction.CallbackContext context)
    {
        if (isRotating) return; // Prevent input during rotation
        
        // E rotates vertically (around X axis, positive direction)
        RotateSelectedCube(90f, RotationAxis.Vertical);
    }
    
    /// <summary>
    /// Called when Tab key is pressed via Input System
    /// </summary>
    private void OnTabPressed(InputAction.CallbackContext context)
    {
        if (!isGrounded) return; // Disable selection while jumping/falling
        
        if (CubeManager.Instance != null)
        {
            CubeManager.Instance.CycleSelection();
        }
    }
    
    /// <summary>
    /// Update the push delay timer
    /// </summary>
    private void UpdatePushDelay()
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
    
    private void ApplyGravity()
    {
        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
    
    private void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            Vector3 currentMovement = GetMovementDirection();
            
            if (currentMovement.magnitude < 0.1f)
            {
                // Stationary jump - no horizontal momentum
                jumpMomentum = Vector3.zero;
            }
            else
            {
                // Moving jump - use at least walk speed as minimum, or current buildup if higher 
                float jumpSpeed = Mathf.Max(walkSpeed, currentSpeedBuildup);
                jumpMomentum = currentMovement.normalized * jumpSpeed;
            }
            
            // Apply vertical jump velocity
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            
            // Trigger jump animation
            if (playerAnimator != null)
            {
                playerAnimator.SetJumpingState(true);
            }
        }
    }
    
    private void UpdateAnimations()
    {
        if (playerAnimator != null)
        {
            // Show actual speed buildup in inspector
            currentSpeedVisual = currentSpeedBuildup;
            
            // Keep original animator logic unchanged
            float animSpeed = currentSpeed > 0.1f ? (isSprinting ? 1.0f : 0.5f) : 0f;
            playerAnimator.UpdateMovementAnimation(animSpeed, isGrounded, velocity.y);
        }
    }
}