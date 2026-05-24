using UnityEditor;

[CustomEditor(typeof(RunodeMovement))]
public class RunodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }
}
