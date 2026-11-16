using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraFollow))]
public class CameraFollowEditor : Editor
{
    private const float MinHandleSeparation = 0.01f;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "ghostOcclusionFullUntilAlongTimToCam",
            "ghostOcclusionGoneFromAlongTimToCam");

        DrawGhostOcclusionAlongTimToCameraSlider();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawGhostOcclusionAlongTimToCameraSlider()
    {
        SerializedProperty fullUntil = serializedObject.FindProperty("ghostOcclusionFullUntilAlongTimToCam");
        SerializedProperty goneFrom = serializedObject.FindProperty("ghostOcclusionGoneFromAlongTimToCam");
        if (fullUntil == null || goneFrom == null)
            return;

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Ghost Fade", EditorStyles.boldLabel);

        float min = fullUntil.floatValue;
        float max = goneFrom.floatValue;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.MinMaxSlider(
            new GUIContent("Zones"),
            ref min,
            ref max,
            0f,
            1f);
        if (EditorGUI.EndChangeCheck())
        {
            min = Mathf.Clamp01(min);
            max = Mathf.Clamp01(max);
            if (max < min + MinHandleSeparation)
                max = Mathf.Min(1f, min + MinHandleSeparation);

            fullUntil.floatValue = min;
            goneFrom.floatValue = max;
        }
    }
}