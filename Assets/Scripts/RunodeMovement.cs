using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BoxCollider))]
public class RunodeMovement : MonoBehaviour
{
    [Header("MOVEMENT")]
    public float moveSpeed = 5f;

    [Header("VISUAL")]
    public Transform visualParent;

    [Header("RANDOM ROTATION")]
    public bool randomRotateOnStart = true;

    [Header("DEBUG")]
    public bool isMoving = false;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = true;
        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
    }

    private void Start()
    {
        if (randomRotateOnStart)
            ApplyRandomRotation();

        SnapToGrid();
    }

    private void ApplyRandomRotation()
    {
        float xRot = Random.Range(0, 4) * 90f;
        float yRot = Random.Range(0, 4) * 90f;
        float zRot = Random.Range(0, 4) * 90f;
        transform.rotation = Quaternion.Euler(xRot, yRot, zRot);
    }

    private void SnapToGrid()
    {
        Vector3 p = transform.position;
        transform.position = new Vector3(Mathf.Round(p.x), p.y, Mathf.Round(p.z));
    }

    /// Rotates the visual mesh only — colliders and triggers stay in place.
    public void RotateVisual(float rotationY)
    {
        if (visualParent != null)
            visualParent.Rotate(0, rotationY, 0);
    }

    /// Returns which direction to push based on where Tim is standing relative to this cube.
    public Vector3 GetRelativePushDirection(Transform pusherTransform)
    {
        Vector3 offset = transform.position - pusherTransform.position;
        if (Mathf.Abs(offset.x) > Mathf.Abs(offset.z))
            return new Vector3(Mathf.Sign(offset.x), 0, 0);
        else
            return new Vector3(0, 0, Mathf.Sign(offset.z));
    }

    /// Returns true if this cube is allowed to move one step in the given direction.
    public bool CanPushSingle(Vector3 direction)
    {
        if (isMoving) return false;
        if (!IsGrounded()) return false;

        Vector3 pushDir = GetGridDirection(direction);
        if (pushDir == Vector3.zero) return false;

        return IsPositionClear(transform.position + pushDir);
    }

    /// Starts moving this cube one grid step in the given direction.
    public void PushSingle(Vector3 direction)
    {
        Vector3 pushDir = GetGridDirection(direction);
        StartCoroutine(MoveTo(transform.position + pushDir, pushDir));
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.6f);
    }

    private Vector3 GetGridDirection(Vector3 direction)
    {
        direction.y = 0;
        direction.Normalize();

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            return direction.x > 0 ? Vector3.right : Vector3.left;
        else if (Mathf.Abs(direction.z) > 0.1f)
            return direction.z > 0 ? Vector3.forward : Vector3.back;

        return Vector3.zero;
    }

    private bool IsPositionClear(Vector3 position)
    {
        Collider[] overlapping = Physics.OverlapBox(position, Vector3.one * 0.45f);

        foreach (Collider col in overlapping)
        {
            if (col.gameObject == gameObject || col.isTrigger) continue;

            RunodeMovement otherRunode = col.GetComponent<RunodeMovement>();
            if (otherRunode != null)
            {
                Vector3 otherPos = otherRunode.transform.position;
                bool isSameColumn = Mathf.Abs(otherPos.x - transform.position.x) < 0.1f &&
                                    Mathf.Abs(otherPos.z - transform.position.z) < 0.1f;
                if (isSameColumn) continue;
            }

            return false;
        }

        return true;
    }

    private System.Collections.IEnumerator MoveTo(Vector3 targetPosition, Vector3 direction)
    {
        isMoving = true;
        Vector3 startPos = transform.position;

        if (direction == Vector3.right || direction == Vector3.left)
            rb.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
        else if (direction == Vector3.forward || direction == Vector3.back)
            rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezeRotation;

        targetPosition.y = startPos.y;

        float elapsed = 0f;
        float moveTime = 1f / moveSpeed;

        while (elapsed < moveTime)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / moveTime);
            Vector3 currentPos = Vector3.Lerp(startPos, targetPosition, progress);
            currentPos.y = transform.position.y;
            transform.position = currentPos;
            yield return null;
        }

        Vector3 finalPos = targetPosition;
        finalPos.y = transform.position.y;
        transform.position = finalPos;

        rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;

        isMoving = false;
        PowerManager.Instance.RequestPowerFlowCheck();
    }
}
