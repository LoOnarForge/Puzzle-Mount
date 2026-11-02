using UnityEngine;

public class SimpleCubeDestroyer : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        RunodeMovement runode = other.GetComponentInParent<RunodeMovement>();
        if (runode != null)
            Destroy(runode.gameObject);
    }
}
