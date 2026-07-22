using UnityEngine;

public class SimplePusher : MonoBehaviour
{
    [Header("Push Settings")]
    public float pushForce = 500f;
    public float pushRange = 2f;
    public LayerMask pushableLayers = -1;
    
    private Camera playerCamera;
    
    void Start()
    {
        // Find camera
        playerCamera = Camera.main;
        if (playerCamera == null)
        {
            playerCamera = FindFirstObjectByType<Camera>();
        }
    }
    
    void Update()
    {
        // Push with left mouse button or space
        if (UnityEngine.Input.GetMouseButtonDown(0) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
        {
            TryPushBlock();
        }
    }
    
    void TryPushBlock()
    {
        Ray ray;
        
        if (playerCamera != null)
        {
            // Raycast from camera center
            ray = playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        }
        else
        {
            // Fallback: raycast forward from this object
            ray = new Ray(transform.position, transform.forward);
        }
        
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, pushRange, pushableLayers))
        {
            PushableBlock pushable = hit.collider.GetComponent<PushableBlock>();
            if (pushable != null)
            {
                Vector3 pushDirection = (hit.point - ray.origin).normalized;
                pushDirection.y = 0; // Keep push horizontal
                
                Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.AddForce(pushDirection * pushForce, ForceMode.Force);
                    Debug.Log($"Pushed {hit.collider.name} with force {pushForce}");
                }
            }
        }
    }
    
    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.cyan;
        style.fontStyle = FontStyle.Bold;
        
        GUI.Label(new Rect(10, Screen.height - 120, 400, 30), "🖱️ LEFT CLICK or SPACE to push blocks", style);
        GUI.Label(new Rect(10, Screen.height - 100, 400, 30), "🎯 Aim at blocks to push them around", style);
    }
}