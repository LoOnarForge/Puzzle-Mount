using UnityEngine;

public class PowerConnectionTrigger : MonoBehaviour
{
    [Header("REFERENCES")]
    public PowerCube cubeScript;
    public SpriteRenderer faceSprite;
    public Transform parentFace;
    
    [Header("POWER STATE")]
    public bool isTriggerPowerReceiver = false;
    
    public void PowerReceived(PowerSource source, Color powerColor)
    {
        isTriggerPowerReceiver = true;
        if (cubeScript != null)
        {
        //    cubeScript.FacePowered(parentFace, this, source, powerColor);
        }
    }
}