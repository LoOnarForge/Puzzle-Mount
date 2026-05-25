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
    [Header("POWER STATE:")]
    public bool IsPowered { get; private set; } = false;
    public PowerSource currentPowerSource { get; private set; } = null;
    public Color currentPowerColor { get; private set; } = Color.white;
    public int distanceFromSource { get; private set; } = 0;
    [Space(30)]

    [Header("TOP FACE:")]
    public PowerLineType topFace = PowerLineType.Empty;
    public Color topFaceColor = Color.white;
    public PowerSource topFaceConnectedPS;

    [Header("BOTTOM FACE:")]
    public PowerLineType bottomFace = PowerLineType.Empty;
    public Color bottomFaceColor = Color.white;
    public PowerSource bottomFaceConnectedPS;

    [Header("NORTH FACE:")]
    public PowerLineType northFace = PowerLineType.Empty;
    public Color northFaceColor = Color.white;
    public PowerSource northFaceConnectedPS;

    [Header("SOUTH FACE:")]
    public PowerLineType southFace = PowerLineType.Empty;
    public Color southFaceColor = Color.white;
    public PowerSource southFaceConnectedPS;

    [Header("EAST FACE:")]
    public PowerLineType eastFace = PowerLineType.Empty;
    public Color eastFaceColor = Color.white;
    public PowerSource eastFaceConnectedPS;

    [Header("WEST FACE:")]
    public PowerLineType westFace = PowerLineType.Empty;
    public Color westFaceColor = Color.white;
    public PowerSource westFaceConnectedPS;

    [Header("FACE TRANSFORM REFERENCES:")]
    public Transform topFaceTransform;
    public Transform bottomFaceTransform;
    public Transform northFaceTransform;
    public Transform southFaceTransform;
    public Transform eastFaceTransform;
    public Transform westFaceTransform;

    [Header("SPRITE REFERENCES:")]
    public Sprite horizontalSprite;
    public Sprite verticalSprite;
    public Sprite cornerSprite;
    public Sprite tSectionSprite;
    public Sprite crossSprite;

    [Header("TOP FACE TRIGGERS:")]
    public PowerConnectionTrigger topUpTrigger;
    public PowerConnectionTrigger topRightTrigger;
    public PowerConnectionTrigger topDownTrigger;
    public PowerConnectionTrigger topLeftTrigger;

    [Header("BOTTOM FACE TRIGGERS:")]
    public PowerConnectionTrigger bottomUpTrigger;
    public PowerConnectionTrigger bottomRightTrigger;
    public PowerConnectionTrigger bottomDownTrigger;
    public PowerConnectionTrigger bottomLeftTrigger;

    [Header("NORTH FACE TRIGGERS:")]
    public PowerConnectionTrigger northUpTrigger;
    public PowerConnectionTrigger northRightTrigger;
    public PowerConnectionTrigger northDownTrigger;
    public PowerConnectionTrigger northLeftTrigger;

    [Header("SOUTH FACE TRIGGERS:")]
    public PowerConnectionTrigger southUpTrigger;
    public PowerConnectionTrigger southRightTrigger;
    public PowerConnectionTrigger southDownTrigger;
    public PowerConnectionTrigger southLeftTrigger;

    [Header("EAST FACE TRIGGERS:")]
    public PowerConnectionTrigger eastUpTrigger;
    public PowerConnectionTrigger eastRightTrigger;
    public PowerConnectionTrigger eastDownTrigger;
    public PowerConnectionTrigger eastLeftTrigger;

    [Header("WEST FACE TRIGGERS:")]
    public PowerConnectionTrigger westUpTrigger;
    public PowerConnectionTrigger westRightTrigger;
    public PowerConnectionTrigger westDownTrigger;
    public PowerConnectionTrigger westLeftTrigger;

   

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

    private void Awake()
    {
        InitializeFaceData();
    }

    private void InitializeFaceData()
    {
        topFaceData    = CreateFaceData(topFaceTransform,    topFace,    topUpTrigger,    topRightTrigger,    topDownTrigger,    topLeftTrigger);
        bottomFaceData = CreateFaceData(bottomFaceTransform, bottomFace, bottomUpTrigger, bottomRightTrigger, bottomDownTrigger, bottomLeftTrigger);
        northFaceData  = CreateFaceData(northFaceTransform,  northFace,  northUpTrigger,  northRightTrigger,  northDownTrigger,  northLeftTrigger);
        southFaceData  = CreateFaceData(southFaceTransform,  southFace,  southUpTrigger,  southRightTrigger,  southDownTrigger,  southLeftTrigger);
        eastFaceData   = CreateFaceData(eastFaceTransform,   eastFace,   eastUpTrigger,   eastRightTrigger,   eastDownTrigger,   eastLeftTrigger);
        westFaceData   = CreateFaceData(westFaceTransform,   westFace,   westUpTrigger,   westRightTrigger,   westDownTrigger,   westLeftTrigger);
    }

    private FaceData CreateFaceData(Transform faceTransform, PowerLineType lineType,
        PowerConnectionTrigger up, PowerConnectionTrigger right,
        PowerConnectionTrigger down, PowerConnectionTrigger left)
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
        if (up    != null && up.gameObject.activeInHierarchy)    activeTriggers.Add(up);
        if (right != null && right.gameObject.activeInHierarchy) activeTriggers.Add(right);
        if (down  != null && down.gameObject.activeInHierarchy)  activeTriggers.Add(down);
        if (left  != null && left.gameObject.activeInHierarchy)  activeTriggers.Add(left);

        data.triggers = activeTriggers.ToArray();
        return data;
    }

    // Called by PowerManager before BFS. Resets this runode to unpowered.
    public void ClearPowerState()
    {
        bool wasPowered = IsPowered;

        IsPowered = false;
        currentPowerSource = null;
        currentPowerColor = Color.white;
        distanceFromSource = 0;

        if (wasPowered)
            ApplyVisualColor(Color.white);
    }

    // Called by PowerSource BFS when this runode is reached and powered.
    public void SetPowered(PowerSource source, Color color, int distance)
    {
        IsPowered = true;
        currentPowerSource = source;
        currentPowerColor = color;
        distanceFromSource = distance;

        ApplyVisualColor(color);
    }

    // Returns all active triggers across all 6 faces. Used by BFS to keep searching outward.
    public IEnumerable<PowerConnectionTrigger> GetAllTriggers()
    {
        foreach (PowerConnectionTrigger t in topFaceData.triggers)    yield return t;
        foreach (PowerConnectionTrigger t in bottomFaceData.triggers) yield return t;
        foreach (PowerConnectionTrigger t in northFaceData.triggers)  yield return t;
        foreach (PowerConnectionTrigger t in southFaceData.triggers)  yield return t;
        foreach (PowerConnectionTrigger t in eastFaceData.triggers)   yield return t;
        foreach (PowerConnectionTrigger t in westFaceData.triggers)   yield return t;
    }

    private void ApplyVisualColor(Color color)
    {
        ApplyFaceColor(topFaceData,    color);
        ApplyFaceColor(bottomFaceData, color);
        ApplyFaceColor(northFaceData,  color);
        ApplyFaceColor(southFaceData,  color);
        ApplyFaceColor(eastFaceData,   color);
        ApplyFaceColor(westFaceData,   color);
    }

    private void ApplyFaceColor(FaceData face, Color color)
    {
        if (face.faceSprite != null)
            face.faceSprite.color = color;
    }
}
