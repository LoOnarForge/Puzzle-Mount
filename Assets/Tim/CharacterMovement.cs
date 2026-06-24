using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CharacterMovement : MonoBehaviour
{
    [Header("MOVEMENT SETTINGS")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float jumpHeight = 2f;
    public float rotationSpeed = 10f;
    
    [Header("SPEED BUILDUP")]
    public float accelerationTime = 0.3f;
    
    [Header("AIR CONTROL")]
    public float momentumDecay = 2f;
    
    [Header("DEBUG - SPEED VISUALIZATION")]
    [SerializeField] private float currentSpeedVisual;
    
    // Physics settings
    public float gravity = -20f;
    
    private CharacterController controller;
    private PlayerInput playerInput;
    private PlayerAnimator playerAnimator;
    private TimCubeInteraction cubeInteraction;
    private bool isMovementEnabled = true;
    public void SetMovementEnabled(bool enabled) { isMovementEnabled = enabled; }

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
        if (Time.timeScale == 0) return;

        CheckGroundStatus();
        ReadInput();

        if (isMovementEnabled)
        {
            HandleMovement();
        }
        else
        {
            // Apply neutral movement to keep physics stable
            if (isGrounded)
            {
                velocity.x = 0;
                velocity.z = 0;
            }
        }

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
            // If jump was NOT triggered (velocity.y <= 0), it's a walk-off
            if (velocity.y <= 0.1f)
            {
                Vector3 groundDir = Vector3.zero;
                if (cameraFollow != null)
                {
                    groundDir = GetMovementDirectionForCameraAngle(cameraFollow.CurrentAngleIndex);
                }
                else
                {
                    groundDir = new Vector3(moveInput.x, 0, moveInput.y);
                }

                if (groundDir.magnitude > 0.1f)
                {
                    jumpMomentum = groundDir.normalized * currentSpeedBuildup;
                }
            }
        }
        
        // Reset jump momentum and jumping animation when landing
        if (isGrounded && !wasGroundedLastFrame)
        {
            jumpMomentum = Vector3.zero;
            
            if (playerAnimator != null)
            {
                playerAnimator.SetJumpingState(false);
            }
        }

        // Apply momentum decay while airborne
        if (!isGrounded && jumpMomentum.magnitude > 0.01f)
        {
            jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, momentumDecay * Time.deltaTime);
        }
    }

    private Vector3 GetMovementDirectionInternal()
    {
        if (isGrounded)
        {
            if (cameraFollow != null)
            {
                return GetMovementDirectionForCameraAngle(cameraFollow.CurrentAngleIndex);
            }
            return new Vector3(moveInput.x, 0, moveInput.y);
        }
        else
        {
            return jumpMomentum;
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
        
        if (!isGrounded && jumpMomentum.magnitude > 0.1f)
        {
            Vector3 movement = moveDirection; 
            controller.Move(movement * Time.deltaTime);
            currentSpeed = moveDirection.magnitude;
        }
        else
        {
            currentSpeed = CalculateMovementSpeed(shouldRun);
            Vector3 movement = moveDirection * currentSpeed;
            controller.Move(movement * Time.deltaTime);
        }
        
        if (moveDirection != Vector3.zero)
        {
            RotatePlayer(moveDirection);
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
        
        // Use moveInput magnitude for better analog control and precision
        float targetSpeed = (shouldRun ? runSpeed : walkSpeed) * moveInput.magnitude;
        
        // Start acceleration from zero (removed startSpeed snap)
        
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
                // Moving jump - use current buildup speed for the jump arc
                float jumpSpeed = currentSpeedBuildup;
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