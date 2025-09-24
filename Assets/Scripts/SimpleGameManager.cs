using UnityEngine;

public class SimpleGameManager : MonoBehaviour
{
    [Header("Game Settings")]
    public bool debugMode = true;
    
    public static SimpleGameManager Instance { get; private set; }
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Initialize()
    {
        Application.targetFrameRate = 60;
        
        if (debugMode)
        {
            Debug.Log("✓ Simple Game Manager Initialized");
        }
    }
    
    void OnGUI()
    {
        if (!debugMode) return;
        
        GUIStyle style = new GUIStyle();
        style.fontSize = 16;
        style.normal.textColor = Color.white;
        
        GUI.Label(new Rect(10, 10, 200, 30), $"FPS: {(1.0f / Time.deltaTime):F1}", style);
    }
}