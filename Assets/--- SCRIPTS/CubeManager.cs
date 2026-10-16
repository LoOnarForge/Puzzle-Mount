using UnityEngine;

/// <summary>
/// Minimal cube manager - handles PowerLine visual system only
/// </summary>
public class CubeManager : MonoBehaviour
{
    [Header("POWERLINE COLORS")]
    public Color unpoweredUnconnectedColor = Color.gray;
    public Color unpoweredConnectedColor = Color.white;
    
    // Singleton instance
    public static CubeManager Instance { get; private set; }
    
    private void Awake()
    {
        // Singleton setup
        if (Instance == null)
        {
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Set PowerLine color based on connection state
    /// </summary>
    public void SetPowerLineColor(SpriteRenderer powerLineRenderer, PowerLineState state, Color poweredColor = default)
    {
        if (powerLineRenderer == null) return;
        
        switch (state)
        {
            case PowerLineState.UnpoweredUnconnected:
                powerLineRenderer.color = unpoweredUnconnectedColor;
                break;
            case PowerLineState.UnpoweredConnected:
                powerLineRenderer.color = unpoweredConnectedColor;
                break;
            case PowerLineState.Powered:
                powerLineRenderer.color = poweredColor != default ? poweredColor : Color.red;
                break;
        }
    }
}

/// <summary>
/// PowerLine connection and power states
/// </summary>
public enum PowerLineState
{
    UnpoweredUnconnected,
    UnpoweredConnected,
    Powered
}