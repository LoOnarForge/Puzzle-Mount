using UnityEngine;
using UnityEngine.InputSystem;

public class TimController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.5f;
    public float jumpForce = 8f;
    public float groundCheckRadius = 0.3f;
    public LayerMask groundMask = 1;
    
    [Header("Push Settings")]
    public float pushForce = 10f;
    public float pushRange = 2f;
    
    private Rigidbody rb;
    private PlayerInput playerInput;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    
    private Vector2 moveInput;
    private bool isGrounded;
    private bool isSprinting;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerInput = GetComponent<PlayerInput>();
        
        moveAction = playerInput.actions["Move"];
        jumpAction = playerInput.actions["Jump"];
        sprintAction = playerInput.actions["Sprint"];
    }
    
    void OnEnable()
    {
        jumpAction.performed += OnJump;
    }
    
    void OnDisable()
    {
        jumpAction.performed -= OnJump;
    }
    
    void Update()
    {
        moveInput = moveAction.ReadValue<Vector2>();
        isSprinting = sprintAction.IsPressed();
        CheckGrounded();
        
        HandleBlockRotation();
        HandleAutoPush();
    }
    
    void FixedUpdate()
    {
        HandleMovement();
    }
    
    void HandleMovement()
    {
        float currentSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        Vector3 movement = new Vector3(moveInput.x, 0, moveInput.y) * currentSpeed;
        
        Vector3 velocity = rb.linearVelocity;
        velocity.x = movement.x;
        velocity.z = movement.z;
        rb.linearVelocity = velocity;
        
        if (movement.magnitude > 0.1f)
        {
            transform.forward = movement.normalized;
        }
    }
    
    void CheckGrounded()
    {
        isGrounded = Physics.CheckSphere(
            transform.position + Vector3.down * 0.1f, 
            groundCheckRadius, 
            groundMask
        );
    }
    
    void OnJump(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
    
    void HandleBlockRotation()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            RotateBlock(Vector3.up);
        }
        
        if (Input.GetKeyDown(KeyCode.E))
        {
            RotateBlock(Vector3.right);
        }
    }
    
    void RotateBlock(Vector3 axis)
    {
        Vector3 rotateDirection = transform.forward;
        Ray rotateRay = new Ray(transform.position, rotateDirection);
        

    }
    
    void HandleAutoPush()
    {
        if (moveInput.magnitude > 0.1f)
        {
            Vector3 pushDirection = transform.forward;
            Ray pushRay = new Ray(transform.position, pushDirection);
            
     
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + Vector3.down * 0.1f, groundCheckRadius);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * pushRange);
    }
}