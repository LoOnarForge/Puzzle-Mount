using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class SocketSettings
{
    public bool isActive = true;
    public int requiredColorIndex;
    public int requiredMW = 1;
    public List<Renderer> socketCrystals = new List<Renderer>();
}

/// <summary>
/// A "Powered Rig" that requires multiple PowerSockets to be satisfied.
/// Controls all child sockets and their visual crystals from this root.
/// </summary>
public class ForgottenGate : MonoBehaviour
{
    [Header("CONFIGURATION")]
    public SocketSettings[] socketConfigs = new SocketSettings[4];

    [Header("STATE")]
    public bool isSatisfied = false;

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock propBlock;
    private List<PowerSocket> cachedSockets = new List<PowerSocket>();

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        SetupSockets();
    }

    private void Update()
    {
        CheckPowerState();
        UpdateCrystals();
    }

    // Configures child sockets and updates their visual requirement markers.
    public void SetupSockets()
    {
        // Find and sort sockets by hierarchy path to ensure Socket 1 is index 0
        cachedSockets = GetComponentsInChildren<PowerSocket>(true)
            .OrderBy(s => GetHierarchyPath(s.transform)).ToList();
        
        for (int i = 0; i < 4; i++)
        {
            if (i < cachedSockets.Count)
            {
                SocketSettings config = socketConfigs[i];
                cachedSockets[i].Initialize(config.isActive, config.requiredColorIndex, config.requiredMW);
            }
        }
        
        UpdateCrystals();
    }

    private string GetHierarchyPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }

    private void CheckPowerState()
    {
        bool anyActive = false;
        bool allSatisfied = true;
        int activeCount = 0;
        int satisfiedCount = 0;

        for (int i = 0; i < 4; i++)
        {
            if (socketConfigs[i].isActive)
            {
                anyActive = true;
                activeCount++;
                
                if (i < cachedSockets.Count && cachedSockets[i].isSatisfied)
                {
                    satisfiedCount++;
                }
                else
                {
                    allSatisfied = false;
                }
            }
        }

        bool previousState = isSatisfied;
        isSatisfied = anyActive && allSatisfied;

        if (isSatisfied && !previousState)
        {
            Debug.Log($"<color=green>[{gameObject.name}] All {activeCount} power conditions met. Gate opening.</color>");
        }
    }

    public void LogGateStatus()
    {
        int activeCount = 0;
        int satisfiedCount = 0;

        for (int i = 0; i < 4; i++)
        {
            if (socketConfigs[i].isActive) activeCount++;
            if (i < cachedSockets.Count && cachedSockets[i].isSatisfied) satisfiedCount++;
        }

        string report = $"<b>[{gameObject.name}]</b> STATUS: {satisfiedCount}/{activeCount} sockets POWERED\n";

        for (int i = 0; i < 4; i++)
        {
            if (!socketConfigs[i].isActive) continue;

            if (i < cachedSockets.Count)
            {
                PowerSocket s = cachedSockets[i];
                string reqColorName = GetColorName(s.requiredColorIndex);
                Color reqColor = (ColorManager.Instance != null) ? ColorManager.Instance.GetColor(s.requiredColorIndex) : Color.white;
                string reqHex = ColorUtility.ToHtmlStringRGB(reqColor);

                if (s.isSatisfied)
                {
                    report += $"  - Socket {i + 1}: <color=green>SUCCESS</color> powered with required <color=#{reqHex}>{reqColorName}</color>. {s.availableMW}/{s.requiredMW} MW.\n";
                }
                else if (s.incomingColor != Color.clear)
                {
                    string incColorName = GetIncomingColorName(s.incomingColor);
                    string incHex = ColorUtility.ToHtmlStringRGB(s.incomingColor);
                    report += $"  - Socket {i + 1}: <color=red>FAILED</color> receiving <color=#{incHex}>{incColorName}</color> (Needs <color=#{reqHex}>{reqColorName}</color>). {s.availableMW}/{s.requiredMW} MW.\n";
                }
                else
                {
                    report += $"  - Socket {i + 1}: <color=grey>NO POWER</color> (Needs <color=#{reqHex}>{reqColorName}</color>). 0/{s.requiredMW} MW.\n";
                }
            }
        }

        Debug.Log(report);
    }

    private string GetIncomingColorName(Color c)
    {
        if (ColorManager.Instance == null) return "Unknown";
        for (int i = 0; i < ColorManager.Instance.colors.Count; i++)
        {
            if (ColorsMatch(c, ColorManager.Instance.colors[i].color))
                return ColorManager.Instance.colors[i].name;
        }
        return "Unknown";
    }

    private bool ColorsMatch(Color a, Color b)
    {
        const float tolerance = 0.05f;
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }

    private string GetColorName(int index)
    {
        if (ColorManager.Instance == null) return "Unknown";
        string[] names = ColorManager.Instance.GetColorNames();
        if (index >= 0 && index < names.Length) return names[index];
        return "Unknown";
    }

    private void UpdateCrystals()
    {
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        ColorManager colorPalette = ColorManager.Instance;
        if (colorPalette == null && !Application.isPlaying)
        {
            colorPalette = Object.FindAnyObjectByType<ColorManager>();
        }

        for (int i = 0; i < 4; i++)
        {
            SocketSettings config = socketConfigs[i];
            if (config.socketCrystals == null || config.socketCrystals.Count == 0) continue;

            if (!config.isActive)
            {
                SetCrystalGroupVisuals(config.socketCrystals, Color.black, 0f, 0);
                continue;
            }

            Color reqColor = (colorPalette != null) ? colorPalette.GetColor(config.requiredColorIndex) : Color.white;

            if (Application.isPlaying && i < cachedSockets.Count)
            {
                PowerSocket socket = cachedSockets[i];
                float progress = (float)socket.availableMW / config.requiredMW;
                int litCount = Mathf.Clamp(Mathf.FloorToInt(progress * config.socketCrystals.Count), 0, config.socketCrystals.Count);
                if (socket.isSatisfied) litCount = config.socketCrystals.Count;

                SetCrystalGroupVisuals(config.socketCrystals, socket.incomingColor != Color.clear ? socket.incomingColor : reqColor, 0.1f, litCount, socket.incomingColor);
            }
            else
            {
                SetCrystalGroupVisuals(config.socketCrystals, reqColor, 0.2f, 0);
            }
        }
    }

    private void SetCrystalGroupVisuals(List<Renderer> renderers, Color color, float dimIntensity, int litCount, Color? incomingColor = null)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            Material targetMat = Application.isPlaying ? r.material : r.sharedMaterial;
            if (targetMat != null) targetMat.EnableKeyword("_EMISSION");

            r.GetPropertyBlock(propBlock);

            if (i < litCount)
            {
                Color displayColor = incomingColor ?? color;
                propBlock.SetColor(BaseColorProperty, displayColor);
                propBlock.SetColor(EmissionColorProperty, displayColor * 2f);
            }
            else
            {
                propBlock.SetColor(BaseColorProperty, color);
                propBlock.SetColor(EmissionColorProperty, color * dimIntensity);
            }

            r.SetPropertyBlock(propBlock);
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying) UpdateCrystals();
    }
}
