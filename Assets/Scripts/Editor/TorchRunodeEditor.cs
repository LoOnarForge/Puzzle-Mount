using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TorchRunode))]
public class TorchRunodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("powerSocketObject"), new GUIContent("Power Socket Object"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pointLight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("spriteLibrary"), new GUIContent("Torch Sprite Library"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topFaceSprite"), new GUIContent("Top Face Sprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topFaceObstructionPort"), new GUIContent("Top Face Obstruction Port"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxPower"), new GUIContent("Max Power (MW)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseLightRange"), new GUIContent("Base Light Range"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("baseLightIntensity"), new GUIContent("Base Light Intensity"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ignorePowerColor"), new GUIContent("Ignore Power Color On Light", "Top-face sprite still uses delivered power color."));
        EditorGUILayout.Slider(serializedObject.FindProperty("fadeDuration"), 0f, 5f, new GUIContent("Fade Duration (s)"));
        EditorGUILayout.Slider(serializedObject.FindProperty("obstructionOffDelay"), 0f, 1f, new GUIContent("Obstruction Off Delay (s)"));
        EditorGUILayout.Slider(serializedObject.FindProperty("powerOnDelay"), 0f, 1f, new GUIContent("Power On Delay (s)"));

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
