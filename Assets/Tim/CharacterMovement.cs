using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CharacterMovement : MonoBehaviour
{
    [Header("MOVEMENT SETTINGS")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    [HideInInspector] public float jumpHeight = 2f;
    [HideInInspector] public float rotationSpeed = 10f;
    
    [Header("SPEED BUILDUP")]
    [HideInInspector] public float startSpeedPercent = 40f;
    [HideInInspector] public float accelerationTime = 0.3f;
    
    [Header("AIR CONTROL")]
    public float momentumDecay = 2f;
    
    [Header("DEBUG - SPEED VISUALIZATION")]
    [SerializeField] private float currentSpeedVisual;
    
    // Hidden physics settings
    private float gravity = -20f;
    
    private CharacterController controller;
    private PlayerInput playerInput;
    private PlayerAnimator playerAnimator;
    private TimCubeInteraction cubeInteraction;
    private CameraFollow cameraFollow;
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
    public bool IsStationary => moveInput.magnitude < 0.1f;
    
    // Public access for TimCubeInteraction
    public Vector3 GetMovementDirectionExternal() => GetMovementDirectionInternal();
    
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        playerAnimator = GetComponent<PlayerAnimator>();
        cubeInteraction = GetComponent<TimCubeInteraction>();
        cameraFollow = FindFirstObjectByType<CameraFollow>();
        
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
            Vector3 currentMovement = GetMovementDirectionInternal();
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
        Vector3 moveDirection = GetMovementDirectionInternal();
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
    
    private Vector3 GetMovementDirectionInternal()
    {
        Vector3 inputDirection = new Vector3(moveInput.x, 0, moveInput.y);
        
        if (isGrounded)
        {
            // Get camera angle and calculate movement directions based on preset angles
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
            Vector3 currentMovement = GetMovementDirectionInternal();
            
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