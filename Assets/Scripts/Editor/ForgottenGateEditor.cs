using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ForgottenGate))]
public class ForgottenGateEditor : Editor
{
    private const int SocketCount = 4;

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
        EditorGUILayout.PropertyField(serializedObject.FindProperty("portalSurface"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pulseSpeed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("minAlpha"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxAlpha"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("scalePulseAmount"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bloomMin"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bloomMax"));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isGateActive"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelCompleted"));

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
