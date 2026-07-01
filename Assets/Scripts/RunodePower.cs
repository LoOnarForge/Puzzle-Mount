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

    public ObstructionController obstructionController;

    public bool IsPowered { get; private set; } = false;
    public PowerSource poweredBySource { get; private set; } = null;
    public int distanceFromSource { get; private set; } = 0;

    [System.Serializable]
    public class FaceData
    {
        public SpriteRenderer faceSprite;
        public BoxCollider faceZone;
        public PowerConnectionTrigger[] triggers; // [0]=Up, [1]=Right, [2]=Down, [3]=Left
        public PowerLineType lineType;
        public bool isFacePowered;
        public Color faceColor = Color.white;
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
    public List<FaceData> allFaces = new List<FaceData>();

    public Color currentPowerColor { get; private set; } = Color.white;

    private void Awake()
    {
        if (obstructionController == null) obstructionController = GetComponent<ObstructionController>();
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

        // PHYSICAL MAPPING CALCULATED FROM LOCAL ROTATIONS (001)
        // 1. VERTICAL CORNERS
        MapInternal(northLeftTrigger,  eastRightTrigger); // NE (+X, +Z)
        MapInternal(northRightTrigger, westLeftTrigger);  // NW (-X, +Z)
        MapInternal(southRightTrigger, eastLeftTrigger);  // SE (+X, -Z)
        MapInternal(southLeftTrigger,  westRightTrigger); // SW (-X, -Z)

        // 2. TOP EDGES (Y+)
        MapInternal(topUpTrigger,    northUpTrigger); // North edge of Top face
        MapInternal(topDownTrigger,  southUpTrigger); // South edge of Top face
        MapInternal(topRightTrigger, eastUpTrigger);  // East edge of Top face
        MapInternal(topLeftTrigger,  westUpTrigger);  // West edge of Top face

        // 3. BOTTOM EDGES (Y-)
        MapInternal(bottomDownTrigger, northDownTrigger); // North edge of Bottom face
        MapInternal(bottomUpTrigger,   southDownTrigger); // South edge of Bottom face
        MapInternal(bottomRightTrigger,eastDownTrigger);  // East edge of Bottom face
        MapInternal(bottomLeftTrigger, westDownTrigger);  // West edge of Bottom face

        // Ensure Bi-directional mapping for all entries
        var keys = new List<PowerConnectionTrigger>(internalNeighborMap.Keys);
        foreach (var key in keys)
        {
            var neighbor = internalNeighborMap[key];
            if (neighbor != null) internalNeighborMap[neighbor] = key;
        }
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

        if (obstructionController != null)
        {
            allFaces[0].faceZone = obstructionController.faceTop;
            allFaces[1].faceZone = obstructionController.faceBottom;
            allFaces[2].faceZone = obstructionController.faceNorth;
            allFaces[3].faceZone = obstructionController.faceSouth;
            allFaces[4].faceZone = obstructionController.faceEast;
            allFaces[5].faceZone = obstructionController.faceWest;
        }
    }

    private FaceData CreateFaceData(Transform faceTransform, PowerLineType lineType,
        PowerConnectionTrigger up, PowerConnectionTrigger right,
        PowerConnectionTrigger down, PowerConnectionTrigger left)
    {
        FaceData data = new FaceData { lineType = lineType, triggers = new[] { up, right, down, left } };
        
        if (faceTransform != null)
        {
            // Try to find the sprite child by its name pattern
            foreach (Transform child in faceTransform)
            {
                if (child.name.StartsWith("Power Line Sprite"))
                {
                    data.faceSprite = child.GetComponent<SpriteRenderer>();
                    break;
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
            face.faceColor = Color.white;
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
        if (!triggerToFaceMap.TryGetValue(entry, out FaceData face))
            return connected;

        if (obstructionController != null && obstructionController.IsFaceObstructed(face.faceZone))
        {
            Debug.Log($"[BFS] {name}: Face {face.faceZone?.name ?? "Unknown"} is obstructed. Connection denied.");
            return connected;
        }

        // face.isFacePowered = true; // Controlled by MarkFacePowered
        // IsPowered = true; 

        int entryIndex = System.Array.IndexOf(face.triggers, entry);
        bool[] activeIndices = GetLineConnectivity(face.lineType);

        for (int i = 0; i < face.triggers.Length; i++)
        {
            if (i != entryIndex && activeIndices[i] && face.triggers[i] != null && face.triggers[i].gameObject.activeInHierarchy)
            {
                // Internal pinch check
                if (obstructionController != null && obstructionController.IsInternalPathPinch(entry, face.triggers[i]))
                {
                    Debug.Log($"[BFS] {name}: Internal path on face between {entry.name} and {face.triggers[i].name} is PINCHED.");
                    continue;
                }

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

    public void RefreshFaceVisuals()
    {
        foreach (var face in allFaces)
        {
            ApplyFaceColor(face, face.isFacePowered ? face.faceColor : Color.white);
        }
    }

    public bool MarkFacePowered(PowerConnectionTrigger t, Color color, string senderName, FaceData sourceFace = null)
    {
        if (triggerToFaceMap.TryGetValue(t, out FaceData targetFace))
        {
            // Ignore if we are looking back at the face that just powered us
            if (sourceFace != null && targetFace == sourceFace) return true;

            if (targetFace.isFacePowered)
            {
                Debug.Log("GAME OVER");
                Debug.Log($"Cube {name} caused short circuit by receiving power from {senderName} while already powered");
                return false;
            }

            targetFace.isFacePowered = true;
            targetFace.faceColor = color;
            currentPowerColor = color;
            IsPowered = true;
        }
        return true;
    }

    public FaceData GetFaceData(PowerConnectionTrigger t)
    {
        triggerToFaceMap.TryGetValue(t, out FaceData face);
        return face;
    }

    public PowerConnectionTrigger GetInternalNeighbor(PowerConnectionTrigger t)
    {
        if (internalNeighborMap.TryGetValue(t, out PowerConnectionTrigger neighbor))
        {
            // Internal bridge check for corner wraps
            if (obstructionController != null && obstructionController.IsInternalPathPinch(t, neighbor))
            {
             //   Debug.Log($"[BFS] {name}: Corner wrap bridge between {t.name} and {neighbor.name} is PINCHED.");
                return null;
            }

          //  Debug.Log($"[BFS] {name}: Corner wrap bridge successful: {t.name} -> {neighbor.name}");
            return neighbor;
        }
        return null;
    }

    private void ApplyFaceColor(FaceData face, Color color)
    {
        if (face.faceSprite == null) return;
        
        if (obstructionController != null && obstructionController.IsFaceObstructed(face.faceZone))
        {
            face.faceSprite.color = new Color(0.08f, 0.08f, 0.08f);
            return;
        }
        face.faceSprite.color = color;
    }

    public IEnumerable<PowerConnectionTrigger> GetAllTriggers() { yield break; }
    public void SetPowered(PowerSource source, Color color, int distance) { }
}
