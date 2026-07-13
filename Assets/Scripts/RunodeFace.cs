using UnityEngine;

[System.Serializable]
public class RunodeFace
{
    public SpriteRenderer faceSprite;
    public BoxCollider faceZone;
    public PowerConnectionTrigger[] triggers; // [0]=Up, [1]=Right, [2]=Down, [3]=Left
    public PowerLineType lineType;
    public bool isFacePowered;
    public Color faceColor = Color.white;

    [Header("LOGIC STATE")]
    public RunodePower cube; // The physical cube this face belongs to
    public PowerSource poweredBySource;
    public int faceIndex; // Self-reference to index in allFaces
    public RunodePower parentCube; // The cube that fed this face (Logic Parent)
    public int parentFaceIndex = -1; // The face on parentCube (or this cube) that fed this face
    public int distanceFromSource = 0;

    // Applies power state to this face. Returns false on short circuit (different source).
    public bool MarkPowered(Color color, RunodeFace sourceFace, PowerSource source, int distance)
    {
        // Ignore if we are looking back at the face that just powered us
        if (sourceFace != null && this == sourceFace) return true;

        // Collision check:
        // If it's already powered by a DIFFERENT source -> Short Circuit!
        if (isFacePowered && poweredBySource != null && poweredBySource != source)
        {
            Debug.Log("GAME OVER");
            Debug.Log($"Cube {cube.name} Face {faceIndex} caused short circuit between {poweredBySource.name} and {source.name}");
            return false;
        }

        // If it's already powered by the SAME source, it's a loop.
        if (isFacePowered && poweredBySource == source)
        {
            return true;
        }

        isFacePowered = true;
        faceColor = color;
        poweredBySource = source;
        distanceFromSource = distance;

        if (sourceFace != null)
        {
            parentCube = sourceFace.cube;
            parentFaceIndex = sourceFace.faceIndex;
        }
        return true;
    }

    // Resets this face's power state and clears its triggers.
    public void Clear(bool visual = true)
    {
        isFacePowered = false;
        faceColor = Color.white;
        poweredBySource = null;
        parentCube = null;
        parentFaceIndex = -1;
        distanceFromSource = 0;

        foreach (var t in triggers)
        {
            if (t != null) t.ClearPowerState();
        }

        if (visual)
        {
            ApplyColor(Color.white, false);
        }
    }

    // Dispatches this face's visual update through the PowerDisplayManager.
    public void ApplyColor(Color color, bool instant = false)
    {
        int index = cube.allFaces.IndexOf(this);
        bool isObstructed = cube.obstructionController != null && cube.obstructionController.IsFaceObstructed(faceZone);

        if (PowerDisplayManager.Instance != null)
        {
            PowerDisplayManager.Instance.UpdateFaceVisuals(cube, index, color, isObstructed, instant);
        }
    }
}
