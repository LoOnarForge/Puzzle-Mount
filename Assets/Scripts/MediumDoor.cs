using System.Collections;
using UnityEngine;

public class MediumDoor : MonoBehaviour
{
    private const float DoorAngleClosed = 0f;
    private const float DoorAngleOpen = 90f;
    private const float DoorMoveDuration = 0.35f;

    [SerializeField] private Transform doorElement;

    private bool isDoorMoving;
    private bool isDoorOpen;
    private float doorElementLocalX;
    private float doorElementLocalZ;

    private void Awake()
    {
        Debug.Assert(doorElement != null, $"{nameof(MediumDoor)} on {name} requires a door element assigned.", this);

        if (doorElement == null)
            return;

        Vector3 localEuler = doorElement.localEulerAngles;
        doorElementLocalX = localEuler.x;
        doorElementLocalZ = localEuler.z;
        doorElement.localRotation = Quaternion.Euler(doorElementLocalX, DoorAngleClosed, doorElementLocalZ);
        isDoorOpen = false;
    }

    // Called by a powered Lever when the player operates it.
    public void OnLeverOperated()
    {
        if (isDoorMoving || doorElement == null)
            return;

        StartCoroutine(AnimateDoorElement());
    }

    // Lerps the door element local Y between closed (0) and open (90).
    private IEnumerator AnimateDoorElement()
    {
        isDoorMoving = true;

        float startAngle = isDoorOpen ? DoorAngleOpen : DoorAngleClosed;
        float endAngle = isDoorOpen ? DoorAngleClosed : DoorAngleOpen;
        float elapsed = 0f;

        while (elapsed < DoorMoveDuration)
        {
            elapsed += Time.deltaTime;
            float angleY = Mathf.Lerp(startAngle, endAngle, Mathf.Clamp01(elapsed / DoorMoveDuration));
            doorElement.localRotation = Quaternion.Euler(doorElementLocalX, angleY, doorElementLocalZ);
            yield return null;
        }

        doorElement.localRotation = Quaternion.Euler(doorElementLocalX, endAngle, doorElementLocalZ);
        isDoorOpen = !isDoorOpen;
        isDoorMoving = false;
    }
}
