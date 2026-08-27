using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public class PistonPS : MonoBehaviour
{
    private const float MinSpan = 0.01f;
    private const float RetractedSpanTolerance = 0.005f;

    [Header("Shaft Endpoints")]
    [SerializeField] private Transform baseAnchor;
    [SerializeField] private Transform faceAnchor;

    private ParticleSystem particleSystem;
    private ParticleSystemRenderer particleRenderer;
    private float referenceWidth;
    private float referenceHeight;
    private float retractedSpan = -1f;

    private void Awake()
    {
        particleSystem = GetComponent<ParticleSystem>();
        particleRenderer = GetComponent<ParticleSystemRenderer>();

        ParticleSystem.MainModule main = particleSystem.main;
        main.playOnAwake = false;

        Vector3 shapeScale = particleSystem.shape.scale;
        referenceWidth = shapeScale.x;
        referenceHeight = shapeScale.y;

        ResolveAnchorsIfMissing();
        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Start()
    {
        TryCaptureRetractedSpan();
    }

    private void LateUpdate()
    {
        ResolveAnchorsIfMissing();
        TryCaptureRetractedSpan();

        if (baseAnchor == null || faceAnchor == null)
        {
            StopParticles();
            return;
        }

        if (!TryGetSpan(out Vector3 start, out _, out Vector3 forward, out float rawSpan))
        {
            StopParticles();
            return;
        }

        if (IsWithinRetractedSpan(rawSpan))
        {
            StopParticles();
            return;
        }

        transform.SetPositionAndRotation(
            start + forward * (rawSpan * 0.5f),
            Quaternion.LookRotation(forward, GetShaftUp(forward)));

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.scale = new Vector3(referenceWidth, referenceHeight, rawSpan);
        shape.position = Vector3.zero;

        PlayParticles();
    }

    private void TryCaptureRetractedSpan()
    {
        if (retractedSpan >= 0f || baseAnchor == null || faceAnchor == null)
            return;

        Piston piston = GetComponentInParent<Piston>();
        if (piston != null && !piston.IsRetracted)
            return;

        if (TryGetSpan(out _, out _, out _, out float span))
            retractedSpan = span;
    }

    private bool IsWithinRetractedSpan(float span)
    {
        if (retractedSpan < 0f)
            return span <= MinSpan;

        return span <= retractedSpan + RetractedSpanTolerance;
    }

    private void ResolveAnchorsIfMissing()
    {
        Transform searchRoot = transform.parent;
        if (searchRoot == null)
            return;

        foreach (Transform child in searchRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "Light Shaft START")
                baseAnchor = child;

            if (child.name == "Light Shaft END")
                faceAnchor = child;
        }
    }

    private void PlayParticles()
    {
        if (particleRenderer != null)
            particleRenderer.enabled = true;

        if (!particleSystem.isPlaying)
            particleSystem.Play();
    }

    private void StopParticles()
    {
        if (particleRenderer != null)
            particleRenderer.enabled = false;

        if (particleSystem.isPlaying)
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private bool TryGetSpan(out Vector3 start, out Vector3 end, out Vector3 forward, out float span)
    {
        Vector3 directionGuess = faceAnchor.position - baseAnchor.position;
        if (directionGuess.sqrMagnitude < MinSpan * MinSpan)
        {
            start = default;
            end = default;
            forward = default;
            span = 0f;
            return false;
        }

        forward = directionGuess.normalized;
        start = baseAnchor.position;
        end = faceAnchor.position;
        span = directionGuess.magnitude;
        forward = directionGuess / span;
        return true;
    }

    private static Vector3 GetShaftUp(Vector3 forward)
    {
        if (Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f)
            return Vector3.forward;

        return Vector3.up;
    }
}
