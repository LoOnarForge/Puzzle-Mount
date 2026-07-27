using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(RunodeCube))]
public class RunodeCubeEditor : Editor
{
    private RunodeCube cube;
    private RunodeLineType[] lastFaceTypes = new RunodeLineType[6];
    private bool initialized = false;

    private void OnEnable()
    {
        cube = (RunodeCube)target;

        lastFaceTypes[0] = cube.topFace;
        lastFaceTypes[1] = cube.bottomFace;
        lastFaceTypes[2] = cube.northFace;
        lastFaceTypes[3] = cube.southFace;
        lastFaceTypes[4] = cube.eastFace;
        lastFaceTypes[5] = cube.westFace;

        string[] faceNames = { "Top Face", "Bottom Face", "North Face", "South Face", "East Face", "West Face" };
        for (int i = 0; i < 6; i++)
            UpdateFaceVisuals(i, lastFaceTypes[i], faceNames[i]);

        initialized = true;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (GUILayout.Button("RESET ALL FACES"))
        {
            cube.topFace    = RunodeLineType.Empty;
            cube.bottomFace = RunodeLineType.Empty;
            cube.northFace  = RunodeLineType.Empty;
            cube.southFace  = RunodeLineType.Empty;
            cube.eastFace   = RunodeLineType.Empty;
            cube.westFace   = RunodeLineType.Empty;

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            EditorUtility.SetDirty(cube);

            string[] faceNames = { "Top Face", "Bottom Face", "North Face", "South Face", "East Face", "West Face" };
            for (int i = 0; i < 6; i++)
            {
                UpdateFaceVisuals(i, RunodeLineType.Empty, faceNames[i]);
                lastFaceTypes[i] = RunodeLineType.Empty;
            }
        }

        DrawFaceSection("═══ TOP FACE ═══",    "topFace");
        DrawFaceSection("═══ BOTTOM FACE ═══", "bottomFace");
        DrawFaceSection("═══ NORTH FACE ═══",  "northFace");
        DrawFaceSection("═══ SOUTH FACE ═══",  "southFace");
        DrawFaceSection("═══ EAST FACE ═══",   "eastFace");
        DrawFaceSection("═══ WEST FACE ═══",   "westFace");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("DEBUG / REFERENCES", EditorStyles.boldLabel);

        bool showRefs = SessionState.GetBool("ShowRunodeCubeRefs", false);
        if (GUILayout.Button(showRefs ? "Hide References" : "Show References"))
        {
            SessionState.SetBool("ShowRunodeCubeRefs", !showRefs);
        }

        if (showRefs)
        {
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
        }

        serializedObject.ApplyModifiedProperties();

        if (!initialized) return;

        RunodeLineType[] currentFaceTypes = {
            cube.topFace, cube.bottomFace, cube.northFace,
            cube.southFace, cube.eastFace, cube.westFace
        };

        string[] faceNamesUpdate = { "Top Face", "Bottom Face", "North Face", "South Face", "East Face", "West Face" };

        for (int i = 0; i < 6; i++)
        {
            if (currentFaceTypes[i] != lastFaceTypes[i])
            {
                UpdateFaceVisuals(i, currentFaceTypes[i], faceNamesUpdate[i]);
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

    private void UpdateFaceVisuals(int faceIndex, RunodeLineType newType, string faceName)
    {
        Transform faceTransform = GetFaceTransform(faceIndex);
        if (faceTransform == null) return;

        Transform spriteTransform = null;

        foreach (Transform child in faceTransform)
        {
            if (child.name.Contains("Line Sprite"))
            {
                spriteTransform = child;
                break;
            }
        }

        if (spriteTransform != null)
        {
            if (newType != RunodeLineType.Empty)
            {
                spriteTransform.gameObject.SetActive(true);
                SpriteRenderer sr = spriteTransform.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Sprite s = GetSpriteForType(newType);
                    if (s != null)
                    {
                        sr.sprite = s;
                        sr.size = s.bounds.size * 1.98f;
                        spriteTransform.localRotation = Quaternion.Euler(0, 0, GetTypeRotation(newType));
                    }
                }
            }
            else
            {
                spriteTransform.gameObject.SetActive(false);
            }
            EditorUtility.SetDirty(spriteTransform.gameObject);
        }

        UpdatePortStates(faceTransform, newType);
        UpdateObstructionPort(faceTransform, newType);
    }

    private void UpdatePortStates(Transform faceTransform, RunodeLineType lineType)
    {
        if (faceTransform == null) return;

        bool[] enableStates = GetPortStates(lineType);

        foreach (Transform child in faceTransform)
        {
            string n = child.name.ToLower();
            if (n.Contains("port") && !n.Contains("obstruction"))
            {
                if (n.Contains("up"))         child.gameObject.SetActive(enableStates[0]);
                else if (n.Contains("right")) child.gameObject.SetActive(enableStates[1]);
                else if (n.Contains("down"))  child.gameObject.SetActive(enableStates[2]);
                else if (n.Contains("left"))  child.gameObject.SetActive(enableStates[3]);
            }
        }
    }

    private bool[] GetPortStates(RunodeLineType lineType)
    {
        switch (lineType)
        {
            case RunodeLineType.Horizontal:        return new[] { false, true,  false, true  };
            case RunodeLineType.Vertical:          return new[] { true,  false, true,  false };
            case RunodeLineType.CornerLeftTop:     return new[] { true,  false, false, true  };
            case RunodeLineType.CornerTopRight:    return new[] { true,  true,  false, false };
            case RunodeLineType.CornerRightBottom: return new[] { false, true,  true,  false };
            case RunodeLineType.CornerBottomLeft:  return new[] { false, false, true,  true  };
            case RunodeLineType.TSectionLeft:      return new[] { true,  true,  false, true  };
            case RunodeLineType.TSectionTop:       return new[] { true,  true,  true,  false };
            case RunodeLineType.TSectionRight:     return new[] { false, true,  true,  true  };
            case RunodeLineType.TSectionBottom:    return new[] { true,  false, true,  true  };
            case RunodeLineType.Cross:             return new[] { true,  true,  true,  true  };
            default:                               return new[] { false, false, false, false };
        }
    }

    private void UpdateObstructionPort(Transform faceTransform, RunodeLineType lineType)
    {
        if (faceTransform == null) return;

        foreach (Transform child in faceTransform)
        {
            if (child.name.ToLower().Contains("obstruction"))
            {
                child.gameObject.SetActive(lineType != RunodeLineType.Empty);
                EditorUtility.SetDirty(child.gameObject);
                break;
            }
        }
    }

    private Transform GetFaceTransform(int faceIndex)
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

    private Sprite GetSpriteForType(RunodeLineType type)
    {
        switch (type)
        {
            case RunodeLineType.Horizontal:        return cube.horizontalSprite;
            case RunodeLineType.Vertical:          return cube.verticalSprite;
            case RunodeLineType.CornerTopRight:
            case RunodeLineType.CornerRightBottom:
            case RunodeLineType.CornerBottomLeft:
            case RunodeLineType.CornerLeftTop:     return cube.cornerSprite;
            case RunodeLineType.TSectionTop:
            case RunodeLineType.TSectionRight:
            case RunodeLineType.TSectionBottom:
            case RunodeLineType.TSectionLeft:      return cube.tSectionSprite;
            case RunodeLineType.Cross:             return cube.crossSprite;
            default:                               return null;
        }
    }

    private float GetTypeRotation(RunodeLineType type)
    {
        switch (type)
        {
            case RunodeLineType.CornerLeftTop:     return 0f;
            case RunodeLineType.CornerTopRight:    return 270f;
            case RunodeLineType.CornerRightBottom: return 180f;
            case RunodeLineType.CornerBottomLeft:  return 90f;
            case RunodeLineType.TSectionLeft:      return 0f;
            case RunodeLineType.TSectionTop:       return 270f;
            case RunodeLineType.TSectionRight:     return 180f;
            case RunodeLineType.TSectionBottom:    return 90f;
            default:                               return 0f;
        }
    }
}
