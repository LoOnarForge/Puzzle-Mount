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

        DrawColorDecor(colorManager);

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("portalObject"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("portalLight"), new GUIContent("Portal Light"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("powerUpTimer"), new GUIContent("Power Up Timer"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("depowerTimer"), new GUIContent("Depower Timer"));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isGateActive"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelCompleted"));

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }

    private void DrawColorDecor(ColorManager colorManager)
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("COLOR DECOR", EditorStyles.boldLabel);

        SerializedProperty spawnTransforms = serializedObject.FindProperty("spawnTransforms");
        if (spawnTransforms.arraySize != 4)
            spawnTransforms.arraySize = 4;

        for (int i = 0; i < spawnTransforms.arraySize; i++)
        {
            EditorGUILayout.PropertyField(
                spawnTransforms.GetArrayElementAtIndex(i),
                new GUIContent($"Spawn Transform {(i + 1):D2}"));
        }

        SerializedProperty colorPrefabs = serializedObject.FindProperty("colorPrefabs");
        if (colorPrefabs.arraySize != 6)
            colorPrefabs.arraySize = 6;

        for (int i = 0; i < colorPrefabs.arraySize; i++)
        {
            string label = colorManager != null && i < colorManager.colors.Count
                ? $"Prefab — {colorManager.GetColorNames()[i]}"
                : $"Prefab — Color Index {i}";

            EditorGUILayout.PropertyField(colorPrefabs.GetArrayElementAtIndex(i), new GUIContent(label));
        }
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
