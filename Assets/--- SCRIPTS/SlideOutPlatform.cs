using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class SlideOutPlatform : MonoBehaviour
{
    private const float SlowSegmentShare = 0.15f;
    private const float SettledPositionTolerance = 0.02f;
    private const int ObstructionOverlapBufferSize = 16;

    [SerializeField] private Transform platform;
    [SerializeField] private Transform positionA;
    [SerializeField] private Transform positionB;
    [SerializeField][Range(0f, 2f)] private float travelDuration = 1f;
    [SerializeField][Range(0f, 5f)] private float movementStartDelay = 0f;
    [SerializeField] private bool isRetracted = true;
    [FormerlySerializedAs("retractObstructionTrigger")]
    [SerializeField] private Collider obstructionTrigger;
    [FormerlySerializedAs("retractObstructionMask")]
    [SerializeField] private LayerMask obstructionMask;

    private bool isExtended;
    private bool isPlatformMoving;
    private bool motionTargetExtended;
    private Coroutine platformMotionCoroutine;
    private Coroutine retractObstructionMonitorCoroutine;
    private readonly Collider[] obstructionOverlapBuffer = new Collider[ObstructionOverlapBufferSize];
    private int timJonesLayer;

    private void Awake()
    {
        timJonesLayer = LayerMask.NameToLayer("TimJones");

        Debug.Assert(platform != null, $"{nameof(SlideOutPlatform)} on {name} requires a {nameof(platform)} transform.", this);
        Debug.Assert(positionA != null, $"{nameof(SlideOutPlatform)} on {name} requires {nameof(positionA)}.", this);
        Debug.Assert(positionB != null, $"{nameof(SlideOutPlatform)} on {name} requires {nameof(positionB)}.", this);

        if (obstructionTrigger != null)
            Debug.Assert(obstructionTrigger.isTrigger, $"{nameof(SlideOutPlatform)} on {name} requires {nameof(obstructionTrigger)} to be a trigger.", this);

        isExtended = !isRetracted;
        SnapPlatformToState(isExtended);
    }

    public void OnLeverOperated()
    {
        ApplyActivatorReverse();
    }

    public void ApplyActivatorReverse()
    {
        bool effectivelyExtended = isPlatformMoving
            ? motionTargetExtended
            : isExtended || IsVisuallyAwayFromRetracted();
        ApplyOpenState(!effectivelyExtended);
    }

    public void ApplyOpenState(bool extend)
    {
        if (!isPlatformMoving && extend == isExtended)
        {
            if (!(!extend && IsVisuallyAwayFromRetracted()))
                return;
        }

        if (isPlatformMoving && extend == motionTargetExtended)
            return;

        if (isPlatformMoving)
            StopPlatformMotion();

        if (!extend && IsRetractPathObstructed())
            return;

        motionTargetExtended = extend;
        platformMotionCoroutine = StartCoroutine(AnimatePlatformToState(extend));
    }

    private void StopPlatformMotion()
    {
        if (platformMotionCoroutine != null)
        {
            StopCoroutine(platformMotionCoroutine);
            platformMotionCoroutine = null;
        }

        if (retractObstructionMonitorCoroutine != null)
        {
            StopCoroutine(retractObstructionMonitorCoroutine);
            retractObstructionMonitorCoroutine = null;
        }

        isPlatformMoving = false;
    }

    private void SnapPlatformToState(bool extend)
    {
        if (platform == null)
            return;

        Transform destination = extend ? positionB : positionA;
        if (destination == null)
            return;

        platform.SetPositionAndRotation(destination.position, destination.rotation);
    }

    private IEnumerator AnimatePlatformToState(bool extending)
    {
        isPlatformMoving = true;

        if (platform == null || positionA == null || positionB == null)
        {
            isPlatformMoving = false;
            platformMotionCoroutine = null;
            yield break;
        }

        Vector3 endPosition = extending ? positionB.position : positionA.position;
        Quaternion endRotation = extending ? positionB.rotation : positionA.rotation;

        float delay = Mathf.Max(movementStartDelay, 0f);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (motionTargetExtended != extending)
        {
            isPlatformMoving = false;
            platformMotionCoroutine = null;
            yield break;
        }

        if (!extending && IsRetractPathObstructed())
        {
            isPlatformMoving = false;
            platformMotionCoroutine = null;
            yield break;
        }

        if (!extending)
            retractObstructionMonitorCoroutine = StartCoroutine(MonitorRetractObstructionDuringMotion());

        Vector3 travelStartPosition = platform.position;
        Quaternion travelStartRotation = platform.rotation;
        float duration = Mathf.Max(travelDuration, 0f);
        if (duration <= 0f)
        {
            platform.SetPositionAndRotation(endPosition, endRotation);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (motionTargetExtended != extending)
                    yield break;

                elapsed += Time.deltaTime;
                float progress = SampleSegmentProgress(extending, Mathf.Clamp01(elapsed / duration));
                platform.SetPositionAndRotation(
                    Vector3.Lerp(travelStartPosition, endPosition, progress),
                    Quaternion.Slerp(travelStartRotation, endRotation, progress));
                yield return null;
            }

            if (motionTargetExtended == extending)
                platform.SetPositionAndRotation(endPosition, endRotation);
        }

        if (retractObstructionMonitorCoroutine != null)
        {
            StopCoroutine(retractObstructionMonitorCoroutine);
            retractObstructionMonitorCoroutine = null;
        }

        if (motionTargetExtended == extending)
            isExtended = extending;

        isPlatformMoving = false;
        platformMotionCoroutine = null;
    }

    private IEnumerator MonitorRetractObstructionDuringMotion()
    {
        while (isPlatformMoving && !motionTargetExtended)
        {
            if (IsRetractPathObstructed())
            {
                StopPlatformMotion();
                isExtended = true;
                motionTargetExtended = true;
                platformMotionCoroutine = StartCoroutine(AnimatePlatformToState(true));
                yield break;
            }

            yield return null;
        }
    }

    private static float SampleExtendBlend(bool extending, float normalizedTime)
    {
        float slowShare = SlowSegmentShare;

        if (extending)
        {
            if (normalizedTime < 1f - slowShare)
            {
                float phaseTime = normalizedTime / (1f - slowShare);
                return Mathf.Lerp(0f, 1f - slowShare, phaseTime);
            }

            float tailTime = (normalizedTime - (1f - slowShare)) / slowShare;
            return Mathf.Lerp(1f - slowShare, 1f, Mathf.SmoothStep(0f, 1f, tailTime));
        }

        if (normalizedTime < slowShare)
        {
            float phaseTime = normalizedTime / slowShare;
            return Mathf.Lerp(1f, 1f - slowShare, Mathf.SmoothStep(0f, 1f, phaseTime));
        }

        float fastTime = (normalizedTime - slowShare) / (1f - slowShare);
        return Mathf.Lerp(1f - slowShare, 0f, fastTime);
    }

    // 0 at move start, 1 at move end — for lerping from current pose (retract path blend runs 1→0 on the full A–B span).
    private static float SampleSegmentProgress(bool extending, float normalizedTime)
    {
        if (extending)
            return SampleExtendBlend(true, normalizedTime);

        return 1f - SampleExtendBlend(false, normalizedTime);
    }

    private bool IsRetractPathObstructed()
    {
        if (obstructionTrigger == null || obstructionMask.value == 0)
            return false;

        Physics.SyncTransforms();

        Bounds bounds = obstructionTrigger.bounds;

        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            obstructionOverlapBuffer,
            obstructionTrigger.transform.rotation,
            obstructionMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            if (IsRetractObstructionHit(obstructionOverlapBuffer[i]))
                return true;
        }

        return false;
    }

    private bool IsRetractObstructionHit(Collider hit)
    {
        if (hit == null || hit == obstructionTrigger)
            return false;

        if (hit.transform.IsChildOf(transform))
            return false;

        if (!hit.enabled)
            return false;

        if (timJonesLayer >= 0 && hit.gameObject.layer == timJonesLayer)
            return false;

        return true;
    }

    private bool IsVisuallyAwayFromRetracted()
    {
        if (platform == null || positionA == null)
            return false;

        float toleranceSqr = SettledPositionTolerance * SettledPositionTolerance;
        return Vector3.SqrMagnitude(platform.position - positionA.position) > toleranceSqr;
    }

}
