using UnityEngine;
using System.Collections.Generic;

public class ObstructionController : MonoBehaviour
{
    [Header("FACE DETECTORS")]
    public BoxCollider faceTop;
    public BoxCollider faceBottom;
    public BoxCollider faceNorth;
    public BoxCollider faceSouth;
    public BoxCollider faceEast;
    public BoxCollider faceWest;

    [Header("EDGE DETECTORS")]
    public BoxCollider edgeTN;
    public BoxCollider edgeTE;
    public BoxCollider edgeTS;
    public BoxCollider edgeTW;
    public BoxCollider edgeBN;
    public BoxCollider edgeBE;
    public BoxCollider edgeBS;
    public BoxCollider edgeBW;
    public BoxCollider edgeNW;
    public BoxCollider edgeNE;
    public BoxCollider edgeSW;
    public BoxCollider edgeSE;

    [Header("TARGET LAYERS")]
    public LayerMask obstructionMask;

    private HashSet<BoxCollider> obstructedZones = new HashSet<BoxCollider>();
    private RunodePower parentRunode;

    private void Awake()
    {
        parentRunode = GetComponentInParent<RunodePower>();
    }

    // Performs the spatial scan for all 18 zones.
    public void PerformSpatialSweep()
    {
        obstructedZones.Clear();

        CheckZone(faceTop);
        CheckZone(faceBottom);
        CheckZone(faceNorth);
        CheckZone(faceSouth);
        CheckZone(faceEast);
        CheckZone(faceWest);

        CheckZone(edgeTN);
        CheckZone(edgeTE);
        CheckZone(edgeTS);
        CheckZone(edgeTW);
        CheckZone(edgeBN);
        CheckZone(edgeBE);
        CheckZone(edgeBS);
        CheckZone(edgeBW);
        CheckZone(edgeNW);
        CheckZone(edgeNE);
        CheckZone(edgeSW);
        CheckZone(edgeSE);

        UpdateTriggerStates();
    }

    private void CheckZone(BoxCollider zone)
    {
        if (zone == null || !zone.gameObject.activeInHierarchy) return;

        Vector3 center = zone.transform.TransformPoint(zone.center);
        Vector3 halfExtents = zone.size * 0.5f;
        Quaternion rotation = zone.transform.rotation;

        // Use a slightly smaller box to avoid grazing neighbors
        Collider[] hits = Physics.OverlapBox(center, halfExtents * 0.95f, rotation, obstructionMask, QueryTriggerInteraction.Ignore);

        foreach (var hit in hits)
        {
            if (IsActualObstruction(hit))
            {
                obstructedZones.Add(zone);
                break;
            }
        }
    }

    private bool IsActualObstruction(Collider col)
    {
        if (col == null) return false;
        
        // Ignore self and children
        if (col.transform == transform.root || col.transform.IsChildOf(transform.root)) return false;

        return true;
    }

    private void UpdateTriggerStates()
    {
        if (parentRunode == null) return;

        // Face obstructions block all 4 triggers on that face
        UpdateFaceTriggers(faceTop, parentRunode.topUpTrigger, parentRunode.topRightTrigger, parentRunode.topDownTrigger, parentRunode.topLeftTrigger);
        UpdateFaceTriggers(faceBottom, parentRunode.bottomUpTrigger, parentRunode.bottomRightTrigger, parentRunode.bottomDownTrigger, parentRunode.bottomLeftTrigger);
        UpdateFaceTriggers(faceNorth, parentRunode.northUpTrigger, parentRunode.northRightTrigger, parentRunode.northDownTrigger, parentRunode.northLeftTrigger);
        UpdateFaceTriggers(faceSouth, parentRunode.southUpTrigger, parentRunode.southRightTrigger, parentRunode.southDownTrigger, parentRunode.southLeftTrigger);
        UpdateFaceTriggers(faceEast, parentRunode.eastUpTrigger, parentRunode.eastRightTrigger, parentRunode.eastDownTrigger, parentRunode.eastLeftTrigger);
        UpdateFaceTriggers(faceWest, parentRunode.westUpTrigger, parentRunode.westRightTrigger, parentRunode.westDownTrigger, parentRunode.westLeftTrigger);
    }

    private void UpdateFaceTriggers(BoxCollider faceZone, params PowerConnectionTrigger[] triggers)
    {
        bool isBlocked = obstructedZones.Contains(faceZone);
        foreach (var t in triggers)
        {
            if (t != null) t.isObstructed = isBlocked;
        }
    }

    // Returns true if the internal path between two triggers is pinched by an edge obstruction.
    public bool IsInternalPathPinch(PowerConnectionTrigger a, PowerConnectionTrigger b)
    {
        // This will be expanded once we map the 12 edges to trigger pairs in the next step.
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        DrawZoneGizmo(faceTop); DrawZoneGizmo(faceBottom);
        DrawZoneGizmo(faceNorth); DrawZoneGizmo(faceSouth); DrawZoneGizmo(faceEast); DrawZoneGizmo(faceWest);
        DrawZoneGizmo(edgeTN); DrawZoneGizmo(edgeTE); DrawZoneGizmo(edgeTS); DrawZoneGizmo(edgeTW);
        DrawZoneGizmo(edgeBN); DrawZoneGizmo(edgeBE); DrawZoneGizmo(edgeBS); DrawZoneGizmo(edgeBW);
        DrawZoneGizmo(edgeNW); DrawZoneGizmo(edgeNE); DrawZoneGizmo(edgeSW); DrawZoneGizmo(edgeSE);
    }

    private void DrawZoneGizmo(BoxCollider zone)
    {
        if (zone == null) return;
        Gizmos.color = obstructedZones.Contains(zone) ? Color.red : Color.green;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = zone.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(zone.center, zone.size);
        Gizmos.matrix = oldMatrix;
    }
}
