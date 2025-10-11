using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles all of Tim's interactions with cubes: selection, pushing, rotation
/// Separated from CharacterMovement to maintain clean architecture
/// </summary>
public class TimCubeInteraction : MonoBehaviour
{
    [Header("Detection Settings")]
    public float detectionTolerance = 0.6f;

    private Transform timTransform;
    
    private void Awake()
    {
        timTransform = transform;
    }

    /// <summary>
    /// Check if Tim is reasonably aligned with a cube for detection/highlighting
    /// More lenient than push alignment - allows slightly off-center detection
    /// </summary>
    public bool IsReasonablyAlignedForDetection(Transform cubeTransform)
    {
        Vector3 cubeCenter = cubeTransform.position;
        Vector3 timPos = timTransform.position;
        
        // Calculate which face of the cube Tim is closest to
        Vector3 localOffset = timPos - cubeCenter;
        
        // Determine the strongest axis (which face Tim is approaching)
        float absX = Mathf.Abs(localOffset.x);
        float absZ = Mathf.Abs(localOffset.z);
        
        // More lenient tolerance for detection (can be slightly off-center)
        // Now exposed as public variable for tuning
        
        bool isAlignedToFace = false;
        
        if (absX > absZ) // Approaching from X direction (left/right faces)
        {
            isAlignedToFace = Mathf.Abs(localOffset.z) < detectionTolerance;
        }
        else // Approaching from Z direction (front/back faces)
        {
            isAlignedToFace = Mathf.Abs(localOffset.x) < detectionTolerance;
        }
        
        return isAlignedToFace;
    }
    
    /// <summary>
    /// Check if Tim is properly aligned to push a cube in the given direction
    /// Stricter tolerance than detection - requires precise face alignment
    /// </summary>
    public bool IsProperlyAlignedToPush(Transform cubeTransform, Vector3 pushDirection)
    {
        Vector3 cubeCenter = cubeTransform.position;
        Vector3 timPos = timTransform.position;
        
        // Calculate which face of the cube Tim is closest to
        Vector3 localOffset = timPos - cubeCenter;
        
        // Determine the strongest axis (which face Tim is approaching)
        float absX = Mathf.Abs(localOffset.x);
        float absZ = Mathf.Abs(localOffset.z);
        
        // Tim must be approaching from a primary face direction, not a corner/edge
        float faceTolerance = 0.4f; // Relaxed tolerance
        
        bool isAlignedToFace = false;
        
        if (absX > absZ) // Approaching from X direction (left/right faces)
        {
            isAlignedToFace = Mathf.Abs(localOffset.z) < faceTolerance;
        }
        else // Approaching from Z direction (front/back faces)
        {
            isAlignedToFace = Mathf.Abs(localOffset.x) < faceTolerance;
        }
        
        return isAlignedToFace;
    }
}