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
        
        // Use stored jump momentum speed when airborne
        if (!isGrounded && jumpMomentum.magnitude > 0.1f)
        {
            currentSpeed = jumpMomentum.magnitude;
        }
        else
        {
            currentSpeed = CalculateMovementSpeed(shouldRun);
        }
        
        Vector3 movement = moveDirection * currentSpeed;
        controller.Move(movement * Time.deltaTime);
        
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
            jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, momentumDecay * Time.deltaTime);
            return jumpMomentum.normalized;
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
            // Store current horizontal movement as jump momentum
            Vector3 currentMovement = new Vector3(moveInput.x, 0, moveInput.y);
            bool shouldRun = isSprinting;
            
            // Use current built-up speed for jump distance
            float jumpSpeed = currentSpeedBuildup;
            
            jumpMomentum = currentMovement.normalized * jumpSpeed;
            
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