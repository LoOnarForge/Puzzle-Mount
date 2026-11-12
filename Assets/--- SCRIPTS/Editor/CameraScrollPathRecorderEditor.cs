using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraScrollPathRecorder))]
public class CameraScrollPathRecorderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var recorder = (CameraScrollPathRecorder)target;

        EditorGUILayout.HelpBox(
            "Edit mode only. Position the camera along the path from max zoom IN to max zoom OUT (Move tool). " +
            "One recording — same path for every cardinal.\n\n" +
            "Record stop at each point, then Apply Path to Camera Follow.",
            MessageType.None);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("followTarget"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lookAtHeight"));
        EditorGUILayout.LabelField($"Recorded stops: {recorder.recordedStops.Count}", EditorStyles.boldLabel);

        if (GUILayout.Button("Record stop"))
        {
            Undo.RecordObject(recorder, "Record camera path stop");
            recorder.RecordStopFromCameraTransform();
            EditorUtility.SetDirty(recorder);
        }

        if (GUILayout.Button("Snap look at Tim (rotation only)"))
        {
            recorder.SnapLookAtTim();
        }

        if (GUILayout.Button("Clear all stops"))
        {
            Undo.RecordObject(recorder, "Clear camera path stops");
            recorder.recordedStops.Clear();
            EditorUtility.SetDirty(recorder);
        }

        if (recorder.recordedStops.Count > 0 && GUILayout.Button("Apply path to Camera Follow"))
        {
            CameraFollow follow = recorder.GetComponent<CameraFollow>();
            Undo.RecordObject(follow, "Apply scroll path");
            follow.SetScrollPathFromRecorder(recorder.recordedStops);
            EditorUtility.SetDirty(follow);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
