using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Piston))]
public class PistonEditor : Editor
{
    private const int SocketCount = 2;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ColorManager colorManager = Object.FindAnyObjectByType<ColorManager>();
        SerializedProperty sockets = serializedObject.FindProperty("sockets");

        if (sockets.arraySize != SocketCount)
            sockets.arraySize = SocketCount;

        for (int i = 0; i < SocketCount; i++)
            DrawSocket($"SOCKET {(i + 1):D2}", sockets.GetArrayElementAtIndex(i), colorManager);

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pistonFace"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxStage"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("moveSpeed"));

        EditorGUILayout.Space(20);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isPowered"));
        }

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }

    private void DrawSocket(string sectionLabel, SerializedProperty slot, ColorManager colorManager)
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField(sectionLabel, EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(slot.FindPropertyRelative("socketObject"), new GUIContent("Socket Object"));
        EditorGUILayout.PropertyField(slot.FindPropertyRelative("isEnabled"), new GUIContent("Is Enabled"));

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
