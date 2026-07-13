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
    public PowerLineType topFace    = PowerLineType.Empty;
    public PowerLineType bottomFace = PowerLineType.Empty;
    public PowerLineType northFace  = PowerLineType.Empty;
    public PowerLineType southFace  = PowerLineType.Empty;
    public PowerLineType eastFace   = PowerLineType.Empty;
    public PowerLineType westFace   = PowerLineType.Empty;

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

    public bool IsPowered
    {
        get
        {
            foreach (var face in GetComponentsInChildren<RunodeFace>())
                if (face.isFacePowered) return true;
            return false;
        }
    }

    private Dictionary<PowerConnectionTrigger, PowerConnectionTrigger> internalNeighborMap =
        new Dictionary<PowerConnectionTrigger, PowerConnectionTrigger>();

    private void Awake()
    {
        if (obstructionController == null) obstructionController = GetComponent<ObstructionController>();
        InitializeInternalNeighborMap();
        InitializeFaceComponents();
    }

    private void InitializeFaceComponents()
    {
        AssignFace(topFaceTransform,    0, obstructionController?.faceTop,    topUpTrigger,    topRightTrigger,    topDownTrigger,    topLeftTrigger,    topFace);
        AssignFace(bottomFaceTransform, 1, obstructionController?.faceBottom, bottomUpTrigger, bottomRightTrigger, bottomDownTrigger, bottomLeftTrigger, bottomFace);
        AssignFace(northFaceTransform,  2, obstructionController?.faceNorth,  northUpTrigger,  northRightTrigger,  northDownTrigger,  northLeftTrigger,  northFace);
        AssignFace(southFaceTransform,  3, obstructionController?.faceSouth,  southUpTrigger,  southRightTrigger,  southDownTrigger,  southLeftTrigger,  southFace);
        AssignFace(eastFaceTransform,   4, obstructionController?.faceEast,   eastUpTrigger,   eastRightTrigger,   eastDownTrigger,   eastLeftTrigger,   eastFace);
        AssignFace(westFaceTransform,   5, obstructionController?.faceWest,   westUpTrigger,   westRightTrigger,   westDownTrigger,   westLeftTrigger,   westFace);
    }

    private void AssignFace(Transform t, int index, BoxCollider zone,
        PowerConnectionTrigger up, PowerConnectionTrigger right,
        PowerConnectionTrigger down, PowerConnectionTrigger left,
        PowerLineType type)
    {
        if (t == null) return;
        RunodeFace face = t.GetComponent<RunodeFace>();
        if (face == null) return;
        face.triggers  = new[] { up, right, down, left };
        face.faceIndex = index;
        face.faceZone  = zone;
        face.lineType  = type;
    }

    private void InitializeInternalNeighborMap()
    {
        internalNeighborMap.Clear();

        MapInternal(northLeftTrigger,   eastRightTrigger);
        MapInternal(northRightTrigger,  westLeftTrigger);
        MapInternal(southRightTrigger,  eastLeftTrigger);
        MapInternal(southLeftTrigger,   westRightTrigger);

        MapInternal(topUpTrigger,    northUpTrigger);
        MapInternal(topDownTrigger,  southUpTrigger);
        MapInternal(topRightTrigger, eastUpTrigger);
        MapInternal(topLeftTrigger,  westUpTrigger);

        MapInternal(bottomDownTrigger,  northDownTrigger);
        MapInternal(bottomUpTrigger,    southDownTrigger);
        MapInternal(bottomRightTrigger, eastDownTrigger);
        MapInternal(bottomLeftTrigger,  westDownTrigger);

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

    // Shim for FaceObstructionDetector compatibility.
    public RunodeFace GetFaceData(PowerConnectionTrigger t)
    {
        return t?.parentRunodeFace;
    }

    // Called by RunodeHighlighter to restore power line colors after darkening.
    public void RefreshFaceVisuals()
    {
        foreach (var face in GetComponentsInChildren<RunodeFace>())
            face.ApplyColor(face.isFacePowered ? face.faceColor : Color.white, false);
    }

    public PowerConnectionTrigger GetInternalNeighbor(PowerConnectionTrigger t)
    {
        if (internalNeighborMap.TryGetValue(t, out PowerConnectionTrigger neighbor))
        {
            if (obstructionController != null && obstructionController.IsInternalPathPinch(t, neighbor))
                return null;
            return neighbor;
        }
        return null;
    }

    public int GetFaceIndexFromPoint(Vector3 worldPoint)
    {
        Transform vParent = transform.GetChild(0);
        Vector3 localPoint = vParent.InverseTransformPoint(worldPoint);
        float lx = Mathf.Abs(localPoint.x), ly = Mathf.Abs(localPoint.y), lz = Mathf.Abs(localPoint.z);

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
}
