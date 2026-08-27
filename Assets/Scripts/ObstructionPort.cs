using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ObstructionPort : MonoBehaviour
{
    [SerializeField] private RunodeLine parentLine;

    [Header("DEBUGGING:")]
    [SerializeField] private List<GameObject> obstructions = new List<GameObject>();
    [SerializeField] private LayerMask obstructionLayers;

    private BoxCollider obstructionTrigger;
    private RunodeMovement ownerRunode;
    private readonly Collider[] overlapResults = new Collider[16];
    private int overlapCount;

    public bool IsObstructed => obstructions.Count > 0;

    private void Awake()
    {
        obstructionTrigger = GetComponent<BoxCollider>();
        ownerRunode = GetComponentInParent<RunodeMovement>();

        if (obstructionTrigger == null)
        {
            Debug.LogError(
                $"{nameof(ObstructionPort)} on {name} requires a {nameof(BoxCollider)}.",
                this);
        }
    }

    public void RefreshObstructionState()
    {
        obstructions.Clear();
        OverlapBox();
        RecordObstructions();
    }

    // Fills the overlap buffer with colliders inside this obstruction trigger.
    private void OverlapBox()
    {
        if (obstructionTrigger == null)
        {
            Debug.LogError(
                $"{nameof(ObstructionPort)} on {name} cannot refresh because its {nameof(BoxCollider)} is missing.",
                this);
            overlapCount = 0;
            return;
        }

        Vector3 center = obstructionTrigger.transform.TransformPoint(obstructionTrigger.center);
        
        Vector3 halfExtents = Vector3.Scale(
            obstructionTrigger.size,
            obstructionTrigger.transform.lossyScale) * 0.5f;

        overlapCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            overlapResults,
            obstructionTrigger.transform.rotation,
            obstructionLayers,
            QueryTriggerInteraction.Collide);
    }

    // Records every overlapping object that belongs to the obstruction layer mask.
    private void RecordObstructions()
    {
        for (int i = 0; i < overlapCount; i++)
        {
            Collider hit = overlapResults[i];

            if (ownerRunode != null && hit.GetComponentInParent<RunodeMovement>() == ownerRunode)
                continue;

            GameObject obstruction = hit.gameObject;

            if (!obstructions.Contains(obstruction))
                obstructions.Add(obstruction);
        }
    }
}
