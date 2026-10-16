using UnityEngine;

// Place in the scene. Shared settings other scripts read at play start.

[DefaultExecutionOrder(-100)]
public class GameConfig : MonoBehaviour
{
    public static GameConfig Instance { get; private set; }

    [Header("POWER WAVE:")]
    [SerializeField] private float powerPropagationDelay = 0.07f;

    public float PowerPropagationDelay => powerPropagationDelay;

    private void Awake()
    {
        Instance = this;
    }
}
