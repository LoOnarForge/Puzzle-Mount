using UnityEngine;
using System.Collections.Generic;

public class PowerSource : MonoBehaviour
{
    [Header("POWER SOURCE:")]
    public Color powerColor = Color.red;

    [Header("POWER OPTIONS:")]
    public int maxPower = 10;
    public int powerUsage = 0;

    [Header("CONNECTIONS:")]
    public List<Collider> TriggersList = new List<Collider>();
    public List<GameObject> connectedCubes = new List<GameObject>();



    private void Start()
    {
        CheckForTriggers();
        UpdatePowerColors();
        CheckForPowerConnections();
    }

    private void CheckForTriggers()
    {
        TriggersList.Clear();

        Collider[] childColliders = GetComponentsInChildren<Collider>();

        foreach (Collider collider in childColliders)
        {
            if (collider.isTrigger && collider is SphereCollider)
            {
                TriggersList.Add(collider);
            }
        }
    }

    private void CheckForPowerConnections()
    {
        foreach (Collider trigger in TriggersList)
        {
            Collider[] overlapping = Physics.OverlapSphere(trigger.transform.position, trigger.bounds.size.x / 2);
            
            foreach (Collider overlappingCollider in overlapping)
            {
                PowerCube cube = overlappingCollider.GetComponentInParent<PowerCube>();
                if (cube != null)
                {
                    AddPowerCube(cube.gameObject);
                }
            }
        }
    }

    private void UpdatePowerColors()
    {
        Transform triggersParent = transform.Find("Triggers");
        if (triggersParent != null)
        {
            SpriteRenderer spriteRenderer = triggersParent.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = powerColor;
            }
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
    }

    private void OnTriggerEnter(Collider other)
    {
        PowerCube cube = other.GetComponentInParent<PowerCube>();
        if (cube != null)
        {
            AddPowerCube(cube.gameObject);
            
            // Get the exact sprite that needs coloring
            SpriteRenderer sprite = other.transform.parent.GetComponent<SpriteRenderer>();
            
            // Get the exact face transform to identify which inspector fields to update
            Transform faceTransform = other.transform.parent.parent;
            
            // Tell cube exactly which sprite to color and which fields to update
            cube.PoweredFromSource(this, sprite, faceTransform, powerColor);
        }
    }


    public void CubeDisconnected(GameObject cubeGameObject)
    {
        RemovePowerCube(cubeGameObject);
        powerUsage -= 1;
    }
}