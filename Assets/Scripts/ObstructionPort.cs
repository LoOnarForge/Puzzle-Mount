using System.Collections.Generic;
using UnityEngine;

public class ObstructionPort : MonoBehaviour
{
    [SerializeField] private RunodeLine parentLine;

    [Header("State (Read-Only)")]
    [SerializeField] private List<GameObject> obstructions = new List<GameObject>();

    [SerializeField] private LayerMask obstructionLayers;

    public RunodeLine ParentLine => parentLine;
    public List<GameObject> Obstructions => obstructions;
    public bool IsObstructed => obstructions.Count > 0;

    private void OnTriggerEnter(Collider other)
    {
        if (parentLine == null) return;

        if ((obstructionLayers & (1 << other.gameObject.layer)) == 0) return;

        if (!obstructions.Contains(other.gameObject))
            obstructions.Add(other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentLine == null) return;

        obstructions.Remove(other.gameObject);
    }
}
