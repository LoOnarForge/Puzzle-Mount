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

public class RunodePower : MonoBehaviour
{
    [Header("TOP FACE")]
    public PowerLineType topFace = PowerLineType.Empty;
    public Color topFaceColor = Color.white;
    public PowerSource topFaceConnectedPS;

    [Header("BOTTOM FACE")]
    public PowerLineType bottomFace = PowerLineType.Empty;
    public Color bottomFaceColor = Color.white;
    public PowerSource bottomFaceConnectedPS;

    [Header("NORTH FACE")]
    public PowerLineType northFace = PowerLineType.Empty;
    public Color northFaceColor = Color.white;
    public PowerSource northFaceConnectedPS;

    [Header("SOUTH FACE")]
    public PowerLineType southFace = PowerLineType.Empty;
    public Color southFaceColor = Color.white;
    public PowerSource southFaceConnectedPS;

    [Header("EAST FACE")]
    public PowerLineType eastFace = PowerLineType.Empty;
    public Color eastFaceColor = Color.white;
    public PowerSource eastFaceConnectedPS;

    [Header("WEST FACE")]
    public PowerLineType westFace = PowerLineType.Empty;
    public Color westFaceColor = Color.white;
    public PowerSource westFaceConnectedPS;

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

    public bool IsPowered { get; private set; } = false;
    public PowerSource poweredBySource { get; private set; } = null;
    public int distanceFromSource { get; private set; } = 0;

    public class FaceData
    {
        public SpriteRenderer faceSprite;
        public FaceObstructionDetector obstructionDetector;
        public PowerConnectionTrigger[] triggers; // [0]=Up, [1]=Right, [2]=Down, [3]=Left
        public PowerLineType lineType;
        public bool isFacePowered;
    }

    private Dictionary<PowerConnectionTrigger, FaceData> triggerToFaceMap = new Dictionary<PowerConnectionTrigger, FaceData>();
    private List<FaceData> allFaces = new List<FaceData>();

    public Color currentPowerColor { get; private set; } = Color.white;

    private void Awake()
    {
        InitializeFaceData();
    }

    private void InitializeFaceData()
    {
        allFaces.Clear();
        triggerToFaceMap.Clear();

        allFaces.Add(CreateFaceData(topFaceTransform,    topFace,    topUpTrigger,    topRightTrigger,    topDownTrigger,    topLeftTrigger));
        allFaces.Add(CreateFaceData(bottomFaceTransform, bottomFace, bottomUpTrigger, bottomRightTrigger, bottomDownTrigger, bottomLeftTrigger));
        allFaces.Add(CreateFaceData(northFaceTransform,  northFace,  northUpTrigger,  northRightTrigger,  northDownTrigger,  northLeftTrigger));
        allFaces.Add(CreateFaceData(southFaceTransform,  southFace,  southUpTrigger,  southRightTrigger,  southDownTrigger,  southLeftTrigger));
        allFaces.Add(CreateFaceData(eastFaceTransform,   eastFace,   eastUpTrigger,   eastRightTrigger,   eastDownTrigger,   eastLeftTrigger));
        allFaces.Add(CreateFaceData(westFaceTransform,   westFace,   westUpTrigger,   westRightTrigger,   westDownTrigger,   westLeftTrigger));
    }

    private FaceData CreateFaceData(Transform faceTransform, PowerLineType lineType,
        PowerConnectionTrigger up, PowerConnectionTrigger right,
        PowerConnectionTrigger down, PowerConnectionTrigger left)
    {
        FaceData data = new FaceData { lineType = lineType, triggers = new[] { up, right, down, left } };
        
        if (faceTransform != null)
        {
            Transform spriteChild = faceTransform.Find("Power Line Sprite");
            if (spriteChild != null)
            {
                data.faceSprite = spriteChild.GetComponent<SpriteRenderer>();
                data.obstructionDetector = spriteChild.GetComponent<FaceObstructionDetector>();

                if (data.obstructionDetector != null)
                {
                    List<PowerConnectionTrigger> activeList = new List<PowerConnectionTrigger>();
                    if (up != null) activeList.Add(up);
                    if (right != null) activeList.Add(right);
                    if (down != null) activeList.Add(down);
                    if (left != null) activeList.Add(left);
                    data.obstructionDetector.Initialize(this, activeList.ToArray());
                }
            }
        }

        foreach (var t in data.triggers)
        {
            if (t != null) triggerToFaceMap[t] = data;
        }
        
        return data;
    }

    /// Resets all face power states and triggers.
    public void ClearPowerState()
    {
        IsPowered = false;
        currentPowerColor = Color.white;
        distanceFromSource = 0;

        foreach (var face in allFaces)
        {
            face.isFacePowered = false;
            foreach (var t in face.triggers)
            {
                if (t != null) t.ClearPowerState();
            }
            ApplyFaceColor(face, Color.white);
        }
    }

    /// Returns other triggers on the same face connected via PowerLineType.
    public List<PowerConnectionTrigger> GetConnectedTriggersOnFace(PowerConnectionTrigger entry)
    {
        List<PowerConnectionTrigger> connected = new List<PowerConnectionTrigger>();
        if (!triggerToFaceMap.TryGetValue(entry, out FaceData face) || face.obstructionDetector.IsObstructed)
            return connected;

        face.isFacePowered = true; 
        IsPowered = true; // Mark cube as touched for 1MW rule

        int entryIndex = System.Array.IndexOf(face.triggers, entry);
        bool[] activeIndices = GetLineConnectivity(face.lineType);

        for (int i = 0; i < face.triggers.Length; i++)
        {
            if (i != entryIndex && activeIndices[i] && face.triggers[i] != null && face.triggers[i].gameObject.activeInHierarchy)
            {
                connected.Add(face.triggers[i]);
            }
        }
        return connected;
    }

    private bool[] GetLineConnectivity(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.Horizontal:        return new[] { false, true,  false, true  };
            case PowerLineType.Vertical:          return new[] { true,  false, true,  false };
            case PowerLineType.CornerLeftTop:     return new[] { true,  false, false, true  };
            case PowerLineType.CornerTopRight:    return new[] { true,  true,  false, false };
            case PowerLineType.CornerRightBottom: return new[] { false, true,  true,  false };
            case PowerLineType.CornerBottomLeft:  return new[] { false, false, true,  true  };
            case PowerLineType.TSectionLeft:      return new[] { true,  true,  false, true  };
            case PowerLineType.TSectionTop:       return new[] { true,  true,  true,  false };
            case PowerLineType.TSectionRight:     return new[] { false, true,  true,  true  };
            case PowerLineType.TSectionBottom:    return new[] { true,  false, true,  true  };
            case PowerLineType.Cross:             return new[] { true,  true,  true,  true  };
            default:                              return new[] { false, false, false, false };
        }
    }

    /// Refreshes visuals for all faces based on their independent power state.
    public void RefreshFaceVisuals(Color color)
    {
        currentPowerColor = color;
        foreach (var face in allFaces)
        {
            ApplyFaceColor(face, face.isFacePowered ? color : Color.white);
        }
    }

    /// Mark a face as powered when a trigger is hit by the BFS.
    public void MarkFacePowered(PowerConnectionTrigger t)
    {
        if (triggerToFaceMap.TryGetValue(t, out FaceData face))
        {
            face.isFacePowered = true;
        }
    }

    /// Explicitly maps triggers that share an edge on the same cube.
    /// Used because triggers on the same Rigidbody do not collide physically.
    public PowerConnectionTrigger GetInternalNeighbor(PowerConnectionTrigger t)
    {
        if (t == topUpTrigger)    return northUpTrigger;
        if (t == topDownTrigger)  return southUpTrigger;
        if (t == topLeftTrigger)  return westUpTrigger;
        if (t == topRightTrigger) return eastUpTrigger;

        if (t == bottomUpTrigger)    return northDownTrigger;
        if (t == bottomDownTrigger)  return southDownTrigger;
        if (t == bottomLeftTrigger)  return westDownTrigger;
        if (t == bottomRightTrigger) return eastDownTrigger;

        if (t == northUpTrigger)    return topUpTrigger;
        if (t == northDownTrigger)  return bottomUpTrigger;
        if (t == northLeftTrigger)  return westRightTrigger;
        if (t == northRightTrigger) return eastLeftTrigger;

        if (t == eastUpTrigger)    return topRightTrigger;
        if (t == eastDownTrigger)  return bottomRightTrigger;
        if (t == eastLeftTrigger)  return northRightTrigger;
        if (t == eastRightTrigger) return southLeftTrigger;

        if (t == southUpTrigger)    return topDownTrigger;
        if (t == southDownTrigger)  return bottomDownTrigger;
        if (t == southLeftTrigger)  return eastRightTrigger;
        if (t == southRightTrigger) return westLeftTrigger;

        if (t == westUpTrigger)    return topLeftTrigger;
        if (t == westDownTrigger)  return bottomLeftTrigger;
        if (t == westLeftTrigger)  return southRightTrigger;
        if (t == westRightTrigger) return northLeftTrigger;

        return null;
    }

    private void ApplyFaceColor(FaceData face, Color color)
    {
        if (face.faceSprite == null) return;
        if (face.obstructionDetector != null && face.obstructionDetector.IsObstructed)
        {
            face.faceSprite.color = new Color(0.08f, 0.08f, 0.08f);
            return;
        }
        face.faceSprite.color = color;
    }

    /// Legacy support for BFS entry.
    public IEnumerable<PowerConnectionTrigger> GetAllTriggers() { yield break; }
    public void SetPowered(PowerSource source, Color color, int distance) { }
}
