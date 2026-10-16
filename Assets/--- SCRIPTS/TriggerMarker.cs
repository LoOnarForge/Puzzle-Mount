using UnityEngine;

/// <summary>
/// Editor-only visual marker for invisible trigger colliders.
/// Attach to the trigger object (or assign its collider). Hidden during Play Mode.
/// </summary>
[DisallowMultipleComponent]
public class TriggerMarker : MonoBehaviour
{
    private const string IconChildName = "_TriggerMarkerIcon";

    [SerializeField] private Collider targetCollider;
    [SerializeField] private Color frameColor = new Color(0.3f, 0.85f, 1f, 0.35f);
    [SerializeField] private Sprite icon;
    [SerializeField] private float iconSize = 0.4f;
    [SerializeField] private Vector3 iconOffset = new Vector3(0.75f, 0.75f, 0f);

    public Collider TargetCollider => targetCollider != null ? targetCollider : GetComponent<Collider>();
    public Color FrameColor => frameColor;
    public Sprite Icon => icon;
    public float IconSize => iconSize;
    public Vector3 IconOffset => iconOffset;

    private void Reset()
    {
        targetCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
            SetEditorVisualsActive(false);
    }

    private void OnDisable()
    {
        SetEditorVisualsActive(false);
    }

    public void SetEditorVisualsActive(bool active)
    {
        Transform iconTransform = transform.Find(IconChildName);
        if (iconTransform != null)
            iconTransform.gameObject.SetActive(active);
    }

    public Transform GetOrCreateIconTransform()
    {
        Transform iconTransform = transform.Find(IconChildName);
        if (iconTransform != null)
            return iconTransform;

        var iconObject = new GameObject(IconChildName);
        iconObject.hideFlags = HideFlags.HideAndDontSave;
        iconTransform = iconObject.transform;
        iconTransform.SetParent(transform, false);
        iconObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        iconObject.AddComponent<SpriteRenderer>();
        return iconTransform;
    }
}
