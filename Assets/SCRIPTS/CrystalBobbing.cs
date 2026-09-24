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

    private float HalfCycleDuration => activeCycleDuration * 0.5f;

    private void Awake()
    {
        restWorldPosition = transform.position;
        RollActiveCycleParameters();
        ApplyBobOffset(-activeAmplitude);
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
                if (UpdateMove(-activeAmplitude, activeAmplitude, HalfCycleDuration))
                    BeginPhase(BobPhase.PauseAtTop);
                break;

            case BobPhase.PauseAtTop:
                if (phaseElapsed >= activeExtremityPauseDuration)
                    BeginPhase(BobPhase.MovingDown);
                break;

            case BobPhase.MovingDown:
                if (UpdateMove(activeAmplitude, -activeAmplitude, HalfCycleDuration))
                    BeginPhase(BobPhase.PauseAtBottom);
                break;

            case BobPhase.PauseAtBottom:
                if (phaseElapsed >= activeExtremityPauseDuration)
                    BeginPhase(BobPhase.MovingUp);
                break;
        }
    }

    private void BeginPhase(BobPhase nextPhase)
    {
        if (nextPhase == BobPhase.MovingUp)
            RollActiveCycleParameters();

        phase = nextPhase;
        phaseElapsed = 0f;
    }

    private void RollActiveCycleParameters()
    {
        activeAmplitude = bobAmplitude * Random.Range(CycleParameterRandomnessMin, CycleParameterRandomnessMax);
        activeCycleDuration = bobCycleDuration * Random.Range(CycleParameterRandomnessMin, CycleParameterRandomnessMax);
        activeExtremityPauseDuration = extremityPauseDuration * Random.Range(CycleParameterRandomnessMin, CycleParameterRandomnessMax);
    }

    // Returns true when the move segment has finished.
    private bool UpdateMove(float fromOffset, float toOffset, float duration)
    {
        float t = Mathf.Clamp01(phaseElapsed / duration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        float offset = Mathf.Lerp(fromOffset, toOffset, eased);
        ApplyBobOffset(offset);

        return phaseElapsed >= duration;
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
