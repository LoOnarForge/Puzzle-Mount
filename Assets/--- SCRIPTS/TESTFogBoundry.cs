using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TESTFogBoundry : MonoBehaviour
{
    [SerializeField] private GameObject targetToDisable;

    private void Awake()
    {
        Collider c = GetComponent<Collider>();
        if (!c.isTrigger)
            Debug.LogWarning($"{nameof(TESTFogBoundry)} on {name} requires a trigger collider.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<CharacterMovement>() == null)
            return;

        if (targetToDisable != null)
            targetToDisable.SetActive(false);
    }
}
