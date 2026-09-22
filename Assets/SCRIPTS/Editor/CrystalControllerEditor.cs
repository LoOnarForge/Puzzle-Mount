using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(CrystalController))]
public class CrystalControllerEditor : Editor
{
    private const string FoldoutEditorPrefsPrefix = "CrystalController_SetFoldout_";

    private SerializedProperty crystalSetsProperty;
    private ReorderableList[] crystalLists;
    private bool[] setFoldouts;
    private bool lastUpdateFoldout = true;

    private void OnEnable()
    {
        crystalSetsProperty = serializedObject.FindProperty("crystalSets");
        LoadFoldoutStates();
        BuildReorderableLists();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("BOULDER BASE:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("boulderBase"), new GUIContent("Boulder Base"));
        SerializedProperty boulderColor = serializedObject.FindProperty("boulderBaseColor");
        boulderColor.colorValue = EditorGUILayout.ColorField(
            new GUIContent("Boulder Base Color"),
            boulderColor.colorValue,
            true,
            true,
            true);
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("LIGHT:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("crystalLight"), new GUIContent("Crystal Light"));
        DrawDefaultLightIntensities();
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("COLOR LERP:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("colorLerpDuration"),
            new GUIContent("Color Lerp Duration (seconds)"));
        EditorGUILayout.Space(8);

        for (int i = 0; i < CrystalController.CrystalColorTypeCount; i++)
        {
            SerializedProperty setProperty = crystalSetsProperty.GetArrayElementAtIndex(i);

            bool wasExpanded = setFoldouts[i];
            setFoldouts[i] = EditorGUILayout.BeginFoldoutHeaderGroup(
                setFoldouts[i],
                CrystalController.GetSetHeaderLabel(i));

            if (setFoldouts[i])
            {
                EditorGUI.indentLevel++;
                DrawColorPair(setProperty.FindPropertyRelative("brightColors"), "Bright Colors:");
                DrawColorPair(setProperty.FindPropertyRelative("darkColors"), "Dark Colors:");

                EditorGUILayout.Space(4);
                DrawCrystalListCount(setProperty.FindPropertyRelative("crystals"));

                if (crystalLists != null && i < crystalLists.Length && crystalLists[i] != null)
                    crystalLists[i].DoLayoutList();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();

            if (wasExpanded != setFoldouts[i])
                SaveSetFoldoutState(i, setFoldouts[i]);

            EditorGUILayout.Space(4);
        }

        lastUpdateFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(lastUpdateFoldout, "LAST POWER SOURCE UPDATE");
        if (lastUpdateFoldout)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lastColorIndex"), new GUIContent("Color Index"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lastMaxMw"), new GUIContent("Max MW"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lastAvailableMw"), new GUIContent("Available MW"));
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDefaultLightIntensities()
    {
        SerializedProperty intensities = serializedObject.FindProperty("defaultLightIntensityPerType");
        if (intensities == null || !intensities.isArray)
            return;

        if (intensities.arraySize != CrystalController.CrystalColorTypeCount)
            intensities.arraySize = CrystalController.CrystalColorTypeCount;

        EditorGUI.indentLevel++;
        for (int i = 0; i < CrystalController.CrystalColorTypeCount; i++)
        {
            SerializedProperty element = intensities.GetArrayElementAtIndex(i);
            element.floatValue = EditorGUILayout.FloatField(
                new GUIContent($"{CrystalController.GetSetHeaderLabel(i)} Default Intensity"),
                element.floatValue);
        }

        EditorGUI.indentLevel--;
    }

    private static void DrawColorPair(SerializedProperty pairProperty, string groupLabel)
    {
        EditorGUILayout.LabelField(groupLabel, EditorStyles.miniBoldLabel);

        EditorGUI.indentLevel++;
        DrawHdrColorField(pairProperty.FindPropertyRelative("baseColor"), "Base Color");
        DrawHdrColorField(pairProperty.FindPropertyRelative("fresnelColor"), "Fresnel Color");
        DrawHdrColorField(pairProperty.FindPropertyRelative("parallaxColor"), "Parallax Color");
        EditorGUI.indentLevel--;
    }

    private static void DrawHdrColorField(SerializedProperty colorProperty, string label)
    {
        colorProperty.colorValue = EditorGUILayout.ColorField(
            new GUIContent(label),
            colorProperty.colorValue,
            true,
            true,
            true);
    }

    private static void DrawCrystalListCount(SerializedProperty crystalsProperty)
    {
        int newCount = EditorGUILayout.IntField("Crystal Count", crystalsProperty.arraySize);
        if (newCount != crystalsProperty.arraySize)
            crystalsProperty.arraySize = Mathf.Max(0, newCount);
    }

    private void BuildReorderableLists()
    {
        crystalLists = new ReorderableList[CrystalController.CrystalColorTypeCount];

        for (int i = 0; i < CrystalController.CrystalColorTypeCount; i++)
        {
            SerializedProperty setProperty = crystalSetsProperty.GetArrayElementAtIndex(i);
            SerializedProperty crystalsProperty = setProperty.FindPropertyRelative("crystals");

            ReorderableList list = new ReorderableList(serializedObject, crystalsProperty, true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    EditorGUI.LabelField(rect, $"Crystals (Order = Dim / Light)");
                },
                drawElementCallback = (rect, index, active, focused) =>
                {
                    SerializedProperty element = crystalsProperty.GetArrayElementAtIndex(index);
                    rect.y += 2f;
                    rect.height = EditorGUIUtility.singleLineHeight;
                    EditorGUI.PropertyField(rect, element, new GUIContent($"Slot {index}"));
                },
                elementHeight = EditorGUIUtility.singleLineHeight + 6f
            };

            crystalLists[i] = list;
        }
    }

    private void LoadFoldoutStates()
    {
        setFoldouts = new bool[CrystalController.CrystalColorTypeCount];
        int targetId = target.GetInstanceID();

        for (int i = 0; i < setFoldouts.Length; i++)
            setFoldouts[i] = EditorPrefs.GetBool(FoldoutEditorPrefsKey(targetId, i), true);
    }

    private void SaveSetFoldoutState(int setIndex, bool expanded)
    {
        EditorPrefs.SetBool(FoldoutEditorPrefsKey(target.GetInstanceID(), setIndex), expanded);
    }

    private static string FoldoutEditorPrefsKey(int targetInstanceId, int setIndex)
    {
        return $"{FoldoutEditorPrefsPrefix}{targetInstanceId}_{setIndex}";
    }
}
