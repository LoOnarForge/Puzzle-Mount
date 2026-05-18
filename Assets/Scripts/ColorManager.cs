using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class NamedColor
{
    public string name = "COLOR";
    public Color color = Color.white;
}

/// <summary>
/// Place in the scene. Defines the shared color palette for all power sources and receivers.
/// Runs before all other scripts to ensure colors are ready in Awake.
/// </summary>
[DefaultExecutionOrder(-100)]
public class ColorManager : MonoBehaviour
{
    public static ColorManager Instance { get; private set; }

    [Header("COLOR PALETTE")]
    public List<NamedColor> colors = new List<NamedColor>()
    {
        new NamedColor { name = "COLOR 01", color = Color.white },
        new NamedColor { name = "COLOR 02", color = Color.white },
        new NamedColor { name = "COLOR 03", color = Color.white },
        new NamedColor { name = "COLOR 04", color = Color.white },
        new NamedColor { name = "COLOR 05", color = Color.white },
        new NamedColor { name = "COLOR 06", color = Color.white },
        new NamedColor { name = "COLOR 07", color = Color.white },
        new NamedColor { name = "COLOR 08", color = Color.white },
        new NamedColor { name = "COLOR 09", color = Color.white },
        new NamedColor { name = "COLOR 10", color = Color.white },
    };

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>Returns the color at the given index. Returns white if index is out of range.</summary>
    public Color GetColor(int index)
    {
        if (index < 0 || index >= colors.Count) return Color.white;
        return colors[index].color;
    }

    /// <summary>Returns all color names for the inspector dropdown.</summary>
    public string[] GetColorNames()
    {
        string[] names = new string[colors.Count];
        for (int i = 0; i < colors.Count; i++)
            names[i] = string.IsNullOrEmpty(colors[i].name) ? $"COLOR {i + 1:00}" : colors[i].name;
        return names;
    }
}
