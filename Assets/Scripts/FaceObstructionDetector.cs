using UnityEngine;

public class FaceObstructionDetector : MonoBehaviour
{
    private static readonly Color ObstructedColor = new Color(0.08f, 0.08f, 0.08f);

    private SpriteRenderer spriteRenderer;
    private RunodePower parentRunode;
    private PowerConnectionTrigger[] faceTriggers;
    private int obstructorCount = 0;
    public bool IsObstructed => obstructorCount > 0;

    // Called by RunodePower after face setup to inject dependencies.
    public void Initialize(RunodePower runode, PowerConnectionTrigger[] triggers)
    {
        parentRunode = runode;
        faceTriggers = triggers;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsObstructor(other)) return;

        obstructorCount++;
        if (obstructorCount == 1)
            SetObstructed(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsObstructor(other)) return;

        obstructorCount = Mathf.Max(0, obstructorCount - 1);
        if (obstructorCount == 0)
            SetObstructed(false);
    }

    private bool IsObstructor(Collider other)
    {
        int layer = other.gameObject.layer;
        return layer == LayerMask.NameToLayer("Runodes") || layer == LayerMask.NameToLayer("Ground");
    }

    private void SetObstructed(bool obstructed)
    {
        foreach (PowerConnectionTrigger trigger in faceTriggers)
        {
            if (trigger != null)
                trigger.SetObstructed(obstructed);
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = obstructed
                ? ObstructedColor
                : parentRunode.IsPowered ? parentRunode.currentPowerColor : Color.white;
        }

        PowerManager.Instance.RequestPowerFlowCheck();
    }
}
