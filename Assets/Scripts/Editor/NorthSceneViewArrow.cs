using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class NorthSceneViewArrow
{
    private const float ArrowLength = 28f;
    private const float ArrowWidth = 10f;
    private const float LineWidth = 5.75f;
    private const float Margin = 32f;

    static NorthSceneViewArrow()
    {
        SceneView.duringSceneGui += DrawArrow;
    }

    private static void DrawArrow(SceneView sceneView)
    {
        if (sceneView == null || sceneView.camera == null)
            return;

        Vector2 northDirection = GetNorthDirection(sceneView.camera);
        float sceneViewWidth = sceneView.camera.pixelWidth / EditorGUIUtility.pixelsPerPoint;
        float sceneViewHeight = sceneView.camera.pixelHeight / EditorGUIUtility.pixelsPerPoint;
        Vector2 arrowPoint = new Vector2(sceneViewWidth - Margin, sceneViewHeight - Margin);
        Vector2 arrowEnd = arrowPoint + northDirection * ArrowLength;
        Vector2 leftWing = arrowPoint - Rotate(northDirection, 150f) * ArrowWidth;
        Vector2 rightWing = arrowPoint - Rotate(northDirection, -150f) * ArrowWidth;

        Handles.BeginGUI();
        Color previousColor = Handles.color;
        Handles.color = Color.white;
        Handles.DrawAAPolyLine(LineWidth, arrowPoint, arrowEnd);
        Handles.DrawAAPolyLine(LineWidth, leftWing, arrowPoint, rightWing);
        Handles.color = previousColor;
        Handles.EndGUI();
    }

    private static Vector2 GetNorthDirection(Camera sceneCamera)
    {
        Vector3 north = Vector3.forward;
        Vector3 cameraRight = sceneCamera.transform.right;
        Vector3 cameraUp = sceneCamera.transform.up;
        Vector2 direction = new Vector2(
            Vector3.Dot(north, cameraRight),
            -Vector3.Dot(north, cameraUp));

        if (direction.sqrMagnitude < 0.001f)
            return Vector2.up;

        return -direction.normalized;
    }

    private static Vector2 Rotate(Vector2 direction, float degrees)
    {
        float radians = degrees * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(radians);
        float sine = Mathf.Sin(radians);

        return new Vector2(
            direction.x * cosine - direction.y * sine,
            direction.x * sine + direction.y * cosine);
    }
}
