using UnityEngine;

/// <summary>
/// Universal child component for powered devices.
/// Acts as a physical sensor that validates incoming power against its requirements.
/// </summary>
public class PowerSocket : MonoBehaviour
{
    [Header("STATE")]
    public bool isActive = false;
    public int requiredColorIndex;
    public int requiredMW;

    [Header("REAL-TIME DATA")]
    public bool isSatisfied = false;
    public Color incomingColor = Color.clear;
    public int availableMW = 0;

    /// <summary>
    /// Configures the socket from the parent device.
    /// </summary>
    public void Initialize(bool active, int colorIndex, int mw)
    {
        isActive = active;
        requiredColorIndex = colorIndex;
        requiredMW = mw;
        
        // Disable the grandparent (Power Socket 0X) if it exists, otherwise just this object
        if (transform.parent != null && transform.parent.parent != null)
        {
            transform.parent.parent.gameObject.SetActive(active);
        }
        else
        {
            gameObject.SetActive(active);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (!isActive) return;

        PowerConnectionTrigger trigger = other.GetComponent<PowerConnectionTrigger>();
        if (trigger != null && trigger.isPowered)
        {
            ProcessIncomingPower(trigger.currentPowerColor, trigger.sourceMW, trigger.distanceFromSource);
        }
        else
        {
            ClearPower();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!isActive) return;

        PowerConnectionTrigger trigger = other.GetComponent<PowerConnectionTrigger>();
        if (trigger != null)
        {
            ClearPower();
        }
    }

    private void ProcessIncomingPower(Color color, int sourceMW, int distance)
    {
        bool wasSatisfied = isSatisfied;
        incomingColor = color;
        availableMW = sourceMW - distance;

        Color targetReqColor = ColorManager.Instance.GetColor(requiredColorIndex);
        bool colorMatches = ColorsMatch(incomingColor, targetReqColor);
        bool powerSufficient = availableMW >= requiredMW;

        isSatisfied = colorMatches && powerSufficient;

        if (isSatisfied && !wasSatisfied)
        {
            LogPowerState(true);
        }
        else if (!isSatisfied && incomingColor != Color.clear)
        {
            LogPowerState(false);
        }
    }

    private void LogPowerState(bool satisfied)
    {
        string colorName = "Unknown";
        if (ColorManager.Instance != null)
        {
            string[] names = ColorManager.Instance.GetColorNames();
            if (requiredColorIndex >= 0 && requiredColorIndex < names.Length)
                colorName = names[requiredColorIndex];
        }

        ForgottenGate parentGate = GetComponentInParent<ForgottenGate>();
        int totalActive = 0;
        int currentSatisfied = 0;
        if (parentGate != null)
        {
            PowerSocket[] all = parentGate.GetComponentsInChildren<PowerSocket>(true);
            foreach (var s in all)
            {
                if (s.isActive)
                {
                    totalActive++;
                    if (s.isSatisfied) currentSatisfied++;
                }
            }
        }

        if (satisfied)
        {
            Debug.Log($"{gameObject.name} powered with the required {colorName} color. {availableMW}/{requiredMW}MW supplied. {currentSatisfied} out of {totalActive} power conditions met.");
        }
        else
        {
            Debug.Log($"{gameObject.name} not powered with the required color. {availableMW}/{requiredMW}MW supplied. {currentSatisfied} out of {totalActive} power conditions met.");
        }
    }

    private void ClearPower()
    {
        if (isSatisfied || incomingColor != Color.clear)
        {
            isSatisfied = false;
            incomingColor = Color.clear;
            availableMW = 0;
            LogPowerState(false);
        }
        else
        {
            isSatisfied = false;
            incomingColor = Color.clear;
            availableMW = 0;
        }
    }

    private bool ColorsMatch(Color a, Color b)
    {
        const float tolerance = 0.05f;
        return Mathf.Abs(a.r - b.r) < tolerance &&
               Mathf.Abs(a.g - b.g) < tolerance &&
               Mathf.Abs(a.b - b.b) < tolerance;
    }
}
