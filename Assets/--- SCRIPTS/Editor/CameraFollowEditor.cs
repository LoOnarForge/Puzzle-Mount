using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraFollow))]
public class CameraFollowEditor : Editor
{
    private const float MinHandleSeparation = 0.01f;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;
        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (iterator.name == "m_Script")
                continue;
            if (iterator.name == "ghostOcclusionFullUntilAlongTimToCam"
                || iterator.name == "ghostOcclusionGoneFromAlongTimToCam")
                continue;

            EditorGUILayout.PropertyField(iterator, true);

            if (iterator.name == "timQuadDistance")
                DrawGhostOcclusionAlongTimToCameraSlider();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawGhostOcclusionAlongTimToCameraSlider()
    {
        SerializedProperty fullUntil = serializedObject.FindProperty("ghostOcclusionFullUntilAlongTimToCam");
        SerializedProperty goneFrom = serializedObject.FindProperty("ghostOcclusionGoneFromAlongTimToCam");
        if (fullUntil == null || goneFrom == null)
            return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Ghost Fade Along Tim To Camera", EditorStyles.boldLabel);

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