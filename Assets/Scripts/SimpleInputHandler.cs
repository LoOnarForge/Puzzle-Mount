// This file is deprecated. 
// The actual SimpleInputHandler is located in /Assets/Scripts/Input/SimpleInputHandler.cs
// This file exists only to prevent compilation errors during migration.
// You can safely delete this file after migration is complete.

using UnityEngine;

[System.Obsolete("Use MountPuzzle.Input.SimpleInputHandler instead")]
public class LegacySimpleInputHandler : MonoBehaviour
{
    void Start()
    {
        Debug.LogWarning("LegacySimpleInputHandler is deprecated. Use MountPuzzle.Input.SimpleInputHandler instead.");
        Destroy(this);
    }
}