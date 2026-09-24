using UnityEngine;

public class CrystalBobbing : MonoBehaviour
{
    private enum BobPhase
    {
        MovingUp,
        PauseAtTop,
        MovingDown,
        PauseAtBottom
    }

    private const float MinBobCycleDuration = 0.01f;
    private const float MinExtremityPauseDuration = 0f;
    private const float CycleParameterRandomnessMin = 0.9f;
    private const float CycleParameterRandomnessMax = 1.1f;

    [Header("BOBBING")]
    [SerializeField] private float bobAmplitude = 0.1f;
    [SerializeField] private float bobCycleDuration = 2f;
    [SerializeField] private float extremityPauseDuration = 0.2f;
    [SerializeField] private bool bobWorldX;
    [SerializeField] private bool bobWorldY = true;
    [SerializeField] private bool bobWorldZ;

    private Vector3 restWorldPosition;
    private BobPhase phase = BobPhase.MovingUp;
    private float phaseElapsed;
    private float activeAmplitude;
    private float activeCycleDuration;
    private float activeExtremityPauseDuration;
    private float pauseHoldOffset;
    private float segmentFromOffset;
    private float segmentToOffset;

    private float HalfCycleDuration => activeCycleDuration * 0.5f;

    private void Awake()
    {
        restWorldPosition = transform.position;
        RollActiveCycleParameters();
        pauseHoldOffset = -activeAmplitude;
        ApplyBobOffset(pauseHoldOffset);
        BeginMoveSegment(activeAmplitude);
    }

    private void OnValidate()
    {
        bobCycleDuration = Mathf.Max(MinBobCycleDuration, bobCycleDuration);
        extremityPauseDuration = Mathf.Max(MinExtremityPauseDuration, extremityPauseDuration);
        bobAmplitude = Mathf.Max(0f, bobAmplitude);
    }

    private void Update()
    {
        phaseElapsed += Time.deltaTime;

        switch (phase)
        {
            case BobPhase.MovingUp:
                if (UpdateMoveSegment(HalfCycleDuration))
                    BeginPhase(BobPhase.PauseAtTop);
                break;

            case BobPhase.PauseAtTop:
                ApplyBobOffset(pauseHoldOffset);
                if (phaseElapsed >= activeExtremityPauseDuration)
                    BeginPhase(BobPhase.MovingDown);
                break;

            case BobPhase.MovingDown:
                if (UpdateMoveSegment(HalfCycleDuration))
                    BeginPhase(BobPhase.PauseAtBottom);
                break;

            case BobPhase.PauseAtBottom:
                ApplyBobOffset(pauseHoldOffset);
                if (phaseElapsed >= activeExtremityPauseDuration)
                    BeginPhase(BobPhase.MovingUp);
                break;
        }
    }

    private void BeginPhase(BobPhase nextPhase)
    {
        phase = nextPhase;
        phaseElapsed = 0f;

        switch (nextPhase)
        {
            case BobPhase.PauseAtTop:
                pauseHoldOffset = activeAmplitude;
                ApplyBobOffset(pauseHoldOffset);
                RollActiveCycleParameters();
                break;

            case BobPhase.PauseAtBottom:
                pauseHoldOffset = -activeAmplitude;
                ApplyBobOffset(pauseHoldOffset);
                break;

            case BobPhase.MovingUp:
                BeginMoveSegment(activeAmplitude);
                break;

            case BobPhase.MovingDown:
                BeginMoveSegment(-activeAmplitude);
                break;
        }
    }

    private void BeginMoveSegment(float toOffset)
    {
        segmentFromOffset = pauseHoldOffset;
        segmentToOffset = toOffset;
    }

    private void RollActiveCycleParameters()
    {
        activeAmplitude = bobAmplitude * Random.Range(CycleParameterRandomnessMin, CycleParameterRandomnessMax);
        activeCycleDuration = bobCycleDuration * Random.Range(CycleParameterRandomnessMin, CycleParameterRandomnessMax);
        activeExtremityPauseDuration = extremityPauseDuration * Random.Range(CycleParameterRandomnessMin, CycleParameterRandomnessMax);
    }

    // Returns true when the move segment has finished.
    private bool UpdateMoveSegment(float duration)
    {
        float t = Mathf.Clamp01(phaseElapsed / duration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        float offset = Mathf.Lerp(segmentFromOffset, segmentToOffset, eased);
        ApplyBobOffset(offset);

        if (phaseElapsed < duration)
            return false;

        pauseHoldOffset = segmentToOffset;
        ApplyBobOffset(pauseHoldOffset);
        return true;
    }

    private void ApplyBobOffset(float offset)
    {
        Vector3 delta = Vector3.zero;
        if (bobWorldX)
            delta.x = offset;
        if (bobWorldY)
            delta.y = offset;
        if (bobWorldZ)
            delta.z = offset;

        transform.position = restWorldPosition + delta;
    }
}
