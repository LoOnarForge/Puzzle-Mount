using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MaterialRandomizer))]
public class MaterialRandomizerEditor : Editor
{
    private const string SetupFoldoutKey = "MaterialRandomizer_SetupFoldout";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("applySameMaterialToAll"),
            new GUIContent("Apply Same Material To All"));

        EditorGUILayout.Space(8);

        SerializedProperty includeInPool = serializedObject.FindProperty("includeInPool");
        EnsureArraySize(includeInPool, MaterialRandomizer.SlotCount);

        for (int i = 0; i < MaterialRandomizer.SlotCount; i++)
        {
            EditorGUILayout.PropertyField(
                includeInPool.GetArrayElementAtIndex(i),
                new GUIContent($"Material {i + 1:00}"));
        }

        EditorGUILayout.Space(8);

        bool setupExpanded = EditorPrefs.GetBool(SetupFoldoutKey, false);
        setupExpanded = EditorGUILayout.Foldout(setupExpanded, "Materials & Renderers", true);
        EditorPrefs.SetBool(SetupFoldoutKey, setupExpanded);

        if (setupExpanded)
        {
            EditorGUI.indentLevel++;

            SerializedProperty materials = serializedObject.FindProperty("materials");
            EnsureArraySize(materials, MaterialRandomizer.SlotCount);

            for (int i = 0; i < MaterialRandomizer.SlotCount; i++)
            {
                EditorGUILayout.PropertyField(
                    materials.GetArrayElementAtIndex(i),
                    new GUIContent($"Material {i + 1:00}"));
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.PropertyField(
                serializedObject.FindProperty("meshRenderers"),
                new GUIContent("Mesh Renderers"),
                true);

            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void EnsureArraySize(SerializedProperty arrayProperty, int size)
    {
        if (arrayProperty.arraySize != size)
            arrayProperty.arraySize = size;
    }
}
