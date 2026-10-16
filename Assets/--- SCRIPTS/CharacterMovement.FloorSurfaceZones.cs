using UnityEngine;

public partial class CharacterMovement
{
    private const float FloorZoneProbeLift = 0.1f;
    private const float FloorZoneProbeRadius = 0.35f;
    private const float FloorZoneFeetVerticalTolerance = 0.15f;

    private static readonly Collider[] FloorZoneOverlapHits = new Collider[8];

    private enum TimFloorZoneKind
    {
        None,
        Slippery,
        Rough
    }

    private struct TimFloorZoneSnapshot
    {
        public TimFloorZoneKind Kind;
        public RoughZone RoughZone;
    }

    private int floorZoneSnapshotFrame = -1;
    private TimFloorZoneSnapshot floorZoneSnapshot;

    // Feet overlap query shared by slippery coast and rough walk/run speeds (zones do not overlap).
    private TimFloorZoneSnapshot GetTimFloorZoneAtFeet()
    {
        if (floorZoneSnapshotFrame == Time.frameCount)
            return floorZoneSnapshot;

        floorZoneSnapshotFrame = Time.frameCount;
        floorZoneSnapshot = QueryTimFloorZoneAtFeet();
        return floorZoneSnapshot;
    }

    private TimFloorZoneSnapshot QueryTimFloorZoneAtFeet()
    {
        TimFloorZoneSnapshot result = default;

        if (controller == null || !controller.enabled)
            return result;

        Bounds timBounds = controller.bounds;
        Vector3 probe = new Vector3(timBounds.center.x, timBounds.min.y + FloorZoneProbeLift, timBounds.center.z);

        int hitCount = Physics.OverlapSphereNonAlloc(
            probe,
            FloorZoneProbeRadius,
            FloorZoneOverlapHits,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        float feetY = probe.y;

        for (int i = 0; i < hitCount; i++)
        {
            Collider zoneCollider = FloorZoneOverlapHits[i];
            if (zoneCollider == null)
                continue;

            Bounds zoneBounds = zoneCollider.bounds;
            if (feetY < zoneBounds.min.y - FloorZoneFeetVerticalTolerance ||
                feetY > zoneBounds.max.y + FloorZoneFeetVerticalTolerance)
            {
                continue;
            }

            if (zoneCollider.GetComponentInParent<SlipperySurfaceZone>() != null)
            {
                result.Kind = TimFloorZoneKind.Slippery;
                return result;
            }

            RoughZone roughZone = zoneCollider.GetComponentInParent<RoughZone>();
            if (roughZone != null)
            {
                result.Kind = TimFloorZoneKind.Rough;
                result.RoughZone = roughZone;
                return result;
            }
        }

        return result;
    }

    private float GetTimFloorWalkSpeed()
    {
        TimFloorZoneSnapshot zone = GetTimFloorZoneAtFeet();
        if (zone.Kind == TimFloorZoneKind.Rough && zone.RoughZone != null)
            return zone.RoughZone.TimWalkSpeed;

        return walkSpeed;
    }

    private float GetTimFloorRunSpeed()
    {
        TimFloorZoneSnapshot zone = GetTimFloorZoneAtFeet();
        if (zone.Kind == TimFloorZoneKind.Rough && zone.RoughZone != null)
            return zone.RoughZone.TimRunSpeed;

        return runSpeed;
    }
}
