using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("Animation Parameters")]
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string groundedParameter = "IsGrounded";
    [SerializeField] private string fallingParameter = "IsFalling";
    
    [Header("Animation Settings")]
    [SerializeField] private float animationSmoothTime = 0.1f;
    [SerializeField] private float fallDetectionDelay = 0.2f;
    
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
    
    public void UpdateMovementAnimation(float targetSpeed, bool isGrounded)
    {
        UpdateSpeedAnimation(targetSpeed);
        UpdateGroundedAnimation(isGrounded);
    }
    
    private void UpdateSpeedAnimation(float targetSpeed)
    {
        currentAnimatedSpeed = Mathf.Lerp(currentAnimatedSpeed, targetSpeed, animationSmoothTime);
        
        if (HasParameter(speedParameter))
        {
            animator.SetFloat(speedParameter, currentAnimatedSpeed);
        }
    }
    
    private void UpdateGroundedAnimation(bool isGrounded)
    {
        if (HasParameter(groundedParameter))
        {
            animator.SetBool(groundedParameter, isGrounded);
        }
        
        HandleFallingAnimation(isGrounded);
        wasGroundedLastFrame = isGrounded;
    }
    
    private void HandleFallingAnimation(bool isGrounded)
    {
        if (!HasParameter(fallingParameter)) return;
        
        if (!isGrounded && wasGroundedLastFrame)
        {
            fallTimer = 0f;
        }
        
        if (!isGrounded)
        {
            fallTimer += Time.deltaTime;
            if (fallTimer >= fallDetectionDelay)
            {
                animator.SetBool(fallingParameter, true);
            }
        }
        else
        {
            animator.SetBool(fallingParameter, false);
            fallTimer = 0f;
        }
    }
    
    public void PlayTriggerAnimation(string triggerName)
    {
        if (HasParameter(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
        else
        {
            Debug.LogWarning($"Trigger '{triggerName}' not found in Animator Controller!");
        }
    }
    
    public bool IsAnimationPlaying(string animationName)
    {
        return animator.GetCurrentAnimatorStateInfo(0).IsName(animationName);
    }
    
    public float GetAnimationProgress()
    {
        return animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
    }
}