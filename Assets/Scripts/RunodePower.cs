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
    public PowerLineType topFace = PowerLineType.Empty;
    public PowerLineType bottomFace = PowerLineType.Empty;
    public PowerLineType northFace = PowerLineType.Empty;
    public PowerLineType southFace = PowerLineType.Empty;
    public PowerLineType eastFace = PowerLineType.Empty;
    public PowerLineType westFace = PowerLineType.Empty;

    public Transform topFaceTransform;
    public Transform bottomFaceTransform;
    public Transform northFaceTransform;
    public Transform southFaceTransform;
    public Transform eastFaceTransform;
    public Transform westFaceTransform;

    public Sprite horizontalSprite;
    public Sprite verticalSprite;
    public Sprite cornerSprite;
    public Sprite tSectionSprite;
    public Sprite crossSprite;

    public PowerConnectionTrigger topUpTrigger;
    public PowerConnectionTrigger topRightTrigger;
    public PowerConnectionTrigger topDownTrigger;
    public PowerConnectionTrigger topLeftTrigger;

    public PowerConnectionTrigger bottomUpTrigger;
    public PowerConnectionTrigger bottomRightTrigger;
    public PowerConnectionTrigger bottomDownTrigger;
    public PowerConnectionTrigger bottomLeftTrigger;

    public PowerConnectionTrigger northUpTrigger;
    public PowerConnectionTrigger northRightTrigger;
    public PowerConnectionTrigger northDownTrigger;
    public PowerConnectionTrigger northLeftTrigger;

    public PowerConnectionTrigger southUpTrigger;
    public PowerConnectionTrigger southRightTrigger;
    public PowerConnectionTrigger southDownTrigger;
    public PowerConnectionTrigger southLeftTrigger;

    public PowerConnectionTrigger eastUpTrigger;
    public PowerConnectionTrigger eastRightTrigger;
    public PowerConnectionTrigger eastDownTrigger;
    public PowerConnectionTrigger eastLeftTrigger;

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

    private static readonly Dictionary<PowerLineType, bool[]> ConnectivityMap = new Dictionary<PowerLineType, bool[]>
    {
        { PowerLineType.Horizontal,        new[] { false, true,  false, true  } },
        { PowerLineType.Vertical,          new[] { true,  false, true,  false } },
        { PowerLineType.CornerLeftTop,     new[] { true,  false, false, true  } },
        { PowerLineType.CornerTopRight,    new[] { true,  true,  false, false } },
        { PowerLineType.CornerRightBottom, new[] { false, true,  true,  false } },
        { PowerLineType.CornerBottomLeft,  new[] { false, false, true,  true  } },
        { PowerLineType.TSectionLeft,      new[] { true,  true,  false, true  } },
        { PowerLineType.TSectionTop,       new[] { true,  true,  true,  false } },
        { PowerLineType.TSectionRight,     new[] { false, true,  true,  true  } },
        { PowerLineType.TSectionBottom,    new[] { true,  false, true,  true  } },
        { PowerLineType.Cross,             new[] { true,  true,  true,  true  } },
        { PowerLineType.Empty,             new[] { false, false, false, false } }
    };

    private const string POWER_LINE_SPRITE_NAME = "Power Line Sprite";

    private Dictionary<PowerConnectionTrigger, FaceData> triggerToFaceMap = new Dictionary<PowerConnectionTrigger, FaceData>();
    private Dictionary<PowerConnectionTrigger, PowerConnectionTrigger> internalNeighborMap = new Dictionary<PowerConnectionTrigger, PowerConnectionTrigger>();
    private List<FaceData> allFaces = new List<FaceData>();

    public Color currentPowerColor { get; private set; } = Color.white;

    private void Awake()
    {
        InitializeFaceData();
        InitializeInternalNeighborMap();
    }

    private void OnEnable()
    {
        if (PowerManager.Instance != null) PowerManager.Instance.RegisterRunode(this);
    }

    private void OnDisable()
    {
        if (PowerManager.Instance != null) PowerManager.Instance.UnregisterRunode(this);
    }

    private void InitializeInternalNeighborMap()
    {
        internalNeighborMap.Clear();

        // Top Face shared edges
        MapInternal(topUpTrigger,    northUpTrigger);
        MapInternal(topDownTrigger,  southUpTrigger);
        MapInternal(topLeftTrigger,  westUpTrigger);
        MapInternal(topRightTrigger, eastUpTrigger);

        // Bottom Face shared edges
        MapInternal(bottomUpTrigger,    northDownTrigger);
        MapInternal(bottomDownTrigger,  southDownTrigger);
        MapInternal(bottomLeftTrigger,  westDownTrigger);
        MapInternal(bottomRightTrigger, eastDownTrigger);

        // North Face shared edges
        MapInternal(northUpTrigger,    topUpTrigger);
        MapInternal(northDownTrigger,  bottomUpTrigger);
        MapInternal(northLeftTrigger,  eastRightTrigger);
        MapInternal(northRightTrigger, westRightTrigger);

        // South Face shared edges
        MapInternal(southUpTrigger,    topDownTrigger);
        MapInternal(southDownTrigger,  bottomDownTrigger);
        MapInternal(southLeftTrigger,  westLeftTrigger);
        MapInternal(southRightTrigger, eastLeftTrigger);

        // East Face shared edges
        MapInternal(eastUpTrigger,    topRightTrigger);
        MapInternal(eastDownTrigger,  bottomRightTrigger);
        MapInternal(eastLeftTrigger,  southRightTrigger);
        MapInternal(eastRightTrigger, northLeftTrigger);

        // West Face shared edges
        MapInternal(westUpTrigger,    topLeftTrigger);
        MapInternal(westDownTrigger,  bottomLeftTrigger);
        MapInternal(westLeftTrigger,  southLeftTrigger);
        MapInternal(westRightTrigger, northRightTrigger);
    }

    private void MapInternal(PowerConnectionTrigger a, PowerConnectionTrigger b)
    {
        if (a != null && b != null) internalNeighborMap[a] = b;
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
            Transform spriteChild = faceTransform.Find(POWER_LINE_SPRITE_NAME);
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

    public List<PowerConnectionTrigger> GetConnectedTriggersOnFace(PowerConnectionTrigger entry)
    {
        List<PowerConnectionTrigger> connected = new List<PowerConnectionTrigger>();
        if (!triggerToFaceMap.TryGetValue(entry, out FaceData face) || face.obstructionDetector.IsObstructed)
            return connected;

        face.isFacePowered = true; 
        IsPowered = true; 

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
        if (ConnectivityMap.TryGetValue(type, out bool[] connectivity))
        {
            return connectivity;
        }
        return ConnectivityMap[PowerLineType.Empty];
    }

    public void RefreshFaceVisuals(Color color)
    {
        currentPowerColor = color;
        foreach (var face in allFaces)
        {
            ApplyFaceColor(face, face.isFacePowered ? color : Color.white);
        }
    }

    public void MarkFacePowered(PowerConnectionTrigger t)
    {
        if (triggerToFaceMap.TryGetValue(t, out FaceData face))
        {
            face.isFacePowered = true;
        }
    }

    public PowerConnectionTrigger GetInternalNeighbor(PowerConnectionTrigger t)
    {
        if (internalNeighborMap.TryGetValue(t, out PowerConnectionTrigger neighbor))
        {
            return neighbor;
        }
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

    public IEnumerable<PowerConnectionTrigger> GetAllTriggers() { yield break; }
    public void SetPowered(PowerSource source, Color color, int distance) { }
}
