using UnityEngine;

[RequireComponent(typeof(Collider))]
public class RunodeDestructionTrigger : MonoBehaviour
{
    [SerializeField] private LayerMask runodeLayerMask;

    private void Awake()
    {
        Collider triggerCollider = GetComponent<Collider>();
        if (!triggerCollider.isTrigger)
            Debug.LogWarning($"{nameof(RunodeDestructionTrigger)} on {name} requires a trigger collider.", this);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsOnAllowedLayer(other.gameObject.layer))
            return;

        RunodeBreaker breaker = other.GetComponentInParent<RunodeBreaker>();
        if (breaker != null)
            breaker.Break();
    }

    private bool IsOnAllowedLayer(int layer)
    {
        if (runodeLayerMask.value == 0)
            return true;

        return (runodeLayerMask.value & (1 << layer)) != 0;
    }
}
