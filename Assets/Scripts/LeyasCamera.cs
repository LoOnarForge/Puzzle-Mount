using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using System.Collections;
using System;

/// Handles Leya's flight using CharacterController for aerial inspection.
/// Features smoothed acceleration and deceleration, organic Lens Breathing, and adaptive audio.
[RequireComponent(typeof(CharacterController))]
public class LeyasCamera : MonoBehaviour
{
    [Header("TARGET")]
    public Transform timTransform;
    public Transform leyasAnchor;

    [Header("FLIGHT SETTINGS")]
    public float baseMoveSpeed = 12f;
    public float accelerationTime = 0.5f;
    public float decelerationTime = 0.5f;
    public float lookSensitivity = 0.15f;
    public float maxRadius = 15f;
    public float maxHeightOffset = 10f;
    public float baseFov = 80f;

    [Header("TRANSITION")]
    public float transitionDuration = 0.4f;

    [Header("AUDIO SETTINGS")]
    public float actionModeVolume = 0.2f;
    public float inspectionMinVolume = 0.4f;
    public float maxVolume = 1.0f;
    public float minPitch = 0.9f;
    public float maxPitch = 1.3f;
    [Range(0f, 2f)] public float forwardEffortMultiplier = 1.2f;
    [Range(0f, 2f)] public float sidewaysEffortMultiplier = 0.5f;

    [Header("BIRD FEEL - BREATHING")]
    [Range(0f, 2f)] public float idleDelay = 0.5f;
    [Range(0f, 5f)] public float rampUpTime = 2.0f;
    [Range(0f, 5f)] public float fovPulseAmplitude = 1.5f;
    [Range(0f, 10f)] public float fovPulseFrequency = 1.2f;

    private CharacterController controller;
    private Camera cam;
    private Volume volume;
    private AudioSource humSource;
    private AudioListener leyaListener;
    private AudioListener mainListener;
    
    private float yaw;
    private float pitch;
    private bool isActive = false;
    private bool isTransitioning = false;
    private MenuManager menuManager;
    private Vector3 currentVelocity;

    private float idleTimer;
    private float internalTime;
    private float currentRamp;
    private Camera mainCam;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        cam = GetComponent<Camera>();
        volume = GetComponent<Volume>();
        humSource = GetComponent<AudioSource>();
        leyaListener = GetComponent<AudioListener>();
        menuManager = FindAnyObjectByType<MenuManager>(FindObjectsInactive.Include);
        mainCam = Camera.main;

        if (mainCam != null) mainListener = mainCam.GetComponent<AudioListener>();
        
        if (cam != null) cam.fieldOfView = baseFov;
        if (volume != null) volume.weight = 0f;
        if (leyaListener != null) leyaListener.enabled = false;

        if (leyasAnchor == null && timTransform != null)
        {
            leyasAnchor = timTransform.Find("Leyas Anchor");
        }

        Deactivate();
    }

    // Activates Leya at the specified position and rotation with a smooth transition.
    public void Activate(Vector3 startPosition, Quaternion startRotation)
    {
        this.enabled = true;
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(startPosition, startRotation, true));
    }

    // Deactivates Leya with a smooth transition back to a target.
    public void DeactivateWithTransition(Vector3 targetPosition, Quaternion targetRotation, Action onComplete)
    {
        StopAllCoroutines();
        StartCoroutine(TransitionRoutine(targetPosition, targetRotation, false, onComplete));
    }

    private IEnumerator TransitionRoutine(Vector3 refPos, Quaternion refRot, bool entering, Action onComplete = null)
    {
        bool wasMoving = isTransitioning;
        isActive = false;
        isTransitioning = true;
        transform.SetParent(null);

        if (controller != null) controller.enabled = entering;
        if (cam != null) cam.enabled = true;

        Vector3 startP = transform.position;
        Quaternion startR = transform.rotation;
        float startFov = cam != null ? cam.fieldOfView : baseFov;
        float startWeight = volume != null ? volume.weight : (entering ? 0f : 1f);
        float startHum = humSource != null ? humSource.volume : (entering ? actionModeVolume : inspectionMinVolume);

        if (entering)
        {
            if (mainListener != null) mainListener.enabled = false;
            if (leyaListener != null) leyaListener.enabled = true;
            if (humSource != null && !humSource.isPlaying) humSource.Play();

            if (!wasMoving)
            {
                transform.position = refPos;
                transform.rotation = refRot;
                startP = refPos;
                startR = refRot;
                if (mainCam != null) startFov = mainCam.fieldOfView;
                if (cam != null) cam.fieldOfView = startFov;
                startWeight = 0f;
                startHum = actionModeVolume;
            }
        }

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0, 1, elapsed / transitionDuration);

            Vector3 endP = entering ? (leyasAnchor != null ? leyasAnchor.position : refPos) : refPos;
            Quaternion endR = entering ? (leyasAnchor != null ? leyasAnchor.rotation : refRot) : refRot;
            float endFov = entering ? baseFov : (mainCam != null ? mainCam.fieldOfView : baseFov);

            transform.position = Vector3.Lerp(startP, endP, t);
            transform.rotation = Quaternion.Slerp(startR, endR, t);
            
            if (cam != null) cam.fieldOfView = Mathf.Lerp(startFov, endFov, t);
            if (volume != null) volume.weight = Mathf.Lerp(startWeight, entering ? 1f : 0f, t);
            if (humSource != null) humSource.volume = Mathf.Lerp(startHum, entering ? inspectionMinVolume : actionModeVolume, t);

            yield return null;
        }

        if (entering)
        {
            if (leyasAnchor != null)
            {
                transform.position = leyasAnchor.position;
                transform.rotation = leyasAnchor.rotation;
            }
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
            currentVelocity = Vector3.zero;
            ResetIdleState();
            idleTimer = idleDelay + 0.1f;
            if (cam != null) cam.fieldOfView = baseFov;
            if (volume != null) volume.weight = 1f;
            isActive = true;
            if (menuManager != null) menuManager.SetInspectionModeUI(true);
        }
        else
        {
            if (leyaListener != null) leyaListener.enabled = false;
            if (mainListener != null) mainListener.enabled = true;
            
            onComplete?.Invoke();
            Deactivate();
        }

        isTransitioning = false;
    }

    // Deactivates components instead of GameObject to keep AudioSource audible.
    public void Deactivate()
    {
        isActive = false;
        if (controller != null) controller.enabled = false;
        if (cam != null) cam.enabled = false;
        
        if (leyasAnchor != null)
        {
            transform.SetParent(leyasAnchor);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        if (humSource != null)
        {
            humSource.volume = actionModeVolume;
            humSource.pitch = minPitch;
            if (!humSource.isPlaying) humSource.Play();
        }

        this.enabled = false;
    }

    private void Update()
    {
        if (!isActive || Keyboard.current == null || Mouse.current == null) return;

        HandleRotation();
        HandleMovement();
        UpdateAudio();
    }

    private void UpdateAudio()
    {
        if (humSource == null) return;

        Vector3 localVel = transform.InverseTransformDirection(currentVelocity);
        
        float forwardEffort = Mathf.Abs(localVel.z) * forwardEffortMultiplier;
        float sideEffort = Mathf.Abs(localVel.x) * sidewaysEffortMultiplier;
        float verticalEffort = Mathf.Abs(localVel.y);

        float totalEffort = Mathf.Clamp01((forwardEffort + sideEffort + verticalEffort) / baseMoveSpeed);

        humSource.volume = Mathf.Lerp(inspectionMinVolume, maxVolume, totalEffort);
        humSource.pitch = Mathf.Lerp(minPitch, maxPitch, totalEffort);
    }

    private void HandleRotation()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        if (mouseDelta.sqrMagnitude > 0.001f) idleTimer = 0f;

        yaw += mouseDelta.x * lookSensitivity;
        pitch -= mouseDelta.y * lookSensitivity;
        pitch = Mathf.Clamp(pitch, -89f, 89f);
        
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void HandleMovement()
    {
        Vector3 targetDir = Vector3.zero;
        bool hasInput = false;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        if (Keyboard.current[Key.W].isPressed) { targetDir += forward; hasInput = true; }
        if (Keyboard.current[Key.S].isPressed) { targetDir -= forward; hasInput = true; }
        if (Keyboard.current[Key.A].isPressed) { targetDir -= right; hasInput = true; }
        if (Keyboard.current[Key.D].isPressed) { targetDir += right; hasInput = true; }
        if (Keyboard.current[Key.Q].isPressed) { targetDir += Vector3.down; hasInput = true; }
        if (Keyboard.current[Key.E].isPressed) { targetDir += Vector3.up; hasInput = true; }

        if (hasInput) idleTimer = 0f;
        else idleTimer += Time.deltaTime;

        Vector3 targetVelocity = targetDir.normalized * baseMoveSpeed;
        float step = (baseMoveSpeed / (targetDir.sqrMagnitude > 0.001f ? accelerationTime : decelerationTime)) * Time.deltaTime;
        currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, step);

        Vector3 movement = currentVelocity * Time.deltaTime;

        if (idleTimer > idleDelay)
        {
            internalTime += Time.deltaTime;
            currentRamp = Mathf.MoveTowards(currentRamp, 1.0f, Time.deltaTime / rampUpTime);
            if (cam != null)
            {
                float fovOffset = Mathf.Sin(internalTime * fovPulseFrequency) * fovPulseAmplitude * currentRamp;
                cam.fieldOfView = baseFov + fovOffset;
            }
        }
        else
        {
            currentRamp = 0f;
            if (cam != null && !isTransitioning) cam.fieldOfView = baseFov;
        }

        if (movement.sqrMagnitude > 0.000001f)
        {
            if (timTransform != null) ApplyClamping(ref movement);
            controller.Move(movement);
        }
    }

    private void ApplyClamping(ref Vector3 movement)
    {
        Vector3 horizontalOffset = Vector3.ProjectOnPlane(transform.position - timTransform.position, Vector3.up);
        if (horizontalOffset.magnitude >= maxRadius)
        {
            Vector3 normal = horizontalOffset.normalized;
            Vector3 horizontalMove = Vector3.ProjectOnPlane(movement, Vector3.up);
            if (Vector3.Dot(horizontalMove, normal) > 0)
            {
                Vector3 projected = Vector3.ProjectOnPlane(horizontalMove, normal);
                movement.x = projected.x;
                movement.z = projected.z;
                
                Vector3 projectedVel = Vector3.ProjectOnPlane(currentVelocity, normal);
                currentVelocity.x = projectedVel.x;
                currentVelocity.z = projectedVel.z;
            }
        }

        float nextY = transform.position.y + movement.y;
        if ((nextY > timTransform.position.y + maxHeightOffset && movement.y > 0) || (nextY < timTransform.position.y && movement.y < 0))
        {
            movement.y = 0;
            currentVelocity.y = 0;
        }
    }

    private void ResetIdleState()
    {
        idleTimer = 0f;
        internalTime = 0f;
        currentRamp = 0f;
        if (cam != null && !isTransitioning) cam.fieldOfView = baseFov;
    }
}
