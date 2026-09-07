using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ButtonDevice : MonoBehaviour
{
    private const float ButtonPressDuration = 0.25f;
    private const float GridAlignmentTolerance = 0.1f;
    private const float MinDescentSpeed = 0.1f;
    private const float MaxHorizontalApproachSpeed = 0.5f;
    private const int OverlapBufferSize = 8;

    [SerializeField] private Transform buttonCap;
    [SerializeField] private float buttonPressDepth = 0.05f;
    [SerializeField] private Collider pressTrigger;
    [SerializeField] private List<GameObject> connectedDevices = new List<GameObject>();

    private readonly List<MediumDoor> mediumDoors = new List<MediumDoor>();
    private readonly List<Elevator> elevators = new List<Elevator>();
    private readonly List<PistonHorizontal> pistonHorizontals = new List<PistonHorizontal>();
    private readonly List<PistonVertical> pistonVerticals = new List<PistonVertical>();
    private readonly Collider[] overlapBuffer = new Collider[OverlapBufferSize];

    private bool isButtonMoving;
    private bool isPressed;
    private Vector3 buttonCapRestLocalPosition;

    private void Awake()
    {
        BuildConnectedDeviceLists();

        if (buttonCap != null)
            buttonCapRestLocalPosition = buttonCap.localPosition;

        if (pressTrigger != null)
            Debug.Assert(pressTrigger.isTrigger, $"{nameof(ButtonDevice)} on {name} requires {nameof(pressTrigger)} to be a trigger.", this);
    }

    private void FixedUpdate()
    {
        if (isPressed || pressTrigger == null)
            return;

        Bounds bounds = pressTrigger.bounds;
        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            overlapBuffer,
            pressTrigger.transform.rotation,
            Physics.AllLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null || hit == pressTrigger)
                continue;

            RunodeMovement cube = hit.GetComponentInParent<RunodeMovement>();
            if (cube == null || !CanCubePressButton(cube))
                continue;

            TryPress();
            return;
        }
    }

    // Called when the player clicks this button within range.
    public void MouseClickDetected()
    {
        TryPress();
    }

    // Runs one press if this button has not already been used.
    private void TryPress()
    {
        if (isPressed || isButtonMoving)
            return;

        isPressed = true;

        if (pressTrigger != null)
            pressTrigger.enabled = false;

        StartCoroutine(ButtonPressMovement());
    }

    // Returns true when a cube is falling onto the button from above.
    private bool CanCubePressButton(RunodeMovement cube)
    {
        if (cube.IsBusy)
            return false;

        if (!IsCubeAlignedOverButton(cube.transform.position))
            return false;

        Rigidbody cubeRigidbody = cube.GetComponent<Rigidbody>();
        if (cubeRigidbody == null)
            return false;

        Vector3 velocity = cubeRigidbody.linearVelocity;
        if (velocity.y > -MinDescentSpeed)
            return false;

        float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        if (horizontalSpeed > MaxHorizontalApproachSpeed && velocity.y > -MinDescentSpeed * 2f)
            return false;

        return true;
    }

    // Returns true when the cube is centered over this button and above its base.
    private bool IsCubeAlignedOverButton(Vector3 cubePosition)
    {
        Vector3 buttonPosition = transform.position;

        if (Mathf.Abs(cubePosition.x - buttonPosition.x) > GridAlignmentTolerance)
            return false;

        if (Mathf.Abs(cubePosition.z - buttonPosition.z) > GridAlignmentTolerance)
            return false;

        return cubePosition.y > buttonPosition.y;
    }

    // Lerps the button cap inward once along local Y.
    private IEnumerator ButtonPressMovement()
    {
        isButtonMoving = true;

        if (buttonCap == null)
        {
            isButtonMoving = false;
            NotifyConnectedDevices();
            yield break;
        }

        Vector3 startPosition = buttonCapRestLocalPosition;
        Vector3 endPosition = buttonCapRestLocalPosition + Vector3.down * buttonPressDepth;
        float elapsed = 0f;

        while (elapsed < ButtonPressDuration)
        {
            elapsed += Time.deltaTime;
            buttonCap.localPosition = Vector3.Lerp(startPosition, endPosition, Mathf.Clamp01(elapsed / ButtonPressDuration));
            yield return null;
        }

        buttonCap.localPosition = endPosition;
        isButtonMoving = false;
        NotifyConnectedDevices();
    }

    // Calls OnLeverOperated on every cached connected device.
    private void NotifyConnectedDevices()
    {
        foreach (MediumDoor mediumDoor in mediumDoors)
            mediumDoor.OnLeverOperated();

        foreach (Elevator elevator in elevators)
            elevator.OnLeverOperated();

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
                elevators.Add(elevator);

            PistonHorizontal pistonHorizontal = deviceObject.GetComponent<PistonHorizontal>();
            if (pistonHorizontal != null)
                pistonHorizontals.Add(pistonHorizontal);

            PistonVertical pistonVertical = deviceObject.GetComponent<PistonVertical>();
            if (pistonVertical != null)
                pistonVerticals.Add(pistonVertical);
        }
    }
}
