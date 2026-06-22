using UnityEngine;
using System.Collections.Generic;


public class FaceObstructionDetector : MonoBehaviour
{
    private static readonly Color ObstructedColor = new Color(0.08f, 0.08f, 0.08f);

    private SpriteRenderer spriteRenderer;
    private RunodePower parentRunode;
    private PowerConnectionTrigger[] faceTriggers;
    private HashSet<Collider> obstructors = new HashSet<Collider>();
    private bool currentlyReportedObstructed = false;
    public bool IsObstructed => currentlyReportedObstructed;

    private void Awake()
    {
        // Move to Ignore Raycast layer so Tim's selection rays pass through to the main cube collider.
        // Trigger events will still fire because we use includeLayers below.
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

        SphereCollider col = GetComponent<SphereCollider>();
        if (col != null)
        {
            col.isTrigger = true;
            
            int mask = (1 << LayerMask.NameToLayer("Runodes")) | (1 << LayerMask.NameToLayer("Ground"));
            col.includeLayers = mask;
        }
    }

    private void Start()
    {
        // Initial scan for objects already inside
        SphereCollider col = GetComponent<SphereCollider>();
        if (col != null)
        {
            Vector3 center = transform.TransformPoint(col.center);
            float radius = col.radius * transform.lossyScale.x;
            int mask = (1 << LayerMask.NameToLayer("Runodes")) | (1 << LayerMask.NameToLayer("Ground"));
            
            Collider[] initialHits = Physics.OverlapSphere(center, radius, mask, QueryTriggerInteraction.Ignore);
            foreach (var hit in initialHits)
            {
                if (IsObstructor(hit))
                    obstructors.Add(hit);
            }
        }
        CheckObstruction();
    }

    // Called by RunodePower after face setup to inject dependencies.
    public void Initialize(RunodePower runode, PowerConnectionTrigger[] triggers)
    {
        parentRunode = runode;
        faceTriggers = triggers;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        // Re-evaluate every frame to catch when neighbor isMoving state changes
        CheckObstruction();
    }

    private void CheckObstruction()
    {
        bool isCurrentlyObstructed = false;

        if (obstructors.Count > 0)
        {
            // Clean up destroyed or inactive colliders
            obstructors.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy || !c.enabled);

            foreach (var col in obstructors)
            {
                // IRONCLAD: Only count as obstruction if the object is NOT currently moving/rotating.
                // This prevents "black line flicker" during animations.
                RunodeMovement move = col.GetComponentInParent<RunodeMovement>();
                if (move != null && move.IsBusy) continue;

                isCurrentlyObstructed = true;
                break;
            }
        }

        if (isCurrentlyObstructed != currentlyReportedObstructed)
        {
            currentlyReportedObstructed = isCurrentlyObstructed;
            SetObstructed(isCurrentlyObstructed);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsObstructor(other)) return;

        if (obstructors.Add(other))
        {
            CheckObstruction();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (obstructors.Remove(other))
        {
            CheckObstruction();
        }
    }

    private bool IsObstructor(Collider other)
    {
        if (other == null) return false;

        // Ignore the runode that owns this detector
        if (parentRunode != null)
        {
            if (other.transform == parentRunode.transform || other.transform.IsChildOf(parentRunode.transform))
                return false;
        }

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
