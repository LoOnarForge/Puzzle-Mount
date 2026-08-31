using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RuneTorch))]
public class RuneTorchEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("powerSocketObject"), new GUIContent("Power Socket Object"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pointLight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spriteLibrary"), new GUIContent("Torch Sprite Library"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topFaceSprite"), new GUIContent("Top Face Sprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxPower"), new GUIContent("Max Power (MW)"));
        EditorGUILayout.Slider(serializedObject.FindProperty("fadeDuration"), 0f, 5f, new GUIContent("Fade Duration (s)"));

        EditorGUILayout.Space(20);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("allocatedMw"), new GUIContent("Allocated MW"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("displayedIntensity"), new GUIContent("Light Intensity"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("poweringSource"), new GUIContent("Powering Source"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isLit"));
        }

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }
}
