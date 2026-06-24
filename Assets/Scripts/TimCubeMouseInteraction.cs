using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

/// Handles all mouse-centric cube interactions: Rotation (Swipe/Click) and Gaze
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

    private RunodeHighlighter lastHighlighter;

    private void Update()
    {
        HandleMouseRotation();
        UpdateGaze();
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        if (Mouse.current == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            RunodeMovement cube = hit.collider.GetComponentInParent<RunodeMovement>();
            if (cube != null)
            {
                RunodeHighlighter highlighter = cube.GetComponent<RunodeHighlighter>();
                if (highlighter != null)
                {
                    bool isActuallyRotatable = IsActuallyRotatable(cube, hit.point);
                    highlighter.SetHighlight(isActuallyRotatable);

                    if (lastHighlighter != null && lastHighlighter != highlighter)
                        lastHighlighter.ClearHighlight();

                    lastHighlighter = highlighter;
                    return;
                }
            }
        }

        if (lastHighlighter != null)
        {
            lastHighlighter.ClearHighlight();
            lastHighlighter = null;
        }
    }

    private bool IsActuallyRotatable(RunodeMovement cube, Vector3 hitPoint)
    {
        if (cube == null) return false;
        if (characterMovement != null && !characterMovement.IsGrounded) return false;

        float dist = Vector3.ProjectOnPlane(cube.transform.position - timTransform.position, Vector3.up).magnitude;
        if (dist > maxRotationDistance) return false;

        RunodeMovement[] stack = GetStackFromCube(cube);
        bool isSelectable = false;
        for (int i = 0; i < Mathf.Min(3, stack.Length); i++)
        {
            if (stack[i] == cube) { isSelectable = true; break; }
        }
        if (!isSelectable) return false;

        float verticalDist = cube.transform.position.y - timTransform.position.y;
        if (verticalDist < -1.5f || verticalDist > 2.5f) return false;

        if (!IsVisibleFromBody(cube, hitPoint)) return false;

        return true;
    }

    // Directs Tim's head to look at the cube surface under the mouse cursor
    private void UpdateGaze()
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

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            // Only gaze at valid Runode cubes
            if (hit.collider.GetComponentInParent<RunodeMovement>() != null)
            {
                playerAnimator.SetLookTarget(hit.point);
                return;
            }
        }

        playerAnimator.SetLookTarget(null);
    }

    private void HandleMouseRotation()
    {
        if (Mouse.current == null) return;

        // Ground check: Tim cannot rotate cubes while jumping or falling
        if (characterMovement != null && !characterMovement.IsGrounded)
        {
            if (isMouseRotating)
            {
                isMouseRotating = false;
                hasTriggeredMouseRotation = false;
                mouseRotTarget = null;
                characterMovement.SetMovementEnabled(true);
            }
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
        {
            bool isLeftClick = Mouse.current.leftButton.wasPressedThisFrame;
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                RunodeMovement cube = hit.collider.GetComponentInParent<RunodeMovement>();
                if (IsActuallyRotatable(cube, hit.point))
                {
                    if (isLeftClick)
                    {
                        isMouseRotating = true;
                        hasTriggeredMouseRotation = false;
                        mouseRotTarget = cube;
                        mouseHitNormal = hit.normal; 
                        lastMousePosition = Mouse.current.position.ReadValue();
                        
                        if (characterMovement != null) characterMovement.SetMovementEnabled(false);
                    }
                    else 
                    {
                        if (isRotating || cube.isRotating) return;
                        
                        Vector3 finalAxis = GetCardinalAxis(hit.normal);
                        StartCoroutine(SmoothRotateCubePhysical(cube, -90f, finalAxis, mouseRotationDuration));
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
                
                if (totalDelta.magnitude < mouseTwitchDeadzone) return;

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
                    Vector3 finalAxis = GetCardinalAxis(rawRotAxis);

                    StartCoroutine(SmoothRotateCubePhysical(mouseRotTarget, 90f, finalAxis, mouseRotationDuration));
                    hasTriggeredMouseRotation = true; 
                }
            }
            else
            {
                if (!hasTriggeredMouseRotation && mouseRotTarget != null)
                {
                    if (!isRotating && !mouseRotTarget.isRotating)
                    {
                        Vector3 finalAxis = GetCardinalAxis(mouseHitNormal);
                        StartCoroutine(SmoothRotateCubePhysical(mouseRotTarget, 90f, finalAxis, mouseRotationDuration));
                    }
                }

                isMouseRotating = false;
                hasTriggeredMouseRotation = false;
                mouseRotTarget = null;
                if (characterMovement != null) characterMovement.SetMovementEnabled(true);
            }
        }
    }

    private Vector3 GetCardinalAxis(Vector3 v)
    {
        float absX = Mathf.Abs(v.x);
        float absY = Mathf.Abs(v.y);
        float absZ = Mathf.Abs(v.z);

        if (absX > absY && absX > absZ) return Vector3.right * Mathf.Sign(v.x);
        if (absY > absX && absY > absZ) return Vector3.up * Mathf.Sign(v.y);
        return Vector3.forward * Mathf.Sign(v.z);
    }

    private IEnumerator SmoothRotateCubePhysical(RunodeMovement cube, float degrees, Vector3 worldAxis, float duration)
    {
        if (cube == null || cube.visualParent == null) yield break;
        
        isRotating = true;
        cube.isRotating = true;
        
        Transform targetTransform = cube.visualParent;
        Quaternion startRotation = targetTransform.localRotation;
        Vector3 rotAxis = cube.transform.InverseTransformDirection(worldAxis);
        
        Quaternion targetRotation = Quaternion.AngleAxis(degrees, rotAxis) * startRotation;
        
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

    private bool IsVisibleFromBody(RunodeMovement targetCube, Vector3 targetPoint)
    {
        Vector3[] origins = new Vector3[]
        {
            timTransform.position + Vector3.up * 1.7f,                             
            timTransform.position + Vector3.up * 1.0f,                             
            timTransform.position + Vector3.up * 1.0f + timTransform.right * 0.25f, 
            timTransform.position + Vector3.up * 1.0f - timTransform.right * 0.25f  
        };

        foreach (Vector3 origin in origins)
        {
            Vector3 dir = (targetPoint - origin);
            float maxDist = dir.magnitude;
            
            if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, maxDist + 0.1f))
            {
                if (hit.collider.transform.IsChildOf(timTransform)) continue;
                RunodeMovement hitCube = hit.collider.GetComponentInParent<RunodeMovement>();
                if (hitCube == targetCube) return true;
            }
        }
        return false;
    }
}
