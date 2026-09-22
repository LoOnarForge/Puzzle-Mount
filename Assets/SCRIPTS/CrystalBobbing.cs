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

    [Header("BOBBING")]
    [SerializeField] private float bobAmplitude = 0.1f;
    [SerializeField] private float bobCycleDuration = 2f;
    [SerializeField] private float extremityPauseDuration = 0.2f;

    private Vector3 restLocalPosition;
    private BobPhase phase = BobPhase.MovingUp;
    private float phaseElapsed;

    private float HalfCycleDuration => bobCycleDuration * 0.5f;

    private void Awake()
    {
        restLocalPosition = transform.localPosition;
        ApplyVerticalOffset(-bobAmplitude);
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
                if (UpdateMove(-bobAmplitude, bobAmplitude, HalfCycleDuration))
                    BeginPhase(BobPhase.PauseAtTop);
                break;

            case BobPhase.PauseAtTop:
                if (phaseElapsed >= extremityPauseDuration)
                    BeginPhase(BobPhase.MovingDown);
                break;

            case BobPhase.MovingDown:
                if (UpdateMove(bobAmplitude, -bobAmplitude, HalfCycleDuration))
                    BeginPhase(BobPhase.PauseAtBottom);
                break;

            case BobPhase.PauseAtBottom:
                if (phaseElapsed >= extremityPauseDuration)
                    BeginPhase(BobPhase.MovingUp);
                break;
        }
    }

    private void BeginPhase(BobPhase nextPhase)
    {
        phase = nextPhase;
        phaseElapsed = 0f;
    }

    // Returns true when the move segment has finished.
    private bool UpdateMove(float fromOffset, float toOffset, float duration)
    {
        float t = Mathf.Clamp01(phaseElapsed / duration);
        float eased = Mathf.SmoothStep(0f, 1f, t);
        float offset = Mathf.Lerp(fromOffset, toOffset, eased);
        ApplyVerticalOffset(offset);

        return phaseElapsed >= duration;
    }

    private void ApplyVerticalOffset(float verticalOffset)
    {
        Vector3 position = restLocalPosition;
        position.y += verticalOffset;
        transform.localPosition = position;
    }
}
