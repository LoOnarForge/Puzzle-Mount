using System.Collections;
using UnityEngine;

public class MediumDoor : MonoBehaviour
{
    private const float DoorMoveDuration = 0.7f;
    private const float DefaultDoorOpenScaleYMultiplier = 0.75f;

    [SerializeField] private Transform doorElement;
    [SerializeField] private float doorLiftDistance;
    [SerializeField] private float doorOpenScaleYMultiplier = DefaultDoorOpenScaleYMultiplier;

    private bool isDoorMoving;
    private bool isDoorOpen;
    private float doorClosedLocalY;
    private float doorClosedLocalScaleY;
    private float doorOpenLocalScaleY;
    private float doorResolvedLiftDistance;
    private float doorLocalHeight;
    private Coroutine animateCoroutine;

    private void Awake()
    {
        Debug.Assert(doorElement != null, $"{nameof(MediumDoor)} on {name} requires a door element assigned.", this);

        if (doorElement == null)
            return;

        doorClosedLocalY = doorElement.localPosition.y;
        doorClosedLocalScaleY = doorElement.localScale.y;
        doorOpenLocalScaleY = doorClosedLocalScaleY * doorOpenScaleYMultiplier;
        doorLocalHeight = ResolveDoorLocalHeight();
        doorResolvedLiftDistance = doorLiftDistance > 0f
            ? doorLiftDistance
            : doorLocalHeight;
        isDoorOpen = false;
    }

    // Toggle activation — used by levers and one-shot activators.
    public void OnLeverOperated()
    {
        ApplyOpenState(!isDoorOpen);
    }

    // State-aware activation — sets open or closed to match activator state (e.g. pressure plate held down).
    public void ApplyOpenState(bool isOpen)
    {
        if (doorElement == null)
            return;

        if (isOpen == isDoorOpen && !isDoorMoving)
            return;

        if (animateCoroutine != null)
            StopCoroutine(animateCoroutine);

        animateCoroutine = StartCoroutine(AnimateDoorToState(isOpen));
    }

    // Lifts the door and shrinks local scale Y so it fits inside the frame lintel.
    private IEnumerator AnimateDoorToState(bool opening)
    {
        isDoorMoving = true;

        float startScaleY = doorElement.localScale.y;
        float startBaseY = doorElement.localPosition.y
            - doorLocalHeight * (doorClosedLocalScaleY - startScaleY) * 0.5f;
        float endScaleY = opening ? doorOpenLocalScaleY : doorClosedLocalScaleY;
        float endBaseY = opening ? doorClosedLocalY + doorResolvedLiftDistance : doorClosedLocalY;
        float elapsed = 0f;

        while (elapsed < DoorMoveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / DoorMoveDuration);
            float scaleY = Mathf.Lerp(startScaleY, endScaleY, t);
            float baseY = Mathf.Lerp(startBaseY, endBaseY, t);
            float pivotOffset = doorLocalHeight * (doorClosedLocalScaleY - scaleY) * 0.5f;

            Vector3 localPosition = doorElement.localPosition;
            localPosition.y = baseY + pivotOffset;
            doorElement.localPosition = localPosition;

            Vector3 localScale = doorElement.localScale;
            localScale.y = scaleY;
            doorElement.localScale = localScale;
            yield return null;
        }

        float finalPivotOffset = doorLocalHeight * (doorClosedLocalScaleY - endScaleY) * 0.5f;
        Vector3 finalPosition = doorElement.localPosition;
        finalPosition.y = endBaseY + finalPivotOffset;
        doorElement.localPosition = finalPosition;

        Vector3 finalScale = doorElement.localScale;
        finalScale.y = endScaleY;
        doorElement.localScale = finalScale;

        isDoorOpen = opening;
        isDoorMoving = false;
        animateCoroutine = null;
    }

    private float ResolveDoorLocalHeight()
    {
        BoxCollider box = doorElement.GetComponent<BoxCollider>();
        if (box == null)
            box = doorElement.GetComponentInChildren<BoxCollider>();

        if (box == null)
            return 1f;

        if (box.transform == doorElement)
            return box.size.y * doorClosedLocalScaleY;

        return box.size.y * box.transform.localScale.y;
    }
}
