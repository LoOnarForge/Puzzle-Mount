using System.Collections.Generic;
using UnityEngine;

public enum PowerLineType
{
    Empty,
    [InspectorName("─ Horizontal")]
    Horizontal,
    [InspectorName("│ Vertical")]
    Vertical,
    [InspectorName("────────")]
    Separator1,
    [InspectorName("┘ Corner Left Top")]
    CornerLeftTop,
    [InspectorName("└ Corner Top Right")]
    CornerTopRight,
    [InspectorName("┌ Corner Right Bottom")]
    CornerRightBottom,
    [InspectorName("┐ Corner Bottom Left")]
    CornerBottomLeft,
    [InspectorName("──────────")]
    Separator2,
    [InspectorName("┴ T Section Left")]
    TSectionLeft,
    [InspectorName("├ T Section Top")]
    TSectionTop,
    [InspectorName("┬ T Section Right")]
    TSectionRight,
    [InspectorName("┤ T Section Bottom")]
    TSectionBottom,
    [InspectorName("────────────")]
    Separator3,
    [InspectorName("┼ Cross")]
    Cross
}

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class PowerCube : MonoBehaviour
{
    [Header("MOVEMENT")]
    public float moveSpeed = 5f;
    
    [Header("VISUAL")]
    public Transform visualParent;
    
    public PowerLineType topFace = PowerLineType.Empty;
    public Color topFaceColor = Color.white;
    public PowerSource topFaceConnectedPS;
    
    public PowerLineType bottomFace = PowerLineType.Empty;
    public Color bottomFaceColor = Color.white;
    public PowerSource bottomFaceConnectedPS;
    
    public PowerLineType northFace = PowerLineType.Empty;
    public Color northFaceColor = Color.white;
    public PowerSource northFaceConnectedPS;
    
    public PowerLineType eastFace = PowerLineType.Empty;
    public Color eastFaceColor = Color.white;
    public PowerSource eastFaceConnectedPS;
    
    public PowerLineType southFace = PowerLineType.Empty;
    public Color southFaceColor = Color.white;
    public PowerSource southFaceConnectedPS;
    
    public PowerLineType westFace = PowerLineType.Empty;
    public Color westFaceColor = Color.white;
    public PowerSource westFaceConnectedPS;

    [Header("RANDOM ROTATION")]
    public bool randomRotateOnStart = true;
    
    [Header("DEBUG SETTINGS")]
    public bool isMoving = false;
    
    [HideInInspector] public GameObject horizontalPrefab;
    [HideInInspector] public GameObject verticalPrefab;
    [HideInInspector] public GameObject cornerTopRightPrefab;
    [HideInInspector] public GameObject cornerRightBottomPrefab;
    [HideInInspector] public GameObject cornerBottomLeftPrefab;
    [HideInInspector] public GameObject cornerLeftTopPrefab;
    [HideInInspector] public GameObject tSectionTopPrefab;
    [HideInInspector] public GameObject tSectionRightPrefab;
    [HideInInspector] public GameObject tSectionBottomPrefab;
    [HideInInspector] public GameObject tSectionLeftPrefab;
    [HideInInspector] public GameObject crossPrefab;
    
    private Rigidbody rb;
    
    private bool[] faceConnections = new bool[6];
    private PowerLineState[] faceStates = new PowerLineState[6];
    private Color[] facePowerColors = new Color[6];
    private PowerSource[] faceConnectedSources = new PowerSource[6];
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
    }
    
    private void Start()
    {
        if (randomRotateOnStart)
        {
            ApplyRandomRotation();
        }
        
        SnapToGrid();
    }
    
    private void ApplyRandomRotation()
    {
        float xRot = Random.Range(0, 4) * 90f;
        float yRot = Random.Range(0, 4) * 90f;
        float zRot = Random.Range(0, 4) * 90f;
        
        CubeUnpowered();
        transform.rotation = Quaternion.Euler(xRot, yRot, zRot);
    }
    
    private void SnapToGrid()
    {
        Vector3 currentPos = transform.position;
        Vector3 gridPos = new Vector3
        (
            Mathf.Round(currentPos.x),
            currentPos.y,
            Mathf.Round(currentPos.z)
        );
        
        transform.position = gridPos;
    }
    
    public void RotateVisual(float rotationY)
    {
        if (visualParent != null)
        {
            visualParent.Rotate(0, rotationY, 0);
        }
    }
    
    public Vector3 GetRelativePushDirection(Transform pusherTransform)
    {
        Vector3 relativePosition = transform.position - pusherTransform.position;
        
        if (Mathf.Abs(relativePosition.x) > Mathf.Abs(relativePosition.z))
        {
            return new Vector3(Mathf.Sign(relativePosition.x), 0, 0);
        }
        else
        {
            return new Vector3(0, 0, Mathf.Sign(relativePosition.z));
        }
    }
    
    public bool CanPushSingle(Vector3 direction)
    {
        if (isMoving) return false;
        
        if (!IsGrounded()) return false;
        
        Vector3 pushDir = GetGridDirection(direction);
        if (pushDir == Vector3.zero) return false;
        
        Vector3 targetPosition = transform.position + pushDir;
        return IsPositionClear(targetPosition);
    }
    
    public void PushSingle(Vector3 direction)
    {
        Vector3 pushDir = GetGridDirection(direction);
        Vector3 targetPosition = transform.position + pushDir;
        StartCoroutine(MoveTo(targetPosition, pushDir));
    }
    
    private bool IsGrounded()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        float rayDistance = 0.6f;
        
        bool isGrounded = Physics.Raycast(rayStart, Vector3.down, rayDistance);
        
        return isGrounded;
    }
    
    private Vector3 GetGridDirection(Vector3 direction)
    {
        direction.y = 0;
        direction.Normalize();
        
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            return direction.x > 0 ? Vector3.right : Vector3.left;
        }
        else if (Mathf.Abs(direction.z) > 0.1f)
        {
            return direction.z > 0 ? Vector3.forward : Vector3.back;
        }
        
        return Vector3.zero;
    }
    
    private bool IsPositionClear(Vector3 position)
    {
        Bounds checkBounds = new Bounds(position, Vector3.one * 0.9f);
        Collider[] overlapping = Physics.OverlapBox(checkBounds.center, checkBounds.extents);
        
        foreach (Collider col in overlapping)
        {
            if (col.gameObject == gameObject || col.isTrigger) continue;
            
            PowerCube otherCube = col.GetComponent<PowerCube>();
            if (otherCube != null)
            {
                Vector3 otherPos = otherCube.transform.position;
                Vector3 myPos = transform.position;
                float alignmentTolerance = 0.1f;
                bool isSameColumn = Mathf.Abs(otherPos.x - myPos.x) < alignmentTolerance && 
                                   Mathf.Abs(otherPos.z - myPos.z) < alignmentTolerance;
                
                if (isSameColumn) continue;
            }
            
            return false;
        }
        
        return true;
    }
    
    private System.Collections.IEnumerator MoveTo(Vector3 targetPosition, Vector3 direction)
    {
        isMoving = true;
        CubeUnpowered();
        Vector3 startPos = transform.position;
        
        RigidbodyConstraints oldConstraints = rb.constraints;
        if (direction == Vector3.right || direction == Vector3.left)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        }
        else if (direction == Vector3.forward || direction == Vector3.back)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotation;
        }
        
        targetPosition.y = startPos.y;
        
        float elapsed = 0f;
        float moveTime = 1f / moveSpeed;
        
        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / moveTime);
            
            Vector3 currentPos = Vector3.Lerp(startPos, targetPosition, progress);
            currentPos.y = transform.position.y;
            transform.position = currentPos;
            yield return null;
        }
        
        Vector3 finalPos = targetPosition;
        finalPos.y = transform.position.y;
        transform.position = finalPos;
        
        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        
        isMoving = false;
    }




    //-----------------------------POWER LINES SECTION-----------------------------------------
    

    public void PoweredFromSource(PowerSource powerSource, SpriteRenderer sprite, Transform faceTransform, Color color)
    {
        if (sprite != null)
            sprite.color = color;

        if (faceTransform.name == "Top Face")
        {
            topFaceConnectedPS = powerSource;
            topFaceColor = color;
        }
        else if (faceTransform.name == "Bottom Face")
        {
            bottomFaceConnectedPS = powerSource;
            bottomFaceColor = color;
        }
        else if (faceTransform.name == "North Face")
        {
            northFaceConnectedPS = powerSource;
            northFaceColor = color;
        }
        else if (faceTransform.name == "East Face")
        {
            eastFaceConnectedPS = powerSource;
            eastFaceColor = color;
        }
        else if (faceTransform.name == "South Face")
        {
            southFaceConnectedPS = powerSource;
            southFaceColor = color;
        }
        else if (faceTransform.name == "West Face")
        {
            westFaceConnectedPS = powerSource;
            westFaceColor = color;
        }

        PoweredFromCube(powerSource, faceTransform, color);
    }

    private void PoweredFromCube(PowerSource originalPS, Transform poweredFace, Color powerColor)
    {
        Collider[] faceColliders = poweredFace.GetComponentsInChildren<Collider>();
        
        foreach (Collider trigger in faceColliders)
        {
            if (!trigger.isTrigger) continue;
            
            Collider[] overlapping = Physics.OverlapSphere(trigger.transform.position, trigger.bounds.size.x / 2);
            
            foreach (Collider other in overlapping)
            {
                PowerCube otherCube = other.GetComponentInParent<PowerCube>();
                if (otherCube == null || otherCube == this) continue;
                
                SpriteRenderer sprite = other.transform.parent.GetComponent<SpriteRenderer>();
                Transform faceTransform = other.transform.parent.parent;
                
                otherCube.PoweredFromSource(originalPS, sprite, faceTransform, powerColor);
            }
        }
    }

    public void CubeUnpowered()
    {
        DisconnectFace(ref topFaceConnectedPS, ref topFaceColor, "Top Face");
        DisconnectFace(ref bottomFaceConnectedPS, ref bottomFaceColor, "Bottom Face");
        DisconnectFace(ref northFaceConnectedPS, ref northFaceColor, "North Face");
        DisconnectFace(ref eastFaceConnectedPS, ref eastFaceColor, "East Face");
        DisconnectFace(ref southFaceConnectedPS, ref southFaceColor, "South Face");
        DisconnectFace(ref westFaceConnectedPS, ref westFaceColor, "West Face");
    }

    private void DisconnectFace(ref PowerSource facePS, ref Color faceColor, string faceName)
    {
        if (facePS != null)
        {
            facePS.CubeDisconnected(this.gameObject);
            facePS = null;
            faceColor = Color.white;
            ResetFaceSprite(faceName);
        }
    }

    private void ResetFaceSprite(string faceName)
    {
        Transform faceTransform = transform.Find($"Visual PC/{faceName}");
        if (faceTransform != null && faceTransform.childCount > 0)
        {
            SpriteRenderer sprite = faceTransform.GetChild(0).GetComponent<SpriteRenderer>();
            if (sprite != null)
                sprite.color = Color.white;
        }
    }
}