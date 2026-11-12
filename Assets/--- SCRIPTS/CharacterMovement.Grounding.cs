using UnityEngine;

public partial class CharacterMovement
{
    private const float CoyoteTimeSeconds = 0.12f;
    private const float WalkOffMaxUpwardVelocity = 0.1f;
    private const float GroundProbeRadiusScale = 0.9f;
    private const float GroundPenetrationCheckOffset = 0.05f;

    [Header("GROUND PROBE")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundProbeDistance = 0.35f;

    private bool isJumping;
    private float coyoteTimeRemaining;
    private float airborneDeathTimer;
    private bool airborneDeathPending;

    private void InitGroundProbe()
    {
        if (groundMask.value != 0)
            return;

        groundMask = Physics.DefaultRaycastLayers;
    }

    private void RefreshGroundedState()
    {
        bool groundedContact = EvaluateGroundContact();

        if (groundedContact)
            coyoteTimeRemaining = CoyoteTimeSeconds;
        else
            coyoteTimeRemaining = Mathf.Max(0f, coyoteTimeRemaining - Time.deltaTime);

        if (groundedContact && !wasGroundedLastFrame)
        {
            jumpMomentum = Vector3.zero;
            jumpFacingDirection = Vector3.zero;

            if (playerAnimator != null)
                playerAnimator.SetJumpingState(false);
        }

        if (isUsingMomentum
            && !groundedContact
            && wasGroundedLastFrame
            && velocity.y <= WalkOffMaxUpwardVelocity)
        {
            Vector3 groundDir = Vector3.zero;
            if (cameraFollow != null)
                groundDir = cameraFollow.GetMovementDirectionForCameraAngle(moveInput);
            else
                groundDir = new Vector3(moveInput.x, 0, moveInput.y);

            if (groundDir.magnitude > 0.1f)
                jumpMomentum = groundDir.normalized * currentSpeedBuildup;
        }

        if (isUsingMomentum && !groundedContact && jumpMomentum.magnitude > 0.01f)
            jumpMomentum = Vector3.Lerp(jumpMomentum, Vector3.zero, momentumDecay * Time.deltaTime);

        isGrounded = groundedContact;
    }

    private bool EvaluateGroundContact()
    {
        bool sphereHit = ProbeSphereGround(out _);
        bool controllerHit = controller.isGrounded;

        if (isJumping)
        {
            if (velocity.y > 0f)
                return false;

            bool landingContact = sphereHit || controllerHit;
            if (landingContact)
                isJumping = false;

            return landingContact;
        }

        bool transportDeck = MovingPlatform.IsTimPhysicallyOnTransportDeck(this);
        return sphereHit || controllerHit || transportDeck;
    }

    private bool ProbeSphereGround(out RaycastHit hit)
    {
        hit = default;

        if (controller == null)
            return false;

        float radius = controller.radius * GroundProbeRadiusScale;
        float halfHeight = controller.height * 0.5f;
        Vector3 worldCenter = transform.TransformPoint(controller.center);
        Vector3 sphereOrigin = worldCenter + Vector3.up * (halfHeight - radius);
        float castDistance = groundProbeDistance + controller.skinWidth;

        if (Physics.SphereCast(
                sphereOrigin,
                radius,
                Vector3.down,
                out hit,
                castDistance,
                groundMask,
                QueryTriggerInteraction.Ignore)
            && !IsTimCollider(hit.collider))
        {
            return true;
        }

        Vector3 embeddedOrigin = sphereOrigin + Vector3.down * GroundPenetrationCheckOffset;
        Collider[] overlaps = Physics.OverlapSphere(embeddedOrigin, radius, groundMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (overlaps[i] != null && !IsTimCollider(overlaps[i]))
                return true;
        }

        return false;
    }

    private bool IsTimCollider(Collider collider)
    {
        return collider != null && collider.transform.IsChildOf(transform);
    }

    private bool CanJumpNow()
    {
        return isGrounded || coyoteTimeRemaining > 0f;
    }

    private void BeginJump()
    {
        isJumping = true;
        coyoteTimeRemaining = 0f;
    }

    private void TickAirborneDeathTimer()
    {
        if (maxAirborneTimeBeforeDeath <= 0f)
            return;

        if (isGrounded)
        {
            if (airborneDeathPending)
            {
                ApplyDeathToss(Vector3.down);
                return;
            }

            airborneDeathTimer = 0f;
            return;
        }

        airborneDeathTimer += Time.deltaTime;

        if (airborneDeathTimer >= maxAirborneTimeBeforeDeath)
            airborneDeathPending = true;
    }
}
