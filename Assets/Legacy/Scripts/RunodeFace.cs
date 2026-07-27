using System.Collections.Generic;
using UnityEngine;

public class RunodeFace : MonoBehaviour
{
    [HideInInspector] public SpriteRenderer faceSprite;
    [HideInInspector] public BoxCollider faceZone;
    [HideInInspector] public PowerConnectionTrigger[] triggers; // [0]=Up, [1]=Right, [2]=Down, [3]=Left
    [HideInInspector] public PowerLineType lineType;
    [HideInInspector] public int faceIndex;

    [Header("LOGIC STATE")]
    public bool isFacePowered;
    public Color faceColor = Color.white;
    public PowerSourceLEGACY poweredBySource;

    // Persists across recalculations. Only cleared when this face genuinely loses power
    // (PowerManager's final pass). PowerDisplayManager compares against this to detect a real
    // short circuit at the moment of coloring, regardless of what BFS wiped and recomputed this pass.
    public PowerSourceLEGACY lastPoweredBySource;

    // Persists alongside lastPoweredBySource for the same reason: PowerManager's Global Logic
    // Clear zeroes distanceFromSource every recalculation before depower visuals are queued, so
    // Clear() cannot rely on distanceFromSource to know how far downstream this face actually was.
    public int lastDistanceFromSource;

    // Persists alongside lastPoweredBySource/lastDistanceFromSource. Records the exact sequence
    // this face was claimed in during its source's BFS pass (set by PowerSource, unique per
    // source per pass). distanceFromSource can tie between two faces of the same physical cube
    // (an internal bridge connection doesn't increment distance), but this value never ties, so
    // PowerDisplayManager can always sort a depower batch into the exact same order the faces
    // were originally powered in, even across a tied pair.
    public int lastPowerOrder;

    public RunodeFace parentFace;
    public int distanceFromSource = 0;

    [Header("VISUAL LAYERS")]
    [HideInInspector] public Color powerColorLayer = Color.white;
    [HideInInspector] public Color highlightColorLayer = Color.white;

    [HideInInspector] public ObstructionController obstructionController;

    public void UpdateSpriteVisuals()
    {
        if (faceSprite == null) return;

        bool isObstructed = obstructionController != null && obstructionController.IsFaceObstructed(faceZone);
        
        Color baseColor = isObstructed ? new Color(0.08f, 0.08f, 0.08f) : powerColorLayer;
        faceSprite.color = baseColor * highlightColorLayer;
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

    private void Awake()
    {
        obstructionController = GetComponentInParent<ObstructionController>();

        triggers = new PowerConnectionTrigger[4];

        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Power Line Sprite"))
                faceSprite = child.GetComponent<SpriteRenderer>();

            PowerConnectionTrigger t = child.GetComponent<PowerConnectionTrigger>();
            if (t != null)
            {
                string n = child.name.ToLower();
                if      (n.Contains("up"))    triggers[0] = t;
                else if (n.Contains("right")) triggers[1] = t;
                else if (n.Contains("down"))  triggers[2] = t;
                else if (n.Contains("left"))  triggers[3] = t;
            }
        }
    }

    // Returns the corner/edge-wrapped neighbor of t, delegating to ObstructionController.
    public PowerConnectionTrigger GetInternalNeighbor(PowerConnectionTrigger t)
    {
        return obstructionController != null ? obstructionController.GetInternalNeighbor(t) : null;
    }

    private void OnEnable()
    {
        if (PowerManager.Instance != null) PowerManager.Instance.RegisterRunodeFace(this);
    }

    private void OnDisable()
    {
        if (PowerManager.Instance != null) PowerManager.Instance.UnregisterRunodeFace(this);
    }

    // Returns all triggers on this face connected to the entry trigger.
    public List<PowerConnectionTrigger> GetConnectedTriggersOnFace(PowerConnectionTrigger entry)
    {
        List<PowerConnectionTrigger> connected = new List<PowerConnectionTrigger>();

        if (obstructionController != null && obstructionController.IsFaceObstructed(faceZone))
        {
            Debug.Log($"[BFS] {name}: Face {faceZone?.name ?? "Unknown"} is obstructed. Connection denied.");
            return connected;
        }

        int entryIndex = System.Array.IndexOf(triggers, entry);
        bool[] activeIndices = GetLineConnectivity(lineType);

        for (int i = 0; i < triggers.Length; i++)
        {
            if (i != entryIndex && activeIndices[i] && triggers[i] != null && triggers[i].gameObject.activeInHierarchy)
            {
                if (obstructionController != null && obstructionController.IsInternalPathPinch(entry, triggers[i]))
                {
                    Debug.Log($"[BFS] {name}: Internal path on face between {entry.name} and {triggers[i].name} is PINCHED.");
                    continue;
                }
                connected.Add(triggers[i]);
            }
        }
        return connected;
    }

    // Claims this face for the given source. BFS never blocks or short-circuits here — every
    // source is free to walk through a face another source already claimed this pass. The one
    // and only game-over check happens later, in PowerDisplayManager, at the moment of coloring.
    public void MarkPowered(Color color, RunodeFace sourceFace, PowerSourceLEGACY source, int distance)
    {
        if (sourceFace != null && this == sourceFace) return;

        isFacePowered = true;
        faceColor = color;
        poweredBySource = source;

        // Only ever update the persisted owner if it's unclaimed or already this source.
        // A different source must never silently overwrite it mid-pass — that's exactly the
        // case PowerDisplayManager needs to still see as a conflict later.
        if (lastPoweredBySource == null || lastPoweredBySource == source)
        {
            lastPoweredBySource = source;
            lastDistanceFromSource = distance;
        }

        distanceFromSource = distance;

        if (sourceFace != null)
            parentFace = sourceFace;
    }

    // Resets this face's power state and clears its triggers.
    public void Clear(bool visual = true, bool instant = false)
    {
        // Captured before the reset below so the depower visual update still knows which
        // source used to own this face and how far downstream it was, for correct grouping/ordering.
        // lastDistanceFromSource is used instead of distanceFromSource because PowerManager's
        // Global Logic Clear already zeroes distanceFromSource earlier in the same recalculation.
        PowerSourceLEGACY previousSource = lastPoweredBySource;
        int previousDistance = lastDistanceFromSource;
        int previousOrder = lastPowerOrder;

        isFacePowered = false;
        faceColor = Color.white;
        poweredBySource = null;
        parentFace = null;
        distanceFromSource = 0;

        if (triggers != null)
        {
            foreach (var t in triggers)
            {
                if (t != null) t.ClearPowerState();
            }
        }

        if (visual)
            ApplyColor(Color.white, previousSource, instant, previousDistance, previousOrder);
    }

    // Buffers this face's visual update with PowerManager. PowerManager hands the full batch to PowerDisplayManager once per recalculation.
    // distanceOverride/orderOverride let callers (e.g. Clear) supply the distance/order this face had before it was reset; otherwise the face's current values are used.
    public void ApplyColor(Color color, PowerSourceLEGACY source = null, bool instant = false, int? distanceOverride = null, int? orderOverride = null)
    {
        bool isObstructed = obstructionController != null && obstructionController.IsFaceObstructed(faceZone);
        int distance = distanceOverride ?? distanceFromSource;
        int order = orderOverride ?? lastPowerOrder;
        if (PowerManager.Instance != null)
            PowerManager.Instance.QueueVisualUpdate(this, color, source, isObstructed, distance, order, instant);
    }

    private bool[] GetLineConnectivity(PowerLineType type)
    {
        if (ConnectivityMap.TryGetValue(type, out bool[] connectivity))
            return connectivity;
        return ConnectivityMap[PowerLineType.Empty];
    }
}
