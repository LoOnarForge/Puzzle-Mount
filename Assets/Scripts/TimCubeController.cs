using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

public enum RotationAxis
{
    Horizontal, // Y-axis rotation
    Vertical    // X-axis rotation
}

/// Handles all of Tim's interactions with cubes: selection, pushing, rotation, gaze, highlight, camera rotation
public class TimCubeController : MonoBehaviour
{
    [Header("PUSH SETTINGS:")]
    public float pushRange = 1.5f;
    [SerializeField] private LayerMask pushDetectionLayer;
    public float initialPushDelay = 0.35f;
    public float fastPushInterval = 0.1f;
    public float precisePushInterval = 0.5f;

    [Header("CUBE SELECTION:")]
    [SerializeField] private RunodeMovement pushableRunode = null;
    [HideInInspector] public float detectionTolerance = 0.6f;

    [Header("MOUSE ROTATION SETTINGS:")]
    [Tooltip("Runodes + Devices only.")]
    public LayerMask interactionLayer;
    public float mouseRotationSensitivity = 1.0f;
    public float mouseRotationDuration = 0.08f;
    public float maxRotationDistance = 5.0f;
    public float maxClickDistance = 3f;
    public float mouseRotationClarity = 1.5f;

    private const float DeviceInteractVerticalMin = -1.5f;
    private const float DeviceInteractVerticalMax = 2.5f;
    private const int DeviceSightRayIterations = 3;
    private const int DeviceSightRaysRequired = 2;
    private const float DeviceSightRayExtension = 0.1f;
    private const float DeviceSightRayStep = 0.01f;
    public float mouseTwitchDeadzone = 0.01f;

    [Header("JUICE SETTINGS:")]
    public float squashAmount = 0.15f;

    [HideInInspector] public bool isRotating = false;

    public RunodeMovement MouseHitCube { get; private set; } = null;
    public Vector3 MouseHitPoint { get; private set; }

    private Transform timTransform;
    private CharacterMovement characterMovement;
    private CharacterController controller;
    private PlayerAnimator playerAnimator;
    private CameraFollow cameraFollow;
    private MenuManager _menuManager;

    // Push delay tracking
    private RunodeMovement currentTargetCube;
    private Vector3 currentPushDirection;
    private float pushDelayTimer;
    private bool isDelayActive;
    private bool isFirstPush = true;

    // Mouse rotation state
    private bool isMouseRotating = false;
    private bool hasTriggeredMouseRotation = false;
    private bool hasMouseMovedDuringDrag = false;
    private RunodeMovement mouseRotTarget = null;
    private Vector2 lastMousePosition;
    private Vector3 mouseHitNormal;

    private bool isCameraDragging = false;
    private Vector2 cameraDragStartPos;

    private bool isPreciseMode = false;

    private void Awake()
    {
        timTransform = transform;
        characterMovement = GetComponent<CharacterMovement>();
        controller = GetComponent<CharacterController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        cameraFollow = FindAnyObjectByType<CameraFollow>();
        _menuManager = Object.FindAnyObjectByType<MenuManager>();

        if (pushDetectionLayer.value == 0)
            pushDetectionLayer = LayerMask.GetMask("Runodes");
    }

    private void Update()
    {
        // Single mouse raycast shared by gaze and rotation
        bool mouseHitValid = false;
        RaycastHit mouseHit = default;
        RunodeMovement mouseHitCube = null;

        if (Mouse.current != null && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out mouseHit, 100f, interactionLayer))
            {
                mouseHitValid = true;
                mouseHitCube = mouseHit.collider.GetComponentInParent<RunodeMovement>();
            }
        }

        MouseHitCube = mouseHitCube;
        MouseHitPoint = mouseHitValid ? mouseHit.point : Vector3.zero;

        if (characterMovement.IsGrounded && !isRotating)
        {
            CheckCubeInteraction();
        }

        HandlePushModeToggle();
        UpdatePushDelay();

        Vector3 moveDirection = characterMovement.GetMovementDirectionExternal();
        if (moveDirection.magnitude < 0.1f)
        {
            ResetPushEngagement();
        }

        HandleMouseRotation(mouseHitValid, mouseHit, mouseHitCube);

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            TryHandleLeverClick(mouseHitValid, mouseHit, mouseHitCube);

        HandleCameraRotation();
        UpdateGaze(mouseHitValid, mouseHit, mouseHitCube);
    }

    // === PUSH LOGIC (from TimCubeInteraction) ===

    private void CheckCubeInteraction()
    {
        float detectionHeight = 0.8f;
        Vector3 rayStart = timTransform.position + Vector3.up * detectionHeight;
        Vector3 rayDirection = timTransform.forward;

        if (Physics.Raycast(rayStart, rayDirection, out RaycastHit hit, pushRange, pushDetectionLayer))
        {
            RunodeMovement hitCube = hit.collider.GetComponentInParent<RunodeMovement>();
            if (hitCube != null)
            {
                pushableRunode = hitCube;
                HandleCubePushing(hitCube);
            }
            else
            {
                pushableRunode = null;
            }
        }
        else
        {
            pushableRunode = null;
        }
    }

    private void HandleCubePushing(RunodeMovement hitCube)
    {
        Vector3 moveDirection = characterMovement.GetMovementDirectionExternal();
        if (moveDirection.magnitude < 0.1f) return;

        if (hitCube != null && IsProperlyAlignedToPush(hitCube.transform, timTransform.forward))
        {
            RunodeMovement[] stackFromTimLevel = hitCube.GetStack();
            if (stackFromTimLevel.Length > 3) return;

            Vector3 pushDirection = hitCube.GetRelativePushDirection(timTransform);

            if (currentTargetCube != hitCube)
            {
                StartNewPushEngagement(hitCube, pushDirection);
            }
            else
            {
                bool significantDirectionChange = currentPushDirection != pushDirection;
                if (significantDirectionChange) StartNewPushEngagement(hitCube, pushDirection);
                else currentPushDirection = pushDirection;
            }

            if (!isDelayActive)
            {
                RunodeMovement[] currentStack = hitCube.GetStack();
                bool pushSuccess = TryPushStack(currentStack, pushDirection);
                if (pushSuccess) StartContinuousPushDelay();
            }
        }
    }

    public bool IsProperlyAlignedToPush(Transform cubeTransform, Vector3 pushDirection)
    {
        Vector3 cubeCenter = cubeTransform.position;
        Vector3 timPos = timTransform.position;
        Vector3 localOffset = timPos - cubeCenter;
        float absX = Mathf.Abs(localOffset.x);
        float absZ = Mathf.Abs(localOffset.z);
        float faceTolerance = 0.4f;
        if (absX > absZ) return Mathf.Abs(localOffset.z) < faceTolerance;
        else return Mathf.Abs(localOffset.x) < faceTolerance;
    }

    private void HandlePushModeToggle()
    {
        if (Keyboard.current != null && Keyboard.current.ctrlKey.wasPressedThisFrame)
        {
            isPreciseMode = !isPreciseMode;
            if (_menuManager != null) _menuManager.SetPreciseMode(isPreciseMode);
        }
    }

    private bool TryPushStack(RunodeMovement[] stack, Vector3 direction)
    {
        if (stack == null || stack.Length == 0) return false;
        foreach (var cube in stack) if (!cube.CanPushSingle(direction)) return false;
        foreach (var cube in stack) cube.PushSingle(direction);
        return true;
    }

    public void StartNewPushEngagement(RunodeMovement cube, Vector3 pushDirection)
    {
        currentTargetCube = cube;
        currentPushDirection = pushDirection;
        pushDelayTimer = 0f;
        isDelayActive = true;
        isFirstPush = true;
        UpdatePushDelay();
    }

    public void StartContinuousPushDelay()
    {
        pushDelayTimer = 0f;
        isDelayActive = true;
        isFirstPush = false;
    }

    public void ResetPushEngagement()
    {
        currentTargetCube = null;
        currentPushDirection = Vector3.zero;
        pushDelayTimer = 0f;
        isDelayActive = false;
        isFirstPush = true;
    }

    public void UpdatePushDelay()
    {
        if (isDelayActive)
        {
            pushDelayTimer += Time.deltaTime;
            float continuousDelay = isPreciseMode ? precisePushInterval : fastPushInterval;
            float requiredDelay = isFirstPush ? initialPushDelay : continuousDelay;
            if (pushDelayTimer >= requiredDelay) isDelayActive = false;
        }
    }

    public RunodeMovement PushableRunode => pushableRunode;

    public RunodeMovement CurrentTargetCube => currentTargetCube;
    public Vector3 CurrentPushDirection => currentPushDirection;
    public float PushDelayTimer => pushDelayTimer;
    public bool IsDelayActive => isDelayActive;
    public bool IsFirstPush => isFirstPush;

    // === MOUSE ROTATION LOGIC (from TimCubeMouseInteraction) ===

    private void HandleCameraRotation()
    {
        if (Mouse.current == null || cameraFollow == null || cameraFollow.IsInspectionMode) return;

        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            cameraDragStartPos = Mouse.current.position.ReadValue();
            isCameraDragging = true;
        }

        if (isCameraDragging && Mouse.current.middleButton.isPressed)
        {
            Vector2 currentPos = Mouse.current.position.ReadValue();
            float deltaX = currentPos.x - cameraDragStartPos.x;

            if (Mathf.Abs(deltaX) > cameraFollow.cameraRotationThreshold)
            {
                if (deltaX > 0) cameraFollow.CycleClockwise();
                else cameraFollow.CycleCounterClockwise();

                cameraDragStartPos = currentPos;
            }
        }

        if (Mouse.current.middleButton.wasReleasedThisFrame)
        {
            isCameraDragging = false;
        }
    }

    private void UpdateGaze(bool mouseHitValid, RaycastHit mouseHit, RunodeMovement mouseHitCube)
    {
        if (playerAnimator == null || characterMovement == null) return;

        // Gaze is active only when stationary or while actively rotating a cube
        bool isStationary = characterMovement.IsStationary;
        if (!isStationary && !isMouseRotating)
        {
            playerAnimator.SetLookTarget(null);
            return;
        }

        if (Mouse.current == null) return;

        if (mouseHitValid && mouseHitCube != null)
        {
            playerAnimator.SetLookTarget(mouseHit.point);
            return;
        }

        playerAnimator.SetLookTarget(null);
    }

    public bool IsCubeRotatable(RunodeMovement cube, Vector3 hitPoint)
    {
        if (cube == null) return false;
        if (!cube.isRotatable) return false;
        if (characterMovement != null && !characterMovement.IsGrounded) return false;

        float dist = Vector3.ProjectOnPlane(cube.transform.position - timTransform.position, Vector3.up).magnitude;
        if (dist > maxRotationDistance) return false;

        if (!cube.IsInStackRange(3)) return false;

        float verticalDist = cube.transform.position.y - timTransform.position.y;
        if (verticalDist < -1.5f || verticalDist > 2.5f) return false;

        if (!cube.IsVisibleFrom(timTransform, hitPoint, interactionLayer)) return false;

        return true;
    }

    public bool IsActuallyRotatable(RunodeMovement cube, Vector3 hitPoint) => IsCubeRotatable(cube, hitPoint);

    private void TryHandleLeverClick(bool mouseHitValid, RaycastHit mouseHit, RunodeMovement mouseHitCube)
    {
        if (!mouseHitValid)
            return;

        if (TryPressButtonFromClick(mouseHit.collider, mouseHit.point))
            return;

        if (Mouse.current != null && Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit[] hits = Physics.RaycastAll(ray, 100f, interactionLayer, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (TryPressButtonFromClick(hits[i].collider, hits[i].point))
                    return;
            }
        }

        if (mouseHitCube != null)
            return;

        Lever lever = mouseHit.collider.GetComponentInParent<Lever>();
        if (lever == null)
            return;

        if (!CanTimInteractWithDevice(mouseHit.point, lever.transform))
            return;

        lever.MouseClickDetected();
    }

    private bool TryPressButtonFromClick(Collider clickedCollider, Vector3 clickPoint)
    {
        if (clickedCollider == null)
            return false;

        ButtonDevice buttonDevice = clickedCollider.GetComponentInParent<ButtonDevice>();
        if (buttonDevice == null)
            return false;

        if (!CanTimInteractWithDevice(clickPoint, buttonDevice.transform))
            return false;

        return buttonDevice.MouseClickDetected(clickedCollider);
    }

    // Same horizontal reach, vertical band, and multi-ray sight rules as cube rotation, applied at the click point on a device.
    private bool CanTimInteractWithDevice(Vector3 clickPoint, Transform deviceRoot)
    {
        if (deviceRoot == null)
            return false;

        if (characterMovement != null && !characterMovement.IsGrounded)
            return false;

        float dist = Vector3.ProjectOnPlane(clickPoint - timTransform.position, Vector3.up).magnitude;
        if (dist > maxRotationDistance)
            return false;

        float verticalDist = clickPoint.y - timTransform.position.y;
        if (verticalDist < DeviceInteractVerticalMin || verticalDist > DeviceInteractVerticalMax)
            return false;

        return CanTimSeeDevice(clickPoint, deviceRoot);
    }

    private bool CanTimSeeDevice(Vector3 targetPoint, Transform deviceRoot)
    {
        Vector3 right = timTransform.right;
        Vector3 back = -timTransform.forward;

        Vector3[] origins = new Vector3[]
        {
            timTransform.position + Vector3.up * 1.7f,
            timTransform.position + Vector3.up * 1.0f,
            timTransform.position + Vector3.up * 1.0f + right * 0.35f + back * 0.3f,
            timTransform.position + Vector3.up * 1.0f - right * 0.35f + back * 0.3f,
            timTransform.position + Vector3.up * 1.35f + right * 0.35f + back * 0.2f,
            timTransform.position + Vector3.up * 0.75f - right * 0.35f + back * 0.2f,
        };

        int hitCount = 0;

        foreach (Vector3 origin in origins)
        {
            Vector3 dir = targetPoint - origin;
            float maxDist = dir.magnitude;
            if (maxDist <= 0.001f)
                continue;

            Vector3 rayDir = dir / maxDist;
            Vector3 currentOrigin = origin;
            float remainingDist = maxDist + DeviceSightRayExtension;

            for (int i = 0; i < DeviceSightRayIterations; i++)
            {
                if (remainingDist <= 0f)
                    break;

                if (!Physics.Raycast(currentOrigin, rayDir, out RaycastHit hit, remainingDist, interactionLayer))
                    break;

                if (hit.collider.transform.IsChildOf(timTransform))
                {
                    remainingDist -= hit.distance;
                    currentOrigin = hit.point + rayDir * DeviceSightRayStep;
                    continue;
                }

                if (IsColliderPartOfDevice(hit.collider, deviceRoot))
                {
                    hitCount++;
                    break;
                }

                break;
            }

            if (hitCount >= DeviceSightRaysRequired)
                return true;
        }

        return hitCount >= DeviceSightRaysRequired;
    }

    private static bool IsColliderPartOfDevice(Collider collider, Transform deviceRoot)
    {
        if (collider == null || deviceRoot == null)
            return false;

        Transform hitTransform = collider.transform;
        return hitTransform == deviceRoot || hitTransform.IsChildOf(deviceRoot);
    }

    private void HandleMouseRotation(bool mouseHitValid, RaycastHit mouseHit, RunodeMovement mouseHitCube)
    {
        if (Mouse.current == null) return;

        // Ground check: Tim cannot rotate cubes while jumping or falling
        if (characterMovement != null && !characterMovement.IsGrounded)
        {
            if (isMouseRotating)
            {
                isMouseRotating = false;
                hasTriggeredMouseRotation = false;
                hasMouseMovedDuringDrag = false;
                mouseRotTarget = null;
            }
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
        {
            bool isLeftClick = Mouse.current.leftButton.wasPressedThisFrame;
            if (mouseHitValid && mouseHitCube != null)
            {
                if (IsActuallyRotatable(mouseHitCube, mouseHit.point))
                {
                    Vector3 visualNormal = mouseHit.normal;

                    if (isLeftClick)
                    {
                        isMouseRotating = true;
                        hasTriggeredMouseRotation = false;
                        hasMouseMovedDuringDrag = false;
                        mouseRotTarget = mouseHitCube;
                        mouseHitNormal = visualNormal;
                        lastMousePosition = Mouse.current.position.ReadValue();
                    }
                    else
                    {
                        if (isRotating || mouseHitCube.isRotating) return;

                        Vector3 finalAxis = RunodeMovement.GetCardinalAxis(visualNormal);
                        StartCoroutine(SmoothRotateCubePhysical(mouseHitCube, -90f, finalAxis, mouseRotationDuration));
                    }
                }
            }
        }

        if (isMouseRotating)
        {
            if (mouseRotTarget != null)
            {
                float dist = Vector3.ProjectOnPlane(mouseRotTarget.transform.position - timTransform.position, Vector3.up).magnitude;
                if (dist > maxRotationDistance)
                {
                    isMouseRotating = false;
                    hasTriggeredMouseRotation = false;
                    hasMouseMovedDuringDrag = false;
                    mouseRotTarget = null;
                    return;
                }
            }

            if (Mouse.current.leftButton.isPressed)
            {
                if (hasTriggeredMouseRotation) return;

                Vector2 currentMousePos = Mouse.current.position.ReadValue();
                Vector2 totalDelta = (currentMousePos - lastMousePosition) / Screen.height;

                if (totalDelta.magnitude < mouseTwitchDeadzone) return;

                hasMouseMovedDuringDrag = true;

                float absX = Mathf.Abs(totalDelta.x);
                float absY = Mathf.Abs(totalDelta.y);
                float maxAxis = Mathf.Max(absX, absY);
                float minAxis = Mathf.Min(absX, absY);

                bool isDirectionClear = maxAxis > minAxis * mouseRotationClarity;
                float threshold = 0.1f / Mathf.Max(0.01f, mouseRotationSensitivity);
                bool isForceTrigger = maxAxis > threshold * 2f;

                if (maxAxis >= threshold && (isDirectionClear || isForceTrigger))
                {
                    if (isRotating || mouseRotTarget.isRotating) return;

                    Camera cam = Camera.main;
                    Vector3 worldSwipeDir = (cam.transform.right * totalDelta.x + cam.transform.up * totalDelta.y).normalized;
                    Vector3 rawRotAxis = Vector3.Cross(mouseHitNormal, worldSwipeDir);
                    Vector3 finalAxis = RunodeMovement.GetCardinalAxis(rawRotAxis);

                    StartCoroutine(SmoothRotateCubePhysical(mouseRotTarget, 90f, finalAxis, mouseRotationDuration));
                    hasTriggeredMouseRotation = true;
                }
            }
            else
            {
                if (!hasTriggeredMouseRotation && !hasMouseMovedDuringDrag && mouseRotTarget != null)
                {
                    if (!isRotating && !mouseRotTarget.isRotating)
                    {
                        Vector3 finalAxis = RunodeMovement.GetCardinalAxis(mouseHitNormal);
                        StartCoroutine(SmoothRotateCubePhysical(mouseRotTarget, 90f, finalAxis, mouseRotationDuration));
                    }
                }

                isMouseRotating = false;
                hasTriggeredMouseRotation = false;
                hasMouseMovedDuringDrag = false;
                mouseRotTarget = null;
            }
        }
    }

    private IEnumerator SmoothRotateCubePhysical(RunodeMovement cube, float degrees, Vector3 worldAxis, float duration)
    {
        isRotating = true;
        yield return StartCoroutine(cube.RotateVisualSmooth(degrees, worldAxis, duration, squashAmount));
        isRotating = false;
    }
}
