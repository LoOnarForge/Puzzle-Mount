using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PowerCube))]
public class PowerCubeEditor : Editor
{
    private PowerLineType[] lastFaceTypes = new PowerLineType[6];
    private bool initialized = false;

    private void OnEnable()
    {
        PowerCube cube = (PowerCube)target;
        if (cube != null)
        {
            // Store initial values
            lastFaceTypes[0] = cube.topFace;
            lastFaceTypes[1] = cube.bottomFace;
            lastFaceTypes[2] = cube.northFace;
            lastFaceTypes[3] = cube.eastFace;
            lastFaceTypes[4] = cube.southFace;
            lastFaceTypes[5] = cube.westFace;
            initialized = true;
        }
    }

    public override void OnInspectorGUI()
    {
        PowerCube cube = (PowerCube)target;
        
        // Draw everything except Power Lines and PowerLine Prefabs sections
        DrawPropertiesExcluding(serializedObject, "topFace", "bottomFace", "northFace", "eastFace", "southFace", "westFace", 
            "horizontalPrefab", "verticalPrefab", "cornerLeftTopPrefab", "cornerTopRightPrefab", "cornerRightBottomPrefab", 
            "cornerBottomLeftPrefab", "tSectionLeftPrefab", "tSectionTopPrefab", "tSectionRightPrefab", "tSectionBottomPrefab", "crossPrefab");
        
        // Power Lines section with reset button
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Power Lines", EditorStyles.boldLabel);
        
        if (GUILayout.Button("RESET ALL FACES"))
        {
            cube.topFace = PowerLineType.Empty;
            cube.bottomFace = PowerLineType.Empty;
            cube.northFace = PowerLineType.Empty;
            cube.eastFace = PowerLineType.Empty;
            cube.southFace = PowerLineType.Empty;
            cube.westFace = PowerLineType.Empty;
            EditorUtility.SetDirty(cube);
        }
        
        // Temporary test buttons
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("TEMP TEST BUTTONS", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All Horizontal")) SetAllFaces(cube, PowerLineType.Horizontal);
        if (GUILayout.Button("All Vertical")) SetAllFaces(cube, PowerLineType.Vertical);
        if (GUILayout.Button("All Cross")) SetAllFaces(cube, PowerLineType.Cross);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All CornerLeftTop")) SetAllFaces(cube, PowerLineType.CornerLeftTop);
        if (GUILayout.Button("All CornerTopRight")) SetAllFaces(cube, PowerLineType.CornerTopRight);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All CornerRightBottom")) SetAllFaces(cube, PowerLineType.CornerRightBottom);
        if (GUILayout.Button("All CornerBottomLeft")) SetAllFaces(cube, PowerLineType.CornerBottomLeft);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All TSectionLeft")) SetAllFaces(cube, PowerLineType.TSectionLeft);
        if (GUILayout.Button("All TSectionTop")) SetAllFaces(cube, PowerLineType.TSectionTop);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All TSectionRight")) SetAllFaces(cube, PowerLineType.TSectionRight);
        if (GUILayout.Button("All TSectionBottom")) SetAllFaces(cube, PowerLineType.TSectionBottom);
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.Space();
        
        // Draw Power Lines dropdowns using SerializedProperty
        SerializedProperty topFaceProp = serializedObject.FindProperty("topFace");
        SerializedProperty bottomFaceProp = serializedObject.FindProperty("bottomFace");
        SerializedProperty northFaceProp = serializedObject.FindProperty("northFace");
        SerializedProperty eastFaceProp = serializedObject.FindProperty("eastFace");
        SerializedProperty southFaceProp = serializedObject.FindProperty("southFace");
        SerializedProperty westFaceProp = serializedObject.FindProperty("westFace");
        
        EditorGUILayout.PropertyField(topFaceProp, new GUIContent("Top Face"));
        EditorGUILayout.PropertyField(bottomFaceProp, new GUIContent("Bottom Face"));
        EditorGUILayout.PropertyField(northFaceProp, new GUIContent("North Face"));
        EditorGUILayout.PropertyField(eastFaceProp, new GUIContent("East Face"));
        EditorGUILayout.PropertyField(southFaceProp, new GUIContent("South Face"));
        EditorGUILayout.PropertyField(westFaceProp, new GUIContent("West Face"));
        
        // Power Line Prefabs section
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Power Line Prefabs", EditorStyles.boldLabel);
        SerializedProperty horizontalPrefab = serializedObject.FindProperty("horizontalPrefab");
        SerializedProperty verticalPrefab = serializedObject.FindProperty("verticalPrefab");
        SerializedProperty cornerLeftTopPrefab = serializedObject.FindProperty("cornerLeftTopPrefab");
        SerializedProperty cornerTopRightPrefab = serializedObject.FindProperty("cornerTopRightPrefab");
        SerializedProperty cornerRightBottomPrefab = serializedObject.FindProperty("cornerRightBottomPrefab");
        SerializedProperty cornerBottomLeftPrefab = serializedObject.FindProperty("cornerBottomLeftPrefab");
        SerializedProperty tSectionLeftPrefab = serializedObject.FindProperty("tSectionLeftPrefab");
        SerializedProperty tSectionTopPrefab = serializedObject.FindProperty("tSectionTopPrefab");
        SerializedProperty tSectionRightPrefab = serializedObject.FindProperty("tSectionRightPrefab");
        SerializedProperty tSectionBottomPrefab = serializedObject.FindProperty("tSectionBottomPrefab");
        SerializedProperty crossPrefab = serializedObject.FindProperty("crossPrefab");
        
        EditorGUILayout.PropertyField(horizontalPrefab);
        EditorGUILayout.PropertyField(verticalPrefab);
        EditorGUILayout.PropertyField(cornerLeftTopPrefab);
        EditorGUILayout.PropertyField(cornerTopRightPrefab);
        EditorGUILayout.PropertyField(cornerRightBottomPrefab);
        EditorGUILayout.PropertyField(cornerBottomLeftPrefab);
        EditorGUILayout.PropertyField(tSectionLeftPrefab);
        EditorGUILayout.PropertyField(tSectionTopPrefab);
        EditorGUILayout.PropertyField(tSectionRightPrefab);
        EditorGUILayout.PropertyField(tSectionBottomPrefab);
        EditorGUILayout.PropertyField(crossPrefab);
        
        serializedObject.ApplyModifiedProperties();
        
        if (!initialized) return;
        
        // Check for changes after drawing inspector
        PowerLineType[] currentFaceTypes = {
            cube.topFace, cube.bottomFace, cube.northFace,
            cube.eastFace, cube.southFace, cube.westFace
        };
        
        string[] faceNames = { "Top Face", "Bottom Face", "North Face", "East Face", "South Face", "West Face" };
        
        for (int i = 0; i < 6; i++)
        {
            if (currentFaceTypes[i] != lastFaceTypes[i])
            {
                UpdateFacePrefab(cube, i, currentFaceTypes[i], faceNames[i]);
                lastFaceTypes[i] = currentFaceTypes[i];
            }
        }
    }
    
    private void UpdateFacePrefab(PowerCube cube, int faceIndex, PowerLineType newType, string faceName)
    {
        // Find face transform under Visual PC first, then at root level
        Transform visualParent = cube.transform.Find("Visual PC");
        Transform faceTransform = null;
        
        if (visualParent != null)
        {
            faceTransform = visualParent.Find(faceName);
        }
        
        if (faceTransform == null)
        {
            faceTransform = cube.transform.Find(faceName);
        }
        
        if (faceTransform == null)
        {
            Debug.LogWarning($"[PowerCube] Could not find face '{faceName}' in cube hierarchy");
            return;
        }
        
        // Clear existing PowerLine children
        for (int i = faceTransform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(faceTransform.GetChild(i).gameObject);
        }
        
        // Instantiate new prefab if not Empty
        if (newType != PowerLineType.Empty)
        {
            GameObject prefab = GetPrefabForType(cube, newType);
            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                instance.transform.SetParent(faceTransform);
                instance.transform.localPosition = Vector3.zero;
                
                // Calculate correct Z rotation based on face and sprite type
                float zRotation = GetZRotationForFaceAndType(faceName, newType);
                instance.transform.localRotation = Quaternion.Euler(0, 0, zRotation);
            }
            else
            {
                Debug.LogWarning($"[PowerCube] No prefab assigned for {newType}");
            }
        }
    }
    
    private GameObject GetPrefabForType(PowerCube cube, PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.Horizontal: return cube.horizontalPrefab;
            case PowerLineType.Vertical: return cube.verticalPrefab;
            case PowerLineType.CornerTopRight: return cube.cornerTopRightPrefab;
            case PowerLineType.CornerRightBottom: return cube.cornerRightBottomPrefab;
            case PowerLineType.CornerBottomLeft: return cube.cornerBottomLeftPrefab;
            case PowerLineType.CornerLeftTop: return cube.cornerLeftTopPrefab;
            case PowerLineType.TSectionTop: return cube.tSectionTopPrefab;
            case PowerLineType.TSectionRight: return cube.tSectionRightPrefab;
            case PowerLineType.TSectionBottom: return cube.tSectionBottomPrefab;
            case PowerLineType.TSectionLeft: return cube.tSectionLeftPrefab;
            case PowerLineType.Cross: return cube.crossPrefab;
            default: return null;
        }
    }
    
    private float GetZRotationForFaceAndType(string faceName, PowerLineType type)
    {
        // Uniform rotation system - same values work on all faces
        return GetTypeRotation(type);
    }
    
    private float GetTypeRotation(PowerLineType type)
    {
        switch (type)
        {
            // Corner pieces
            case PowerLineType.CornerLeftTop: return 0f;
            case PowerLineType.CornerTopRight: return 270f;
            case PowerLineType.CornerRightBottom: return 180f; 
            case PowerLineType.CornerBottomLeft: return 90f;
            
            // T-section pieces 
            case PowerLineType.TSectionLeft: return 0f;
            case PowerLineType.TSectionTop: return 270f;
            case PowerLineType.TSectionRight: return 180f;
            case PowerLineType.TSectionBottom: return 90f;
            
            // Straight pieces and cross
            case PowerLineType.Horizontal: return 0f;
            case PowerLineType.Vertical: return 0f;
            case PowerLineType.Cross: return 0f;
            
            default: return 0f;
        }
    }
    
    private void SetAllFaces(PowerCube cube, PowerLineType type)
    {
        cube.topFace = type;
        cube.bottomFace = type;
        cube.northFace = type;
        cube.eastFace = type;
        cube.southFace = type;
        cube.westFace = type;
        EditorUtility.SetDirty(cube);
    }
}