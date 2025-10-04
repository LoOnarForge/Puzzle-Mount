using UnityEngine;
using UnityEditor;

public class LayerSetup
{
    [MenuItem("Tools/Setup PowerLines Layer")]
    public static void CreatePowerLinesLayer()
    {
        string layerName = "PowerLines";
        
        // Get the TagManager
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        
        // Check if layer already exists
        for (int i = 0; i < layers.arraySize; i++)
        {
            SerializedProperty layerProperty = layers.GetArrayElementAtIndex(i);
            if (layerProperty.stringValue == layerName)
            {
                Debug.Log($"Layer '{layerName}' already exists at index {i}");
                return;
            }
        }
        
        // Find first empty slot after built-in layers (index 8 and up)
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layerProperty = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layerProperty.stringValue))
            {
                layerProperty.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                Debug.Log($"Created layer '{layerName}' at index {i}");
                return;
            }
        }
        
        Debug.LogError("No empty layer slots available!");
    }
}