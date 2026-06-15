using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PowerSource))]
public class PowerSourceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PowerSource source = (PowerSource)target;

        ColorManager colorManager = FindAnyObjectByType<ColorManager>();

        if (colorManager == null)
        {
            EditorGUILayout.HelpBox("ColorManager not found in scene.", MessageType.Warning);
            return;
        }

        Color previewColor = colorManager.GetColor(source.colorIndex);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Color Preview", EditorStyles.boldLabel);

        Rect rect = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, previewColor);
    }
}
