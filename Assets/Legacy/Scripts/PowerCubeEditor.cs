// ============================================================
// COMMENTED OUT — replaced by RunodeEditor.cs
// Keep this file as reference until the new editor is confirmed working.
// ============================================================

/*
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
            lastFaceTypes[3] = cube.southFace;
            lastFaceTypes[4] = cube.eastFace;
            lastFaceTypes[5] = cube.westFace;
            initialized = true;
        }
    }

    public override void OnInspectorGUI()
    {
        PowerCube cube = (PowerCube)target;
        
        // Draw basic properties (Movement, Random Rotation, Debug Settings)
        DrawPropertiesExcluding(serializedObject, "topFace", "bottomFace", "northFace", "southFace", "eastFace", "westFace",
            "topFaceColor", "bottomFaceColor", "northFaceColor", "southFaceColor", "eastFaceColor", "westFaceColor",
            "topFaceConnectedPS", "bottomFaceConnectedPS", "northFaceConnectedPS", "southFaceConnectedPS", "eastFaceConnectedPS", "westFaceConnectedPS",
            "topFaceTransform", "bottomFaceTransform", "northFaceTransform", "southFaceTransform", "eastFaceTransform", "westFaceTransform",
            "horizontalSprite", "verticalSprite", "cornerSprite", "tSectionSprite", "crossSprite",
            "topUpTrigger", "topRightTrigger", "topDownTrigger", "topLeftTrigger",
            "bottomUpTrigger", "bottomRightTrigger", "bottomDownTrigger", "bottomLeftTrigger",
            "northUpTrigger", "northRightTrigger", "northDownTrigger", "northLeftTrigger",
            "southUpTrigger", "southRightTrigger", "southDownTrigger", "southLeftTrigger",
            "eastUpTrigger", "eastRightTrigger", "eastDownTrigger", "eastLeftTrigger",
            "westUpTrigger", "westRightTrigger", "westDownTrigger", "westLeftTrigger");
        
        // Draw each face section with all its properties grouped together
        DrawFaceSection("                    ═══ TOP FACE ═══", "topFace", "topFaceColor", "topFaceConnectedPS");
        DrawFaceSection("                  ═══ BOTTOM FACE ═══", "bottomFace", "bottomFaceColor", "bottomFaceConnectedPS");
        DrawFaceSection("                   ═══ NORTH FACE ═══", "northFace", "northFaceColor", "northFaceConnectedPS");
        DrawFaceSection("                   ═══ SOUTH FACE ═══", "southFace", "southFaceColor", "southFaceConnectedPS");
        DrawFaceSection("                    ═══ EAST FACE ═══", "eastFace", "eastFaceColor", "eastFaceConnectedPS");
        DrawFaceSection("                    ═══ WEST FACE ═══", "westFace", "westFaceColor", "westFaceConnectedPS");
        
        // Power Lines section with reset button  
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Power Lines", EditorStyles.boldLabel);
        
        if (GUILayout.Button("RESET ALL FACES"))
        {
            cube.topFace = PowerLineType.Empty;
            cube.bottomFace = PowerLineType.Empty;
            cube.northFace = PowerLineType.Empty;
            cube.southFace = PowerLineType.Empty;
            cube.eastFace = PowerLineType.Empty;
            cube.westFace = PowerLineType.Empty;
            EditorUtility.SetDirty(cube);
        }
        
        // Temporary test buttons at the bottom
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
        
        // Draw reference fields at bottom
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Inspector References", EditorStyles.boldLabel);
        
        // Face Transform References
        EditorGUILayout.LabelField("FACE TRANSFORM REFERENCES", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topFaceTransform"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomFaceTransform"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("northFaceTransform"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("southFaceTransform"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eastFaceTransform"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("westFaceTransform"));
        
        // Sprite References
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("SPRITE REFERENCES", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("horizontalSprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalSprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerSprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tSectionSprite"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("crossSprite"));
        
        // Trigger References
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("TOP FACE TRIGGERS", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topUpTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topRightTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topDownTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topLeftTrigger"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("BOTTOM FACE TRIGGERS", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomUpTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomRightTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomDownTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomLeftTrigger"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("NORTH FACE TRIGGERS", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("northUpTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("northRightTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("northDownTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("northLeftTrigger"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("SOUTH FACE TRIGGERS", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("southUpTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("southRightTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("southDownTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("southLeftTrigger"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("EAST FACE TRIGGERS", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eastUpTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eastRightTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eastDownTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("eastLeftTrigger"));
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("WEST FACE TRIGGERS", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("westUpTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("westRightTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("westDownTrigger"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("westLeftTrigger"));
        
        serializedObject.ApplyModifiedProperties();
        
        if (!initialized) return;
        
        // Check for changes after drawing inspector
        PowerLineType[] currentFaceTypes = {
            cube.topFace, cube.bottomFace, cube.northFace,
            cube.southFace, cube.eastFace, cube.westFace
        };
        
        string[] faceNames = { "Top Face", "Bottom Face", "North Face", "South Face", "East Face", "West Face" };
        
        for (int i = 0; i < 6; i++)
        {
            if (currentFaceTypes[i] != lastFaceTypes[i])
            {
                UpdateFacePrefab(cube, i, currentFaceTypes[i], faceNames[i]);
                lastFaceTypes[i] = currentFaceTypes[i];
            }
        }
    }
    
    private void DrawFaceSection(string header, string faceProperty, string colorProperty, string connectedPSProperty)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
        
        SerializedProperty faceProp = serializedObject.FindProperty(faceProperty);
        SerializedProperty colorProp = serializedObject.FindProperty(colorProperty);
        SerializedProperty psProp = serializedObject.FindProperty(connectedPSProperty);
        
        EditorGUILayout.PropertyField(faceProp, new GUIContent("Sprite Type"));
        EditorGUILayout.PropertyField(colorProp, new GUIContent("Line Color"));
        EditorGUILayout.PropertyField(psProp, new GUIContent("Connected PS"));
    }
    
    private void UpdateFacePrefab(PowerCube cube, int faceIndex, PowerLineType newType, string faceName)
    {
        Transform faceTransform = GetFaceTransform(cube, faceIndex);
        
        if (faceTransform == null)
        {
            Debug.LogWarning($"[PowerCube] Face transform not assigned for {faceName}");
            return;
        }
        
        Transform spriteTransform = faceTransform.Find("Power Line Sprite");
        if (spriteTransform == null)
        {
            Debug.LogWarning($"[PowerCube] No 'Power Line Sprite' child found on {faceName}");
            return;
        }
        
        SpriteRenderer spriteRenderer = spriteTransform.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning($"[PowerCube] No SpriteRenderer found on {faceName}/Power Line Sprite");
            return;
        }
        
        if (newType != PowerLineType.Empty)
        {
            Sprite sprite = GetSpriteForType(cube, newType);
            if (sprite != null)
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.size = sprite.bounds.size * 1.98f;
                float rotation = GetTypeRotation(newType);
                spriteTransform.localRotation = Quaternion.Euler(0, 0, rotation);
            }
            else
            {
                Debug.LogWarning($"[PowerCube] No sprite assigned for {newType}");
                spriteRenderer.sprite = null;
            }
        }
        else
        {
            spriteRenderer.sprite = null;
            spriteTransform.localRotation = Quaternion.identity;
        }
        
        UpdateTriggerStates(cube, faceIndex, newType);
    }
    
    private void UpdateTriggerStates(PowerCube cube, int faceIndex, PowerLineType lineType)
    {
        PowerConnectionTrigger[] triggers = GetFaceTriggers(cube, faceIndex);
        bool[] enableStates = GetTriggerStates(lineType);
        
        for (int i = 0; i < 4; i++)
        {
            if (triggers[i] != null)
            {
                triggers[i].gameObject.SetActive(enableStates[i]);
            }
        }
    }
    
    private PowerConnectionTrigger[] GetFaceTriggers(PowerCube cube, int faceIndex)
    {
        switch (faceIndex)
        {
            case 0: return new PowerConnectionTrigger[] { cube.topUpTrigger, cube.topRightTrigger, cube.topDownTrigger, cube.topLeftTrigger };
            case 1: return new PowerConnectionTrigger[] { cube.bottomUpTrigger, cube.bottomRightTrigger, cube.bottomDownTrigger, cube.bottomLeftTrigger };
            case 2: return new PowerConnectionTrigger[] { cube.northUpTrigger, cube.northRightTrigger, cube.northDownTrigger, cube.northLeftTrigger };
            case 3: return new PowerConnectionTrigger[] { cube.southUpTrigger, cube.southRightTrigger, cube.southDownTrigger, cube.southLeftTrigger };
            case 4: return new PowerConnectionTrigger[] { cube.eastUpTrigger, cube.eastRightTrigger, cube.eastDownTrigger, cube.eastLeftTrigger };
            case 5: return new PowerConnectionTrigger[] { cube.westUpTrigger, cube.westRightTrigger, cube.westDownTrigger, cube.westLeftTrigger };
            default: return new PowerConnectionTrigger[4];
        }
    }
    
    private bool[] GetTriggerStates(PowerLineType lineType)
    {
        switch (lineType)
        {
            case PowerLineType.Horizontal:
                return new bool[] { false, true, false, true };
            case PowerLineType.Vertical:
                return new bool[] { true, false, true, false };
            case PowerLineType.CornerLeftTop:
                return new bool[] { true, false, false, true };
            case PowerLineType.CornerTopRight:
                return new bool[] { true, true, false, false };
            case PowerLineType.CornerRightBottom:
                return new bool[] { false, true, true, false };
            case PowerLineType.CornerBottomLeft:
                return new bool[] { false, false, true, true };
            case PowerLineType.TSectionLeft:
                return new bool[] { true, true, false, true };
            case PowerLineType.TSectionTop:
                return new bool[] { true, true, true, false };
            case PowerLineType.TSectionRight:
                return new bool[] { false, true, true, true };
            case PowerLineType.TSectionBottom:
                return new bool[] { true, false, true, true };
            case PowerLineType.Cross:
                return new bool[] { true, true, true, true };
            case PowerLineType.Empty:
            default:
                return new bool[] { false, false, false, false };
        }
    }
    
    private Transform GetFaceTransform(PowerCube cube, int faceIndex)
    {
        switch (faceIndex)
        {
            case 0: return cube.topFaceTransform;
            case 1: return cube.bottomFaceTransform;
            case 2: return cube.northFaceTransform;
            case 3: return cube.southFaceTransform;
            case 4: return cube.eastFaceTransform;
            case 5: return cube.westFaceTransform;
            default: return null;
        }
    }
    
    private Sprite GetSpriteForType(PowerCube cube, PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.Horizontal: return cube.horizontalSprite;
            case PowerLineType.Vertical: return cube.verticalSprite;
            case PowerLineType.CornerTopRight:
            case PowerLineType.CornerRightBottom:
            case PowerLineType.CornerBottomLeft:
            case PowerLineType.CornerLeftTop:
                return cube.cornerSprite;
            case PowerLineType.TSectionTop:
            case PowerLineType.TSectionRight:
            case PowerLineType.TSectionBottom:
            case PowerLineType.TSectionLeft:
                return cube.tSectionSprite;
            case PowerLineType.Cross: return cube.crossSprite;
            default: return null;
        }
    }
    
    private float GetZRotationForFaceAndType(string faceName, PowerLineType type)
    {
        return GetTypeRotation(type);
    }
    
    private float GetTypeRotation(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.CornerLeftTop: return 0f;
            case PowerLineType.CornerTopRight: return 270f;
            case PowerLineType.CornerRightBottom: return 180f; 
            case PowerLineType.CornerBottomLeft: return 90f;
            case PowerLineType.TSectionLeft: return 0f;
            case PowerLineType.TSectionTop: return 270f;
            case PowerLineType.TSectionRight: return 180f;
            case PowerLineType.TSectionBottom: return 90f;
            default: return 0f;
        }
    }
    
    private void SetAllFaces(PowerCube cube, PowerLineType type)
    {
        cube.topFace = type;
        cube.bottomFace = type;
        cube.northFace = type;
        cube.southFace = type;
        cube.eastFace = type;
        cube.westFace = type;
        EditorUtility.SetDirty(cube);
    }
}

*/