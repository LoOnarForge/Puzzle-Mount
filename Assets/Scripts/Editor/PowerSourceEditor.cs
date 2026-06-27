using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(PowerSource))]
public class PowerSourceEditor : Editor
{
    private PowerSource powerSource;
    private PowerLineType lastTopFace;
    private bool initialized = false;

    private void OnEnable()
    {
        powerSource = (PowerSource)target;
        lastTopFace = powerSource.topFace;
        UpdateFacePrefab(powerSource.topFace);
        initialized = true;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("═══ TOP FACE ═══", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("topFace"), GUIContent.none);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("POWER SOURCE SETTINGS", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("colorIndex"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxPower"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceCrystals"));

        EditorGUILayout.Space();
        if (GUILayout.Button("RESET TOP FACE"))
        {
            powerSource.topFace = PowerLineType.Empty;
            EditorUtility.SetDirty(powerSource);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("DEBUG / REFERENCES", EditorStyles.boldLabel);

        bool showRefs = SessionState.GetBool("ShowSourceRefs", false);
        if (GUILayout.Button(showRefs ? "Hide References" : "Show References"))
        {
            SessionState.SetBool("ShowSourceRefs", !showRefs);
        }

        if (showRefs)
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("topFaceTransform"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("powerSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("horizontalSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("verticalSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("cornerSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tSectionSprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("crossSprite"));

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("upTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("rightTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("downTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("leftTrigger"));
        }

        serializedObject.ApplyModifiedProperties();

        if (initialized && powerSource.topFace != lastTopFace)
        {
            UpdateFacePrefab(powerSource.topFace);
            lastTopFace = powerSource.topFace;
        }
    }

    private void UpdateFacePrefab(PowerLineType newType)
    {
        if (powerSource.topFaceTransform == null) return;

        Transform spriteTransform = powerSource.topFaceTransform.Find("Power Line Sprite");
        if (spriteTransform == null) return;

        SpriteRenderer sr = spriteTransform.GetComponent<SpriteRenderer>();
        if (sr == null) return;

        if (newType != PowerLineType.Empty)
        {
            spriteTransform.gameObject.SetActive(true);
            Sprite s = GetSprite(newType);
            if (s != null)
            {
                sr.sprite = s;
                sr.size = s.bounds.size * 1.98f;
                spriteTransform.localRotation = Quaternion.Euler(0, 0, GetRotation(newType));
            }
        }
        else
        {
            spriteTransform.gameObject.SetActive(false);
        }

        UpdateTriggers(newType);
        EditorUtility.SetDirty(spriteTransform.gameObject);
    }

    private void UpdateTriggers(PowerLineType type)
    {
        bool[] states = GetStates(type);
        if (powerSource.upTrigger != null) powerSource.upTrigger.gameObject.SetActive(states[0]);
        if (powerSource.rightTrigger != null) powerSource.rightTrigger.gameObject.SetActive(states[1]);
        if (powerSource.downTrigger != null) powerSource.downTrigger.gameObject.SetActive(states[2]);
        if (powerSource.leftTrigger != null) powerSource.leftTrigger.gameObject.SetActive(states[3]);
    }

    private bool[] GetStates(PowerLineType type)
    {
        switch (type)
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

    private Sprite GetSprite(PowerLineType type)
    {
        switch (type)
        {
            case PowerLineType.Horizontal: return powerSource.horizontalSprite;
            case PowerLineType.Vertical: return powerSource.verticalSprite;
            case PowerLineType.CornerTopRight:
            case PowerLineType.CornerRightBottom:
            case PowerLineType.CornerBottomLeft:
            case PowerLineType.CornerLeftTop: return powerSource.cornerSprite;
            case PowerLineType.TSectionTop:
            case PowerLineType.TSectionRight:
            case PowerLineType.TSectionBottom:
            case PowerLineType.TSectionLeft: return powerSource.tSectionSprite;
            case PowerLineType.Cross: return powerSource.crossSprite;
            default: return null;
        }
    }

    private float GetRotation(PowerLineType type)
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
}
