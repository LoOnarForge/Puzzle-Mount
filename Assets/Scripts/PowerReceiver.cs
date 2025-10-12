using UnityEngine;

public class PowerReceiver : MonoBehaviour
{
    [Header("POWER RECEIVER")]
    public Color requiredPowerColor = Color.red;
    
    [Header("DEBUG")]
    public bool isPowered = false;
    public Color currentPowerColor = Color.clear;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("PowerLines"))
        {
            CheckPowerConnection(other);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("PowerLines"))
        {
            CheckPowerDisconnection(other);
        }
    }
    
    private void CheckPowerConnection(Collider powerLineCollider)
    {
        PowerCube cube = powerLineCollider.GetComponentInParent<PowerCube>();
        if (cube != null)
        {
            int faceIndex = cube.GetFaceIndexFromCollider(powerLineCollider);
            if (faceIndex >= 0)
            {
                Color faceColor = cube.GetFacePowerColor(faceIndex);
                if (faceColor != Color.clear && ColorsMatch(faceColor, requiredPowerColor))
                {
                    isPowered = true;
                    currentPowerColor = faceColor;
                }
            }
        }
    }
    
    private void CheckPowerDisconnection(Collider powerLineCollider)
    {
        isPowered = false;
        currentPowerColor = Color.clear;
    }
    
    private bool ColorsMatch(Color color1, Color color2)
    {
        float tolerance = 0.1f;
        return Vector4.Distance(color1, color2) < tolerance;
    }
}