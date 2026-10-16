using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class PrefabPlacementGridSnap
{
    private const float GridUnit = 1f;

    private static bool isApplyingSnap;
    private static int assetPrefabDropPendingUntilFrame = -1;

    static PrefabPlacementGridSnap()
    {
        ObjectChangeEvents.changesPublished += OnChangesPublished;
        SceneView.duringSceneGui += OnSceneViewGui;
        EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyWindowItemGui;
    }

    private static void OnSceneViewGui(SceneView sceneView)
    {
        TryMarkAssetPrefabDropFromDragPerform();
    }

    private static void OnHierarchyWindowItemGui(int instanceId, Rect selectionRect)
    {
        TryMarkAssetPrefabDropFromDragPerform();
    }

    private static void TryMarkAssetPrefabDropFromDragPerform()
    {
        Event currentEvent = Event.current;
        if (currentEvent == null || currentEvent.type != EventType.DragPerform)
            return;

        if (!IsDragFromProjectPrefabsOnly())
            return;

        MarkAssetPrefabDropPending();
    }

    private static void MarkAssetPrefabDropPending()
    {
        assetPrefabDropPendingUntilFrame = Time.frameCount + 1;
    }

    private static bool IsAssetPrefabDropPending()
    {
        return Time.frameCount <= assetPrefabDropPendingUntilFrame;
    }

    private static bool IsDragFromProjectPrefabsOnly()
    {
        Object[] references = DragAndDrop.objectReferences;
        if (references == null || references.Length == 0)
            return false;

        for (int i = 0; i < references.Length; i++)
        {
            if (!IsProjectPrefabAssetReference(references[i]))
                return false;
        }

        return true;
    }

    private static bool IsProjectPrefabAssetReference(Object reference)
    {
        if (reference == null)
            return false;

        string assetPath = AssetDatabase.GetAssetPath(reference);
        if (string.IsNullOrEmpty(assetPath))
            return false;

        return PrefabUtility.GetPrefabAssetType(reference) != PrefabAssetType.NotAPrefab;
    }

    private static void OnChangesPublished(ref ObjectChangeEventStream stream)
    {
        if (isApplyingSnap || EditorApplication.isPlayingOrWillChangePlaymode || !IsAssetPrefabDropPending())
            return;

        for (int i = 0; i < stream.length; i++)
        {
            if (stream.GetEventType(i) != ObjectChangeKind.CreateGameObjectHierarchy)
                continue;

            stream.GetCreateGameObjectHierarchyEvent(i, out CreateGameObjectHierarchyEventArgs createEvent);

            GameObject gameObject = EditorUtility.InstanceIDToObject(createEvent.instanceId) as GameObject;
            if (gameObject == null || !ShouldSnapCreatedObject(gameObject))
                continue;

            SnapWorldPositionToIntegerGrid(gameObject.transform);
        }
    }

    private static bool ShouldSnapCreatedObject(GameObject gameObject)
    {
        if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
            return PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject) == gameObject;

        return gameObject.transform.parent == null;
    }

    private static void SnapWorldPositionToIntegerGrid(Transform transform)
    {
        Vector3 position = transform.position;
        Vector3 snapped = new Vector3(
            Mathf.Round(position.x / GridUnit) * GridUnit,
            Mathf.Round(position.y / GridUnit) * GridUnit,
            Mathf.Round(position.z / GridUnit) * GridUnit);

        if (position == snapped)
            return;

        isApplyingSnap = true;
        Undo.RecordObject(transform, "Snap To Grid");
        transform.position = snapped;
        isApplyingSnap = false;
    }
}
