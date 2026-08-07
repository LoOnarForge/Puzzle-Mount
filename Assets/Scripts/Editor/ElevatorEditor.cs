using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Elevator))]
public class ElevatorEditor : Editor
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
        EditorGUILayout.PropertyField(serializedObject.FindProperty("platform"));

        SerializedProperty stops = serializedObject.FindProperty("stops");
        EditorGUILayout.PropertyField(stops, true);

        using (new EditorGUI.DisabledScope(stops.arraySize == 0))
        {
            if (GUILayout.Button("Add Stop"))
                AddStop((Elevator)target, stops);
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("moveSpeed"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentStopIndex"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("deathForceMultiplier"));

        EditorGUILayout.Space(20);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("isPowered"));
        }

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }

    private void AddStop(Elevator elevator, SerializedProperty stops)
    {
        Transform lastStop = stops.GetArrayElementAtIndex(stops.arraySize - 1).objectReferenceValue as Transform;
        if (lastStop == null)
            return;

        Undo.RecordObject(elevator, "Add Elevator Stop");

        GameObject copy = Object.Instantiate(lastStop.gameObject, lastStop.position, lastStop.rotation, lastStop.parent);
        copy.name = GetNextNumberedName(lastStop.name);
        Undo.RegisterCreatedObjectUndo(copy, "Add Elevator Stop");

        stops.arraySize++;
        stops.GetArrayElementAtIndex(stops.arraySize - 1).objectReferenceValue = copy.transform;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(elevator);
    }

    private static string GetNextNumberedName(string sourceName)
    {
        int open = sourceName.LastIndexOf('(');
        int close = sourceName.LastIndexOf(')');

        if (open < 0 || close <= open || close != sourceName.Length - 1)
            return sourceName;

        string numberPart = sourceName.Substring(open + 1, close - open - 1);
        if (!int.TryParse(numberPart, out int number))
            return sourceName;

        string prefix = sourceName.Substring(0, open);
        string nextNumber = (number + 1).ToString("D" + numberPart.Length);
        return prefix + "(" + nextNumber + ")";
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
