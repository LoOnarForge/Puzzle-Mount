using UnityEngine;
using UnityEngine.InputSystem;

[ExecuteAlways]
public class RotationIndicatorPOC : MonoBehaviour
{
    [Header("ORBIT SETTINGS")]
    public float orbitRadius = 0.8f;
    public float orbitSpeed = 150f;
    public float arcDegrees = 300f;

    [Header("TAIL SETTINGS")]
    public float tailWidth = 0.08f;
    [Range(0, 1)] public float tailAlpha = 0.5f;

    [Header("BUBBLE SETTINGS")]
    public float bubbleScale = 0.25f;
    public Color bubbleColor = Color.white;
    public Sprite bubbleSprite; 

    [Header("LETTER SETTINGS")]
    public string qLetterText = "Q";
    public string eLetterText = "E";
    public int fontSize = 150;
    public Color letterColor = Color.black;

    [Header("COLORS")]
    public Color qColor = new Color(0.2f, 1f, 0.4f, 1f);
    public Color eColor = new Color(1f, 0.6f, 0.2f, 1f);
    public Color shiftColor = new Color(1f, 0.2f, 0.2f, 1f);

    private float orbitAngle;
    private GameObject qPivot, ePivot;

    private void OnEnable() { Cleanup(); Setup(); }
    private void OnDisable() { Cleanup(); }

    private void Cleanup()
    {
        if (this == null) return;
        for (int i = transform.childCount - 1; i >= 0; i--) {
            if (transform.GetChild(i).name.EndsWith("_POC")) {
                if (Application.isPlaying) Destroy(transform.GetChild(i).gameObject);
                else DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }
    }

    private void Setup()
    {
        if (this == null) return;
        qPivot = CreatePivot("Q_POC", Vector3.up, qLetterText);
        ePivot = CreatePivot("E_POC", Vector3.right, eLetterText);
    }

    private GameObject CreatePivot(string name, Vector3 axis, string label)
    {
        GameObject pivot = new GameObject(name);
        pivot.transform.SetParent(this.transform, false);

        // 1. Tail
        GameObject tailObj = new GameObject("Tail");
        tailObj.transform.SetParent(pivot.transform, false);
        LineRenderer lr = tailObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = false;
        lr.positionCount = 40;
        lr.widthMultiplier = tailWidth;
        lr.widthCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0.1f));
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.material.renderQueue = 3000;

        Vector3 headDir = (axis == Vector3.up) ? Vector3.forward : Vector3.up;
        for (int i = 0; i < 40; i++) {
            float t = i / 39f;
            lr.SetPosition(i, Quaternion.AngleAxis(-t * arcDegrees, axis) * headDir * orbitRadius);
        }

        // 2. Bubble
        GameObject bubble = new GameObject("Bubble");
        bubble.transform.SetParent(pivot.transform, false);
        bubble.transform.localPosition = headDir * orbitRadius;

        // 3. Circle Background
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(bubble.transform, false);
        SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
        sr.sprite = bubbleSprite;
        sr.color = bubbleColor;
        sr.sortingOrder = 10;

        // 4. Letter
        GameObject textObj = new GameObject("Letter");
        textObj.transform.SetParent(bubble.transform, false);
        textObj.transform.localPosition = new Vector3(0, 0, -0.01f);
        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.text = label;
        tm.color = letterColor;
        tm.fontStyle = FontStyle.Bold;
        tm.fontSize = fontSize;
        tm.characterSize = 0.05f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        
        MeshRenderer mr = textObj.GetComponent<MeshRenderer>();
        mr.sortingOrder = 11;
        // TextMesh default material has a special shader. To make it hide behind cube,
        // we use a standard sprite-compatible material but assign the font texture.
        Material textMat = new Material(Shader.Find("Sprites/Default"));
        textMat.mainTexture = tm.font.material.mainTexture;
        textMat.renderQueue = 3001;
        mr.sharedMaterial = textMat;

        return pivot;
    }

    private void Update()
    {
        if (qPivot == null || ePivot == null) Setup();

        bool isShift = false;
        if (Application.isPlaying && Keyboard.current != null) isShift = Keyboard.current.shiftKey.isPressed;
        
        if (Application.isPlaying) orbitAngle += Time.deltaTime * orbitSpeed * (isShift ? -1 : 1);
        else orbitAngle = 45f;

        qPivot.transform.localRotation = Quaternion.AngleAxis(orbitAngle, Vector3.up);
        ePivot.transform.localRotation = Quaternion.AngleAxis(orbitAngle, Vector3.right);

        Camera cam = Camera.main;
        #if UNITY_EDITOR
        if (cam == null) cam = Camera.current;
        #endif

        UpdatePivotVisuals(qPivot, qColor, isShift, cam);
        UpdatePivotVisuals(ePivot, eColor, isShift, cam);
    }

    private void UpdatePivotVisuals(GameObject pivot, Color baseColor, bool isShift, Camera cam)
    {
        if (!pivot) return;
        Color color = isShift ? shiftColor : baseColor;
        
        // Tail
        LineRenderer lr = pivot.GetComponentInChildren<LineRenderer>();
        if (lr) {
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) },
                      new[] { new GradientAlphaKey(tailAlpha, 0), new GradientAlphaKey(0, 0.8f) });
            lr.colorGradient = g;
            lr.widthMultiplier = tailWidth;
        }

        // Bubble
        Transform bubble = pivot.transform.Find("Bubble");
        if (bubble && cam) {
            bubble.rotation = cam.transform.rotation;
            bubble.localScale = Vector3.one * bubbleScale;

            // MATHEMATICAL OCCLUSION
            // If the bubble is on the far side of the cube relative to the camera, hide it.
            Vector3 toCam = (cam.transform.position - transform.position).normalized;
            Vector3 toBubble = (bubble.position - transform.position).normalized;
            float dot = Vector3.Dot(toCam, toBubble);
            
            // Hide if dot product is negative (pointing away from camera)
            bool isVisible = dot > -0.2f; 
            bubble.gameObject.SetActive(isVisible);
            if (lr) lr.enabled = isVisible;

            // Sync visual props
            bubble.GetComponentInChildren<SpriteRenderer>().color = bubbleColor;
            TextMesh tm = bubble.GetComponentInChildren<TextMesh>();
            tm.color = letterColor;
            tm.text = (pivot == qPivot) ? qLetterText : eLetterText;
        }
    }
}
