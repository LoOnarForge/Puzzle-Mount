using UnityEngine;

[DisallowMultipleComponent]
public class EditorLightSwitch : MonoBehaviour
{
    [SerializeField] private bool isActive = true;

    private void Awake()
    {
        if (!isActive)
            return;

        gameObject.SetActive(false);
    }
}
