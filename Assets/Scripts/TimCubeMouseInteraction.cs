using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

/// Handles all mouse-centric cube interactions: Rotation (Swipe/Click)
public class TimCubeMouseInteraction : MonoBehaviour
{
    [Header("MOUSE ROTATION SETTINGS:")]
    public float mouseRotationSensitivity = 1.0f;
    public float mouseRotationDuration = 0.08f;
    public float maxRotationDistance = 5.0f;
    public float mouseRotationClarity = 1.5f;   // One axis must be this many times larger than the other
    public float mouseTwitchDeadzone = 0.01f;   // Initial % of screen to ignore click-twitch

    [Header("JUICE SETTINGS:")]
    public float squashAmount = 0.15f;

    [HideInInspector] public bool isRotating = false;

    private Transform timTransform;
    private CharacterMovement characterMovement;
    private PlayerAnimator playerAnimator;

    private bool isMouseRotating = false;
    private bool hasTriggeredMouseRotation = false;
    private RunodeMovement mouseRotTarget = null;
    private Vector2 lastMousePosition;
    private Vector3 mouseHitNormal;

    private void Awake()
    {
        timTransform = transform;
        characterMovement = GetComponent<CharacterMovement>();
        playerAnimator = GetComponent<PlayerAnimator>();
    }

    private void Update()
    {
        HandleMouseRotation();
    }

    private void HandleMouseRotation()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                RunodeMovement cube = hit.collider.GetComponentInParent<RunodeMovement>();
                if (cube != null)
                {
                    float dist = Vector3.ProjectOnPlane(cube.transform.position - timTransform.position, Vector3.up).magnitude;
                    if (dist <= maxRotationDistance)
                    {
                        // Limit interaction to bottom 3 cubes
                        RunodeMovement[] stack = GetStackFromCube(cube);
                        bool isSelectable = false;
                        for (int i = 0; i < Mathf.Min(3, stack.Length); i++)
                        {
                            if (stack[i] == cube) { isSelectable = true; break; }
                        }
                        if (!isSelectable) return;

                        // LoS check: Find the root cube
                        RunodeMovement rootCube = stack[0];
                        Vector3 rayStartTim = timTransform.position + Vector3.up * 0.8f;
                        Vector3 targetCenter = rootCube.transform.position;
                        Vector3 dirToTarget = (targetCenter - rayStartTim).normalized;
                        float distToTarget = Vector3.Distance(rayStartTim, targetCenter);

                        RaycastHit[] hits = Physics.RaycastAll(rayStartTim, dirToTarget, distToTarget - 0.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        foreach (var loSHit in hits)
                        {
                            if (loSHit.collider.transform.IsChildOf(transform) || loSHit.collider.gameObject == gameObject) continue;
                            RunodeMovement hitCube = loSHit.collider.GetComponentInParent<RunodeMovement>();
                            if (hitCube != null)
                            {
                                bool inSameStack = false;
                                foreach(var s in stack) if(s == hitCube) inSameStack = true;
                                if (inSameStack) continue;
                            }
                            if (Vector3.Distance(loSHit.collider.bounds.ClosestPoint(timTransform.position), timTransform.position) < 0.2f) continue;
                            return; // Blocked
                        }

                        isMouseRotating = true;
                        hasTriggeredMouseRotation = false; // Reset trigger state
                        mouseRotTarget = cube;
                        mouseHitNormal = hit.normal; 
                        lastMousePosition = Mouse.current.position.ReadValue();
                        
                        if (characterMovement != null) characterMovement.SetMovementEnabled(false);
                    }
                }
            }
        }

        if (isMouseRotating)
        {
            if (Mouse.current.leftButton.isPressed)
            {
                if (hasTriggeredMouseRotation) return; 

                Vector2 currentMousePos = Mouse.current.position.ReadValue();
                Vector2 totalDelta = (currentMousePos - lastMousePosition) / Screen.height;
                
                // 1. Twitch Deadzone: Ignore the physical jerk of the click
                if (totalDelta.magnitude < mouseTwitchDeadzone) return;

                float absX = Mathf.Abs(totalDelta.x);
                float absY = Mathf.Abs(totalDelta.y);
                float maxAxis = Mathf.Max(absX, absY);
                float minAxis = Mathf.Min(absX, absY);

                // 2. Clarity Check: One axis must clearly win (Safe Wedge logic)
                bool isDirectionClear = maxAxis > minAxis * mouseRotationClarity;
                
                float threshold = 0.1f / Mathf.Max(0.01f, mouseRotationSensitivity);
                
                // 3. Force Trigger: If they drag really far (2x threshold), we commit regardless
                bool isForceTrigger = maxAxis > threshold * 2f; 

                if (maxAxis >= threshold && (isDirectionClear || isForceTrigger))
                {
                    if (isRotating || mouseRotTarget.isRotating) return;

                    Camera cam = Camera.main;
                    Vector3 worldSwipeDir = (cam.transform.right * totalDelta.x + cam.transform.up * totalDelta.y).normalized;

                    // Calculate Rotation Axis (Normal x Swipe)
                    Vector3 rawRotAxis = Vector3.Cross(mouseHitNormal, worldSwipeDir);

                    // Snap to cardinal world axis
                    Vector3 finalAxis = Vector3.zero;
                    float rotX = Mathf.Abs(rawRotAxis.x);
                    float rotY = Mathf.Abs(rawRotAxis.y);
                    float rotZ = Mathf.Abs(rawRotAxis.z);

                    if (rotX > rotY && rotX > rotZ) finalAxis = Vector3.right * Mathf.Sign(rawRotAxis.x);
                    else if (rotY > rotX && rotY > rotZ) finalAxis = Vector3.up * Mathf.Sign(rawRotAxis.y);
                    else finalAxis = Vector3.forward * Mathf.Sign(rawRotAxis.z);

                    StartCoroutine(SmoothRotateCubePhysical(mouseRotTarget, 90f, finalAxis, mouseRotationDuration));
                    hasTriggeredMouseRotation = true; 
                }
            }
            else
            {
                isMouseRotating = false;
                hasTriggeredMouseRotation = false;
                mouseRotTarget = null;
                if (characterMovement != null) characterMovement.SetMovementEnabled(true);
            }
        }
    }

    private IEnumerator SmoothRotateCubePhysical(RunodeMovement cube, float degrees, Vector3 worldAxis, float duration)
    {
        if (cube == null || cube.visualParent == null) yield break;
        
        isRotating = true;
        cube.isRotating = true;
        
        Transform targetTransform = cube.visualParent;
        Quaternion startRotation = targetTransform.localRotation;
        
        // Convert the world-space axis into the coordinate space of the cube's parent
        Vector3 rotAxis = cube.transform.InverseTransformDirection(worldAxis);
        
        // Apply rotation relative to current orientation
        Quaternion targetRotation = Quaternion.AngleAxis(degrees, rotAxis) * startRotation;
        
        // Snap to exact 90s
        Vector3 targetEuler = targetRotation.eulerAngles;
        targetEuler.x = Mathf.Round(targetEuler.x / 90f) * 90f;
        targetEuler.y = Mathf.Round(targetEuler.y / 90f) * 90f;
        targetEuler.z = Mathf.Round(targetEuler.z / 90f) * 90f;
        targetRotation = Quaternion.Euler(targetEuler);
        
        Quaternion overshootRot = Quaternion.AngleAxis(degrees + (degrees > 0 ? 5f : -5f), rotAxis) * startRotation;
        
        float elapsedTime = 0f;
        Vector3 originalScale = targetTransform.localScale;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            
            if (t < 0.8f) targetTransform.localRotation = Quaternion.Slerp(startRotation, overshootRot, t / 0.8f);
            else targetTransform.localRotation = Quaternion.Slerp(overshootRot, targetRotation, (t - 0.8f) / 0.2f);

            float squash = Mathf.Sin(t * Mathf.PI) * squashAmount;
            targetTransform.localScale = new Vector3(originalScale.x * (1 + squash), originalScale.y * (1 - squash), originalScale.z * (1 + squash));
            yield return null;
        }
        
        targetTransform.localRotation = targetRotation;
        
        float bounceTime = duration * 0.5f;
        float bElapsed = 0;
        while (bElapsed < bounceTime)
        {
            bElapsed += Time.deltaTime;
            float bt = bElapsed / bounceTime;
            float bounceSquash = Mathf.Sin(bt * Mathf.PI) * (squashAmount * 0.5f);
            targetTransform.localScale = new Vector3(originalScale.x * (1 - bounceSquash), originalScale.y * (1 + bounceSquash), originalScale.z * (1 - bounceSquash));
            yield return null;
        }
        targetTransform.localScale = originalScale;

        Physics.SyncTransforms();
        cube.isRotating = false;
        isRotating = false;
        if (PowerManager.Instance != null) PowerManager.Instance.RequestPowerFlowCheck();
    }

    private RunodeMovement[] GetStackFromCube(RunodeMovement baseCube)
    {
        if (baseCube == null) return new RunodeMovement[0];
        
        List<RunodeMovement> stack = new List<RunodeMovement> { baseCube };
        RunodeMovement[] allCubes = Object.FindObjectsByType<RunodeMovement>(FindObjectsSortMode.None);
        Vector3 basePos = baseCube.transform.position;
        
        foreach (RunodeMovement cube in allCubes)
        {
            if (cube == baseCube) continue;
            
            Vector3 cubePos = cube.transform.position;
            bool isAligned = Mathf.Abs(cubePos.x - basePos.x) < 0.1f && 
                             Mathf.Abs(cubePos.z - basePos.z) < 0.1f;
            bool isAbove = cubePos.y > basePos.y;
            
            if (isAligned && isAbove)
                stack.Add(cube);
        }
        
        stack.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        return stack.ToArray();
    }
}
