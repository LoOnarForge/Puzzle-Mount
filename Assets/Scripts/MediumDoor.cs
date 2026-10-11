using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class MediumDoor : MonoBehaviour
{
    private const int DoorPieceCount = 5;
    private const float MovementEaseInOutPower = 2.5f;
    private const float SkullTransitionLeadTime = 0.5f;
    private const float MinPieceTravelDuration = 0.05f;
    private const float ClosedPositionTolerance = 0.02f;

    [Serializable]
    private struct DoorPieceMotion
    {
        [SerializeField] private Transform piece;
        [SerializeField] private Transform openDestination;
        [SerializeField] [Range(0f, 1f)] private float startDelay;
        [SerializeField] [Range(0.05f, 5f)] private float travelDuration;

        public Transform Piece => piece;
        public Transform OpenDestination => openDestination;
        public float StartDelay => startDelay;
        public float TravelDuration => travelDuration;
    }

    [SerializeField] private DoorPieceMotion[] doorPieces;
    [SerializeField] private SkullBehaviour[] skullBehaviours;
    [FormerlySerializedAs("closeObstructionMask")]
    [SerializeField] private LayerMask doorObstructionMask;
    [SerializeField] private Vector3 closeObstructionBoxCenter = new Vector3(0f, 0.537f, 0f);
    [SerializeField] private Vector3 closeObstructionBoxSize = new Vector3(1.04f, 2f, 0.46f);

    private const int ObstructionOverlapBufferSize = 16;

    private Vector3[] pieceClosedWorldPositions;
    private Quaternion[] pieceClosedWorldRotations;
    private Coroutine[] pieceAnimateCoroutines;

    private bool isDoorMoving;
    private bool isDoorOpen;
    private bool doorMotionTargetOpen;
    private Coroutine doorSequenceCoroutine;
    private Coroutine doorSkullSequenceCoroutine;
    private Coroutine closeObstructionMonitorCoroutine;
    private readonly Collider[] obstructionOverlapBuffer = new Collider[ObstructionOverlapBufferSize];
    private int timJonesLayer;

    private void Awake()
    {
        timJonesLayer = LayerMask.NameToLayer("TimJones");
        EnsureDoorPieceSlots();

        pieceClosedWorldPositions = new Vector3[DoorPieceCount];
        pieceClosedWorldRotations = new Quaternion[DoorPieceCount];
        pieceAnimateCoroutines = new Coroutine[DoorPieceCount];

        CacheClosedPositionsFromPieces();
        isDoorOpen = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureDoorPieceSlots();
    }
#endif

    // Keeps five inspector slots; copies existing entries when the array was shorter.
    private void EnsureDoorPieceSlots()
    {
        if (doorPieces != null && doorPieces.Length == DoorPieceCount)
            return;

        DoorPieceMotion[] resized = new DoorPieceMotion[DoorPieceCount];
        if (doorPieces != null)
        {
            int copyCount = Mathf.Min(doorPieces.Length, DoorPieceCount);
            for (int i = 0; i < copyCount; i++)
                resized[i] = doorPieces[i];
        }

        doorPieces = resized;
    }

    private void CacheClosedPositionsFromPieces()
    {
        for (int i = 0; i < DoorPieceCount; i++)
        {
            Transform piece = doorPieces[i].Piece;
            if (piece == null)
                continue;

            pieceClosedWorldPositions[i] = piece.position;
            pieceClosedWorldRotations[i] = piece.rotation;
        }
    }

    private void Start()
    {
        if (isDoorOpen || skullBehaviours == null)
            return;

        foreach (SkullBehaviour skullBehaviour in skullBehaviours)
        {
            if (skullBehaviour == null)
                continue;

            StartCoroutine(skullBehaviour.TransitionDefaultToClosed());
        }
    }

    // Toggle activation — used by levers and one-shot activators.
    public void OnLeverOperated()
    {
        ApplyOpenState(!isDoorOpen);
    }

    // Reverses settled state or in-progress motion — used by pressure plates and buttons.
    public void ApplyActivatorReverse()
    {
        bool effectivelyOpen = isDoorMoving ? doorMotionTargetOpen : isDoorOpen || IsVisuallyAwayFromClosed();
        ApplyOpenState(!effectivelyOpen);
    }

    // Drives toward explicit open or closed (also used internally after reverse).
    public void ApplyOpenState(bool isOpen)
    {
        if (!isDoorMoving && isOpen == isDoorOpen)
        {
            if (!(!isOpen && IsVisuallyAwayFromClosed()))
                return;
        }

        if (isDoorMoving && isOpen == doorMotionTargetOpen)
            return;

        if (!isOpen && IsClosePathObstructed())
            return;

        if (isDoorMoving)
            StopDoorMotion();

        doorMotionTargetOpen = isOpen;
        doorSequenceCoroutine = StartCoroutine(AnimateDoorToState(isOpen, runSkullTransition: true));
    }

    private void StopDoorMotion()
    {
        if (doorSequenceCoroutine != null)
        {
            StopCoroutine(doorSequenceCoroutine);
            doorSequenceCoroutine = null;
        }

        if (doorSkullSequenceCoroutine != null)
        {
            StopCoroutine(doorSkullSequenceCoroutine);
            doorSkullSequenceCoroutine = null;
        }

        if (closeObstructionMonitorCoroutine != null)
        {
            StopCoroutine(closeObstructionMonitorCoroutine);
            closeObstructionMonitorCoroutine = null;
        }

        for (int i = 0; i < DoorPieceCount; i++)
        {
            if (pieceAnimateCoroutines[i] == null)
                continue;

            StopCoroutine(pieceAnimateCoroutines[i]);
            pieceAnimateCoroutines[i] = null;
        }

        isDoorMoving = false;
    }

    private IEnumerator AnimateDoorToState(bool opening, bool runSkullTransition)
    {
        isDoorMoving = true;

        float piecePhaseDuration = GetLongestPieceAnimationDuration();

        if (!opening)
            closeObstructionMonitorCoroutine = StartCoroutine(MonitorCloseObstructionDuringMotion());
        else if (runSkullTransition)
            doorSkullSequenceCoroutine = StartCoroutine(RunSkullTransitionsWithLead(true, piecePhaseDuration));

        for (int i = 0; i < DoorPieceCount; i++)
            pieceAnimateCoroutines[i] = StartCoroutine(AnimatePieceToState(i, opening));

        for (int i = 0; i < DoorPieceCount; i++)
        {
            yield return pieceAnimateCoroutines[i];
            pieceAnimateCoroutines[i] = null;
        }

        if (!opening)
            yield return StartCoroutine(TransitionAllSkulls(false));
        else if (runSkullTransition)
        {
            yield return doorSkullSequenceCoroutine;
            doorSkullSequenceCoroutine = null;
        }

        if (closeObstructionMonitorCoroutine != null)
        {
            StopCoroutine(closeObstructionMonitorCoroutine);
            closeObstructionMonitorCoroutine = null;
        }

        isDoorOpen = opening;
        isDoorMoving = false;
        doorSequenceCoroutine = null;
    }

    private IEnumerator MonitorCloseObstructionDuringMotion()
    {
        while (isDoorMoving && !doorMotionTargetOpen)
        {
            if (IsClosePathObstructed())
            {
                StopDoorMotion();
                isDoorOpen = true;
                doorMotionTargetOpen = true;
                doorSequenceCoroutine = StartCoroutine(AnimateDoorToState(true, runSkullTransition: false));
                yield break;
            }

            yield return null;
        }
    }

    private bool IsClosePathObstructed()
    {
        if (doorObstructionMask.value == 0)
            return false;

        Physics.SyncTransforms();

        Vector3 center = transform.TransformPoint(closeObstructionBoxCenter);
        Vector3 halfExtents = Vector3.Scale(closeObstructionBoxSize, transform.lossyScale) * 0.5f;

        int hitCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            obstructionOverlapBuffer,
            transform.rotation,
            doorObstructionMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            if (IsCloseObstructionHit(obstructionOverlapBuffer[i]))
                return true;
        }

        return false;
    }

    private bool IsCloseObstructionHit(Collider hit)
    {
        if (hit == null)
            return false;

        if (hit.transform.IsChildOf(transform))
            return false;

        if (!hit.enabled)
            return false;

        if (timJonesLayer >= 0 && hit.gameObject.layer == timJonesLayer)
            return false;

        return true;
    }

    private IEnumerator AnimatePieceToState(int pieceIndex, bool opening)
    {
        DoorPieceMotion motion = doorPieces[pieceIndex];
        Transform piece = motion.Piece;
        Transform openDestination = motion.OpenDestination;
        float delay = motion.StartDelay;
        float travelDuration = motion.TravelDuration;

        if (piece == null)
        {
            Debug.LogError($"{nameof(MediumDoor)} on {name} has no piece transform at index {pieceIndex}.", this);
            yield break;
        }

        if (opening && openDestination == null)
        {
            Debug.LogError($"{nameof(MediumDoor)} on {name} has no open destination at index {pieceIndex}.", this);
            yield break;
        }

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        Vector3 startPosition = piece.position;
        Quaternion startRotation = piece.rotation;

        Vector3 endPosition = opening
            ? openDestination.position
            : pieceClosedWorldPositions[pieceIndex];
        Quaternion endRotation = opening
            ? openDestination.rotation
            : pieceClosedWorldRotations[pieceIndex];

        if (travelDuration <= 0f)
            travelDuration = MinPieceTravelDuration;

        float elapsed = 0f;
        while (elapsed < travelDuration)
        {
            elapsed += Time.deltaTime;
            float pathT = SampleMovementPathT(Mathf.Clamp(elapsed, 0f, travelDuration), travelDuration);
            piece.position = Vector3.Lerp(startPosition, endPosition, pathT);
            piece.rotation = Quaternion.Slerp(startRotation, endRotation, pathT);
            yield return null;
        }

        piece.position = endPosition;
        piece.rotation = endRotation;
    }

    private float GetLongestPieceAnimationDuration()
    {
        float longest = 0f;

        for (int i = 0; i < DoorPieceCount; i++)
        {
            DoorPieceMotion motion = doorPieces[i];
            float travelDuration = motion.TravelDuration;
            if (travelDuration <= 0f)
                travelDuration = MinPieceTravelDuration;

            float total = motion.StartDelay + travelDuration;
            if (total > longest)
                longest = total;
        }

        return longest;
    }

    private IEnumerator RunSkullTransitionsWithLead(bool opening, float piecePhaseDuration)
    {
        float wait = Mathf.Max(0f, piecePhaseDuration - SkullTransitionLeadTime);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        yield return StartCoroutine(TransitionAllSkulls(opening));
    }

    private IEnumerator TransitionAllSkulls(bool opening)
    {
        if (skullBehaviours == null || skullBehaviours.Length == 0)
            yield break;

        Coroutine[] skullTransitionCoroutines = new Coroutine[skullBehaviours.Length];

        for (int i = 0; i < skullBehaviours.Length; i++)
        {
            SkullBehaviour skullBehaviour = skullBehaviours[i];
            if (skullBehaviour == null)
                continue;

            skullTransitionCoroutines[i] = StartCoroutine(opening
                ? skullBehaviour.TransitionClosedToOpen()
                : skullBehaviour.TransitionDefaultToClosed());
        }

        for (int i = 0; i < skullTransitionCoroutines.Length; i++)
        {
            if (skullTransitionCoroutines[i] == null)
                continue;

            yield return skullTransitionCoroutines[i];
        }
    }

    private static float SampleMovementPathT(float elapsed, float travelDuration)
    {
        float t = travelDuration > 0f ? elapsed / travelDuration : 1f;
        t = Mathf.Clamp01(t);

        if (t < 0.5f)
            return 0.5f * Mathf.Pow(2f * t, MovementEaseInOutPower);

        return 1f - 0.5f * Mathf.Pow(2f * (1f - t), MovementEaseInOutPower);
    }

    private bool IsVisuallyAwayFromClosed()
    {
        float toleranceSqr = ClosedPositionTolerance * ClosedPositionTolerance;

        for (int i = 0; i < DoorPieceCount; i++)
        {
            Transform piece = doorPieces[i].Piece;
            if (piece == null)
                continue;

            if (Vector3.SqrMagnitude(piece.position - pieceClosedWorldPositions[i]) > toleranceSqr)
                return true;
        }

        return false;
    }
}
