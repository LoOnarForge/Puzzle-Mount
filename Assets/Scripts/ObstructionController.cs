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

    // Face index → trigger index pairs for the 12 corner/edge connections.
    // Faces: 0=Top 1=Bottom 2=North 3=South 4=East 5=West
    // Triggers: 0=Up 1=Right 2=Down 3=Left
    private Dictionary<PowerConnectionTrigger, PowerConnectionTrigger> internalNeighborMap =
        new Dictionary<PowerConnectionTrigger, PowerConnectionTrigger>();
    private Dictionary<PowerConnectionTrigger, BoxCollider> neighborEdgeMap =
        new Dictionary<PowerConnectionTrigger, BoxCollider>();

    private void Start()
    {
        InitializeInternalNeighborMap();
    }

    private void InitializeInternalNeighborMap()
    {
        internalNeighborMap.Clear();
        neighborEdgeMap.Clear();

        RunodeFace[] faces = new RunodeFace[6];
        foreach (var face in GetComponentsInChildren<RunodeFace>())
        {
            if (face.faceIndex >= 0 && face.faceIndex < 6)
                faces[face.faceIndex] = face;
        }

        // Vertical corners
        MapInternal(faces, 2, 3, 4, 1, edgeNE); // northLeft  ↔ eastRight
        MapInternal(faces, 2, 1, 5, 3, edgeNW); // northRight ↔ westLeft
        MapInternal(faces, 3, 1, 4, 3, edgeSE); // southRight ↔ eastLeft
        MapInternal(faces, 3, 3, 5, 1, edgeSW); // southLeft  ↔ westRight

        // Top edges
        MapInternal(faces, 0, 0, 2, 0, edgeTN); // topUp    ↔ northUp
        MapInternal(faces, 0, 2, 3, 0, edgeTS); // topDown  ↔ southUp
        MapInternal(faces, 0, 1, 4, 0, edgeTE); // topRight ↔ eastUp
        MapInternal(faces, 0, 3, 5, 0, edgeTW); // topLeft  ↔ westUp

        // Bottom edges
        MapInternal(faces, 1, 2, 2, 2, edgeBN); // bottomDown  ↔ northDown
        MapInternal(faces, 1, 0, 3, 2, edgeBS); // bottomUp    ↔ southDown
        MapInternal(faces, 1, 1, 4, 2, edgeBE); // bottomRight ↔ eastDown
        MapInternal(faces, 1, 3, 5, 2, edgeBW); // bottomLeft  ↔ westDown
    }

    private void MapInternal(RunodeFace[] faces, int faceA, int trigA, int faceB, int trigB, BoxCollider edgeZone)
    {
        if (faces[faceA] == null || faces[faceB] == null) return;
        if (faces[faceA].triggers == null || faces[faceB].triggers == null) return;
        var a = faces[faceA].triggers[trigA];
        var b = faces[faceB].triggers[trigB];
        if (a == null || b == null) return;

        internalNeighborMap[a] = b;
        internalNeighborMap[b] = a;
        if (edgeZone != null)
        {
            neighborEdgeMap[a] = edgeZone;
            neighborEdgeMap[b] = edgeZone;
        }
    }

    // Performs the spatial scan for all 18 zones.
    public void PerformSpatialSweep()
    {
        obstructedZones.Clear();

        CheckZone(faceTop);    CheckZone(faceBottom);
        CheckZone(faceNorth);  CheckZone(faceSouth);
        CheckZone(faceEast);   CheckZone(faceWest);

        CheckZone(edgeTN); CheckZone(edgeTE); CheckZone(edgeTS); CheckZone(edgeTW);
        CheckZone(edgeBN); CheckZone(edgeBE); CheckZone(edgeBS); CheckZone(edgeBW);
        CheckZone(edgeNW); CheckZone(edgeNE); CheckZone(edgeSW); CheckZone(edgeSE);

        UpdateTriggerStates();
    }

    private void CheckZone(BoxCollider zone)
    {
        if (zone == null || !zone.gameObject.activeInHierarchy) return;

        Vector3 center      = zone.transform.TransformPoint(zone.center);
        Vector3 halfExtents = zone.size * 0.5f;
        Quaternion rotation = zone.transform.rotation;

        Collider[] hits = Physics.OverlapBox(center, halfExtents, rotation, obstructionMask, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (IsActualObstruction(hit)) { obstructedZones.Add(zone); break; }
        }
    }

    private bool IsActualObstruction(Collider col)
    {
        if (col == null) return false;
        if (col.transform == transform.root || col.transform.IsChildOf(transform.root)) return false;
        return true;
    }

    private void UpdateTriggerStates()
    {
        foreach (RunodeFace face in GetComponentsInChildren<RunodeFace>())
        {
            if (face.triggers == null) continue;
            bool blocked = face.faceZone != null && obstructedZones.Contains(face.faceZone);
            foreach (var t in face.triggers)
            {
                if (t != null) t.isObstructed = blocked;
            }
        }
    }

    public bool IsFaceObstructed(BoxCollider faceZone)
    {
        return obstructedZones.Contains(faceZone);
    }

    // Returns the corner/edge-wrapped neighbor of t, or null if the path is pinched.
    public PowerConnectionTrigger GetInternalNeighbor(PowerConnectionTrigger t)
    {
        if (!internalNeighborMap.TryGetValue(t, out var neighbor)) return null;
        if (neighborEdgeMap.TryGetValue(t, out var edge) && obstructedZones.Contains(edge)) return null;
        return neighbor;
    }

    // Returns true if the internal path between two triggers is blocked by an edge obstruction.
    public bool IsInternalPathPinch(PowerConnectionTrigger a, PowerConnectionTrigger b)
    {
        if (a == null || b == null) return false;
        if (internalNeighborMap.TryGetValue(a, out var neighbor) && neighbor == b)
            return neighborEdgeMap.TryGetValue(a, out var edge) && obstructedZones.Contains(edge);
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        DrawZoneGizmo(faceTop);  DrawZoneGizmo(faceBottom);
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
