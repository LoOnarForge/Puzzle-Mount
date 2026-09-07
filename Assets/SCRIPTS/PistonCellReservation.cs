using System.Collections.Generic;
using UnityEngine;

// Shared grid-cell claims for piston extend and cube push steps.
public static class PistonCellReservation
{
    public enum Priority
    {
        ExtendOnly = 0,
        PushStep = 1
    }

    private struct CellClaim
    {
        public MonoBehaviour Owner;
        public Priority Priority;
    }

    private static readonly Dictionary<Vector3Int, CellClaim> claims = new Dictionary<Vector3Int, CellClaim>();

    // Tries to claim one or more grid cells for this step. Higher priority can replace ExtendOnly claims.
    public static bool TryClaimAll(
        MonoBehaviour owner,
        Priority priority,
        Vector3 worldCellA,
        Vector3 worldCellB = default,
        bool useSecondCell = false)
    {
        if (owner == null)
            return false;

        Vector3Int cellA = ToCellKey(worldCellA);
        if (!TryClaimCell(cellA, owner, priority))
            return false;

        if (!useSecondCell)
            return true;

        Vector3Int cellB = ToCellKey(worldCellB);
        if (cellB == cellA || TryClaimCell(cellB, owner, priority))
            return true;

        if (claims.TryGetValue(cellA, out CellClaim claim) && claim.Owner == owner)
            claims.Remove(cellA);

        return false;
    }

    // True when this piston still owns the claimed grid cell.
    public static bool OwnsCell(MonoBehaviour owner, Vector3 worldPosition)
    {
        if (owner == null)
            return false;

        Vector3Int cell = ToCellKey(worldPosition);
        return claims.TryGetValue(cell, out CellClaim claim) && claim.Owner == owner;
    }

    // Clears every cell claimed by this piston.
    public static void Release(MonoBehaviour owner)
    {
        if (owner == null)
            return;

        List<Vector3Int> ownedCells = null;

        foreach (KeyValuePair<Vector3Int, CellClaim> entry in claims)
        {
            if (entry.Value.Owner != owner)
                continue;

            ownedCells ??= new List<Vector3Int>();
            ownedCells.Add(entry.Key);
        }

        if (ownedCells == null)
            return;

        foreach (Vector3Int cell in ownedCells)
            claims.Remove(cell);
    }

    private static bool TryClaimCell(Vector3Int cell, MonoBehaviour owner, Priority priority)
    {
        if (claims.TryGetValue(cell, out CellClaim existing) && existing.Owner != null && existing.Owner != owner)
        {
            if (priority <= existing.Priority)
                return false;
        }

        claims[cell] = new CellClaim
        {
            Owner = owner,
            Priority = priority
        };
        return true;
    }

    private static Vector3Int ToCellKey(Vector3 worldPosition)
    {
        return new Vector3Int(
            Mathf.RoundToInt(worldPosition.x),
            Mathf.RoundToInt(worldPosition.y),
            Mathf.RoundToInt(worldPosition.z));
    }
}
