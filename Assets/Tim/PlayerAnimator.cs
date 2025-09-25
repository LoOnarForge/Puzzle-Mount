using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("Animation Parameters")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string groundedParameter = "IsGrounded";
    [SerializeField] private string fallingParameter = "IsFalling";
    [SerializeField] private string jumpingParameter = "IsJumping";
    
    [Header("Animation Settings")]
    [SerializeField] private float animationSmoothTime = 0.1f;
    [SerializeField] private float fallDetectionDelay = 0.2f;
    [SerializeField] private float minimumFallSpeed = 3f;
    
    [Header("Debug - Fall Timer Visualization")]
    [SerializeField] private float fallTimerVisual;
    [SerializeField] private bool isCurrentlyFalling;
    
    private Animator animator;
    private float currentAnimatedSpeed;
    private float fallTimer;
    private bool wasGroundedLastFrame;
    
    private void Awake()
    {
        animator = GetComponent<Animator>();
        
        if (!animator)
        {
            Debug.LogError($"Animator component missing on {gameObject.name}!");
            enabled = false;
            return;
        }
        
        ValidateAnimationParameters();
    }
    
    private void ValidateAnimationParameters()
    {
        if (!HasParameter(speedParameter))
            Debug.LogWarning($"Animation parameter '{speedParameter}' not found in Animator Controller!");
        
        if (!HasParameter(groundedParameter))
            Debug.LogWarning($"Animation parameter '{groundedParameter}' not found in Animator Controller!");
        
        if (!HasParameter(fallingParameter))
            Debug.LogWarning($"Animation parameter '{fallingParameter}' not found in Animator Controller!");
            
        if (!HasParameter(jumpingParameter))
            Debug.LogWarning($"Animation parameter '{jumpingParameter}' not found in Animator Controller!");
    }
    
    private bool HasParameter(string paramName)
    {
        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (param.name == paramName)
                return true;
        }
        return false;
    }
    
    public void SetJumpingState(bool isJumping)
    {
        if (HasParameter(jumpingParameter))
        {
            animator.SetBool(jumpingParameter, isJumping);
        }
    }
    
    public void SetFallingState(bool isFalling)
    {
        if (HasParameter(fallingParameter))
        {
            animator.SetBool(fallingParameter, isFalling);
        }
    }
    
    public void UpdateMovementAnimation(float targetSpeed, bool isGrounded, float verticalVelocity = 0f)
    {
        UpdateSpeedAnimation(targetSpeed);
        UpdateGroundedAnimation(isGrounded, verticalVelocity);
    }
    
    private void UpdateSpeedAnimation(float targetSpeed)
    {
        currentAnimatedSpeed = Mathf.Lerp(currentAnimatedSpeed, targetSpeed, animationSmoothTime);
        
        if (HasParameter(speedParameter))
        {
            animator.SetFloat(speedParameter, currentAnimatedSpeed);
        }
    }
    
    private void UpdateGroundedAnimation(bool isGrounded, float verticalVelocity = 0f)
    {
        if (HasParameter(groundedParameter))
        {
            animator.SetBool(groundedParameter, isGrounded);
        }
        
        HandleFallingAnimation(isGrounded, verticalVelocity);
        wasGroundedLastFrame = isGrounded;
    }
    
    private void HandleFallingAnimation(bool isGrounded, float verticalVelocity = 0f)
    {
        if (!HasParameter(fallingParameter)) return;
        
        if (!isGrounded && wasGroundedLastFrame)
        {
            fallTimer = 0f;
        }
        
        if (!isGrounded)
        {
            fallTimer += Time.deltaTime;
            
            // Show timer and conditions in inspector
            fallTimerVisual = fallTimer;
            
            // Only trigger falling if we've been airborne long enough AND falling fast enough
            bool fallingFastEnough = verticalVelocity < -minimumFallSpeed;
            bool longEnoughAirtime = fallTimer >= fallDetectionDelay;
            
            if (longEnoughAirtime && fallingFastEnough)
            {
                isCurrentlyFalling = true;
                animator.SetBool(fallingParameter, true);
                
                // Reset jumping when falling starts
                if (HasParameter(jumpingParameter))
                {
                    animator.SetBool(jumpingParameter, false);
                }
            }
            else
            {
                isCurrentlyFalling = false;
            }
        }
        else
        {
            // Reset everything when grounded
            fallTimerVisual = 0f;
            isCurrentlyFalling = false;
            animator.SetBool(fallingParameter, false);
            fallTimer = 0f;
        }
    }
}