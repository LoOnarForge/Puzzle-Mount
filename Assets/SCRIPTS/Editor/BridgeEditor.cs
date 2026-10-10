using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Bridge))]
public class BridgeEditor : Editor
{
    private const int SocketCount = 2;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("isSelfPowered"), new GUIContent("Is Self Powered"));

        EditorGUILayout.Space(20);
        ColorManager colorManager = Object.FindAnyObjectByType<ColorManager>();
        SerializedProperty sockets = serializedObject.FindProperty("sockets");

        if (sockets.arraySize != SocketCount)
            sockets.arraySize = SocketCount;

        for (int i = 0; i < SocketCount; i++)
            DrawSocket(i, sockets.GetArrayElementAtIndex(i), colorManager);

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bridgeWidth"));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("startPlatform"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("endPlatform"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cubePrefab"));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cubePopSpeed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("overshootPercent"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("columnWaveDelay"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cubeRandomDelay"));

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Obstruction", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("bridgeObstructionMask"),
            new GUIContent("Bridge Obstruction Mask"));

        EditorGUILayout.Space(20);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isPowered"));
        }

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }

    private static void DrawSocket(int socketIndex, SerializedProperty slot, ColorManager colorManager)
    {
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField($"SOCKET {(socketIndex + 1):D2}:", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(slot.FindPropertyRelative("socketObject"), new GUIContent($"Power Socket {(socketIndex + 1):D2}"));
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
