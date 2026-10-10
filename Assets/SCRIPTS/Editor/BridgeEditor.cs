using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Bridge))]
public class BridgeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ColorManager colorManager = Object.FindAnyObjectByType<ColorManager>();
        DrawSocket(serializedObject.FindProperty("socket"), colorManager);

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startPlatform"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("endPlatform"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("gapColliderObject"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cubePrefab"));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cubePopSpeed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overshootPercent"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("columnWaveDelay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cubeRandomDelay"));

        EditorGUILayout.Space(20);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isPowered"));
        }

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }

    private static void DrawSocket(SerializedProperty slot, ColorManager colorManager)
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("SOCKET 01:", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(slot.FindPropertyRelative("powerSocket"), new GUIContent("Power Socket"));

        SerializedProperty colorIndex = slot.FindPropertyRelative("requiredColorIndex");

        if (colorManager != null && colorManager.colors.Count > 0)
        {
            string[] colorNames = colorManager.GetColorNames();
            colorIndex.intValue = EditorGUILayout.Popup("Required Color", colorIndex.intValue, colorNames);

            int selectedIndex = Mathf.Clamp(colorIndex.intValue, 0, colorManager.colors.Count - 1);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ColorField("Selected Color", colorManager.GetColor(selectedIndex));
            }
        }
        else
        {
            EditorGUILayout.PropertyField(colorIndex);
            EditorGUILayout.HelpBox("No ColorManager was found in the open scene.", MessageType.Info);
        }

        EditorGUILayout.PropertyField(slot.FindPropertyRelative("requiredMw"), new GUIContent("Required MW"));

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(slot.FindPropertyRelative("allocatedMw"), new GUIContent("Allocated MW"));
            EditorGUILayout.PropertyField(slot.FindPropertyRelative("poweringSource"), new GUIContent("Powering Source"));
        }
    }
}
