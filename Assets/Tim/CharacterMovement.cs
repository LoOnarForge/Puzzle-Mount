using UnityEngine;
using UnityEngine.InputSystem;

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
    
    // Hidden physics settings
    private float gravity = -20f;
    
    private CharacterController controller;
    private PlayerInput playerInput;
    private PlayerAnimator playerAnimator;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    
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
    }
    
    private void OnEnable()
    {
        if (jumpAction != null)
            jumpAction.performed += OnJump;
    }
    
    private void OnDisable()
    {
        if (jumpAction != null)
            jumpAction.performed -= OnJump;
    }
    
    private void Update()
    {
        CheckGroundStatus();
        ReadInput();
        HandleMovement();
        ApplyGravity();
        UpdateAnimations();
    }
    
    private void CheckGroundStatus()
    {
        wasGroundedLastFrame = isGrounded;
        isGrounded = controller.isGrounded;
        
        // Capture momentum when walking off edge (grounded → airborne)
        if (!isGrounded && wasGroundedLastFrame)
        {
            Vector3 currentMovement = new Vector3(moveInput.x, 0, moveInput.y);
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
                CheckCubePushing(moveDirection);
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
            return inputDirection;
        }
        else
        {
            // Use stored jump momentum when airborne
            // Don't normalize here - keep the magnitude intact
            jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, momentumDecay * Time.deltaTime);
            return jumpMomentum; // Return full vector with magnitude
        }
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
    /// Check if Tim is trying to push a cube and attempt to push it
    /// Only pushes when Tim is properly aligned and approaching from the front
    /// </summary>
    private void CheckCubePushing(Vector3 moveDirection)
    {
        // Cast multiple rays at different heights to catch cubes at various elevations
        Vector3 rayDirection = moveDirection.normalized;
        
        // Try raycasts at different heights
        Vector3[] rayStartPositions = {
            transform.position + Vector3.up * 0.5f,  // Tim's center
            transform.position + Vector3.up * 1.0f,  // Tim's chest height
            transform.position + Vector3.up * 1.5f   // Tim's head height
        };
        
        RaycastHit hit;
        
        foreach (Vector3 rayStart in rayStartPositions)
        {
            if (Physics.Raycast(rayStart, rayDirection, out hit, pushRange))
            {
                Cube cube = hit.collider.GetComponent<Cube>();
                if (cube != null)
                {
                    // Check if Tim is properly aligned to push this cube
                    if (IsProperlyAlignedToPush(cube.transform, rayDirection))
                    {
                        // Try to push the cube in Tim's movement direction
                        cube.TryPush(rayDirection);
                        return; // Exit after first successful push attempt
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
            Vector3 currentMovement = new Vector3(moveInput.x, 0, moveInput.y);
            
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