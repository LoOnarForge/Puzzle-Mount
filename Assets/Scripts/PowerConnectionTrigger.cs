using UnityEngine;

public class PowerConnectionTrigger : MonoBehaviour
{
    [Header("REFERENCES")]
    public PowerCube cubeScript;
    public SpriteRenderer faceSprite;
    public Transform parentFace;
    public PowerSource parentPowerSource;
    
    [Header("POWER STATE")]
    public bool isTriggerPowerSource = false;
    public bool isTriggerPowerReceiver = false;
    
    private Color currentPowerColor;
    
    private void Awake()
    {
        if (isTriggerPowerSource && parentPowerSource != null)
        {
            currentPowerColor = parentPowerSource.powerColor;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        PowerConnectionTrigger otherTrigger = other.GetComponent<PowerConnectionTrigger>();
        if (otherTrigger == null) return;
        
        if (isTriggerPowerSource)
        {
            otherTrigger.PowerReceived(parentPowerSource, currentPowerColor);
        }
    }
    
    public void PowerReceived(PowerSource source, Color powerColor)
    {
        isTriggerPowerReceiver = true;
        currentPowerColor = powerColor;
        if (cubeScript != null)
        {
        //    cubeScript.FacePowered(parentFace, this, source, powerColor);
        }
    }
}