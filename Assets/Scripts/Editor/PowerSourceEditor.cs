using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PowerSource))]
public class PowerSourceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPowerSourceSettings();
        DrawState();
        DrawPowerSourcePorts();
        DrawVisualElements();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawPowerSourceSettings()
    {
        SerializedProperty colorIndex = serializedObject.FindProperty("colorIndex");
        SerializedProperty maxMW = serializedObject.FindProperty("maxMW");
        ColorManager colorManager = Object.FindAnyObjectByType<ColorManager>();

        EditorGUILayout.LabelField("POWER SOURCE SETTINGS:", EditorStyles.boldLabel);

        if (colorManager != null && colorManager.colors.Count > 0)
        {
            string[] colorNames = colorManager.GetColorNames();
            colorIndex.intValue = EditorGUILayout.Popup("Circuit Color", colorIndex.intValue, colorNames);

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

        EditorGUILayout.PropertyField(maxMW);
    }

    private void DrawPowerSourcePorts()
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("POWER SOURCE PORTS:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("upPort"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rightPort"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("downPort"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("leftPort"));
    }

    private void DrawVisualElements()
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("VISUAL ELEMENTS:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("colorElements"), true);
    }

    private void DrawState()
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("STATE:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("availableMW"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("circuitMembers"));
    }
}
