using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(-50)]
public class PowerSource : MonoBehaviour
{
    [Header("POWER SOURCE")]
    public int colorIndex;
    [HideInInspector] public Color powerColor;

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
        powerColor = ColorManager.Instance.GetColor(colorIndex);
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
            powerUsage += 1;
        }
    }

    public void RemovePowerCube(GameObject cube)
    {
        connectedCubes.Remove(cube);
        powerUsage -= 1;
    }
}