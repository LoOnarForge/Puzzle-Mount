using UnityEngine;
using System.Collections.Generic;

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
/// Controls all child sockets and their visual requirements from this root.
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
        PowerSocket[] childSockets = GetComponentsInChildren<PowerSocket>(true);
        
        for (int i = 0; i < 4; i++)
        {
            if (i < childSockets.Length)
            {
                SocketSettings config = socketConfigs[i];
                childSockets[i].Initialize(config.isActive, config.requiredColorIndex, config.requiredMW);
            }
        }
        
        UpdateCrystals();
    }

    private void CheckPowerState()
    {
        bool anyActive = false;
        bool allSatisfied = true;
        int activeCount = 0;
        int satisfiedCount = 0;

        PowerSocket[] childSockets = GetComponentsInChildren<PowerSocket>(true);

        for (int i = 0; i < 4; i++)
        {
            if (socketConfigs[i].isActive && i < childSockets.Length)
            {
                anyActive = true;
                activeCount++;
                
                if (childSockets[i].isSatisfied)
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
            Debug.Log($"[ForgottenGate] All {activeCount} power conditions met. Gate opening.");
        }
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
        PowerSocket[] childSockets = GetComponentsInChildren<PowerSocket>(true);

        // Try to find ColorManager in editor if instance is null
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

            if (Application.isPlaying && i < childSockets.Length)
            {
                PowerSocket socket = childSockets[i];
                
                // Calculate how many crystals to light up (Scale 0 to List Count)
                float progress = (float)socket.availableMW / config.requiredMW;
                int litCount = Mathf.Clamp(Mathf.FloorToInt(progress * config.socketCrystals.Count), 0, config.socketCrystals.Count);
                
                // If requirement is met, force all to light up
                if (socket.isSatisfied) litCount = config.socketCrystals.Count;

                SetCrystalGroupVisuals(config.socketCrystals, socket.incomingColor != Color.clear ? socket.incomingColor : reqColor, 0.1f, litCount, socket.incomingColor);
            }
            else
            {
                // Editor mode: Show all crystals in dim requirement color
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
                // Lit crystal (Power progress)
                Color displayColor = incomingColor ?? color;
                propBlock.SetColor(BaseColorProperty, displayColor);
                propBlock.SetColor(EmissionColorProperty, displayColor * 2f);
            }
            else
            {
                // Unlit crystal (Showing requirement)
                propBlock.SetColor(BaseColorProperty, color);
                propBlock.SetColor(EmissionColorProperty, color * dimIntensity);
            }

            r.SetPropertyBlock(propBlock);
        }
    }

    private void OnValidate()
    {
        // Updates visuals in the editor without hitting Play
        if (!Application.isPlaying)
        {
            UpdateCrystals();
        }
    }
}
