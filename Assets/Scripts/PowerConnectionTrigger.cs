using UnityEngine;

public class PowerConnectionTrigger : MonoBehaviour
{
    [Header("REFERENCES:")]
    public PowerCube cubeScript;
    public SpriteRenderer faceSprite;
    
    [Header("POWER STATE:")]
    public bool isTriggerPowerReceiver = false;
    
    private void OnTriggerEnter(Collider other)
    {
        if (cubeScript != null)
        {
          //  cubeScript.TriggerEntered(this, other);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (cubeScript != null)
        {
          //  cubeScript.TriggerExited(this, other);
        }
    }
}