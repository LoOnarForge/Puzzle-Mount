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
        // Check if separator was selected
        if (newType == PowerLineType.Separator1 || newType == PowerLineType.Separator2 || newType == PowerLineType.Separator3)
        {
            Debug.LogError("WRONG FACE SELECTED !!!");
            
            // Reset to Empty
            switch (faceIndex)
            {
                case 0: cube.topFace = PowerLineType.Empty; break;
                case 1: cube.bottomFace = PowerLineType.Empty; break;
                case 2: cube.northFace = PowerLineType.Empty; break;
                case 3: cube.eastFace = PowerLineType.Empty; break;
                case 4: cube.southFace = PowerLineType.Empty; break;
                case 5: cube.westFace = PowerLineType.Empty; break;
            }
            EditorUtility.SetDirty(cube);
            return;
        }
        
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
                instance.transform.localRotation = GetRotationForFaceAndType(faceName, newType);
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
    
    private Quaternion GetRotationForFaceAndType(string faceName, PowerLineType type)
    {
        // Only T-sections and corners need rotation adjustment
        if (!IsRotationNeeded(type))
            return Quaternion.identity;
            
        switch (faceName)
        {
            case "Top Face":
                return GetTopFaceRotation(type);
            case "Bottom Face":
                return GetBottomFaceRotation(type);
            case "North Face":
                return GetNorthFaceRotation(type);
            case "East Face":
                return GetEastFaceRotation(type);
            case "South Face":
                return GetSouthFaceRotation(type);
            case "West Face":
                return GetWestFaceRotation(type);
            default:
                return Quaternion.identity;
        }
    }
    
    private bool IsRotationNeeded(PowerLineType type)
    {
        // Skip separators
        if (type == PowerLineType.Separator1 || type == PowerLineType.Separator2 || type == PowerLineType.Separator3)
            return false;
            
        return type == PowerLineType.CornerLeftTop || type == PowerLineType.CornerTopRight ||
               type == PowerLineType.CornerRightBottom || type == PowerLineType.CornerBottomLeft ||
               type == PowerLineType.TSectionLeft || type == PowerLineType.TSectionTop ||
               type == PowerLineType.TSectionRight || type == PowerLineType.TSectionBottom;
    }
    
    private Quaternion GetTopFaceRotation(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.CornerLeftTop: return Quaternion.identity; // 1st quarter
            case PowerLineType.CornerTopRight: return Quaternion.Euler(0, 0, 270); // 2nd quarter
            case PowerLineType.CornerRightBottom: return Quaternion.Euler(0, 0, 180); // 3rd quarter
            case PowerLineType.CornerBottomLeft: return Quaternion.Euler(0, 0, 90); // 4th quarter
            case PowerLineType.TSectionLeft: return Quaternion.identity;
            case PowerLineType.TSectionTop: return Quaternion.Euler(0, 0, 270);
            case PowerLineType.TSectionRight: return Quaternion.Euler(0, 0, 180);
            case PowerLineType.TSectionBottom: return Quaternion.Euler(0, 0, 90);
            default: return Quaternion.identity;
        }
    }
    
    private Quaternion GetBottomFaceRotation(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.CornerLeftTop: return Quaternion.Euler(180, 0, 0);
            case PowerLineType.CornerTopRight: return Quaternion.Euler(180, 0, 90);
            case PowerLineType.CornerRightBottom: return Quaternion.Euler(180, 180, 0);
            case PowerLineType.CornerBottomLeft: return Quaternion.Euler(180, 0, 270);
            case PowerLineType.TSectionLeft: return Quaternion.Euler(180, 0, 0);
            case PowerLineType.TSectionTop: return Quaternion.Euler(180, 0, 90);
            case PowerLineType.TSectionRight: return Quaternion.Euler(180, 180, 0);
            case PowerLineType.TSectionBottom: return Quaternion.Euler(180, 0, 270);
            default: return Quaternion.identity;
        }
    }
    
    private Quaternion GetNorthFaceRotation(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.CornerLeftTop: return Quaternion.Euler(90, 0, 0);
            case PowerLineType.CornerTopRight: return Quaternion.Euler(90, 90, 0);
            case PowerLineType.CornerRightBottom: return Quaternion.Euler(90, 180, 0);
            case PowerLineType.CornerBottomLeft: return Quaternion.Euler(90, 270, 0);
            case PowerLineType.TSectionLeft: return Quaternion.Euler(90, 0, 0);
            case PowerLineType.TSectionTop: return Quaternion.Euler(90, 90, 0);
            case PowerLineType.TSectionRight: return Quaternion.Euler(90, 180, 0);
            case PowerLineType.TSectionBottom: return Quaternion.Euler(90, 270, 0);
            default: return Quaternion.identity;
        }
    }
    
    private Quaternion GetEastFaceRotation(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.CornerLeftTop: return Quaternion.Euler(0, 0, 90);
            case PowerLineType.CornerTopRight: return Quaternion.Euler(90, 0, 90);
            case PowerLineType.CornerRightBottom: return Quaternion.Euler(180, 0, 90);
            case PowerLineType.CornerBottomLeft: return Quaternion.Euler(270, 0, 90);
            case PowerLineType.TSectionLeft: return Quaternion.Euler(0, 0, 90);
            case PowerLineType.TSectionTop: return Quaternion.Euler(90, 0, 90);
            case PowerLineType.TSectionRight: return Quaternion.Euler(180, 0, 90);
            case PowerLineType.TSectionBottom: return Quaternion.Euler(270, 0, 90);
            default: return Quaternion.identity;
        }
    }
    
    private Quaternion GetSouthFaceRotation(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.CornerLeftTop: return Quaternion.Euler(270, 0, 0);
            case PowerLineType.CornerTopRight: return Quaternion.Euler(270, 90, 0);
            case PowerLineType.CornerRightBottom: return Quaternion.Euler(270, 180, 0);
            case PowerLineType.CornerBottomLeft: return Quaternion.Euler(270, 270, 0);
            case PowerLineType.TSectionLeft: return Quaternion.Euler(270, 0, 0);
            case PowerLineType.TSectionTop: return Quaternion.Euler(270, 90, 0);
            case PowerLineType.TSectionRight: return Quaternion.Euler(270, 180, 0);
            case PowerLineType.TSectionBottom: return Quaternion.Euler(270, 270, 0);
            default: return Quaternion.identity;
        }
    }
    
    private Quaternion GetWestFaceRotation(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.CornerLeftTop: return Quaternion.Euler(0, 0, 270);
            case PowerLineType.CornerTopRight: return Quaternion.Euler(90, 0, 270);
            case PowerLineType.CornerRightBottom: return Quaternion.Euler(180, 0, 270);
            case PowerLineType.CornerBottomLeft: return Quaternion.Euler(270, 0, 270);
            case PowerLineType.TSectionLeft: return Quaternion.Euler(0, 0, 270);
            case PowerLineType.TSectionTop: return Quaternion.Euler(90, 0, 270);
            case PowerLineType.TSectionRight: return Quaternion.Euler(180, 0, 270);
            case PowerLineType.TSectionBottom: return Quaternion.Euler(270, 0, 270);
            default: return Quaternion.identity;
        }
    }
}