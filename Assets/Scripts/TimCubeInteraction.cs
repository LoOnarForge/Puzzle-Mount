using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public enum RotationAxis
{
    Horizontal, // Y-axis rotation
    Vertical    // X-axis rotation
}

/// Handles all of Tim's interactions with cubes: selection, pushing, rotation
public class TimCubeInteraction : MonoBehaviour
{
    [Header("PUSH SETTINGS:")]
    public float pushRange = 1.5f; 
    public float initialPushDelay = 0.35f;
    public float fastPushInterval = 0.1f;
    public float precisePushInterval = 0.5f;

    [Header("CUBE SELECTION:")]
    [SerializeField] private RunodeMovement detectedCube = null;
    [SerializeField] private RunodeMovement highlightedCube = null;
    [HideInInspector] public float detectionTolerance = 0.6f;

    private Transform timTransform;
    private CharacterMovement characterMovement;
    private CharacterController controller;
    private PlayerAnimator playerAnimator;
    private MenuManager _menuManager;
    private TimCubeMouseInteraction mouseInteraction;
    
    // Push delay tracking
    private RunodeMovement currentTargetCube;
    private Vector3 currentPushDirection;
    private float pushDelayTimer;
    private bool isDelayActive;
    private bool isFirstPush = true;
    private bool isPushingThisFrame;
    
    // Highlighted cube tracking
    private RunodeMovement currentlyHighlightedCube = null;
    
    // Tim's own cube selection system
    private RunodeMovement[] currentStack = null;
    private int selectedIndex = 0;
    private RunodeMovement selectedCube = null;
    
    // Cache for performance optimization
    private RunodeMovement[] allCubesCache = null;
    private float lastCubesCacheTime = 0f;
    private const float CACHE_REFRESH_INTERVAL = 1.0f;
    
    public RunodeMovement GetHighlightedCube() { return currentlyHighlightedCube; }

    private void Awake()
    {
        timTransform = transform;
        characterMovement = GetComponent<CharacterMovement>();
        controller = GetComponent<CharacterController>();
        playerAnimator = GetComponent<PlayerAnimator>();
        _menuManager = Object.FindFirstObjectByType<MenuManager>();
        mouseInteraction = GetComponent<TimCubeMouseInteraction>();
    }
    
    private void Update()
    {
        isPushingThisFrame = false;

        if (characterMovement.IsGrounded && (mouseInteraction == null || !mouseInteraction.isRotating)) 
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
    }

    private void CheckCubeInteraction()
    {
        float detectionHeight = 0.8f;
        Vector3 rayStart = timTransform.position + Vector3.up * detectionHeight;
        Vector3 rayDirection = timTransform.forward;
        
        if (Physics.Raycast(rayStart, rayDirection, out RaycastHit hit, pushRange))
        {
            RunodeMovement hitCube = hit.collider.GetComponentInParent<RunodeMovement>();
            if (hitCube != null)
            {
                detectedCube = hitCube;
                HandleCubeHighlighting(hitCube);
                HandleCubePushing(hitCube);
            }
            else
            {
                detectedCube = null;
                HandleCubeHighlighting(null);
            }
        }
        else
        {
            detectedCube = null;
            HandleCubeHighlighting(null);
        }
    }

    private void HandleCubeHighlighting(RunodeMovement hitCube)
    {
        if (hitCube != null && IsReasonablyAlignedForDetection(hitCube.transform))
        {
            if (currentlyHighlightedCube != hitCube)
            {
                currentlyHighlightedCube = hitCube;
                currentStack = GetStackFromHighlightedCube(hitCube);
                selectedIndex = 0;
                selectedCube = (currentStack != null && currentStack.Length > 0) ? currentStack[0] : null;
                highlightedCube = selectedCube;
            }
        }
        else
        {
            if (currentlyHighlightedCube != null)
            {
                currentlyHighlightedCube = null;
                currentStack = null;
                selectedCube = null;
                selectedIndex = 0;
                highlightedCube = null;
            }
        }
    }

    private void HandleCubePushing(RunodeMovement hitCube)
    {
        Vector3 moveDirection = characterMovement.GetMovementDirectionExternal();
        if (moveDirection.magnitude < 0.1f) return;
        
        if (hitCube != null && IsProperlyAlignedToPush(hitCube.transform, timTransform.forward))
        {
            RunodeMovement[] stackFromTimLevel = GetStackFromHighlightedCube();
            if (stackFromTimLevel.Length > 3) return;
            
            isPushingThisFrame = true;
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
                RunodeMovement[] currentStack = GetStackFromHighlightedCube(hitCube);
                bool pushSuccess = TryPushStack(currentStack, pushDirection);
                if (pushSuccess) StartContinuousPushDelay();
            }      
        }
    }

    public bool IsReasonablyAlignedForDetection(Transform cubeTransform)
    {
        Vector3 cubeCenter = cubeTransform.position;
        Vector3 timPos = timTransform.position;
        Vector3 localOffset = timPos - cubeCenter;
        float absX = Mathf.Abs(localOffset.x);
        float absZ = Mathf.Abs(localOffset.z);
        if (absX > absZ) return Mathf.Abs(localOffset.z) < detectionTolerance;
        else return Mathf.Abs(localOffset.x) < detectionTolerance;
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
    
    private RunodeMovement[] GetAllCubesOptimized()
    {
        float currentTime = Time.time;
        if (allCubesCache == null || currentTime - lastCubesCacheTime > CACHE_REFRESH_INTERVAL)
        {
            allCubesCache = Object.FindObjectsByType<RunodeMovement>(FindObjectsSortMode.None);
            lastCubesCacheTime = currentTime;
        }
        return allCubesCache;
    }
    
    private RunodeMovement[] GetStackFromHighlightedCube(RunodeMovement targetCube = null)
    {
        RunodeMovement baseCube = targetCube ?? currentlyHighlightedCube;
        if (baseCube == null) return new RunodeMovement[0];
        List<RunodeMovement> stack = new List<RunodeMovement> { baseCube };
        RunodeMovement[] allCubes = GetAllCubesOptimized();
        Vector3 basePos = baseCube.transform.position;
        foreach (RunodeMovement cube in allCubes)
        {
            if (cube == baseCube) continue;
            Vector3 cubePos = cube.transform.position;
            bool isAligned = Mathf.Abs(cubePos.x - basePos.x) < 0.1f && Mathf.Abs(cubePos.z - basePos.z) < 0.1f;
            bool isAbove = cubePos.y > basePos.y;
            if (isAligned && isAbove) stack.Add(cube);
        }
        stack.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        return stack.ToArray();
    }
    
    private void OnDrawGizmos()
    {
        if (selectedCube != null)
        {
            bool canPushSelected = IsProperlyAlignedToPush(selectedCube.transform, timTransform.forward);
            Gizmos.color = canPushSelected ? Color.green : Color.yellow;
            Gizmos.DrawWireCube(selectedCube.transform.position, selectedCube.transform.localScale * 1.1f);
        }
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

    private bool isPreciseMode = false;
    public RunodeMovement CurrentTargetCube => currentTargetCube;
    public Vector3 CurrentPushDirection => currentPushDirection;
    public float PushDelayTimer => pushDelayTimer;
    public bool IsDelayActive => isDelayActive;
    public bool IsFirstPush => isFirstPush;
}
