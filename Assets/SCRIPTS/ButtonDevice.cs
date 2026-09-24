using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class ButtonDevice : MonoBehaviour
{
    private const int PressPoseCount = 4;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private const float PressStepDurationMin = 0f;
    private const float PressStepDurationMax = 1f;
    private const float PauseBetweenMovesMin = 0f;
    private const float PauseBetweenMovesMax = 1f;
    private const float GridAlignmentTolerance = 0.1f;
    private const float MinDescentSpeed = 0.1f;
    private const float MaxHorizontalApproachSpeed = 0.5f;
    private const int OverlapBufferSize = 8;
    private const float EyeFadeOutEnd = 0.52f;
    private const float EyeFadeInStart = 0.48f;
    private const float EyeFlashDurationMin = 0f;
    private const float EyeFlashDurationMax = 1f;
    private const float EyeFlashBoost = 2.5f;

    [FormerlySerializedAs("buttonCap")]
    [SerializeField] private Transform skullButton;
    [SerializeField] private Transform pressPose0;
    [SerializeField] private Transform pressPose1;
    [SerializeField] private Transform pressPose2;
    [SerializeField] private Transform pressPose3;
    [SerializeField] [Range(PressStepDurationMin, PressStepDurationMax)] private float pressStepDuration = 0.25f;
    [SerializeField] [Range(PauseBetweenMovesMin, PauseBetweenMovesMax)] private float pauseBetweenMoves = 0.15f;
    [SerializeField] private Renderer eyesRenderer;
    [ColorUsage(true, true)]
    [SerializeField] private Color defaultEyeColor = Color.white;
    [ColorUsage(true, true)]
    [FormerlySerializedAs("pressedEyeEmissionColor")]
    [SerializeField] private Color pressedEyeColor = Color.white;
    [FormerlySerializedAs("eyeFlashHalfWidth")]
    [SerializeField] [Range(EyeFlashDurationMin, EyeFlashDurationMax)] private float eyeFlashDuration = 0.075f;
    [SerializeField] private ParticleSystem pressParticleSystem;
    [SerializeField] private Collider pressTrigger;
    [SerializeField] private List<GameObject> connectedDevices = new List<GameObject>();

    private readonly List<MediumDoor> mediumDoors = new List<MediumDoor>();
    private readonly List<Elevator> elevators = new List<Elevator>();
    private readonly List<PistonHorizontal> pistonHorizontals = new List<PistonHorizontal>();
    private readonly List<PistonVertical> pistonVerticals = new List<PistonVertical>();
    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];

    private bool isButtonMoving;
    private bool isPressed;
    private MaterialPropertyBlock eyesPropertyBlock;

    private void Awake()
    {
        BuildConnectedDeviceLists();
        eyesPropertyBlock = new MaterialPropertyBlock();
        ApplyEyeEmission(defaultEyeColor);

        if (pressTrigger != null)
            Debug.Assert(pressTrigger.isTrigger, $"{nameof(ButtonDevice)} on {name} requires {nameof(pressTrigger)} to be a trigger.", this);
    }

    private void FixedUpdate()
    {
        if (isPressed || pressTrigger == null)
            return;

        Bounds bounds = pressTrigger.bounds;
        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            overlapBuffer,
            pressTrigger.transform.rotation,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null || hit == pressTrigger)
                continue;

            RunodeMovement cube = hit.GetComponentInParent<RunodeMovement>();
            if (cube == null || !CanCubePressButton(cube))
                continue;

            TryPress();
            return;
        }
    }

    // Called when the player clicks this button within range; returns true when the hit counts as this button.
    public bool MouseClickDetected(Collider clickedCollider)
    {
        if (!IsAllowedClickCollider(clickedCollider))
            return false;

        TryPress();
        return true;
    }

    private bool IsAllowedClickCollider(Collider clickedCollider)
    {
        if (clickedCollider == null)
            return false;

        if (skullButton != null)
            return clickedCollider.transform == skullButton || clickedCollider.transform.IsChildOf(skullButton);

        if (pressTrigger == null)
            return false;

        return clickedCollider == pressTrigger || clickedCollider.transform.IsChildOf(pressTrigger.transform);
    }

    // Runs one press if this button has not already been used.
    private void TryPress()
    {
        if (isPressed || isButtonMoving)
            return;

        isPressed = true;

        if (pressTrigger != null)
            pressTrigger.enabled = false;

        StartCoroutine(ButtonPressMovement());
    }

    // Returns true when a cube is falling onto the button from above.
    private bool CanCubePressButton(RunodeMovement cube)
    {
        if (cube.IsBusy)
            return false;

        if (!IsCubeAlignedOverButton(cube.transform.position))
            return false;

        Rigidbody cubeRigidbody = cube.GetComponent<Rigidbody>();
        if (cubeRigidbody == null)
            return false;

        Vector3 velocity = cubeRigidbody.linearVelocity;
        if (velocity.y > -MinDescentSpeed)
            return false;

        float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        if (horizontalSpeed > MaxHorizontalApproachSpeed && velocity.y > -MinDescentSpeed * 2f)
            return false;

        return true;
    }

    // Returns true when the cube is centered over this button and above its base.
    private bool IsCubeAlignedOverButton(Vector3 cubePosition)
    {
        Vector3 buttonPosition = transform.position;

        if (Mathf.Abs(cubePosition.x - buttonPosition.x) > GridAlignmentTolerance)
            return false;

        if (Mathf.Abs(cubePosition.z - buttonPosition.z) > GridAlignmentTolerance)
            return false;

        return cubePosition.y > buttonPosition.y;
    }

    // Lerps the skull through press pose transforms with pauses between each segment.
    private IEnumerator ButtonPressMovement()
    {
        isButtonMoving = true;

        if (skullButton == null)
        {
            isButtonMoving = false;
            NotifyConnectedDevices();
            yield break;
        }

        for (int i = 0; i < PressPoseCount - 1; i++)
        {
            Transform fromPose = GetPressPose(i);
            Transform toPose = GetPressPose(i + 1);
            if (toPose == null)
                break;

            Vector3 startPosition = fromPose != null ? fromPose.position : skullButton.position;
            Quaternion startRotation = fromPose != null ? fromPose.rotation : skullButton.rotation;
            Vector3 endPosition = toPose.position;
            Quaternion endRotation = toPose.rotation;
            if (pressStepDuration <= 0f)
            {
                skullButton.position = endPosition;
                skullButton.rotation = endRotation;
            }
            else
            {
                float elapsed = 0f;

                while (elapsed < pressStepDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / pressStepDuration);
                    skullButton.position = Vector3.Lerp(startPosition, endPosition, t);
                    skullButton.rotation = Quaternion.Slerp(startRotation, endRotation, t);
                    yield return null;
                }

                skullButton.position = endPosition;
                skullButton.rotation = endRotation;
            }

            if (pauseBetweenMoves > 0f && i < PressPoseCount - 2)
                yield return new WaitForSeconds(pauseBetweenMoves);
        }

        EnablePressParticleSystem();
        yield return LerpEyeEmissionToPressed();

        isButtonMoving = false;
        NotifyConnectedDevices();
    }

    private IEnumerator LerpEyeEmissionToPressed()
    {
        if (eyesRenderer == null)
            yield break;

        Color startEmission = defaultEyeColor;
        Color endEmission = pressedEyeColor;

        if (pressStepDuration <= 0f)
        {
            ApplyEyeEmission(endEmission);
            yield break;
        }

        ApplyEyeEmission(startEmission);

        float elapsed = 0f;
        while (elapsed < pressStepDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / pressStepDuration);
            ApplyEyeEmission(LerpEyeColorWithFlash(startEmission, endEmission, t));
            yield return null;
        }

        ApplyEyeEmission(endEmission);
    }

    // Only A or B at a time (no mixed hue). Flash is extra brightness on that same color at the handoff — never additive white/RGB mix.
    private Color LerpEyeColorWithFlash(Color from, Color to, float t)
    {
        t = Mathf.Clamp01(t);

        float flashPhase = Mathf.Clamp01((t - (0.5f - eyeFlashDuration)) / (eyeFlashDuration * 2f));
        float flashMul = 1f + EyeFlashBoost * Mathf.Sin(flashPhase * Mathf.PI);

        Color result;
        if (t < 0.5f)
        {
            float fade = 1f - Mathf.SmoothStep(0f, EyeFadeOutEnd, t);
            result = from * (fade * flashMul);
        }
        else
        {
            float fade = Mathf.SmoothStep(EyeFadeInStart, 1f, t);
            result = to * (fade * flashMul);
        }

        result.a = Mathf.Lerp(from.a, to.a, t);
        return result;
    }

    private void ApplyEyeEmission(Color emissionColor)
    {
        eyesRenderer.GetPropertyBlock(eyesPropertyBlock);
        eyesPropertyBlock.SetColor(EmissionColorId, emissionColor);
        eyesRenderer.SetPropertyBlock(eyesPropertyBlock);
    }

    private void EnablePressParticleSystem()
    {
        if (pressParticleSystem == null)
            return;

        pressParticleSystem.gameObject.SetActive(true);
        pressParticleSystem.Play();
    }

    private Transform GetPressPose(int index)
    {
        switch (index)
        {
            case 0: return pressPose0;
            case 1: return pressPose1;
            case 2: return pressPose2;
            case 3: return pressPose3;
            default: return null;
        }
    }

    // Calls OnLeverOperated on every cached connected device.
    private void NotifyConnectedDevices()
    {
        foreach (MediumDoor mediumDoor in mediumDoors)
            mediumDoor.OnLeverOperated();

        foreach (Elevator elevator in elevators)
            elevator.OnLeverOperated();

        foreach (PistonHorizontal pistonHorizontal in pistonHorizontals)
            pistonHorizontal.OnLeverOperated();

        foreach (PistonVertical pistonVertical in pistonVerticals)
            pistonVertical.OnLeverOperated();
    }

    // Builds typed device lists once from the inspector object list.
    private void BuildConnectedDeviceLists()
    {
        mediumDoors.Clear();
        elevators.Clear();
        pistonHorizontals.Clear();
        pistonVerticals.Clear();

        foreach (GameObject deviceObject in connectedDevices)
        {
            if (deviceObject == null)
                continue;

            MediumDoor mediumDoor = deviceObject.GetComponent<MediumDoor>();
            if (mediumDoor != null)
                mediumDoors.Add(mediumDoor);

            Elevator elevator = deviceObject.GetComponent<Elevator>();
            if (elevator != null)
                elevators.Add(elevator);

            PistonHorizontal pistonHorizontal = deviceObject.GetComponent<PistonHorizontal>();
            if (pistonHorizontal != null)
                pistonHorizontals.Add(pistonHorizontal);

            PistonVertical pistonVertical = deviceObject.GetComponent<PistonVertical>();
            if (pistonVertical != null)
                pistonVerticals.Add(pistonVertical);
        }
    }
}
