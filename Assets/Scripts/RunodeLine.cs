using System.Collections;
using UnityEngine;

public class RunodeLine : MonoBehaviour
{
    [SerializeField] private SpriteRenderer lineSprite;

    [Space(20)]
    [SerializeField] private LinePort[] ports;
    [SerializeField] private ObstructionPort obstructionPort;
    
    [Space(20)]
    [Header("STATE:")]
    [SerializeField] private bool isPowered;
    [SerializeField] private bool isFaceBlocked;

    private float darkeningSpeed = 15f;


    public void FaceObstructed()
    {
        isFaceBlocked = true;
        StopAllCoroutines();
        StartCoroutine(DarkenSpriteLine());
        DeactivateLinePorts();
    }
    public void FaceCleared()
    {
        isFaceBlocked = false;
        StopAllCoroutines();
        StartCoroutine(BrightenSpriteLine());
        ActivateLinePorts();
    }

    private IEnumerator DarkenSpriteLine()
    {
        while (lineSprite != null && lineSprite.color != Color.black)
        {
            lineSprite.color = Color.Lerp(lineSprite.color, Color.black, Time.deltaTime * darkeningSpeed);
            yield return null;
        }

        if (lineSprite != null)
            lineSprite.color = Color.black;
    }
    private IEnumerator BrightenSpriteLine()
    {
        while (lineSprite != null && lineSprite.color != Color.white)
        {
            lineSprite.color = Color.Lerp(lineSprite.color, Color.white, Time.deltaTime * darkeningSpeed);
            yield return null;
        }

        if (lineSprite != null)
            lineSprite.color = Color.white;
    }


    private void DeactivateLinePorts()
    {
        if (ports == null) return;
        foreach (var port in ports)
        {
            if (port != null)
            {
                port.SetFaceBlocked(true);
                port.SetFaceColliderEnabled(false);
            }
        }
    }
    private void ActivateLinePorts()
    {
        if (ports == null) return;
        foreach (var port in ports)
        {
            if (port != null)
            {
                port.SetFaceBlocked(false);
                port.SetFaceColliderEnabled(true);
            }
        }
    }

    // Refreshes the face obstruction and then all active port connections in order.

    public void RefreshFaceAndPortStates()
    {
        RefreshFaceObstructionState();
        RefreshPortObstructions();
        RefreshPortConnections();
    }

    public void RefreshFaceObstructionState()
    {
        if (obstructionPort == null || !obstructionPort.isActiveAndEnabled)
            return;

        obstructionPort.RefreshObstructionState();

        if (isFaceBlocked == obstructionPort.IsObstructed)
            return;

        if (obstructionPort.IsObstructed)
            FaceObstructed();
        else
            FaceCleared();
    }
    public void RefreshPortObstructions()
    {
        if (ports == null)
            return;

        foreach (LinePort port in ports)
        {
            if (port != null && port.isActiveAndEnabled)
                port.RefreshPortObstructionState();
        }
    }
    public void RefreshPortConnections()
    {

        if (ports == null)
            return;

        foreach (LinePort port in ports)
        {
            if (port != null && port.isActiveAndEnabled)
                port.RefreshPortConnection();
        }
    }



}
