using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class RunodeMovement : MonoBehaviour
{
    [Header("MOVEMENT:")]
    public float moveSpeed = 5f;

    [Header("RANDOM ROTATION:")]
    public bool randomRotateOnStart = true;

    [Header("DEBUG:")]
    public bool isMovingPos = false;
    public bool isRotating { get; private set; }

    public bool IsBusy => isMovingPos || isRotating;

    [Header("PHYSICS LAYERS:")]
    [SerializeField] private LayerMask groundCheckMask = 0;
    [SerializeField] private LayerMask obstructionCheckMask = 0;

    private Rigidbody rb;
    private Transform visualParent;
    private RunodeCube runodeCube;
    private bool wasGrounded;

    // Stores the rotation as it was set in the Editor
    private Quaternion baseRotation;
    public Quaternion BaseRotation => baseRotation;

    private void Awake()
    {
        visualParent = transform.GetChild(0);

        // Capture the base rotation before any randomization happens
        if (visualParent != null)
        {
            baseRotation = visualParent.localRotation;
        }

        rb = GetComponent<Rigidbody>();
        runodeCube = GetComponent<RunodeCube>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
    }
    private void Start()
    {
        if (randomRotateOnStart)
        {
            ApplyRandomRotation();
        }

        SnapToGrid();
    }

    private void FixedUpdate()
    {
        bool isGrounded = IsGrounded();

        if (!wasGrounded && isGrounded && runodeCube != null)
        {
            runodeCube.RefreshCubeAndAdjacentConnections();
        }

        wasGrounded = isGrounded;
    }

    public void SetKinematic(bool kinematic)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.isKinematic = kinematic;

        if (!kinematic)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        }
    }

    private void SnapToGrid()
    {
        Vector3 p = transform.position;
        transform.position = new Vector3(Mathf.Round(p.x), p.y, Mathf.Round(p.z));
    }

    // Rotates the visual mesh only — colliders and triggers stay in place.
    public void RotateVisual(float rotationY)
    {
        if (visualParent != null)
        {
            visualParent.Rotate(0, rotationY, 0);
        }
    }

    // Returns which direction to push based on where Tim is standing relative to this Runode.
    public Vector3 GetRelativePushDirection(Transform pusherTransform)
    {
        Vector3 offset = transform.position - pusherTransform.position;

        if (Mathf.Abs(offset.x) > Mathf.Abs(offset.z))
        {
            return new Vector3(Mathf.Sign(offset.x), 0, 0);
        }
        else
        {
            return new Vector3(0, 0, Mathf.Sign(offset.z));
        }
    }

    // Returns true if this cube is allowed to move one step in the given direction.
    public bool CanPushSingle(Vector3 direction)
    {
        if (isMovingPos) return false;
        if (!IsGrounded()) return false;

        Vector3 pushDir = GetGridDirection(direction);
        if (pushDir == Vector3.zero) return false;

        Vector3 snappedStart = new Vector3(Mathf.Round(transform.position.x), transform.position.y, Mathf.Round(transform.position.z));
        return IsPositionClear(snappedStart + pushDir);
    }

    // Starts moving this cube one grid step in the given direction.
    public void PushSingle(Vector3 direction)
    {
        Vector3 pushDir = GetGridDirection(direction);
        Vector3 snappedStart = new Vector3(Mathf.Round(transform.position.x), transform.position.y, Mathf.Round(transform.position.z));
        StartCoroutine(MoveTo(snappedStart + pushDir, pushDir));
    }

    private bool IsGrounded()
    {
        int mask = groundCheckMask == 0 ? ~0 : groundCheckMask;
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.6f, mask);
    }

    private Vector3 GetGridDirection(Vector3 direction)
    {
        direction.y = 0;
        direction.Normalize();

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            return direction.x > 0 ? Vector3.right : Vector3.left;
        }
        else if (Mathf.Abs(direction.z) > 0.1f)
        {
            return direction.z > 0 ? Vector3.forward : Vector3.back;
        }

        return Vector3.zero;
    }

    private bool IsPositionClear(Vector3 position)
    {
        int mask = obstructionCheckMask == 0 ? ~0 : obstructionCheckMask;
        Collider[] overlapping = Physics.OverlapBox(position, Vector3.one * 0.45f, Quaternion.identity, mask);

        foreach (Collider col in overlapping)
        {
            if (col.gameObject == gameObject || col.isTrigger) continue;

            RunodeMovement otherRunode = col.GetComponent<RunodeMovement>();
            if (otherRunode != null)
            {
                Vector3 otherPos = otherRunode.transform.position;

                if (Mathf.Abs(otherPos.x - transform.position.x) < 0.1f &&
                    Mathf.Abs(otherPos.z - transform.position.z) < 0.1f)
                {
                    continue;
                }
            }

            return false;
        }

        return true;
    }

    private System.Collections.IEnumerator MoveTo(Vector3 targetPosition, Vector3 direction)
    {
        // Instant visual wipe for the affected circuit
        if (PowerManager.Instance != null)
            PowerManager.Instance.InvalidateSubtree(transform, true);

        isMovingPos = true;
        Vector3 startPos = transform.position;

        if (direction == Vector3.right || direction == Vector3.left)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        }
        else if (direction == Vector3.forward || direction == Vector3.back)
        {
            rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotation;
        }

        targetPosition.y = startPos.y;

        float elapsed = 0f;
        float moveTime = 1f / moveSpeed;

        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / moveTime);

            Vector3 currentPos = Vector3.Lerp(startPos, targetPosition, progress);
            currentPos.y = startPos.y; // Strict vertical lock

            transform.position = currentPos;

            yield return null;
        }

        Physics.SyncTransforms();
        runodeCube.RefreshCubeAndAdjacentConnections();

        isMovingPos = false;
        PowerManager.Instance.RequestPowerFlowCheck(transform);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // If we hit something while falling/moving physically, refresh power
        if (!isMovingPos && !isRotating)
        {
            PowerManager.Instance.RequestPowerFlowCheck(transform);
        }
    }

    private void ApplyRandomRotation()
    {
        float xRot = Random.Range(0, 4) * 90f;
        float yRot = Random.Range(0, 4) * 90f;
        float zRot = Random.Range(0, 4) * 90f;

        if (visualParent != null)
        {
            visualParent.localRotation = Quaternion.Euler(xRot, yRot, zRot);
        }
    }

    // === MOVED FROM Tim scripts during merge ===

    private RunodeMovement[] allCubesCache = null;
    private float lastCubesCacheTime = 0f;
    private const float CACHE_REFRESH_INTERVAL = 1.0f;

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

    public RunodeMovement[] GetStack()
    {
        List<RunodeMovement> stack = new List<RunodeMovement> { this };
        RunodeMovement[] allCubes = GetAllCubesOptimized();
        Vector3 basePos = transform.position;
        foreach (RunodeMovement cube in allCubes)
        {
            if (cube == this) continue;
            Vector3 cubePos = cube.transform.position;
            bool isAligned = Mathf.Abs(cubePos.x - basePos.x) < 0.1f && Mathf.Abs(cubePos.z - basePos.z) < 0.1f;
            bool isAbove = cubePos.y > basePos.y;
            if (isAligned && isAbove) stack.Add(cube);
        }
        stack.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        return stack.ToArray();
    }

    public bool IsInStackRange(int maxDepth)
    {
        RunodeMovement[] stack = GetStack();
        for (int i = 0; i < Mathf.Min(maxDepth, stack.Length); i++)
        {
            if (stack[i] == this) return true;
        }
        return false;
    }

    public bool IsVisibleFrom(Transform observer, Vector3 targetPoint, LayerMask layerMask)
    {
        Vector3 cubeCenter = transform.position;
        Vector3 right = observer.right;
        Vector3 back = -observer.forward;

        Vector3[] origins = new Vector3[]
        {
            observer.position + Vector3.up * 1.7f,
            observer.position + Vector3.up * 1.0f,
            observer.position + Vector3.up * 1.0f + right * 0.35f + back * 0.3f,
            observer.position + Vector3.up * 1.0f - right * 0.35f + back * 0.3f,
            observer.position + Vector3.up * 1.35f + right * 0.35f + back * 0.2f,
            observer.position + Vector3.up * 0.75f - right * 0.35f + back * 0.2f,
        };

        int hitCount = 0;

        foreach (Vector3 origin in origins)
        {
            Vector3 dir = (cubeCenter - origin);
            float maxDist = dir.magnitude;
            Vector3 rayDir = dir.normalized;

            Vector3 currentOrigin = origin;
            float remainingDist = maxDist + 0.1f;

            for (int i = 0; i < 3; i++)
            {
                if (remainingDist <= 0f) break;

                if (!Physics.Raycast(currentOrigin, rayDir, out RaycastHit hit, remainingDist, layerMask))
                    break;

                if (hit.collider.transform.IsChildOf(observer))
                {
                    remainingDist -= hit.distance;
                    currentOrigin = hit.point + rayDir * 0.01f;
                    continue;
                }

                RunodeMovement hitCube = hit.collider.GetComponentInParent<RunodeMovement>();
                if (hitCube == this)
                {
                    hitCount++;
                    break;
                }

                if (hitCube != null && IsSameStackBelow(hitCube))
                {
                    remainingDist -= hit.distance;
                    currentOrigin = hit.point + rayDir * 0.01f;
                    continue;
                }

                break;
            }

            if (hitCount >= 2) return true;
        }

        return hitCount >= 2;
    }

    private bool IsSameStackBelow(RunodeMovement other)
    {
        Vector3 myPos = transform.position;
        Vector3 otherPos = other.transform.position;
        bool isAligned = Mathf.Abs(otherPos.x - myPos.x) < 0.1f && Mathf.Abs(otherPos.z - myPos.z) < 0.1f;
        bool isBelow = otherPos.y < myPos.y;
        return isAligned && isBelow;
    }

    public static Vector3 GetCardinalAxis(Vector3 v)
    {
        float absX = Mathf.Abs(v.x);
        float absY = Mathf.Abs(v.y);
        float absZ = Mathf.Abs(v.z);

        if (absX > absY && absX > absZ) return Vector3.right * Mathf.Sign(v.x);
        if (absY > absX && absY > absZ) return Vector3.up * Mathf.Sign(v.y);
        return Vector3.forward * Mathf.Sign(v.z);
    }

    public IEnumerator RotateVisualSmooth(float degrees, Vector3 worldAxis, float duration, float squashAmount)
    {
        if (visualParent == null) yield break;

        if (PowerManager.Instance != null)
            PowerManager.Instance.InvalidateSubtree(transform, true);

        isRotating = true;

        Transform targetTransform = visualParent;
        Quaternion startRotation = targetTransform.localRotation;
        Vector3 rotAxis = transform.InverseTransformDirection(worldAxis);

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
        runodeCube.RefreshCubeAndAdjacentConnections();

        RunodePower p = GetComponent<RunodePower>();
        if (PowerManager.Instance != null) PowerManager.Instance.RequestPowerFlowCheck(p);
    }
}