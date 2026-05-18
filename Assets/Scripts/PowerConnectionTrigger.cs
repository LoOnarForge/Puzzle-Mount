using UnityEngine;

public class PowerConnectionTrigger : MonoBehaviour
{
    [Header("REFERENCES")]
    public PowerCube cubeScript;
    public SpriteRenderer faceSprite;
    public Transform parentFace;
    public PowerSource parentPowerSource;
    
    [Header("POWER STATE")]
    public bool isPowered = false;
    public bool isTriggerPowerReceiver = false;
    
    private Color currentPowerColor;
    
    private void Awake()
    {
        if (parentPowerSource != null)
        {
            isPowered = true;
        }
    }

    private void Start()
    {
        if (parentPowerSource != null)
        {
            currentPowerColor = parentPowerSource.powerColor;
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        PowerConnectionTrigger otherTrigger = other.GetComponent<PowerConnectionTrigger>();
        if (otherTrigger == null) return;
        
        if (isPowered && isTriggerPowerReceiver && !otherTrigger.isPowered)
        {
            if (cubeScript != null)
            {
                cubeScript.UnpowerFace(parentFace);
            }
            return;
        }
        
        if (isPowered && !otherTrigger.isPowered && parentPowerSource != null)
        {
            if (parentPowerSource.powerUsage < parentPowerSource.maxPower)
            {
                otherTrigger.PowerReceived(parentPowerSource, currentPowerColor);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!isTriggerPowerReceiver) return;
        
        PowerConnectionTrigger otherTrigger = other.GetComponent<PowerConnectionTrigger>();
        if (otherTrigger == null) return;
        
        if (cubeScript != null)
        {
            cubeScript.UnpowerFace(parentFace);
        }
    }
    
    public void PowerReceived(PowerSource source, Color powerColor)
    {
        isPowered = true;
        isTriggerPowerReceiver = true;
        currentPowerColor = powerColor;
        parentPowerSource = source;
        if (cubeScript != null)
        {
            cubeScript.FacePowered(parentFace, this, source, powerColor);
        }
    }
    
    public void SecondaryPowerReceived(PowerSource source, Color powerColor)
    {
        isPowered = true;
        isTriggerPowerReceiver = false;
        currentPowerColor = powerColor;
        parentPowerSource = source;
    }
}