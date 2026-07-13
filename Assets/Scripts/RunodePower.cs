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

    public bool IsPowered => allFaces.Exists(f => f.isFacePowered);

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

    private Dictionary<PowerConnectionTrigger, RunodeFace> triggerToFaceMap = new Dictionary<PowerConnectionTrigger, RunodeFace>();
    private Dictionary<PowerConnectionTrigger, PowerConnectionTrigger> internalNeighborMap = new Dictionary<PowerConnectionTrigger, PowerConnectionTrigger>();
    public List<RunodeFace> allFaces = new List<RunodeFace>();

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

        allFaces.Add(CreateFaceData(topFaceTransform,    topFace,    topUpTrigger,    topRightTrigger,    topDownTrigger,    topLeftTrigger, 0));
        allFaces.Add(CreateFaceData(bottomFaceTransform, bottomFace, bottomUpTrigger, bottomRightTrigger, bottomDownTrigger, bottomLeftTrigger, 1));
        allFaces.Add(CreateFaceData(northFaceTransform,  northFace,  northUpTrigger,  northRightTrigger,  northDownTrigger,  northLeftTrigger, 2));
        allFaces.Add(CreateFaceData(southFaceTransform,  southFace,  southUpTrigger,  southRightTrigger,  southDownTrigger,  southLeftTrigger, 3));
        allFaces.Add(CreateFaceData(eastFaceTransform,   eastFace,   eastUpTrigger,   eastRightTrigger,   eastDownTrigger,   eastLeftTrigger, 4));
        allFaces.Add(CreateFaceData(westFaceTransform,   westFace,   westUpTrigger,   westRightTrigger,   westDownTrigger,   westLeftTrigger, 5));

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

    private RunodeFace CreateFaceData(Transform faceTransform, PowerLineType lineType,
        PowerConnectionTrigger up, PowerConnectionTrigger right,
        PowerConnectionTrigger down, PowerConnectionTrigger left, int index)
    {
        RunodeFace data = new RunodeFace { lineType = lineType, triggers = new[] { up, right, down, left }, faceIndex = index, cube = this };
        
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

    public void ClearPowerState(bool visual = true, PowerSource filterSource = null)
    {
        foreach (var face in allFaces)
        {
            if (filterSource != null && face.poweredBySource != filterSource) continue;

            face.Clear(visual);
        }
    }

    public void ClearFace(int index, bool visual = true)
    {
        if (index < 0 || index >= allFaces.Count) return;
        allFaces[index].Clear(visual);
    }
    
    public List<PowerConnectionTrigger> GetConnectedTriggersOnFace(PowerConnectionTrigger entry)
    {
        List<PowerConnectionTrigger> connected = new List<PowerConnectionTrigger>();
        if (!triggerToFaceMap.TryGetValue(entry, out RunodeFace face))
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
            // By default, refresh calls are not instant to allow for the BFS sequential feel.
            // But we pass through the isFacePowered color to ensure visual matches logic.
            face.ApplyColor(face.isFacePowered ? face.faceColor : Color.white, false);
        }
    }

    /// <summary>
    /// Updates visuals for a specific face. Use for targeted BFS updates.
    /// </summary>
    public void UpdateFaceVisuals(int index, Color color, bool instant = false)
    {
        if (index < 0 || index >= allFaces.Count) return;
        allFaces[index].ApplyColor(color, instant);
    }

    /// <summary>
    /// Forces an immediate visual update of all faces on this cube.
    /// Use for highlighting or editor updates.
    /// </summary>
    public void RefreshFaceVisualsInstant()
    {
        foreach (var face in allFaces)
        {
            face.ApplyColor(face.isFacePowered ? face.faceColor : Color.white, true);
        }
    }

    public bool MarkFacePowered(PowerConnectionTrigger t, Color color, string senderName, RunodeFace sourceFace = null, PowerSource source = null, int distance = 0)
    {
        if (triggerToFaceMap.TryGetValue(t, out RunodeFace targetFace))
        {
            return targetFace.MarkPowered(color, sourceFace, source, distance);
        }
        return true;
    }

    public int GetFaceIndexFromPoint(Vector3 worldPoint)
    {
        // Use the visual parent's coordinate space to identify the physical surface hit, 
        // regardless of the root object's orientation.
        Transform vParent = transform.GetChild(0);
        Vector3 localPoint = vParent.InverseTransformPoint(worldPoint);
        float lx = Mathf.Abs(localPoint.x), ly = Mathf.Abs(localPoint.y), lz = Mathf.Abs(localPoint.z);

        // Order in allFaces: 0:Top, 1:Bottom, 2:North, 3:South, 4:East, 5:West
        if (ly * 1.1f > lx && ly * 1.1f > lz) return localPoint.y > 0 ? 0 : 1;
        if (lx >= lz) return localPoint.x > 0 ? 4 : 5;
        return localPoint.z > 0 ? 2 : 3;
    }

    public Vector3 GetFaceNormal(int index)
    {
        Transform vParent = transform.GetChild(0);
        switch (index)
        {
            case 0: return vParent.up;
            case 1: return -vParent.up;
            case 2: return vParent.forward;
            case 3: return -vParent.forward;
            case 4: return vParent.right;
            case 5: return -vParent.right;
            default: return vParent.up;
        }
    }

    public RunodeFace GetFaceData(PowerConnectionTrigger t)
    {
        triggerToFaceMap.TryGetValue(t, out RunodeFace face);
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

    public IEnumerable<PowerConnectionTrigger> GetAllTriggers() { yield break; }
    public void SetPowered(PowerSource source, Color color, int distance) { }
}
