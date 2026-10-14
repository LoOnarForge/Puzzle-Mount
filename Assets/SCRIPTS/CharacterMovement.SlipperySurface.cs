using UnityEngine;

public partial class CharacterMovement
{
    private const float SlipperyBaseCoastMeters = 1f;
    private const float SlipperyInputDeadzone = 0.1f;
    private const float SlipperyDiagonalAxisMin = 0.4f;
    private const float SlipperyDiagonalGraceSeconds = 0.15f;
    private const float SlipperyCoastStartGraceSeconds = 0.15f;
    private const float SlipperyZoneProbeLift = 0.1f;
    private const float SlipperyZoneProbeRadius = 0.35f;
    private const float SlipperyZoneFeetVerticalTolerance = 0.15f;
    private const float SlipperyDirectionEpsilon = 0.0001f;
    private const float SlipperyNeverDiagonalTime = -999f;

    private static readonly Collider[] SlipperyZoneOverlapHits = new Collider[8];

    private SlipperyCoastSample pendingCoastSample;
    private SlipperyCoastMotion activeCoast;

    private struct SlipperyCoastSample
    {
        public Vector3 Direction;
        public float Speed;
        public bool WasRunning;
        public float LastDiagonalTime;
        public float LastSampleTime;
    }

    private struct SlipperyCoastMotion
    {
        public bool IsActive;
        public Vector3 Direction;
        public float InitialSpeed;
        public float TotalDistance;
        public float Elapsed;
        public float DistanceTraveled;
    }

    private void CaptureSlipperyCoastWhileMoving(bool shouldRun, Vector3 moveDirection, float groundMoveSpeed)
    {
        if (!CanSampleSlipperyCoast())
            return;

        UpdatePendingCoastSample(shouldRun, moveDirection, groundMoveSpeed);
    }

    private void HandleSlipperySurfaceMovement()
    {
        if (controller == null || !controller.enabled)
        {
            ClearSlipperyCoast();
            return;
        }

        if (!isGrounded)
        {
            if (activeCoast.IsActive)
                TickActiveCoast();
            else
                ClearSlipperyCoast();
            return;
        }

        bool onSlippery = IsOverSlipperyZone();

        if (HasMoveInput())
        {
            activeCoast.IsActive = false;
            if (!onSlippery)
                ClearPendingCoastSample();
            return;
        }

        if (!activeCoast.IsActive)
        {
            if (!TryStartCoastFromSample())
            {
                if (!onSlippery)
                    ClearSlipperyCoast();
                return;
            }
        }

        TickActiveCoast();
    }

    private bool CanSampleSlipperyCoast()
    {
        return HasMoveInput() && isGrounded && controller != null && IsOverSlipperyZone();
    }

    private bool HasMoveInput()
    {
        return moveInput.magnitude >= SlipperyInputDeadzone;
    }

    private void UpdatePendingCoastSample(bool shouldRun, Vector3 moveDirection, float groundMoveSpeed)
    {
        if (!TryGetHorizontalMoveDirection(moveDirection, out Vector3 normalizedDirection, out float directionLength))
            return;

        bool isDiagonal = IsDiagonalDirection(normalizedDirection);

        // Ignore a lone-axis frame right after diagonal input (staggered key release).
        if (!isDiagonal &&
            Time.time - pendingCoastSample.LastDiagonalTime < SlipperyDiagonalGraceSeconds)
        {
            return;
        }

        float targetSpeed = (shouldRun ? runSpeed : walkSpeed) * moveInput.magnitude;
        if (isDiagonal)
            targetSpeed *= directionLength;

        if (!isDiagonal)
            normalizedDirection = SnapToCardinalDirection(normalizedDirection);

        pendingCoastSample.Direction = normalizedDirection;
        pendingCoastSample.Speed = Mathf.Max(groundMoveSpeed * directionLength, targetSpeed);
        pendingCoastSample.WasRunning = shouldRun;
        pendingCoastSample.LastSampleTime = Time.time;

        if (isDiagonal)
            pendingCoastSample.LastDiagonalTime = Time.time;
    }

    private bool TryStartCoastFromSample()
    {
        if (pendingCoastSample.Direction.sqrMagnitude <= SlipperyDirectionEpsilon)
            return false;

        bool onSlippery = IsOverSlipperyZone();
        if (!onSlippery &&
            Time.time - pendingCoastSample.LastSampleTime > SlipperyCoastStartGraceSeconds)
        {
            return false;
        }

        float fallbackSpeed = pendingCoastSample.WasRunning ? runSpeed : walkSpeed;
        float initialSpeed = pendingCoastSample.Speed > SlipperyInputDeadzone
            ? pendingCoastSample.Speed
            : fallbackSpeed;

        activeCoast.IsActive = true;
        activeCoast.Direction = pendingCoastSample.Direction;
        activeCoast.InitialSpeed = initialSpeed;
        activeCoast.TotalDistance = ComputeCoastDistance(initialSpeed);
        activeCoast.Elapsed = 0f;
        activeCoast.DistanceTraveled = 0f;
        return true;
    }

    private float ComputeCoastDistance(float initialSpeed)
    {
        float speedRatio = walkSpeed > SlipperyDirectionEpsilon ? initialSpeed / walkSpeed : 1f;
        float distance = SlipperyBaseCoastMeters * speedRatio;
        float maxDistance = SlipperyBaseCoastMeters * (runSpeed / walkSpeed);
        return Mathf.Clamp(distance, SlipperyBaseCoastMeters, maxDistance);
    }

    private void TickActiveCoast()
    {
        float deceleration = (activeCoast.InitialSpeed * activeCoast.InitialSpeed) / (2f * activeCoast.TotalDistance);
        activeCoast.Elapsed += Time.deltaTime;

        float distance = activeCoast.InitialSpeed * activeCoast.Elapsed -
                         0.5f * deceleration * activeCoast.Elapsed * activeCoast.Elapsed;
        distance = Mathf.Clamp(distance, activeCoast.DistanceTraveled, activeCoast.TotalDistance);

        float step = distance - activeCoast.DistanceTraveled;
        if (step > 0f)
            controller.Move(activeCoast.Direction * step);

        activeCoast.DistanceTraveled = distance;

        if (activeCoast.DistanceTraveled >= activeCoast.TotalDistance)
            ClearSlipperyCoast();
    }

    private bool IsOverSlipperyZone()
    {
        Bounds timBounds = controller.bounds;
        Vector3 probe = new Vector3(timBounds.center.x, timBounds.min.y + SlipperyZoneProbeLift, timBounds.center.z);

        int hitCount = Physics.OverlapSphereNonAlloc(
            probe,
            SlipperyZoneProbeRadius,
            SlipperyZoneOverlapHits,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        float feetY = probe.y;

        for (int i = 0; i < hitCount; i++)
        {
            Collider zoneCollider = SlipperyZoneOverlapHits[i];
            if (zoneCollider.GetComponentInParent<SlipperySurfaceZone>() == null)
                continue;

            Bounds zoneBounds = zoneCollider.bounds;
            if (feetY < zoneBounds.min.y - SlipperyZoneFeetVerticalTolerance ||
                feetY > zoneBounds.max.y + SlipperyZoneFeetVerticalTolerance)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryGetHorizontalMoveDirection(Vector3 moveDirection, out Vector3 normalized, out float length)
    {
        Vector3 horizontal = moveDirection;
        horizontal.y = 0f;
        length = horizontal.magnitude;

        if (length <= SlipperyDirectionEpsilon)
        {
            normalized = Vector3.zero;
            return false;
        }

        normalized = horizontal / length;
        return true;
    }

    private static bool IsDiagonalDirection(Vector3 normalizedDirection)
    {
        float absX = Mathf.Abs(normalizedDirection.x);
        float absZ = Mathf.Abs(normalizedDirection.z);
        return absX >= SlipperyDiagonalAxisMin && absZ >= SlipperyDiagonalAxisMin;
    }

    private static Vector3 SnapToCardinalDirection(Vector3 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
            return new Vector3(Mathf.Sign(direction.x), 0f, 0f);

        if (Mathf.Abs(direction.z) > SlipperyDirectionEpsilon)
            return new Vector3(0f, 0f, Mathf.Sign(direction.z));

        return direction;
    }

    private void ClearPendingCoastSample()
    {
        pendingCoastSample = default;
        pendingCoastSample.LastDiagonalTime = SlipperyNeverDiagonalTime;
    }

    private void ClearSlipperyCoast()
    {
        activeCoast = default;
        ClearPendingCoastSample();
    }
}
