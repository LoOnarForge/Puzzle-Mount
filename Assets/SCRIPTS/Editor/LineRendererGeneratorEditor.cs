using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LineRendererGenerator))]
public class LineRendererGeneratorEditor : Editor
{
    private const int DefaultPivotCount = 2;
    private const int DefaultZigzagAmount = 3;
    private const int PivotCountMax = 20;
    private const int ZigzagAmountMax = 10;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("lineRendererPrefab"));

        LineRendererGenerator generator = (LineRendererGenerator)target;
        if (generator.LineRendererPrefab == null)
            EditorGUILayout.HelpBox("Assign the shared Line Renderer prefab used for all connection lines.", MessageType.Warning);
        else if (generator.LineRendererPrefab.GetComponent<LineRenderer>() == null)
            EditorGUILayout.HelpBox("Line Renderer Prefab must have a LineRenderer on its root.", MessageType.Warning);

        EditorGUILayout.Space(12);
        if (GUILayout.Button("Collect Devices List"))
            CollectDevices(generator);

        EditorGUILayout.Space(12);
        EditorGUI.BeginChangeCheck();
        DrawLinks(generator);
        if (EditorGUI.EndChangeCheck())
            EditorUtility.SetDirty(generator);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawLinks(LineRendererGenerator generator)
    {
        List<LineRendererGeneratorLinkEntry> links = generator.LinksEditable;
        if (links.Count == 0)
        {
            EditorGUILayout.HelpBox("No links yet. Press Collect Devices List.", MessageType.Info);
            return;
        }

        for (int i = 0; i < links.Count; i++)
        {
            LineRendererGeneratorLinkEntry entry = links[i];
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(entry.deviceName ?? "(missing name)", EditorStyles.boldLabel);
            entry.device = (GameObject)EditorGUILayout.ObjectField("Device", entry.device, typeof(GameObject), true);
            entry.pivotCount = EditorGUILayout.IntSlider("Pivot Points", entry.pivotCount, 0, PivotCountMax);
            entry.zigzagAmount = EditorGUILayout.IntSlider("Zigzag", entry.zigzagAmount, 0, ZigzagAmountMax);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create Line Renderer Child"))
                CreateOrRecreateLineChild(generator, entry);
            if (GUILayout.Button("Delete From LineRendererGenerator"))
                RemoveLinkAt(generator, i);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }
    }

    private static void CollectDevices(LineRendererGenerator generator)
    {
        List<GameObject> connected = ReadConnectedDevicesFromActivator(generator.gameObject);
        if (connected.Count == 0)
        {
            Debug.LogWarning($"LineRendererGenerator on '{generator.name}': no activator or no connected devices found.", generator);
            return;
        }

        Undo.RecordObject(generator, "Collect Line Renderer Generator Devices");

        List<LineRendererGeneratorLinkEntry> links = generator.LinksEditable;
        var existingByName = new Dictionary<string, LineRendererGeneratorLinkEntry>();
        foreach (LineRendererGeneratorLinkEntry link in links)
        {
            if (string.IsNullOrEmpty(link.deviceName))
                continue;
            if (!existingByName.ContainsKey(link.deviceName))
                existingByName.Add(link.deviceName, link);
        }

        foreach (GameObject device in connected)
        {
            if (device == null)
                continue;

            string name = device.name;
            if (existingByName.TryGetValue(name, out LineRendererGeneratorLinkEntry existing))
            {
                existing.device = device;
                continue;
            }

            var added = new LineRendererGeneratorLinkEntry
            {
                deviceName = name,
                device = device,
                pivotCount = DefaultPivotCount,
                zigzagAmount = DefaultZigzagAmount
            };
            links.Add(added);
            existingByName.Add(name, added);
        }

        EditorUtility.SetDirty(generator);
    }

    private static List<GameObject> ReadConnectedDevicesFromActivator(GameObject activatorRoot)
    {
        var result = new List<GameObject>();
        MonoBehaviour activator = GetActivatorComponent(activatorRoot);
        if (activator == null)
            return result;

        SerializedObject serializedActivator = new SerializedObject(activator);
        SerializedProperty devicesProperty = serializedActivator.FindProperty("connectedDevices");
        if (devicesProperty == null || !devicesProperty.isArray)
            return result;

        for (int i = 0; i < devicesProperty.arraySize; i++)
        {
            Object reference = devicesProperty.GetArrayElementAtIndex(i).objectReferenceValue;
            if (reference is GameObject deviceObject)
                result.Add(deviceObject);
        }

        return result;
    }

    private static MonoBehaviour GetActivatorComponent(GameObject activatorRoot)
    {
        Lever lever = activatorRoot.GetComponent<Lever>();
        if (lever != null)
            return lever;

        ButtonDevice button = activatorRoot.GetComponent<ButtonDevice>();
        if (button != null)
            return button;

        PressurePlate plate = activatorRoot.GetComponent<PressurePlate>();
        if (plate != null)
            return plate;

        return null;
    }

    private static void CreateOrRecreateLineChild(LineRendererGenerator generator, LineRendererGeneratorLinkEntry entry)
    {
        if (entry.device == null)
        {
            Debug.LogWarning($"LineRendererGenerator on '{generator.name}': no device assigned for '{entry.deviceName}'.", generator);
            return;
        }

        GameObject prefab = generator.LineRendererPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"LineRendererGenerator on '{generator.name}': assign Line Renderer Prefab before creating lines.", generator);
            return;
        }

        if (prefab.GetComponent<LineRenderer>() == null)
        {
            Debug.LogWarning($"LineRendererGenerator on '{generator.name}': prefab has no LineRenderer on root.", generator);
            return;
        }

        string childName = entry.deviceName;
        Transform existing = FindDirectChildByName(generator.transform, childName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        GameObject lineObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, generator.transform);
        Undo.RegisterCreatedObjectUndo(lineObject, "Create Line Renderer Connection");
        lineObject.name = childName;

        LineRenderer lineRenderer = lineObject.GetComponent<LineRenderer>();
        if (lineRenderer != null && !lineRenderer.useWorldSpace)
            lineRenderer.useWorldSpace = true;

        Vector3 start = generator.transform.position;
        Vector3 end = entry.device.transform.position;
        Vector3[] points = LineRendererPathBuilder.BuildPoints(
            start,
            end,
            entry.deviceName,
            entry.pivotCount,
            entry.zigzagAmount);

        lineRenderer.positionCount = points.Length;
        lineRenderer.SetPositions(points);

        EditorUtility.SetDirty(generator);
        EditorUtility.SetDirty(lineObject);
    }

    private static void RemoveLinkAt(LineRendererGenerator generator, int index)
    {
        List<LineRendererGeneratorLinkEntry> links = generator.LinksEditable;
        if (index < 0 || index >= links.Count)
            return;

        LineRendererGeneratorLinkEntry entry = links[index];
        Transform existing = FindDirectChildByName(generator.transform, entry.deviceName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        Undo.RecordObject(generator, "Remove Line Renderer Generator Link");
        links.RemoveAt(index);
        EditorUtility.SetDirty(generator);
    }

    private static Transform FindDirectChildByName(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;
        }

        return null;
    }
}
