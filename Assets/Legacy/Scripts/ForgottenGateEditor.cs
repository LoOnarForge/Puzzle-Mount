using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ForgottenGate))]
public class ForgottenGateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ForgottenGate gate = (ForgottenGate)target;

        EditorGUI.BeginChangeCheck();
        
        // Draw the default inspector for the socket configs and other fields
        base.OnInspectorGUI();

        if (EditorGUI.EndChangeCheck())
        {
            // If anything changed, update the child sockets in the scene
            Undo.RecordObjects(gate.GetComponentsInChildren<PowerSocket>(true), "Update Gate Sockets");
            gate.SetupSockets();
            EditorUtility.SetDirty(gate);
        }

        if (GUILayout.Button("Force Refresh Sockets"))
        {
            gate.SetupSockets();
        }
    }
}
