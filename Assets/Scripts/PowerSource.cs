using UnityEngine;
using System.Collections.Generic;

public class PowerSource : MonoBehaviour
{
    [Header("POWER SOURCE")]
    public Color powerColor = Color.red;

    [Header("POWER OPTIONS")]
    public int maxPower = 10;
    public int powerUsage = 0;

    [Header("CONNECTIONS")]
    public List<GameObject> connectedCubes = new List<GameObject>();
    
    [Header("TRIGGER REFERENCES")]
    public PowerConnectionTrigger upTrigger;
    public PowerConnectionTrigger rightTrigger;
    public PowerConnectionTrigger downTrigger;
    public PowerConnectionTrigger leftTrigger;
    
    [Header("SPRITE REFERENCE")]
    public SpriteRenderer powerSprite;

    private void Start()
    {
        UpdatePowerColors();
    }

    private void UpdatePowerColors()
    {
        if (powerSprite != null)
        {
            powerSprite.color = powerColor;
        }
    }

    public void AddPowerCube(GameObject cube)
    {
        if (!connectedCubes.Contains(cube))
        {
            connectedCubes.Add(cube);
        }
    }

    public void RemovePowerCube(GameObject cube)
    {
        connectedCubes.Remove(cube);
        powerUsage -= 1;
    }
}