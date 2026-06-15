using UnityEngine;

public class PowerLineBlocker : MonoBehaviour
{
    public bool IsBlocked { get; private set; }
    private int overlapCount = 0;

    private void OnTriggerEnter(Collider other)
    {
        // Only block if we hit a body collider (not another trigger)
        // and it's on the Runode layer or environment
        if (other.isTrigger) return;
        
        overlapCount++;
        IsBlocked = overlapCount > 0;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.isTrigger) return;

        overlapCount--;
        if (overlapCount < 0) overlapCount = 0;
        IsBlocked = overlapCount > 0;
    }
}
