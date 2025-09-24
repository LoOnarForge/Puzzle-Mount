using UnityEngine;

public class PushableBlock : MonoBehaviour
{
    [Header("Pushable Block Settings")]
    public float pushForce = 5f;
    public bool snapToGrid = true;
    public LayerMask groundLayers = -1;
    
    private Rigidbody rb;
    private Vector3 lastPosition;
    private bool isBeingPushed = false;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // Set up proper physics settings for fun gameplay
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.None; // Allow rotation and movement
        rb.linearDamping = 0.2f;   // Low drag for smooth movement
        rb.angularDamping = 0.5f;  // Moderate rotation damping
        rb.mass = 2f;              // Good mass for pushing
        
        lastPosition = transform.position;
        
        // Make sure we're on the Pushable layer
        gameObject.layer = 6; // Pushable layer
    }
    
    void Update()
    {
        // Check if block stopped moving and snap to grid
        if (snapToGrid && !isBeingPushed)
        {
            if (Vector3.Distance(transform.position, lastPosition) < 0.01f)
            {
                SnapToGrid();
            }
        }
        
        lastPosition = transform.position;
        isBeingPushed = false;
    }
    
    public void Push(Vector3 direction, float force)
    {
        if (rb != null)
        {
            rb.AddForce(direction * force, ForceMode.Force);
            isBeingPushed = true;
        }
    }
    
    void SnapToGrid()
    {
        Vector3 snapPos = new Vector3(
            Mathf.Round(transform.position.x),
            transform.position.y, // Don't snap Y to allow stacking
            Mathf.Round(transform.position.z)
        );
        
        // Only snap if the difference is small to avoid teleporting
        if (Vector3.Distance(transform.position, snapPos) < 0.3f)
        {
            transform.position = snapPos;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        // Stop sliding on collision
        if (rb != null && collision.gameObject.layer == 5) // Ground layer
        {
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Show grid position when selected
        Gizmos.color = Color.yellow;
        Vector3 gridPos = new Vector3(
            Mathf.Round(transform.position.x),
            transform.position.y,
            Mathf.Round(transform.position.z)
        );
        Gizmos.DrawWireCube(gridPos, Vector3.one);
    }
}