using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(DPSCrystalController))]
public class DPSCrystalControllerEditor : Editor
{
    private const string FoldoutEditorPrefsPrefix = "DPSCrystalController_SetFoldout_";

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

        EditorGUILayout.LabelField("LIGHT:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("crystalLight"), new GUIContent("Crystal Light"));
        DrawDefaultLightIntensities();
        EditorGUILayout.Space(8);

        EditorGUILayout.LabelField("COLOR LERP:", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            serializedObject.FindProperty("colorLerpDuration"),
            new GUIContent("Color Lerp Duration (seconds)"));
        EditorGUILayout.Space(8);

        for (int i = 0; i < DPSCrystalController.CrystalColorTypeCount; i++)
        {
            SerializedProperty setProperty = crystalSetsProperty.GetArrayElementAtIndex(i);

            bool wasExpanded = setFoldouts[i];
            setFoldouts[i] = EditorGUILayout.BeginFoldoutHeaderGroup(
                setFoldouts[i],
                DPSCrystalController.GetSetHeaderLabel(i));

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

        lastUpdateFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(lastUpdateFoldout, "LAST DEVICE SOCKET UPDATE");
        if (lastUpdateFoldout)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lastColorIndex"), new GUIContent("Color Index"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lastRequiredMw"), new GUIContent("Required MW"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("lastAllocatedMw"), new GUIContent("Allocated MW"));
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

        if (intensities.arraySize != DPSCrystalController.CrystalColorTypeCount)
            intensities.arraySize = DPSCrystalController.CrystalColorTypeCount;

        EditorGUI.indentLevel++;
        for (int i = 0; i < DPSCrystalController.CrystalColorTypeCount; i++)
        {
            SerializedProperty element = intensities.GetArrayElementAtIndex(i);
            element.floatValue = EditorGUILayout.FloatField(
                new GUIContent(DPSCrystalController.GetLightIntensityFieldLabel(i)),
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
        newCount = Mathf.Clamp(newCount, 0, DPSCrystalController.MaxCrystalSlots);
        if (newCount != crystalsProperty.arraySize)
            crystalsProperty.arraySize = newCount;
    }

    private void BuildReorderableLists()
    {
        crystalLists = new ReorderableList[DPSCrystalController.CrystalColorTypeCount];

        for (int i = 0; i < DPSCrystalController.CrystalColorTypeCount; i++)
        {
            SerializedProperty setProperty = crystalSetsProperty.GetArrayElementAtIndex(i);
            SerializedProperty crystalsProperty = setProperty.FindPropertyRelative("crystals");

            ReorderableList list = new ReorderableList(serializedObject, crystalsProperty, true, false, true, true)
            {
                onAddCallback = reorderableList =>
                {
                    if (crystalsProperty.arraySize >= DPSCrystalController.MaxCrystalSlots)
                        return;

                    crystalsProperty.arraySize++;
                    reorderableList.index = crystalsProperty.arraySize - 1;
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
        setFoldouts = new bool[DPSCrystalController.CrystalColorTypeCount];
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
