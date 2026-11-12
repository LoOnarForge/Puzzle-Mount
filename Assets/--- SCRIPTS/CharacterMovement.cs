using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(50)]
[RequireComponent(typeof(CharacterController))]
public partial class CharacterMovement : MonoBehaviour
{
    [Header("MOVEMENT SETTINGS")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float jumpHeight = 2f;
    public float rotationSpeed = 10f;
    
    [Header("SPEED BUILDUP")]
    public float accelerationTime = 0.3f;
    [Range(0.5f, 10f)]
    public float speedRampRate = 5f;
    
    [Header("AIR CONTROL")]
    public float momentumDecay = 2f;

    [Header("JUMP")]
    [SerializeField] private bool isUsingMomentum = true;
    [SerializeField] [Range(0f, 100f)] private float jumpPlatformMovementPercent = 35f;
    
    [Header("DEBUG - SPEED VISUALIZATION")]
    [SerializeField] private float currentSpeedVisual;

    [Header("DEATH TOSS")]
    [SerializeField] private float deathForceMultiplier = 1f;

    [SerializeField] [Range(0f, 5f)] private float maxAirborneTimeBeforeDeath;

    private const float DeathTossStrength = 8f;
    private const float DeathTossUpwardRatio = 0.5f;
    private const float SprintTapThreshold = 0.2f;
    private const float WithPlatformJumpMomentumScale = 1f;
    private const float SidewaysPlatformJumpMomentumScale = 0.1f;
    private const float AgainstPlatformJumpMomentumScale = 0.2f;
    private const float PlatformJumpDirectionAlignmentThreshold = 0.707106781f;
    
    [HideInInspector] public float gravity = -20f;
    
    private CharacterController controller;
    private PlayerInput playerInput;
    private PlayerAnimator playerAnimator;
    private TimCubeController cubeInteraction;
    private Animator animator;
    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;
    private Rigidbody pelvisRigidbody;
    private bool isMovementEnabled = true;
    private bool isDead;

    public bool IsDead => isDead;
    public Transform CameraFollowTarget => isDead && pelvisRigidbody != null ? pelvisRigidbody.transform : transform;
    public void SetMovementEnabled(bool enabled) { isMovementEnabled = enabled; }

    private CameraFollow cameraFollow;
    private MenuManager menuManager;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    
    private Vector2 moveInput;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isSprinting;
    private bool sprintToggledOn;
    private bool sprintPressActive;
    private float sprintPressStartTime;
    private float currentSpeed;
    
    // Air control variables
    private Vector3 jumpMomentum;
    private Vector3 jumpFacingDirection;
    private bool wasGroundedLastFrame;
    
    // Speed buildup variables
    private float currentSpeedBuildup;

    public bool IsAirborneDeathPending => airborneDeathPending;
    public bool IsGrounded => isGrounded;
    public float CurrentSpeed => currentSpeed;
    public bool IsStationary => moveInput.magnitude < 0.1f;
    public bool IsMoving => currentSpeed > 0.05f;
    
    // Public access for TimCubeInteraction
    public Vector3 GetMovementDirectionExternal() => GetMovementDirectionInternal();
    
    private void OnValidate()
    {
        maxAirborneTimeBeforeDeath = Mathf.Clamp(Mathf.Round(maxAirborneTimeBeforeDeath * 10f) / 10f, 0f, 5f);
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        playerAnimator = GetComponent<PlayerAnimator>();
        cubeInteraction = GetComponent<TimCubeController>();
        animator = GetComponent<Animator>();
        cameraFollow = FindAnyObjectByType<CameraFollow>();
        menuManager = FindAnyObjectByType<MenuManager>();
        CacheRagdollParts();
        
        // Fix PlayerInput notification behavior
        if (playerInput != null)
        {
            playerInput.notificationBehavior = UnityEngine.InputSystem.PlayerNotifications.InvokeUnityEvents;
        }
        
        SetupInputActions();
        InitGroundProbe();
    }

    // Turns Tim into a ragdoll and flings him away from the hit direction.
    public void ApplyDeathToss(Vector3 hitDirection, float forceMultiplierOverride = 0f)
    {
        if (isDead)
            return;

        isDead = true;
        SetMovementEnabled(false);

        if (cubeInteraction != null)
            cubeInteraction.enabled = false;

        if (playerInput != null)
            playerInput.enabled = false;

        if (controller != null)
            controller.enabled = false;

        if (animator != null)
            animator.enabled = false;

        foreach (Rigidbody ragdollBody in ragdollRigidbodies)
        {
            ragdollBody.isKinematic = false;
            ragdollBody.WakeUp();
        }

        foreach (Collider ragdollCollider in ragdollColliders)
            ragdollCollider.enabled = true;

        float multiplier = forceMultiplierOverride > 0f ? forceMultiplierOverride : deathForceMultiplier;
        Vector3 tossDirection = -hitDirection;
        tossDirection.y = 0f;

        if (tossDirection.sqrMagnitude < 0.01f)
            tossDirection = -transform.forward;

        tossDirection.Normalize();

        Vector3 velocityChange =
            tossDirection * DeathTossStrength * multiplier +
            Vector3.up * DeathTossStrength * DeathTossUpwardRatio * multiplier;

        Rigidbody tossBody = pelvisRigidbody != null ? pelvisRigidbody : GetPelvisFallback();
        if (tossBody != null)
            tossBody.linearVelocity = velocityChange;
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
        if (Time.timeScale == 0 || isDead)
            return;

        MovingPlatform.ApplyCarriedMotionToTim(this, controller);

        wasGroundedLastFrame = isGrounded;
        ReadInput();

        if (isMovementEnabled)
        {
            HandleMovement();
        }
        else
        {
            // Reset speed variables to ensure natural transition to idle
            currentSpeed = 0f;
            currentSpeedBuildup = 0f;

            // Apply neutral movement to keep physics stable
            if (isGrounded)
            {
                velocity.x = 0;
                velocity.z = 0;
            }
        }

        ApplyGravity();
        RefreshGroundedState();
        TickAirborneDeathTimer();
        UpdateAnimations();
    }

    private Vector3 GetMovementDirectionInternal()
    {
        if (isGrounded)
        {
            if (cameraFollow != null)
            {
                return cameraFollow.GetMovementDirectionForCameraAngle(moveInput);
            }
            return new Vector3(moveInput.x, 0, moveInput.y);
        }
        else
        {
            if (!isUsingMomentum)
                return Vector3.zero;

            return jumpMomentum;
        }
    }
    private void ReadInput()
    {
        if (moveAction != null)
            moveInput = moveAction.ReadValue<Vector2>();
        
        HandleSprintInput();
    }

    private void HandleSprintInput()
    {
        if (sprintAction == null) return;

        if (sprintAction.WasPressedThisFrame())
        {
            sprintPressStartTime = Time.time;
            sprintPressActive = true;
        }

        if (sprintAction.WasReleasedThisFrame() && sprintPressActive)
        {
            float pressDuration = Time.time - sprintPressStartTime;

            if (pressDuration <= SprintTapThreshold)
            {
                sprintToggledOn = !sprintToggledOn;
                if (menuManager != null)
                    menuManager.SetSprintToggle(sprintToggledOn);
            }
            else if (sprintToggledOn)
            {
                sprintToggledOn = false;
                if (menuManager != null)
                    menuManager.SetSprintToggle(false);
            }

            sprintPressActive = false;
        }

        isSprinting = sprintToggledOn || sprintAction.IsPressed();
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
            CaptureSlipperyCoastWhileMoving(shouldRun, moveDirection, currentSpeed);
            Vector3 movement = moveDirection * currentSpeed;
            controller.Move(movement * Time.deltaTime);
            HandleSlipperySurfaceMovement();
        }
        
        Vector3 facingDirection = GetFacingDirectionForMovement(moveDirection);
        if (facingDirection != Vector3.zero)
            RotatePlayer(facingDirection);
    }

    
    private float CalculateMovementSpeed(bool shouldRun)
    {
        if (moveInput.magnitude < 0.1f)
        {
            currentSpeedBuildup = 0f;
            return 0f;
        }
        
        // Use moveInput magnitude for better analog control and precision
        float targetSpeed = (shouldRun ? GetTimFloorRunSpeed() : GetTimFloorWalkSpeed()) * moveInput.magnitude;
        
        // Start acceleration from zero (removed startSpeed snap)
        
        // Exponential buildup to max speed (running ramps twice as fast)
        float rampRate = shouldRun ? speedRampRate * 2f : speedRampRate;
        float lerpRate = Time.deltaTime / accelerationTime * rampRate;
        currentSpeedBuildup = Mathf.Lerp(currentSpeedBuildup, targetSpeed, lerpRate);

        return currentSpeedBuildup;
    }
    
    private static float GetPlatformJumpMomentumScale(Vector3 jumpDirection, Vector3 platformVelocity)
    {
        Vector3 platformHorizontal = platformVelocity;
        platformHorizontal.y = 0f;

        if (platformHorizontal.sqrMagnitude <= 0.0001f)
            return 0f;

        platformHorizontal.Normalize();
        float alignment = Vector3.Dot(jumpDirection, platformHorizontal);

        if (alignment >= PlatformJumpDirectionAlignmentThreshold)
            return WithPlatformJumpMomentumScale;

        if (alignment <= -PlatformJumpDirectionAlignmentThreshold)
            return AgainstPlatformJumpMomentumScale;

        return SidewaysPlatformJumpMomentumScale;
    }

    private Vector3 GetFacingDirectionForMovement(Vector3 moveDirection)
    {
        if (isJumping && jumpFacingDirection.sqrMagnitude > 0.01f)
            return jumpFacingDirection;

        return moveDirection;
    }

    private void RotatePlayer(Vector3 direction)
    {
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
    
    private void ApplyGravity()
    {
        if (isGrounded && !isJumping && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
    
    private void OnJump(InputAction.CallbackContext context)
    {
        if (!isMovementEnabled) return;

        if (CanJumpNow())
        {
            BeginJump();
            Vector3 currentMovement = GetMovementDirectionInternal();
            
            if (currentMovement.magnitude < 0.1f)
            {
                jumpFacingDirection = Vector3.zero;
            }
            else
            {
                jumpFacingDirection = currentMovement;
                jumpFacingDirection.y = 0f;
                jumpFacingDirection.Normalize();
            }

            jumpMomentum = Vector3.zero;
            if (isUsingMomentum && jumpFacingDirection.sqrMagnitude > 0.01f)
            {
                jumpMomentum = jumpFacingDirection * currentSpeedBuildup;

                if (jumpPlatformMovementPercent > 0f
                    && MovingPlatform.TryGetPlatformVelocityForJump(this, out Vector3 platformVelocity))
                {
                    float directionScale = GetPlatformJumpMomentumScale(jumpFacingDirection, platformVelocity);
                    float appliedPercent = jumpPlatformMovementPercent * 0.01f * directionScale;
                    jumpMomentum += platformVelocity * appliedPercent;
                }
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
            float animSpeed = 0f;
            if (currentSpeed > 0.1f)
                animSpeed = isSprinting ? 1.0f : 0.5f;
            else if (isJumping || (!isGrounded && velocity.y > 0f))
                animSpeed = 0.5f;

            playerAnimator.UpdateMovementAnimation(animSpeed, isGrounded, velocity.y);
        }
    }

    private void CacheRagdollParts()
    {
        Rigidbody[] allRigidbodies = GetComponentsInChildren<Rigidbody>();
        List<Rigidbody> ragdollBodies = new List<Rigidbody>();
        List<Collider> ragdollBodyColliders = new List<Collider>();

        foreach (Rigidbody body in allRigidbodies)
        {
            if (body.gameObject == gameObject)
                continue;

            ragdollBodies.Add(body);

            Collider bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
                ragdollBodyColliders.Add(bodyCollider);
            }
        }

        ragdollRigidbodies = ragdollBodies.ToArray();
        ragdollColliders = ragdollBodyColliders.ToArray();

        Transform pelvis = transform.Find("Armature/Root_M");
        if (pelvis != null)
            pelvisRigidbody = pelvis.GetComponent<Rigidbody>();
    }

    private Rigidbody GetPelvisFallback()
    {
        if (ragdollRigidbodies == null || ragdollRigidbodies.Length == 0)
            return null;

        foreach (Rigidbody body in ragdollRigidbodies)
        {
            if (body.name == "Root_M")
                return body;
        }

        return ragdollRigidbodies[0];
    }
}