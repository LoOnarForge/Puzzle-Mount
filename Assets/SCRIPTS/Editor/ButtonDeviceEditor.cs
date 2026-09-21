using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ButtonDevice))]
public class ButtonDeviceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("skullButton"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressTrigger"));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressPose0"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressPose1"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressPose2"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressPose3"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressPose4"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pressStepDuration"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pauseBetweenMoves"));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("connectedDevices"), true);

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }
}
