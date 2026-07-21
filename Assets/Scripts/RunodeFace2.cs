using System.Collections.Generic;
using UnityEngine;

public class RunodeFace2 : MonoBehaviour
{

    [Header("STATUS:")]
    public PowerLineType powerLineType = PowerLineType.Empty;
    public GameObject poweredBySource;

    public bool isFaceDirty; 
    public bool isFacePowered;
    public bool isFaceBlocked;

    [Header("PORT STATUS:")]
    public bool upBlocked;
    public bool rightBlocked;
    public bool downBlocked;
    public bool leftBlocked;

    [Header("REFERENCES:")]
    public SpriteRenderer lineRenderer;

    public List<PortTrigger> activePorts = new List<PortTrigger>();


    private void Awake()
    {
        InitializeActivePortsList();
    }
    public void SetInternalPortBlocked(PortID portName, bool blocked)
    {
        if (portName == PortID.Up) upBlocked = blocked;
        else if (portName == PortID.Right) rightBlocked = blocked;
        else if (portName == PortID.Down) downBlocked = blocked;
        else if (portName == PortID.Left) leftBlocked = blocked;
    }

    public void SetFaceState(bool blocked)
    {
        isFaceBlocked = blocked;

        if (isFaceBlocked)
        {
            if (lineRenderer != null) lineRenderer.color = new Color(0.18f, 0.18f, 0.18f); 
            isFacePowered = false;
            poweredBySource = null;
        }
        else
        {
            if (lineRenderer != null) lineRenderer.color = Color.white;
        }
    }

    public void UpdateFaceVisuals(Color newColor, GameObject source)
    {
        if (isFaceBlocked) return;

        isFacePowered = newColor != Color.white;
        poweredBySource = isFacePowered ? source : null;

        if (lineRenderer != null)
        {
            lineRenderer.color = newColor;
        }
    }


    private void InitializeActivePortsList()
    {
        activePorts.Clear();

        PortTrigger[] foundPorts = GetComponentsInChildren<PortTrigger>(false);

        foreach (PortTrigger port in foundPorts)
        {
            activePorts.Add(port);

            port.parentFace = this;
        }
    }
}
