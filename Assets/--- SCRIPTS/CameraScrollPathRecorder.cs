using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Edit mode: position the camera, Record stop. One orbit-normalized path — works for all Q/E cardinals.
/// Stop 0 = max zoom in, last stop = max zoom out.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(CameraFollow))]
public class CameraScrollPathRecorder : MonoBehaviour
{
    public Transform followTarget;
    public float lookAtHeight = 2f;
    public List<Vector3> recordedStops = new List<Vector3>();

    private CameraFollow cameraFollow;

    private void OnEnable()
    {
        cameraFollow = GetComponent<CameraFollow>();
        if (followTarget == null && cameraFollow != null)
            followTarget = cameraFollow.target;
    }

    public Transform GetPivotTransform()
    {
        if (followTarget == null && cameraFollow != null)
            followTarget = cameraFollow.target;

        if (followTarget == null)
            return null;

        CharacterMovement movement = followTarget.GetComponent<CharacterMovement>();
        if (movement != null && movement.CameraFollowTarget != null)
            return movement.CameraFollowTarget;

        return followTarget;
    }

    public void RecordStopFromCameraTransform()
    {
        Transform pivot = GetPivotTransform();
        if (pivot == null || cameraFollow == null)
            return;

        Vector3 northLocal = cameraFollow.WorldOffsetToOrbitLocal(transform.position - pivot.position);
        recordedStops.Add(northLocal);
        SnapLookAtTim();
    }

    public void SnapLookAtTim()
    {
        Transform pivot = GetPivotTransform();
        if (pivot == null)
            return;

        Vector3 lookAt = pivot.position + Vector3.up * lookAtHeight;
        Vector3 forward = lookAt - transform.position;
        if (forward.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private void OnDrawGizmosSelected()
    {
        Transform pivot = GetPivotTransform();
        if (pivot == null || cameraFollow == null || recordedStops.Count == 0)
            return;

        Gizmos.color = new Color(0f, 1f, 0.2f, 0.9f);
        Vector3 pivotWorld = pivot.position;
        Vector3 prev = pivotWorld + cameraFollow.OrbitLocalOffsetToWorld(recordedStops[0]);
        for (int i = 0; i < recordedStops.Count; i++)
        {
            Vector3 p = pivotWorld + cameraFollow.OrbitLocalOffsetToWorld(recordedStops[i]);
            Gizmos.DrawSphere(p, 0.25f);
            if (i > 0)
                Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
