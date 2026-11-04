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
        SerializedProperty stopPrototype = serializedObject.FindProperty("stopPrototype");
        EditorGUILayout.PropertyField(stopPrototype, new GUIContent("Stop Prototype"));

        SerializedProperty stops = serializedObject.FindProperty("stops");

        MovingPlatform movingPlatform = (MovingPlatform)target;
        if (stopPrototype.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Assign Stop Prototype before adding stops.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(stops.arraySize >= movingPlatform.MaxStops || stopPrototype.objectReferenceValue == null))
        {
            if (GUILayout.Button(stops.arraySize == 0 ? "Add First Stop" : "Add Stop"))
            {
                if (stops.arraySize == 0)
                    AddFirstStop(movingPlatform, stops, stopPrototype);
                else
                    AddStop(movingPlatform, stops, stopPrototype);
            }
        }

        if (stops.arraySize > movingPlatform.MaxStops)
        {
            EditorGUILayout.HelpBox($"This platform supports at most {movingPlatform.MaxStops} stops.", MessageType.Warning);
            stops.arraySize = movingPlatform.MaxStops;
        }

        EditorGUILayout.Space(10);
        for (int i = 0; i < stops.arraySize; i++)
            DrawStop(movingPlatform, stops, stopPrototype, i, stops.GetArrayElementAtIndex(i));

        EditorGUILayout.Space(20);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("currentStopIndex"));

        serializedObject.ApplyModifiedProperties();

        if (Application.isPlaying)
            Repaint();
    }

    private void DrawStop(MovingPlatform movingPlatform, SerializedProperty stops, SerializedProperty stopPrototype, int stopIndex, SerializedProperty stop)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField($"STOP {(stopIndex + 1):D2}:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(stop.FindPropertyRelative("stopTransform"), new GUIContent("Stop Transform"));
        SerializedProperty stopTimer = stop.FindPropertyRelative("stopTimer");
        stopTimer.intValue = EditorGUILayout.IntSlider(new GUIContent("Stop Timer"), stopTimer.intValue, 0, 10);
        SerializedProperty speed = stop.FindPropertyRelative("speed");
        speed.intValue = EditorGUILayout.IntSlider(new GUIContent("Speed"), speed.intValue, 0, 10);

        if (stopIndex > 0 && GUILayout.Button($"Delete Stop {(stopIndex + 1):D2}"))
            RemoveStop(movingPlatform, stops, stopPrototype, stopIndex);
    }

    private void RemoveStop(MovingPlatform movingPlatform, SerializedProperty stops, SerializedProperty stopPrototype, int stopIndex)
    {
        if (stopIndex < 0 || stopIndex >= stops.arraySize)
            return;

        Undo.RecordObject(movingPlatform, "Delete Platform Stop");

        GameObject prototype = stopPrototype.objectReferenceValue as GameObject;
        Transform stopTransform = stops.GetArrayElementAtIndex(stopIndex).FindPropertyRelative("stopTransform").objectReferenceValue as Transform;
        if (stopTransform != null && (prototype == null || stopTransform.gameObject != prototype))
            Undo.DestroyObjectImmediate(stopTransform.gameObject);

        stops.DeleteArrayElementAtIndex(stopIndex);

        SerializedProperty currentStopIndex = serializedObject.FindProperty("currentStopIndex");
        if (stops.arraySize == 0)
            currentStopIndex.intValue = 0;
        else if (currentStopIndex.intValue >= stops.arraySize)
            currentStopIndex.intValue = stops.arraySize - 1;
        else if (currentStopIndex.intValue > stopIndex)
            currentStopIndex.intValue--;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(movingPlatform);
    }

    private void AddFirstStop(MovingPlatform movingPlatform, SerializedProperty stops, SerializedProperty stopPrototype)
    {
        GameObject instance = CreateStopInstance(movingPlatform, stopPrototype, GetFirstStopSpawnPose(movingPlatform));
        if (instance == null)
            return;

        Undo.RecordObject(movingPlatform, "Add Platform Stop");

        stops.arraySize = 1;
        SerializedProperty newStop = stops.GetArrayElementAtIndex(0);
        newStop.FindPropertyRelative("stopTransform").objectReferenceValue = instance.transform;
        newStop.FindPropertyRelative("stopTimer").intValue = 0;
        newStop.FindPropertyRelative("speed").intValue = 2;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(movingPlatform);
    }

    private void AddStop(MovingPlatform movingPlatform, SerializedProperty stops, SerializedProperty stopPrototype)
    {
        if (stops.arraySize >= movingPlatform.MaxStops)
            return;

        Transform lastStop = stops.GetArrayElementAtIndex(stops.arraySize - 1).FindPropertyRelative("stopTransform").objectReferenceValue as Transform;
        StopSpawnPose spawnPose = lastStop != null
            ? new StopSpawnPose(lastStop.position, lastStop.rotation, lastStop.parent)
            : GetFirstStopSpawnPose(movingPlatform);

        GameObject instance = CreateStopInstance(movingPlatform, stopPrototype, spawnPose);
        if (instance == null)
            return;

        Undo.RecordObject(movingPlatform, "Add Platform Stop");

        string nameSource = lastStop != null ? lastStop.name : instance.name;
        instance.name = GetNextNumberedName(nameSource);

        stops.arraySize++;
        SerializedProperty newStop = stops.GetArrayElementAtIndex(stops.arraySize - 1);
        newStop.FindPropertyRelative("stopTransform").objectReferenceValue = instance.transform;

        SerializedProperty previousStop = stops.GetArrayElementAtIndex(stops.arraySize - 2);
        newStop.FindPropertyRelative("stopTimer").intValue = previousStop.FindPropertyRelative("stopTimer").intValue;
        newStop.FindPropertyRelative("speed").intValue = previousStop.FindPropertyRelative("speed").intValue;

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(movingPlatform);
    }

    private static StopSpawnPose GetFirstStopSpawnPose(MovingPlatform movingPlatform)
    {
        SerializedObject serializedPlatform = new SerializedObject(movingPlatform);
        Transform platform = serializedPlatform.FindProperty("platform").objectReferenceValue as Transform;
        Transform parent = movingPlatform.transform;
        Vector3 position = platform != null ? platform.position : parent.position;
        Quaternion rotation = platform != null ? platform.rotation : parent.rotation;
        return new StopSpawnPose(position, rotation, parent);
    }

    private static GameObject CreateStopInstance(MovingPlatform movingPlatform, SerializedProperty stopPrototype, StopSpawnPose spawnPose)
    {
        GameObject prototype = stopPrototype.objectReferenceValue as GameObject;
        if (prototype == null)
            return null;

        Transform parent = spawnPose.Parent != null ? spawnPose.Parent : movingPlatform.transform;
        GameObject instance = InstantiateStopPrototype(prototype, parent, spawnPose.Position, spawnPose.Rotation);
        if (instance == null)
            return null;

        Undo.RegisterCreatedObjectUndo(instance, "Add Platform Stop");
        return instance;
    }

    private static GameObject InstantiateStopPrototype(GameObject prototype, Transform parent, Vector3 position, Quaternion rotation)
    {
        GameObject instance;

        if (PrefabUtility.IsPartOfPrefabAsset(prototype))
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prototype, parent);
        else
            instance = Object.Instantiate(prototype, position, rotation, parent);

        if (instance == null)
            return null;

        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    private static string GetNextNumberedName(string sourceName)
    {
        int open = sourceName.LastIndexOf('(');
        int close = sourceName.LastIndexOf(')');

        if (open < 0 || close <= open || close != sourceName.Length - 1)
            return sourceName + " (002)";

        string numberPart = sourceName.Substring(open + 1, close - open - 1);
        if (!int.TryParse(numberPart, out int number))
            return sourceName;

        string prefix = sourceName.Substring(0, open);
        string nextNumber = (number + 1).ToString("D" + numberPart.Length);
        return prefix + "(" + nextNumber + ")";
    }

    private readonly struct StopSpawnPose
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Transform Parent { get; }

        public StopSpawnPose(Vector3 position, Quaternion rotation, Transform parent)
        {
            Position = position;
            Rotation = rotation;
            Parent = parent;
        }
    }
}
