using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TriggerMarker))]
[CanEditMultipleObjects]
public class TriggerMarkerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetCollider"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("frameColor"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("icon"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("iconSize"));

        serializedObject.ApplyModifiedProperties();
    }
}

[InitializeOnLoad]
static class TriggerMarkerSceneDrawer
{
    private const int CircleSegments = 24;

    static TriggerMarkerSceneDrawer()
    {
        SceneView.beforeSceneGui += HandleSceneSelection;
        SceneView.duringSceneGui += DrawAllMarkers;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode)
            return;

        foreach (TriggerMarker marker in Object.FindObjectsByType<TriggerMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            marker.SetEditorVisualsActive(false);
    }

    private static void DrawAllMarkers(SceneView sceneView)
    {
        if (Application.isPlaying)
            return;

        foreach (TriggerMarker marker in Object.FindObjectsByType<TriggerMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            DrawFrame(marker);
            SyncIcon(marker, sceneView);
        }
    }

    private static void HandleSceneSelection(SceneView sceneView)
    {
        if (Application.isPlaying)
            return;

        Event e = Event.current;
        if (e.type != EventType.MouseDown || e.button != 0 || e.alt || Tools.viewToolActive)
            return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        TriggerMarker bestMarker = null;
        float bestDistance = float.MaxValue;

        foreach (TriggerMarker marker in Object.FindObjectsByType<TriggerMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Collider collider = marker.TargetCollider;
            if (collider == null || !collider.enabled)
                continue;

            if (TryGetPickDistance(ray, collider, out float distance) && distance < bestDistance)
            {
                bestDistance = distance;
                bestMarker = marker;
            }
        }

        if (bestMarker == null)
            return;

        if (Physics.Raycast(ray, out _, bestDistance - 0.001f, ~0, QueryTriggerInteraction.Ignore))
            return;

        GameObject target = bestMarker.gameObject;

        // Already selected — leave the click alone so move/rotate/scale handles work.
        if (IsInSelection(target) && !e.shift)
            return;

        SelectMarker(target);
        e.Use();
    }

    private static bool IsInSelection(GameObject target)
    {
        GameObject[] selected = Selection.gameObjects;
        for (int i = 0; i < selected.Length; i++)
        {
            if (selected[i] == target)
                return true;
        }

        return false;
    }

    private static bool TryGetPickDistance(Ray ray, Collider collider, out float distance)
    {
        if (collider.Raycast(ray, out RaycastHit hit, float.MaxValue))
        {
            distance = hit.distance;
            return true;
        }

        switch (collider)
        {
            case BoxCollider box:
                return RaycastBox(ray, box, out distance);
            case SphereCollider sphere:
                return RaycastSphere(ray, sphere, out distance);
        }

        return collider.bounds.IntersectRay(ray, out distance);
    }

    private static bool RaycastBox(Ray worldRay, BoxCollider box, out float distance)
    {
        distance = float.MaxValue;
        Transform transform = box.transform;
        Vector3 localOrigin = transform.InverseTransformPoint(worldRay.origin);
        Vector3 localDirection = transform.InverseTransformDirection(worldRay.direction);
        Ray localRay = new Ray(localOrigin, localDirection);

        Bounds localBounds = new Bounds(box.center, box.size);
        if (!localBounds.IntersectRay(localRay, out float localDistance))
            return false;

        Vector3 worldHit = transform.TransformPoint(localOrigin + localDirection * localDistance);
        distance = Vector3.Dot(worldHit - worldRay.origin, worldRay.direction.normalized);
        return distance >= 0f;
    }

    private static bool RaycastSphere(Ray ray, SphereCollider sphere, out float distance)
    {
        distance = float.MaxValue;
        Vector3 center = sphere.transform.TransformPoint(sphere.center);
        float radius = sphere.radius * GetMaxAxisScale(sphere.transform);
        Vector3 offset = ray.origin - center;

        float b = Vector3.Dot(offset, ray.direction);
        float c = Vector3.Dot(offset, offset) - radius * radius;
        float discriminant = b * b - c;
        if (discriminant < 0f)
            return false;

        float t = -b - Mathf.Sqrt(discriminant);
        if (t < 0f)
            t = -b + Mathf.Sqrt(discriminant);
        if (t < 0f)
            return false;

        distance = t;
        return true;
    }

    private static void SelectMarker(GameObject target)
    {
        if (Event.current.shift)
        {
            Object[] current = Selection.objects;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] == target)
                {
                    Selection.activeGameObject = target;
                    return;
                }
            }

            Object[] updated = new Object[current.Length + 1];
            current.CopyTo(updated, 0);
            updated[current.Length] = target;
            Selection.objects = updated;
            return;
        }

        Selection.activeGameObject = target;
    }

    private static void DrawFrame(TriggerMarker marker)
    {
        Collider collider = marker.TargetCollider;
        if (collider == null)
            return;

        Handles.color = marker.FrameColor;

        switch (collider)
        {
            case BoxCollider box:
                DrawBox(box);
                break;
            case SphereCollider sphere:
                DrawSphere(sphere);
                break;
            case CapsuleCollider capsule:
                DrawCapsule(capsule);
                break;
            default:
                DrawBounds(collider.bounds);
                break;
        }
    }

    private static void DrawBox(BoxCollider box)
    {
        Matrix4x4 matrix = box.transform.localToWorldMatrix;
        using (new Handles.DrawingScope(matrix))
            Handles.DrawWireCube(box.center, box.size);
    }

    private static void DrawBounds(Bounds bounds)
    {
        Handles.DrawWireCube(bounds.center, bounds.size);
    }

    private static void DrawSphere(SphereCollider sphere)
    {
        Vector3 center = sphere.transform.TransformPoint(sphere.center);
        float radius = sphere.radius * GetMaxAxisScale(sphere.transform);

        DrawCircle(center, radius, Vector3.right);
        DrawCircle(center, radius, Vector3.up);
        DrawCircle(center, radius, Vector3.forward);
    }

    private static void DrawCapsule(CapsuleCollider capsule)
    {
        Transform transform = capsule.transform;
        Vector3 axis = GetCapsuleAxis(capsule.direction);
        float radius = capsule.radius * GetPerpendicularScale(transform, axis);
        float height = Mathf.Max(capsule.height * GetAxisScale(transform, axis), radius * 2f);
        float cylinderHalf = Mathf.Max(0f, height * 0.5f - radius);

        Vector3 center = transform.TransformPoint(capsule.center);
        Vector3 worldAxis = transform.TransformDirection(axis).normalized;
        Vector3 topCenter = center + worldAxis * cylinderHalf;
        Vector3 bottomCenter = center - worldAxis * cylinderHalf;

        GetPerpendicularAxes(worldAxis, out Vector3 axisA, out Vector3 axisB);

        DrawCircle(topCenter, radius, worldAxis);
        DrawCircle(bottomCenter, radius, worldAxis);

        DrawLine(topCenter + axisA * radius, bottomCenter + axisA * radius);
        DrawLine(topCenter - axisA * radius, bottomCenter - axisA * radius);
        DrawLine(topCenter + axisB * radius, bottomCenter + axisB * radius);
        DrawLine(topCenter - axisB * radius, bottomCenter - axisB * radius);

        DrawArc(topCenter, axisA, axisB, radius, 0f, 180f);
        DrawArc(topCenter, axisB, -axisA, radius, 0f, 180f);
        DrawArc(bottomCenter, axisA, -axisB, radius, 0f, 180f);
        DrawArc(bottomCenter, axisB, axisA, radius, 0f, 180f);
    }

    private static void DrawCircle(Vector3 center, float radius, Vector3 normal)
    {
        if (radius <= 0f)
            return;

        GetPerpendicularAxes(normal, out Vector3 axisA, out Vector3 axisB);
        DrawArc(center, axisA, axisB, radius, 0f, 360f);
    }

    private static void DrawArc(Vector3 center, Vector3 startAxis, Vector3 endAxis, float radius, float startDegrees, float endDegrees)
    {
        if (radius <= 0f)
            return;

        Vector3 previousPoint = center + startAxis * radius;
        int segments = Mathf.Max(8, Mathf.RoundToInt(CircleSegments * (endDegrees - startDegrees) / 360f));

        for (int i = 1; i <= segments; i++)
        {
            float t = Mathf.Lerp(startDegrees, endDegrees, i / (float)segments);
            float radians = t * Mathf.Deg2Rad;
            Vector3 point = center + (startAxis * Mathf.Cos(radians) + endAxis * Mathf.Sin(radians)) * radius;
            DrawLine(previousPoint, point);
            previousPoint = point;
        }
    }

    private static void DrawLine(Vector3 from, Vector3 to)
    {
        Handles.DrawLine(from, to);
    }

    private static void SyncIcon(TriggerMarker marker, SceneView sceneView)
    {
        if (marker.Icon == null)
        {
            marker.SetEditorVisualsActive(false);
            return;
        }

        Collider collider = marker.TargetCollider;
        if (collider == null)
        {
            marker.SetEditorVisualsActive(false);
            return;
        }

        Transform iconTransform = marker.GetOrCreateIconTransform();
        iconTransform.gameObject.SetActive(true);
        iconTransform.position = collider.bounds.center;

        if (sceneView != null && sceneView.camera != null)
            iconTransform.rotation = sceneView.camera.transform.rotation;

        SpriteRenderer spriteRenderer = iconTransform.GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = marker.Icon;
        spriteRenderer.color = marker.FrameColor;

        float targetSize = Mathf.Max(0.01f, marker.IconSize);
        Vector2 spriteSize = marker.Icon.bounds.size;
        float maxSpriteAxis = Mathf.Max(spriteSize.x, spriteSize.y);
        float scale = maxSpriteAxis > 0f ? targetSize / maxSpriteAxis : targetSize;
        iconTransform.localScale = new Vector3(scale, scale, 1f);
    }

    private static Vector3 GetCapsuleAxis(int direction)
    {
        return direction switch
        {
            0 => Vector3.right,
            2 => Vector3.forward,
            _ => Vector3.up
        };
    }

    private static float GetAxisScale(Transform transform, Vector3 localAxis)
    {
        Vector3 worldAxis = transform.TransformDirection(localAxis);
        return worldAxis.magnitude;
    }

    private static float GetPerpendicularScale(Transform transform, Vector3 localAxis)
    {
        Vector3 scale = transform.lossyScale;
        if (localAxis == Vector3.right)
            return Mathf.Max(scale.y, scale.z);
        if (localAxis == Vector3.up)
            return Mathf.Max(scale.x, scale.z);
        return Mathf.Max(scale.x, scale.y);
    }

    private static float GetMaxAxisScale(Transform transform)
    {
        Vector3 scale = transform.lossyScale;
        return Mathf.Max(scale.x, scale.y, scale.z);
    }

    private static void GetPerpendicularAxes(Vector3 normal, out Vector3 axisA, out Vector3 axisB)
    {
        Vector3 reference = Mathf.Abs(normal.y) < 0.99f ? Vector3.up : Vector3.forward;
        axisA = Vector3.Cross(normal, reference).normalized;
        axisB = Vector3.Cross(normal, axisA).normalized;
    }
}
