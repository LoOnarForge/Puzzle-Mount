using UnityEngine;

public class PowerSource : MonoBehaviour
{
    [Header("POWER SOURCE")]
    public Color powerColor = Color.red;
    public int maxPowerCapacity = 10;
    
    [Header("DEBUG")]
    public int currentPowerUsage = 0;
    
    public bool RequestPower()
    {
        if (currentPowerUsage < maxPowerCapacity)
        {
            currentPowerUsage++;
            return true;
        }
        return false;
    }
    
    public void ReleasePower()
    {
        if (currentPowerUsage > 0)
        {
            currentPowerUsage--;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("PowerLines"))
        {
            HandlePowerConnection(other, true);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("PowerLines"))
        {
            HandlePowerConnection(other, false);
        }
    }
    
    private void HandlePowerConnection(Collider powerLineCollider, bool connecting)
    {
        PowerCube cube = powerLineCollider.GetComponentInParent<PowerCube>();
        if (cube != null)
        {
            int faceIndex = cube.GetFaceIndexFromCollider(powerLineCollider);
            if (faceIndex >= 0)
            {
                if (connecting)
                {
                    cube.TryConnectToSource(this, faceIndex);
                }
                else
                {
                    cube.DisconnectFromSource(this, faceIndex);
                }
            }
        }
    }
}