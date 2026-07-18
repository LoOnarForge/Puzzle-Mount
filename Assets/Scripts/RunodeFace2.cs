using System.Collections.Generic;
using UnityEngine;

public class RunodeFace2 : MonoBehaviour
{

    [Header("DEBUG STATUS:")]
    public PowerLineType powerLineType = PowerLineType.Empty;
    public GameObject poweredBySource;
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
        InitializeActivePorts();
    }
    public void SetInternalPortBlocked(PortID portName, bool blocked)
    {
        if (portName == PortID.Up) upBlocked = blocked;
        else if (portName == PortID.Right) rightBlocked = blocked;
        else if (portName == PortID.Down) downBlocked = blocked;
        else if (portName == PortID.Left) leftBlocked = blocked;

        RefreshState();
    }

    public void SetFaceBlocked(bool blocked)
    {
        isFaceBlocked = blocked;

        if (isFaceBlocked)
        {
            if (lineRenderer != null) lineRenderer.color = new Color(0.18f, 0.18f, 0.18f); // Obstructed Dark Gray
            isFacePowered = false;
            poweredBySource = null;
        }
        else
        {
            if (lineRenderer != null) lineRenderer.color = Color.white;
        }

        RefreshState();
    }

    public void RefreshState() 
    { 
        // Snapshot logic for Overlord will go here
    }

    public void UpdateVisuals(Color newColor, GameObject source)
    {
        if (isFaceBlocked) return;

        isFacePowered = newColor != Color.white;
        poweredBySource = isFacePowered ? source : null;

        if (lineRenderer != null)
        {
            lineRenderer.color = newColor;
        }
    }


    private void InitializeActivePorts()
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
