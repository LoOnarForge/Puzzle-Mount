using UnityEngine;

public class PowerSourcePort : MonoBehaviour
{
    [Header("POWER SOURCE PORT:")]
    [SerializeField] private PowerSource parentPowerSource;

    private PortType type = PortType.Giver;
    private int powerIndex = 0;

    private void OnTriggerEnter(Collider other)
    {
        LinePort linePort = other.GetComponent<LinePort>();
        if (linePort == null)
            return;

        RunodeLine line = linePort.GetComponentInParent<RunodeLine>();
        parentPowerSource.PowerRunodeLine(line, linePort);
    }
}
