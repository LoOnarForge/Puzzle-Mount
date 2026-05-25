using System.Collections.Generic;
using UnityEngine;


[DefaultExecutionOrder(-40)]
public class PowerManager : MonoBehaviour
{
    public static PowerManager Instance { get; private set; }

    [SerializeField] private List<PowerSource> powerSources = new List<PowerSource>();
    private bool recalculationRequested = false;

    private void Awake()
    {
        Instance = this;
    }

    // Called by PowerSources on Start to register themselves.
    public void RegisterSource(PowerSource source)
    {
        if (!powerSources.Contains(source))
            powerSources.Add(source);
    }

    // Called by PowerConnectionTrigger on enter/exit, or by cubes on move/rotate.
    public void RequestPowerFlowCheck()
    {
        recalculationRequested = true;
    }

    private void LateUpdate()
    {
        if (!recalculationRequested) return;

        recalculationRequested = false;
        RecalculateAllSources();
    }

    private void RecalculateAllSources()
    {
        // Clear all cube power states globally before any source runs BFS.
        ClearAllCubeStates();

        // Each source independently runs BFS on its own connected graph.
        foreach (PowerSource source in powerSources)
        {
            source.RunBFS();
        }
    }

    private void ClearAllCubeStates()
    {
        RunodePower[] allRunodes = FindObjectsByType<RunodePower>(FindObjectsInactive.Exclude);
        foreach (RunodePower runode in allRunodes)
        {
            runode.ClearPowerState();
        }
    }
}
