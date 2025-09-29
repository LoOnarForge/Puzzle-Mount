using UnityEngine;

/// <summary>
/// Main game manager for game-specific logic
/// CubeManager and other managers should be manually placed in scenes as needed
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public bool debugMode = false;
    
    private void Awake()
    {
        // Game-specific initialization only
        // Managers should be manually placed in scenes for explicit control
        if (debugMode)
        {
            Debug.Log("[GameManager] Game initialized - Managers should be manually placed in scene");
        }
    }
}