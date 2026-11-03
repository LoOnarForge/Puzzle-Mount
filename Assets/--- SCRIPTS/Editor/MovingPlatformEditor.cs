using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MovingPlatform))]
public class MovingPlatformEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("isTransportingTim"), new GUIContent("Is Transporting Tim"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isTransportingRunodes"), new GUIContent("Is Transporting Runodes"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("occupancyTrigger"), new GUIContent("Occupancy Trigger"));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("platform"));

        SerializedProperty stops = serializedObject.FindProperty("stops");

        MovingPlatform movingPlatform = (MovingPlatform)target;
        using (new EditorGUI.DisabledScope(stops.arraySize >= movingPlatform.MaxStops))
        {
            if (GUILayout.Button(stops.arraySize == 0 ? "Add First Stop" : "Add Stop"))
            {
                if (stops.arraySize == 0)
                    AddFirstStop(movingPlatform, stops);
                else
                    AddStop(movingPlatform, stops);
            }
        }

        if (stops.arraySize > movingPlatform.MaxStops)
        {
            EditorGUILayout.HelpBox($"This platform supports at most {movingPlatform.MaxStops} stops.", MessageType.Warning);
            stops.arraySize = movingPlatform.MaxStops;
        }

        EditorGUILayout.Space(10);
        for (int i = 0; i < stops.arraySize; i++)
            DrawStop(i, stops.GetArrayElementAtIndex(i));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentStopIndex"));

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }

    private void DrawStop(int stopIndex, SerializedProperty stop)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"STOP {(stopIndex + 1):D2}:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stop.FindPropertyRelative("stopTransform"), new GUIContent("Stop Transform"));
        SerializedProperty stopTimer = stop.FindPropertyRelative("stopTimer");
        stopTimer.intValue = EditorGUILayout.IntSlider(new GUIContent("Stop Timer"), stopTimer.intValue, 0, 10);
        SerializedProperty speed = stop.FindPropertyRelative("speed");
        speed.intValue = EditorGUILayout.IntSlider(new GUIContent("Speed"), speed.intValue, 0, 10);
    }

    private void AddFirstStop(MovingPlatform movingPlatform, SerializedProperty stops)
    {
        Undo.RecordObject(movingPlatform, "Add Platform Stop");

        stops.arraySize = 1;
        SerializedProperty newStop = stops.GetArrayElementAtIndex(0);
        newStop.FindPropertyRelative("stopTimer").intValue = 0;
        newStop.FindPropertyRelative("speed").intValue = 2;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(movingPlatform);
    }

    private void AddStop(MovingPlatform movingPlatform, SerializedProperty stops)
    {
        if (stops.arraySize >= movingPlatform.MaxStops)
            return;

        Transform lastStop = null;
        if (stops.arraySize > 0)
            lastStop = stops.GetArrayElementAtIndex(stops.arraySize - 1).FindPropertyRelative("stopTransform").objectReferenceValue as Transform;

        if (lastStop == null)
            return;

        Undo.RecordObject(movingPlatform, "Add Platform Stop");

        GameObject copy = Object.Instantiate(lastStop.gameObject, lastStop.position, lastStop.rotation, lastStop.parent);
        copy.name = GetNextNumberedName(lastStop.name);
        Undo.RegisterCreatedObjectUndo(copy, "Add Platform Stop");

        stops.arraySize++;
        SerializedProperty newStop = stops.GetArrayElementAtIndex(stops.arraySize - 1);
        newStop.FindPropertyRelative("stopTransform").objectReferenceValue = copy.transform;

        SerializedProperty previousStop = stops.GetArrayElementAtIndex(stops.arraySize - 2);
        newStop.FindPropertyRelative("stopTimer").intValue = previousStop.FindPropertyRelative("stopTimer").intValue;
        newStop.FindPropertyRelative("speed").intValue = previousStop.FindPropertyRelative("speed").intValue;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(movingPlatform);
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
}
