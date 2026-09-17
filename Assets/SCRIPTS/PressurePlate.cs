using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    private const float PlateSwitchDuration = 0.25f;
    private const float PressDetectionDelay = 0.2f;
    private const int OverlapBufferSize = 16;
    private const int MaxSupportedPistonMaxStage = 1;
    private const int MaxSupportedElevatorStops = 2;
    private const string RunodesLayerName = "Runodes";
    private const string TimLayerName = "TimJones";

    [SerializeField] private Transform plateTop;
    [SerializeField] private float platePressDepth = 0.05f;
    [SerializeField] private Collider weightTrigger;
    [SerializeField] private List<GameObject> connectedDevices = new List<GameObject>();

    private readonly List<MediumDoor> mediumDoors = new List<MediumDoor>();
    private readonly List<Elevator> elevators = new List<Elevator>();
    private readonly List<Piston> pistons = new List<Piston>();
    private readonly List<PistonHorizontal> pistonHorizontals = new List<PistonHorizontal>();
    private readonly List<PistonVertical> pistonVerticals = new List<PistonVertical>();
    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];
    private readonly HashSet<Collider> countedWeightColliders = new HashSet<Collider>();

    private int weightLayerMask;
    private int runodesLayer;
    private int timLayer;
    private bool isPlateMoving;
    private bool isPressed;
    private Coroutine pendingPressCoroutine;

    private Vector3 plateTopRestLocalPosition;

    private void Awake()
    {
        weightLayerMask = LayerMask.GetMask(RunodesLayerName, TimLayerName);
        runodesLayer = LayerMask.NameToLayer(RunodesLayerName);
        timLayer = LayerMask.NameToLayer(TimLayerName);
        BuildConnectedDeviceLists();

        if (plateTop != null)
            plateTopRestLocalPosition = plateTop.localPosition;

        if (weightTrigger != null)
            Debug.Assert(weightTrigger.isTrigger, $"{nameof(PressurePlate)} on {name} requires {nameof(weightTrigger)} to be a trigger.", this);
    }

    private void FixedUpdate()
    {
        if (isPlateMoving || weightTrigger == null)
            return;

        bool hasWeight = HasWeightOnPlate();

        if (hasWeight && !isPressed && pendingPressCoroutine == null)
        {
            pendingPressCoroutine = StartCoroutine(DelayedPress());
        }
        else if (!hasWeight && pendingPressCoroutine != null)
        {
            StopCoroutine(pendingPressCoroutine);
            pendingPressCoroutine = null;
        }
        else if (!hasWeight && isPressed)
        {
            isPressed = false;
            StartCoroutine(PlateSwitchingMovement(false));
            NotifyConnectedDevices(false);
        }
    }

    // Waits briefly, then presses only if weight is still on the plate.
    private IEnumerator DelayedPress()
    {
        yield return new WaitForSeconds(PressDetectionDelay);

        pendingPressCoroutine = null;

        if (isPlateMoving || isPressed || weightTrigger == null)
            yield break;

        if (!HasWeightOnPlate())
            yield break;

        isPressed = true;
        StartCoroutine(PlateSwitchingMovement(true));
        NotifyConnectedDevices(true);
    }

    // Lerps the plate top local Y between rest and pressed depth.
    private IEnumerator PlateSwitchingMovement(bool pressingDown)
    {
        isPlateMoving = true;

        if (plateTop == null)
        {
            isPlateMoving = false;
            yield break;
        }

        Vector3 startPosition = pressingDown ? plateTopRestLocalPosition : plateTopRestLocalPosition + Vector3.down * platePressDepth;
        Vector3 endPosition = pressingDown ? plateTopRestLocalPosition + Vector3.down * platePressDepth : plateTopRestLocalPosition;
        float elapsed = 0f;

        while (elapsed < PlateSwitchDuration)
        {
            elapsed += Time.deltaTime;
            plateTop.localPosition = Vector3.Lerp(startPosition, endPosition, Mathf.Clamp01(elapsed / PlateSwitchDuration));
            yield return null;
        }

        plateTop.localPosition = endPosition;
        isPlateMoving = false;
    }

    // Returns true when a Runode or Tim collider overlaps the weight trigger volume.
    private bool HasWeightOnPlate()
    {
        Bounds triggerBounds = weightTrigger.bounds;
        countedWeightColliders.Clear();

        int hitCount = Physics.OverlapBoxNonAlloc(
            triggerBounds.center,
            triggerBounds.extents,
            overlapBuffer,
            weightTrigger.transform.rotation,
            weightLayerMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null || hit == weightTrigger || !countedWeightColliders.Add(hit))
                continue;

            if (IsValidWeightCollider(hit))
                return true;
        }

        return HasTimCharacterControllerInsideTrigger(triggerBounds);
    }

    // Returns true for any collider on the Runodes or TimJones layer.
    private bool IsValidWeightCollider(Collider hit)
    {
        int layer = hit.gameObject.layer;
        if (layer != runodesLayer && layer != timLayer)
            return false;

        CharacterMovement tim = hit.GetComponentInParent<CharacterMovement>();
        if (tim != null && tim.IsDead)
            return false;

        return true;
    }

    // CharacterController is not returned by OverlapBox, so check Tim bounds directly.
    private static bool HasTimCharacterControllerInsideTrigger(Bounds triggerBounds)
    {
        CharacterMovement tim = Object.FindAnyObjectByType<CharacterMovement>();
        if (tim == null || tim.IsDead)
            return false;

        CharacterController controller = tim.GetComponent<CharacterController>();
        if (controller == null)
            return false;

        return triggerBounds.Intersects(controller.bounds);
    }

    // State-aware doors follow plate pressure; other devices toggle on each notify.
    private void NotifyConnectedDevices(bool pressed)
    {
        foreach (MediumDoor mediumDoor in mediumDoors)
            mediumDoor.ApplyOpenState(pressed);

        foreach (Elevator elevator in elevators)
            elevator.OnLeverOperated();

        foreach (Piston piston in pistons)
            piston.OnLeverOperated();

        foreach (PistonHorizontal pistonHorizontal in pistonHorizontals)
            pistonHorizontal.OnLeverOperated();

        foreach (PistonVertical pistonVertical in pistonVerticals)
            pistonVertical.OnLeverOperated();
    }

    // Builds typed device lists once from the inspector object list.
    private void BuildConnectedDeviceLists()
    {
        mediumDoors.Clear();
        elevators.Clear();
        pistons.Clear();
        pistonHorizontals.Clear();
        pistonVerticals.Clear();

        foreach (GameObject deviceObject in connectedDevices)
        {
            if (deviceObject == null)
                continue;

            MediumDoor mediumDoor = deviceObject.GetComponent<MediumDoor>();
            if (mediumDoor != null)
                mediumDoors.Add(mediumDoor);

            Elevator elevator = deviceObject.GetComponent<Elevator>();
            if (elevator != null)
            {
                if (elevator.StageCount > MaxSupportedElevatorStops)
                {
                    Debug.Log($"[{nameof(PressurePlate)}] '{name}' ignored connected elevator '{deviceObject.name}' because it has {elevator.StageCount} stops (maximum is {MaxSupportedElevatorStops}).", this);
                    continue;
                }

                elevators.Add(elevator);
            }

            Piston piston = deviceObject.GetComponent<Piston>();
            if (piston != null)
            {
                if (piston.MaxStage > MaxSupportedPistonMaxStage)
                {
                    Debug.Log($"[{nameof(PressurePlate)}] '{name}' ignored connected piston '{deviceObject.name}' because maxStage is {piston.MaxStage} (maximum is {MaxSupportedPistonMaxStage}).", this);
                    continue;
                }

                pistons.Add(piston);
            }

            PistonHorizontal pistonHorizontal = deviceObject.GetComponent<PistonHorizontal>();
            if (pistonHorizontal != null)
            {
                if (pistonHorizontal.MaxStage > MaxSupportedPistonMaxStage)
                {
                    Debug.Log($"[{nameof(PressurePlate)}] '{name}' ignored connected piston '{deviceObject.name}' because maxStage is {pistonHorizontal.MaxStage} (maximum is {MaxSupportedPistonMaxStage}).", this);
                    continue;
                }

                pistonHorizontals.Add(pistonHorizontal);
            }

            PistonVertical pistonVertical = deviceObject.GetComponent<PistonVertical>();
            if (pistonVertical != null)
            {
                if (pistonVertical.MaxStage > MaxSupportedPistonMaxStage)
                {
                    Debug.Log($"[{nameof(PressurePlate)}] '{name}' ignored connected piston '{deviceObject.name}' because maxStage is {pistonVertical.MaxStage} (maximum is {MaxSupportedPistonMaxStage}).", this);
                    continue;
                }

                pistonVerticals.Add(pistonVertical); 
            }
        }
    }
}
