using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RunodePower))]
public class RunodePowerEditor : Editor
{
    private RunodePower cubePower;
    private PowerLineType[] lastFaceTypes = new PowerLineType[6];
    private bool initialized = false;

    private void OnEnable()
    {
        cubePower = (RunodePower)target;

        lastFaceTypes[0] = cubePower.topFace;
        lastFaceTypes[1] = cubePower.bottomFace;
        lastFaceTypes[2] = cubePower.northFace;
        lastFaceTypes[3] = cubePower.southFace;
        lastFaceTypes[4] = cubePower.eastFace;
        lastFaceTypes[5] = cubePower.westFace;

        string[] faceNames = { "Top Face", "Bottom Face", "North Face", "South Face", "East Face", "West Face" };
        for (int i = 0; i < 6; i++)
            UpdateFacePrefab(i, lastFaceTypes[i], faceNames[i]);

        initialized = true;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawFaceSection("═══ TOP FACE ═══",    "topFace");
        DrawFaceSection("═══ BOTTOM FACE ═══",   "bottomFace");
        DrawFaceSection("═══ NORTH FACE ═══",   "northFace");
        DrawFaceSection("═══ SOUTH FACE ═══",   "southFace");
        DrawFaceSection("═══ EAST FACE ═══",   "eastFace");
        DrawFaceSection("═══ WEST FACE ═══",   "westFace");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Power Lines", EditorStyles.boldLabel);

        if (GUILayout.Button("RESET ALL FACES"))
        {
            cubePower.topFace    = PowerLineType.Empty;
            cubePower.bottomFace = PowerLineType.Empty;
            cubePower.northFace  = PowerLineType.Empty;
            cubePower.southFace  = PowerLineType.Empty;
            cubePower.eastFace   = PowerLineType.Empty;
            cubePower.westFace   = PowerLineType.Empty;
            
            // Apply changes immediately to trigger visual updates
            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            EditorUtility.SetDirty(cubePower);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("TEMP TEST BUTTONS", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All Horizontal")) SetAllFaces(PowerLineType.Horizontal);
        if (GUILayout.Button("All Vertical"))   SetAllFaces(PowerLineType.Vertical);
        if (GUILayout.Button("All Cross"))      SetAllFaces(PowerLineType.Cross);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All CornerLT"))    SetAllFaces(PowerLineType.CornerLeftTop);
        if (GUILayout.Button("All CornerTR"))   SetAllFaces(PowerLineType.CornerTopRight);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All CornerRB")) SetAllFaces(PowerLineType.CornerRightBottom);
        if (GUILayout.Button("All CornerBL"))  SetAllFaces(PowerLineType.CornerBottomLeft);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All T-Left"))  SetAllFaces(PowerLineType.TSectionLeft);
        if (GUILayout.Button("All T-Top"))   SetAllFaces(PowerLineType.TSectionTop);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("All T-Right"))  SetAllFaces(PowerLineType.TSectionRight);
        if (GUILayout.Button("All T-Bottom")) SetAllFaces(PowerLineType.TSectionBottom);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("DEBUG / REFERENCES", EditorStyles.boldLabel);
        
        bool showRefs = SessionState.GetBool("ShowRunodeRefs", false);
        if (GUILayout.Button(showRefs ? "Hide References" : "Show References"))
        {
            SessionState.SetBool("ShowRunodeRefs", !showRefs);
        }

        if (showRefs)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("obstructionController"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("topFaceTransform"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomFaceTransform"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("northFaceTransform"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("southFaceTransform"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eastFaceTransform"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("westFaceTransform"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("horizontalSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tSectionSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("crossSprite"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("topUpTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("topRightTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("topDownTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("topLeftTrigger"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomUpTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomRightTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomDownTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("bottomLeftTrigger"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("northUpTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("northRightTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("northDownTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("northLeftTrigger"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("southUpTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("southRightTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("southDownTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("southLeftTrigger"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("eastUpTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eastRightTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eastDownTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("eastLeftTrigger"));

            EditorGUILayout.PropertyField(serializedObject.FindProperty("westUpTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("westRightTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("westDownTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("westLeftTrigger"));
        }

        serializedObject.ApplyModifiedProperties();

        if (!initialized) return;

        PowerLineType[] currentFaceTypes = {
            cubePower.topFace, cubePower.bottomFace, cubePower.northFace,
            cubePower.southFace, cubePower.eastFace, cubePower.westFace
        };

        string[] faceNames = { "Top Face", "Bottom Face", "North Face", "South Face", "East Face", "West Face" };

        for (int i = 0; i < 6; i++)
        {
            if (currentFaceTypes[i] != lastFaceTypes[i])
            {
                UpdateFacePrefab(i, currentFaceTypes[i], faceNames[i]);
                lastFaceTypes[i] = currentFaceTypes[i];
            }
        }
    }

    private void DrawFaceSection(string header, string faceProperty)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(header, EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty(faceProperty), GUIContent.none);
    }

    private void UpdateFacePrefab(int faceIndex, PowerLineType newType, string faceName)
    {
        Transform faceTransform = GetFaceTransform(faceIndex);
        if (faceTransform == null) return;

        // Use the same finding logic as RunodePower to ensure we find the sprite
        Transform spriteTransform = null;
        foreach (Transform child in faceTransform)
        {
            if (child.name.StartsWith("Power Line Sprite"))
            {
                spriteTransform = child;
                break;
            }
        }

        if (spriteTransform == null) return;

        SpriteRenderer sr = spriteTransform.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (newType != PowerLineType.Empty)
        {
            spriteTransform.gameObject.SetActive(true);
            Sprite s = GetSpriteForType(newType);
            if (s != null)
            {
                sr.sprite = s;
                sr.size = s.bounds.size * 1.98f;
                spriteTransform.localRotation = Quaternion.Euler(0, 0, GetTypeRotation(newType));
            }
        }
        else
        {
            spriteTransform.gameObject.SetActive(false);
        }

        UpdateTriggerStates(faceIndex, newType);
        EditorUtility.SetDirty(spriteTransform.gameObject);
    }

    private void UpdateTriggerStates(int faceIndex, PowerLineType lineType)
    {
        PowerConnectionTrigger[] triggers = GetFaceTriggers(faceIndex);
        bool[] enableStates = GetTriggerStates(lineType);

        for (int i = 0; i < 4; i++)
        {
            if (triggers[i] != null)
                triggers[i].gameObject.SetActive(enableStates[i]);
        }
    }

    private PowerConnectionTrigger[] GetFaceTriggers(int faceIndex)
    {
        switch (faceIndex)
        {
            case 0: return new[] { cubePower.topUpTrigger,    cubePower.topRightTrigger,    cubePower.topDownTrigger,    cubePower.topLeftTrigger };
            case 1: return new[] { cubePower.bottomUpTrigger, cubePower.bottomRightTrigger, cubePower.bottomDownTrigger, cubePower.bottomLeftTrigger };
            case 2: return new[] { cubePower.northUpTrigger,  cubePower.northRightTrigger,  cubePower.northDownTrigger,  cubePower.northLeftTrigger };
            case 3: return new[] { cubePower.southUpTrigger,  cubePower.southRightTrigger,  cubePower.southDownTrigger,  cubePower.southLeftTrigger };
            case 4: return new[] { cubePower.eastUpTrigger,   cubePower.eastRightTrigger,   cubePower.eastDownTrigger,   cubePower.eastLeftTrigger };
            case 5: return new[] { cubePower.westUpTrigger,   cubePower.westRightTrigger,   cubePower.westDownTrigger,   cubePower.westLeftTrigger };
            default: return new PowerConnectionTrigger[4];
        }
    }

    private bool[] GetTriggerStates(PowerLineType lineType)
    {
        switch (lineType)
        {
            case PowerLineType.Horizontal:        return new[] { false, true,  false, true  };
            case PowerLineType.Vertical:          return new[] { true,  false, true,  false };
            case PowerLineType.CornerLeftTop:     return new[] { true,  false, false, true  };
            case PowerLineType.CornerTopRight:    return new[] { true,  true,  false, false };
            case PowerLineType.CornerRightBottom: return new[] { false, true,  true,  false };
            case PowerLineType.CornerBottomLeft:  return new[] { false, false, true,  true  };
            case PowerLineType.TSectionLeft:      return new[] { true,  true,  false, true  };
            case PowerLineType.TSectionTop:       return new[] { true,  true,  true,  false };
            case PowerLineType.TSectionRight:     return new[] { false, true,  true,  true  };
            case PowerLineType.TSectionBottom:    return new[] { true,  false, true,  true  };
            case PowerLineType.Cross:             return new[] { true,  true,  true,  true  };
            default:                              return new[] { false, false, false, false };
        }
    }

    private Transform GetFaceTransform(int faceIndex)
    {
        switch (faceIndex)
        {
            case 0: return cubePower.topFaceTransform;
            case 1: return cubePower.bottomFaceTransform;
            case 2: return cubePower.northFaceTransform;
            case 3: return cubePower.southFaceTransform;
            case 4: return cubePower.eastFaceTransform;
            case 5: return cubePower.westFaceTransform;
            default: return null;
        }
    }

    private Sprite GetSpriteForType(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.Horizontal:        return cubePower.horizontalSprite;
            case PowerLineType.Vertical:          return cubePower.verticalSprite;
            case PowerLineType.CornerTopRight:
            case PowerLineType.CornerRightBottom:
            case PowerLineType.CornerBottomLeft:
            case PowerLineType.CornerLeftTop:     return cubePower.cornerSprite;
            case PowerLineType.TSectionTop:
            case PowerLineType.TSectionRight:
            case PowerLineType.TSectionBottom:
            case PowerLineType.TSectionLeft:      return cubePower.tSectionSprite;
            case PowerLineType.Cross:             return cubePower.crossSprite;
            default: return null;
        }
    }

    private float GetTypeRotation(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.CornerLeftTop:     return 0f;
            case PowerLineType.CornerTopRight:    return 270f;
            case PowerLineType.CornerRightBottom: return 180f;
            case PowerLineType.CornerBottomLeft:  return 90f;
            case PowerLineType.TSectionLeft:      return 0f;
            case PowerLineType.TSectionTop:       return 270f;
            case PowerLineType.TSectionRight:     return 180f;
            case PowerLineType.TSectionBottom:    return 90f;
            default: return 0f;
        }
    }

    private void SetAllFaces(PowerLineType type)
    {
        cubePower.topFace    = type;
        cubePower.bottomFace = type;
        cubePower.northFace  = type;
        cubePower.southFace  = type;
        cubePower.eastFace   = type;
        cubePower.westFace   = type;
        
        // Apply changes immediately to trigger visual updates
        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        EditorUtility.SetDirty(cubePower);
    }
}
