using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ButtonDevice))]
public class ButtonDeviceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("buttonCap"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressLocalYOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressTrigger"));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("connectedDevices"), true);

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }
}
