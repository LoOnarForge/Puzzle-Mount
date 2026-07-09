// ============================================================
// COMMENTED OUT — replaced by RunodeMovement.cs + RunodePower.cs
// Keep this file as reference until the new scripts are confirmed working.
// ============================================================

/*
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
    
    public PowerLineType southFace = PowerLineType.Empty;
    public Color southFaceColor = Color.white;
    public PowerSource southFaceConnectedPS;
    
    public PowerLineType eastFace = PowerLineType.Empty;
    public Color eastFaceColor = Color.white;
    public PowerSource eastFaceConnectedPS;
    
    public PowerLineType westFace = PowerLineType.Empty;
    public Color westFaceColor = Color.white;
    public PowerSource westFaceConnectedPS;

    [Header("RANDOM ROTATION")]
    public bool randomRotateOnStart = true;
    
    [Header("DEBUG SETTINGS")]
    public bool isMoving = false;
    
    #region Inspector References
    
    [Header("FACE TRANSFORM REFERENCES")]
    public Transform topFaceTransform;
    public Transform bottomFaceTransform;
    public Transform northFaceTransform;
    public Transform southFaceTransform;
    public Transform eastFaceTransform;
    public Transform westFaceTransform;
    
    [Header("SPRITE REFERENCES")]
    public Sprite horizontalSprite;
    public Sprite verticalSprite;
    public Sprite cornerSprite;
    public Sprite tSectionSprite;
    public Sprite crossSprite;
    
    [Header("TOP FACE TRIGGERS")]
    public PowerConnectionTrigger topUpTrigger;
    public PowerConnectionTrigger topRightTrigger;
    public PowerConnectionTrigger topDownTrigger;
    public PowerConnectionTrigger topLeftTrigger;
    
    [Header("BOTTOM FACE TRIGGERS")]
    public PowerConnectionTrigger bottomUpTrigger;
    public PowerConnectionTrigger bottomRightTrigger;
    public PowerConnectionTrigger bottomDownTrigger;
    public PowerConnectionTrigger bottomLeftTrigger;
    
    [Header("NORTH FACE TRIGGERS")]
    public PowerConnectionTrigger northUpTrigger;
    public PowerConnectionTrigger northRightTrigger;
    public PowerConnectionTrigger northDownTrigger;
    public PowerConnectionTrigger northLeftTrigger;
    
    [Header("SOUTH FACE TRIGGERS")]
    public PowerConnectionTrigger southUpTrigger;
    public PowerConnectionTrigger southRightTrigger;
    public PowerConnectionTrigger southDownTrigger;
    public PowerConnectionTrigger southLeftTrigger;
    
    [Header("EAST FACE TRIGGERS")]
    public PowerConnectionTrigger eastUpTrigger;
    public PowerConnectionTrigger eastRightTrigger;
    public PowerConnectionTrigger eastDownTrigger;
    public PowerConnectionTrigger eastLeftTrigger;
    
    [Header("WEST FACE TRIGGERS")]
    public PowerConnectionTrigger westUpTrigger;
    public PowerConnectionTrigger westRightTrigger;
    public PowerConnectionTrigger westDownTrigger;
    public PowerConnectionTrigger westLeftTrigger;
    
    #endregion
    
    private Rigidbody rb;
    
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        
        InitializeFaceData();
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
        RunodePower p = GetComponent<RunodePower>();
        PowerManager.Instance.RequestPowerFlowCheck(p);
    }




    //-----------------------------POWER LINES SECTION-----------------------------------------

    [Header("POWER STATE")]
    public bool IsPowered { get; private set; } = false;
    public PowerSource poweredBySource { get; private set; } = null;
    public Color currentPowerColor { get; private set; } = Color.white;
    public int distanceFromSource { get; private set; } = 0;

    public struct FaceData
    {
        public SpriteRenderer faceSprite;
        public PowerConnectionTrigger[] triggers;
        public PowerLineType lineType;
    }

    private FaceData topFaceData;
    private FaceData bottomFaceData;
    private FaceData northFaceData;
    private FaceData southFaceData;
    private FaceData eastFaceData;
    private FaceData westFaceData;

    private void InitializeFaceData()
    {
        topFaceData = CreateFaceData(topFaceTransform, topFace, topUpTrigger, topRightTrigger, topDownTrigger, topLeftTrigger);
        bottomFaceData = CreateFaceData(bottomFaceTransform, bottomFace, bottomUpTrigger, bottomRightTrigger, bottomDownTrigger, bottomLeftTrigger);
        northFaceData = CreateFaceData(northFaceTransform, northFace, northUpTrigger, northRightTrigger, northDownTrigger, northLeftTrigger);
        southFaceData = CreateFaceData(southFaceTransform, southFace, southUpTrigger, southRightTrigger, southDownTrigger, southLeftTrigger);
        eastFaceData = CreateFaceData(eastFaceTransform, eastFace, eastUpTrigger, eastRightTrigger, eastDownTrigger, eastLeftTrigger);
        westFaceData = CreateFaceData(westFaceTransform, westFace, westUpTrigger, westRightTrigger, westDownTrigger, westLeftTrigger);
    }

    private FaceData CreateFaceData(Transform faceTransform, PowerLineType lineType, PowerConnectionTrigger up, PowerConnectionTrigger right, PowerConnectionTrigger down, PowerConnectionTrigger left)
    {
        FaceData data = new FaceData();
        data.lineType = lineType;

        if (faceTransform != null)
        {
            Transform spriteChild = faceTransform.Find("Power Line Sprite");
            if (spriteChild != null)
                data.faceSprite = spriteChild.GetComponent<SpriteRenderer>();
        }

        List<PowerConnectionTrigger> activeTriggers = new List<PowerConnectionTrigger>();

        if (up != null && up.gameObject.activeInHierarchy) activeTriggers.Add(up);
        if (right != null && right.gameObject.activeInHierarchy) activeTriggers.Add(right);
        if (down != null && down.gameObject.activeInHierarchy) activeTriggers.Add(down);
        if (left != null && left.gameObject.activeInHierarchy) activeTriggers.Add(left);

        data.triggers = activeTriggers.ToArray();

        return data;
    }

    /// Called by PowerManager before BFS. Resets this cube to unpowered state.
    public void ClearPowerState()
    {
        bool wasPowered = IsPowered;

        IsPowered = false;
        poweredBySource = null;
        currentPowerColor = Color.white;
        distanceFromSource = 0;

        if (wasPowered)
            ApplyVisualColor(Color.white);
    }

    /// Called by PowerSource BFS when this cube is reached and powered.
    public void SetPowered(PowerSource source, Color color, int distance)
    {
        IsPowered = true;
        poweredBySource = source;
        currentPowerColor = color;
        distanceFromSource = distance;

        ApplyVisualColor(color);
    }

    /// Returns all active triggers across all faces. Used by BFS to continue outward.
    public IEnumerable<PowerConnectionTrigger> GetAllTriggers()
    {
        foreach (PowerConnectionTrigger t in topFaceData.triggers) yield return t;
        foreach (PowerConnectionTrigger t in bottomFaceData.triggers) yield return t;
        foreach (PowerConnectionTrigger t in northFaceData.triggers) yield return t;
        foreach (PowerConnectionTrigger t in southFaceData.triggers) yield return t;
        foreach (PowerConnectionTrigger t in eastFaceData.triggers) yield return t;
        foreach (PowerConnectionTrigger t in westFaceData.triggers) yield return t;
    }

    private void ApplyVisualColor(Color color)
    {
        ApplyFaceColor(topFaceData, color);
        ApplyFaceColor(bottomFaceData, color);
        ApplyFaceColor(northFaceData, color);
        ApplyFaceColor(southFaceData, color);
        ApplyFaceColor(eastFaceData, color);
        ApplyFaceColor(westFaceData, color);
    }

    private void ApplyFaceColor(FaceData face, Color color)
    {
        if (face.faceSprite != null)
            face.faceSprite.color = color;
    }
}

*/