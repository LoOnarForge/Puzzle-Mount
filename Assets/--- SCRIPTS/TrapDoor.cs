using System.Collections;
using UnityEngine;

public class TrapDoor : MonoBehaviour
{
    private const int BlockCount = 4;
    private const float ColliderDisableDelay = 0.2f;
    private const float SettleDurationShare = 0.5f;
    private const float GrowEasePower = 2f;
    private const float ShrinkEasePower = 2.5f;
    private const float MinPopSpeed = 0.01f;
    private const float OvershootPercent = 10f;
    private const float ClosedScaleTolerance = 0.02f;
    private const int ObstructionOverlapBufferSize = 16;

    // Trap Door (001) close-check volume (local to trap root; was temp child at 5,0,-5 with 0.95×1×0.95 box).
    private static readonly Vector3 CloseObstructionBoxCenter = new Vector3(5f, 0f, -5f);
    private static readonly Vector3 CloseObstructionBoxSize = new Vector3(0.95f, 1f, 0.95f);

    [SerializeField] private Transform[] blocks = new Transform[BlockCount];
    [SerializeField, Range(1f, 40f)] private float blockPopSpeed = 10f;
    [SerializeField, Range(0f, 0.5f)] private float blockStagger = 0.04f;

    private Vector3[] closedLocalScales;
    private Collider[][] blockColliders;
    private int closeObstructionMask;

    private bool isTrapOpen;
    private bool isTrapMoving;
    private bool trapMotionTargetOpen;
    private Coroutine trapSequenceCoroutine;
    private Coroutine closeObstructionMonitorCoroutine;
    private readonly Collider[] obstructionOverlapBuffer = new Collider[ObstructionOverlapBufferSize];

    private void Awake()
    {
        closedLocalScales = new Vector3[BlockCount];
        blockColliders = new Collider[BlockCount][];
        closeObstructionMask = LayerMask.GetMask("Runodes");

        for (int i = 0; i < BlockCount; i++)
        {
            if (blocks[i] == null)
            {
                blockColliders[i] = System.Array.Empty<Collider>();
                continue;
            }

            closedLocalScales[i] = blocks[i].localScale;
            blockColliders[i] = blocks[i].GetComponentsInChildren<Collider>(true);
        }

        isTrapOpen = false;
    }

    public void OnLeverOperated()
    {
        ApplyActivatorReverse();
    }

    public void ApplyActivatorReverse()
    {
        bool effectivelyOpen = isTrapMoving ? trapMotionTargetOpen : isTrapOpen || IsVisuallyAwayFromClosed();
        ApplyOpenState(!effectivelyOpen);
    }

    // Drives toward explicit open or closed.
    public void ApplyOpenState(bool isOpen)
    {
        if (!isTrapMoving && isOpen == isTrapOpen)
        {
            if (!(!isOpen && IsVisuallyAwayFromClosed()))
                return;
        }

        if (isTrapMoving && isOpen == trapMotionTargetOpen)
            return;

        if (!isOpen && IsClosePathObstructed())
            return;

        if (isTrapMoving)
            StopTrapMotion();

        trapMotionTargetOpen = isOpen;
        trapSequenceCoroutine = StartCoroutine(AnimateTrapToState(isOpen));
    }

    private void StopTrapMotion()
    {
        if (trapSequenceCoroutine != null)
        {
            StopCoroutine(trapSequenceCoroutine);
            trapSequenceCoroutine = null;
        }

        if (closeObstructionMonitorCoroutine != null)
            StopCoroutine(closeObstructionMonitorCoroutine);

        closeObstructionMonitorCoroutine = null;
        isTrapMoving = false;
    }

    private IEnumerator AnimateTrapToState(bool opening)
    {
        isTrapMoving = true;

        float travelDuration = 1f / Mathf.Max(blockPopSpeed, MinPopSpeed);
        float settleDuration = travelDuration * SettleDurationShare;
        float overshootScale = 1f + OvershootPercent * 0.01f;
        float phaseDuration = opening ? travelDuration : travelDuration + settleDuration;
        float totalDuration = blockStagger * (BlockCount - 1) + phaseDuration;

        float[] startFactors = new float[BlockCount];
        for (int i = 0; i < BlockCount; i++)
            startFactors[i] = GetScaleFactor(i);

        if (!opening)
            closeObstructionMonitorCoroutine = StartCoroutine(MonitorCloseObstructionDuringMotion());

        float elapsed = 0f;
        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            for (int i = 0; i < BlockCount; i++)
            {
                Transform block = blocks[i];
                if (block == null)
                    continue;

                float delay = i * blockStagger;
                float localTime = elapsed - delay;
                Vector3 closedScale = closedLocalScales[i];
                float factor = startFactors[i];

                if (localTime > 0f)
                {
                    if (opening)
                    {
                        float t = Mathf.Clamp01(localTime / travelDuration);
                        factor = startFactors[i] * (1f - Mathf.Pow(t, ShrinkEasePower));
                    }
                    else
                    {
                        float grow = SampleBridgeGrowScale(localTime, travelDuration, settleDuration, overshootScale);
                        factor = startFactors[i] + (1f - startFactors[i]) * grow;
                    }
                }

                block.localScale = closedScale * factor;
                UpdateBlockCollider(i, opening, localTime);
            }

            yield return null;
        }

        for (int i = 0; i < BlockCount; i++)
            SetBlockSettled(i, opening);

        if (closeObstructionMonitorCoroutine != null)
        {
            StopCoroutine(closeObstructionMonitorCoroutine);
            closeObstructionMonitorCoroutine = null;
        }

        isTrapOpen = opening;
        isTrapMoving = false;
        trapSequenceCoroutine = null;
    }

    private IEnumerator MonitorCloseObstructionDuringMotion()
    {
        while (isTrapMoving && !trapMotionTargetOpen)
        {
            if (IsClosePathObstructed())
            {
                StopTrapMotion();
                isTrapOpen = true;
                trapMotionTargetOpen = true;
                trapSequenceCoroutine = StartCoroutine(AnimateTrapToState(true));
                yield break;
            }

            yield return null;
        }
    }

    private void UpdateBlockCollider(int index, bool opening, float localTime)
    {
        if (opening)
        {
            if (localTime >= ColliderDisableDelay || GetScaleFactor(index) <= ClosedScaleTolerance)
                SetBlockCollidersEnabled(index, false);
        }
        else if (GetScaleFactor(index) >= 1f - ClosedScaleTolerance)
        {
            SetBlockCollidersEnabled(index, true);
        }
    }

    private void SetBlockSettled(int index, bool opening)
    {
        Transform block = blocks[index];
        if (block != null)
            block.localScale = opening ? Vector3.zero : closedLocalScales[index];

        SetBlockCollidersEnabled(index, !opening);
    }

    private void SetBlockCollidersEnabled(int index, bool enabled)
    {
        Collider[] colliders = blockColliders[index];
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = enabled;
        }
    }

    private float GetScaleFactor(int index)
    {
        Transform block = blocks[index];
        if (block == null)
            return 1f;

        Vector3 closed = closedLocalScales[index];
        return closed.x > 0f ? block.localScale.x / closed.x : 1f;
    }

    private static float SampleBridgeGrowScale(float time, float growDuration, float settleDuration, float overshootScale)
    {
        if (time <= 0f)
            return 0f;

        if (time < growDuration)
        {
            float growT = time / growDuration;
            float eased = 1f - Mathf.Pow(1f - growT, GrowEasePower);
            return Mathf.Lerp(0f, overshootScale, eased);
        }

        float settleTime = time - growDuration;
        if (settleTime < settleDuration)
        {
            float settleT = settleDuration > 0f ? settleTime / settleDuration : 1f;
            float eased = Mathf.Pow(settleT, GrowEasePower);
            return Mathf.Lerp(overshootScale, 1f, eased);
        }

        return 1f;
    }

    private bool IsVisuallyAwayFromClosed()
    {
        for (int i = 0; i < BlockCount; i++)
        {
            if (GetScaleFactor(i) < 1f - ClosedScaleTolerance)
                return true;
        }

        return false;
    }

    private bool IsClosePathObstructed()
    {
        if (closeObstructionMask == 0)
            return false;

        Physics.SyncTransforms();

        Vector3 center = transform.TransformPoint(CloseObstructionBoxCenter);
        Vector3 halfExtents = Vector3.Scale(CloseObstructionBoxSize, transform.lossyScale) * 0.5f;

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            obstructionOverlapBuffer,
            transform.rotation,
            closeObstructionMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = obstructionOverlapBuffer[i];
            if (hit == null || !hit.enabled || hit.transform.IsChildOf(transform))
                continue;

            return true;
        }

        return false;
    }
}
