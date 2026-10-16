using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PressurePlate : MonoBehaviour
{
    private const float PlateSwitchDuration = 0.25f;
    private const float PressDetectionDelay = 0.2f;
    private const int OverlapBufferSize = 16;
    private const string RunodesLayerName = "Runodes";
    private const string TimLayerName = "TimJones";
    private const string DevicesLayerName = "Devices";

    [SerializeField] private Transform plateTop;
    [SerializeField] private float platePressDepth = 0.05f;
    [SerializeField] private Collider weightTrigger;
    [SerializeField] private SkullBehaviour skullBehaviour;
    [SerializeField] private List<GameObject> connectedDevices = new List<GameObject>();

    private readonly List<MediumDoor> mediumDoors = new List<MediumDoor>();
    private readonly List<TrapDoor> trapDoors = new List<TrapDoor>();
    private readonly List<Bridge> bridges = new List<Bridge>();
    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];
    private readonly HashSet<Collider> countedWeightColliders = new HashSet<Collider>();

    private int weightLayerMask;
    private int runodesLayer;
    private int timLayer;
    private int devicesLayer;
    private bool isPlateMoving;
    private bool isPressed;
    private Coroutine pendingPressCoroutine;
    private Coroutine skullTransitionCoroutine;

    private Vector3 plateTopRestLocalPosition;

    private void Awake()
    {
        weightLayerMask = LayerMask.GetMask(RunodesLayerName, TimLayerName, DevicesLayerName);
        runodesLayer = LayerMask.NameToLayer(RunodesLayerName);
        timLayer = LayerMask.NameToLayer(TimLayerName);
        devicesLayer = LayerMask.NameToLayer(DevicesLayerName);
        BuildConnectedDeviceLists();

        if (plateTop != null)
            plateTopRestLocalPosition = plateTop.localPosition;

        if (weightTrigger != null)
            Debug.Assert(weightTrigger.isTrigger, $"{nameof(PressurePlate)} on {name} requires {nameof(weightTrigger)} to be a trigger.", this);

        if (skullBehaviour == null)
            skullBehaviour = GetComponentInChildren<SkullBehaviour>(true);
    }

    private void FixedUpdate()
    {
        if (weightTrigger == null)
            return;

        bool hasWeight = HasWeightOnPlate();

        if (!hasWeight && isPressed)
        {
            isPressed = false;
            StartCoroutine(PlateSwitchingMovement(false));
            NotifyConnectedDevices(false);
        }

        if (isPlateMoving)
            return;

        if (hasWeight && !isPressed && pendingPressCoroutine == null)
        {
            pendingPressCoroutine = StartCoroutine(DelayedPress());
        }
        else if (!hasWeight && pendingPressCoroutine != null)
        {
            StopCoroutine(pendingPressCoroutine);
            pendingPressCoroutine = null;
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

    // Returns true for any collider on the Runodes, TimJones, or Devices layer.
    private bool IsValidWeightCollider(Collider hit)
    {
        int layer = hit.gameObject.layer;
        if (layer != runodesLayer && layer != timLayer && layer != devicesLayer)
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

    // Each press and release reverses connected doors and bridges (including mid-animation).
    private void NotifyConnectedDevices(bool pressed)
    {
        ApplySkullPressedState(pressed);

        foreach (MediumDoor mediumDoor in mediumDoors)
            mediumDoor.ApplyActivatorReverse();

        foreach (TrapDoor trapDoor in trapDoors)
            trapDoor.ApplyActivatorReverse();

        foreach (Bridge bridge in bridges)
            bridge.ApplyOpenState(pressed);
    }

    // Matches MediumDoor skull timing: pressed = closed to open, released = default to closed.
    private void ApplySkullPressedState(bool pressed)
    {
        if (skullBehaviour == null)
            return;

        if (skullTransitionCoroutine != null)
        {
            StopCoroutine(skullTransitionCoroutine);
            skullTransitionCoroutine = null;
        }

        skullTransitionCoroutine = StartCoroutine(pressed
            ? skullBehaviour.TransitionClosedToOpen()
            : skullBehaviour.TransitionDefaultToClosed());
    }

    // Builds typed device lists once from the inspector object list.
    private void BuildConnectedDeviceLists()
    {
        mediumDoors.Clear();
        trapDoors.Clear();
        bridges.Clear();

        foreach (GameObject deviceObject in connectedDevices)
        {
            if (deviceObject == null)
                continue;

            MediumDoor mediumDoor = deviceObject.GetComponent<MediumDoor>();
            if (mediumDoor != null)
            {
                mediumDoors.Add(mediumDoor);
                continue;
            }

            TrapDoor trapDoor = deviceObject.GetComponent<TrapDoor>();
            if (trapDoor != null)
            {
                trapDoors.Add(trapDoor);
                continue;
            }

            Bridge bridge = deviceObject.GetComponent<Bridge>();
            if (bridge != null)
            {
                bridges.Add(bridge);
                continue;
            }

            Debug.Log($"[{nameof(PressurePlate)}] '{name}' ignored connected object '{deviceObject.name}' — only {nameof(MediumDoor)}, {nameof(TrapDoor)}, and {nameof(Bridge)} are supported.", this);
        }
    }
}
