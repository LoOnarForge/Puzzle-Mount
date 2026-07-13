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
    public PowerSource poweredBySource;
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

    // Applies power state to this face. Returns false on short circuit.
    public bool MarkPowered(Color color, RunodeFace sourceFace, PowerSource source, int distance)
    {
        if (sourceFace != null && this == sourceFace) return true;

        if (isFacePowered && poweredBySource != null && poweredBySource != source)
        {
            Debug.Log("GAME OVER");
            Debug.Log($"Cube {transform.root.name} Face {faceIndex} caused short circuit between {poweredBySource.name} and {source.name}");
            return false;
        }

        if (isFacePowered && poweredBySource == source)
            return true;

        isFacePowered = true;
        faceColor = color;
        poweredBySource = source;
        distanceFromSource = distance;

        if (sourceFace != null)
            parentFace = sourceFace;

        return true;
    }

    // Resets this face's power state and clears its triggers.
    public void Clear(bool visual = true, bool instant = false)
    {
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
            ApplyColor(Color.white, instant);
    }

    // Dispatches this face's visual update through PowerDisplayManager.
    public void ApplyColor(Color color, bool instant = false)
    {
        bool isObstructed = obstructionController != null && obstructionController.IsFaceObstructed(faceZone);
        if (PowerDisplayManager.Instance != null)
            PowerDisplayManager.Instance.UpdateFaceVisuals(this, color, isObstructed, instant);
    }

    private bool[] GetLineConnectivity(PowerLineType type)
    {
        if (ConnectivityMap.TryGetValue(type, out bool[] connectivity))
            return connectivity;
        return ConnectivityMap[PowerLineType.Empty];
    }
}
