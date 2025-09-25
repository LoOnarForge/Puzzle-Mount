using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CharacterMovement_Working : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float jumpHeight = 2f;
    public float rotationSpeed = 10f;
    
    [Header("Physics")]
    public float gravity = -20f;
    
    [Header("Air Control")]
    public bool useCoyoteTime = false;
    public float coyoteTime = 0.2f;
    public float momentumDecay = 2f;
    
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
    private float coyoteTimer;
    private bool wasGroundedLastFrame;
    
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
        
        // Reset jump momentum when landing
        if (isGrounded && !wasGroundedLastFrame)
        {
            jumpMomentum = Vector3.zero;
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
            // Reset coyote timer when grounded
            coyoteTimer = useCoyoteTime ? coyoteTime : 0f;
            return inputDirection;
        }
        else
        {
            // Handle air control based on selected approach
            if (useCoyoteTime)
            {
                return HandleCoyoteTimeAirControl(inputDirection);
            }
            else
            {
                return HandleImmediateAirControl();
            }
        }
    }
    
    private Vector3 HandleImmediateAirControl()
    {
        // Option A: Use stored jump momentum (locked at jump time)
        // Decay momentum over time
        jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, momentumDecay * Time.deltaTime);
        return jumpMomentum.normalized;
    }
    
    private Vector3 HandleCoyoteTimeAirControl(Vector3 inputDirection)
    {
        // Option B: Legacy-style coyote time
        if (coyoteTimer > 0)
        {
            coyoteTimer -= Time.deltaTime;
            return inputDirection; // Still controllable during coyote time
        }
        else
        {
            // Use stored jump momentum after coyote time
            jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, momentumDecay * Time.deltaTime);
            return jumpMomentum.normalized;
        }
    }
    
    private float CalculateMovementSpeed(bool shouldRun)
    {
        if (moveInput.magnitude < 0.1f) return 0f;
        
        return shouldRun ? runSpeed : walkSpeed;
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
            float jumpSpeed = CalculateMovementSpeed(shouldRun);
            
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
            float animSpeed = currentSpeed > 0.1f ? (isSprinting ? 1.0f : 0.5f) : 0f;
            playerAnimator.UpdateMovementAnimation(animSpeed, isGrounded, velocity.y);
        }
    }
}