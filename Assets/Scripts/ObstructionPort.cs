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
    private readonly Collider[] overlapResults = new Collider[16];
    private int overlapCount;

    public bool IsObstructed => obstructions.Count > 0;

    private void Awake()
    {
        obstructionTrigger = GetComponent<BoxCollider>();

        if (obstructionTrigger == null)
        {
            Debug.LogError(
                $"{nameof(ObstructionPort)} on {name} requires a {nameof(BoxCollider)}.",
                this);
        }
    }

    // Refreshes the complete obstruction state from the current overlap state.
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
            GameObject obstruction = overlapResults[i].gameObject;

            if (!obstructions.Contains(obstruction))
                obstructions.Add(obstruction);
        }
    }
}
