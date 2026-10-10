using UnityEngine;

// Colliders with this component are ignored by piston obstruction probes (e.g. door close triggers on Devices).
public class PistonProbeObstructionExempt : MonoBehaviour
{
    public static bool AppliesTo(Collider hit)
    {
        if (hit == null)
            return false;

        return hit.GetComponent<PistonProbeObstructionExempt>() != null
            || hit.GetComponentInParent<PistonProbeObstructionExempt>() != null;
    }
}
