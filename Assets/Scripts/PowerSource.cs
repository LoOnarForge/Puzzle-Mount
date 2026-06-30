using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PowerSource : MonoBehaviour
{
    public int colorIndex;
    [HideInInspector] public Color powerColor;

    public int maxPower = 10;
    public int currentCubesPowered = 0;

    public PowerLineType topFace = PowerLineType.Empty;
    public Transform topFaceTransform;

    public Sprite horizontalSprite;
    public Sprite verticalSprite;
    public Sprite cornerSprite;
    public Sprite tSectionSprite;
    public Sprite crossSprite;

    public PowerConnectionTrigger upTrigger;
    public PowerConnectionTrigger rightTrigger;
    public PowerConnectionTrigger downTrigger;
    public PowerConnectionTrigger leftTrigger;

    public SpriteRenderer powerSprite;

    [Header("VISUAL CRYSTALS")]
    public List<Renderer> sourceCrystals = new List<Renderer>();
    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorProperty = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock propBlock;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (topFaceTransform != null)
        {
            // The detector component has been removed as per the new spatial system.
            // PowerSource triggers are handled by the BFS which now respects obstruction flags updated by PowerManager.
        }
    }

    private void Start()
    {
        powerColor = ColorManager.Instance.GetColor(colorIndex);
        UpdateSourceVisual();
        PowerManager.Instance.RegisterSource(this);
        PowerManager.Instance.RequestPowerFlowCheck();
    }

    private void UpdateSourceVisual()
    {
        if (powerSprite != null)
            powerSprite.color = powerColor;

        if (topFaceTransform != null)
        {
            Transform spriteChild = topFaceTransform.Find("Power Line Sprite");
            if (spriteChild != null)
            {
                SpriteRenderer sr = spriteChild.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = powerColor;
            }
        }

        UpdateCrystalVisuals();
    }

    private void UpdateCrystalVisuals()
    {
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        
        Color targetColor = powerColor;
        // If not playing, we need to fetch color manually for the preview
        if (!Application.isPlaying)
        {
            ColorManager colorPalette = Object.FindAnyObjectByType<ColorManager>();
            if (colorPalette != null) targetColor = colorPalette.GetColor(colorIndex);
        }

        foreach (var r in sourceCrystals)
        {
            if (r == null) continue;

            Material targetMat = Application.isPlaying ? r.material : r.sharedMaterial;
            if (targetMat != null) targetMat.EnableKeyword("_EMISSION");

            r.GetPropertyBlock(propBlock);
            propBlock.SetColor(BaseColorProperty, targetColor);
            propBlock.SetColor(EmissionColorProperty, targetColor * 2f);
            r.SetPropertyBlock(propBlock);
        }
    }

    private void OnValidate()
    {
        UpdateCrystalVisuals();
    }

    public void RunBFS()
    {
        Queue<PowerConnectionTrigger> queue = new Queue<PowerConnectionTrigger>();
        HashSet<PowerConnectionTrigger> visitedTriggers = new HashSet<PowerConnectionTrigger>();
        HashSet<RunodePower> visitedCubes = new HashSet<RunodePower>();
        
        currentCubesPowered = 0;

        EnqueueSourceTrigger(upTrigger, queue, visitedTriggers, visitedCubes);
        EnqueueSourceTrigger(rightTrigger, queue, visitedTriggers, visitedCubes);
        EnqueueSourceTrigger(downTrigger, queue, visitedTriggers, visitedCubes);
        EnqueueSourceTrigger(leftTrigger, queue, visitedTriggers, visitedCubes);

        while (queue.Count > 0)
        {
            PowerConnectionTrigger current = queue.Dequeue();
            RunodePower currentCube = current.parentRunodePower;

            PowerConnectionTrigger neighbor = FindExternalNeighbor(current);
            if (neighbor != null && neighbor.gameObject.activeInHierarchy && !neighbor.isObstructed && !visitedTriggers.Contains(neighbor))
            {
                RunodePower neighborCube = neighbor.parentRunodePower;
                if (neighborCube == null) neighborCube = neighbor.GetComponentInParent<RunodePower>();
                
                bool isNewCube = neighborCube != null && !visitedCubes.Contains(neighborCube);

                if (isNewCube && currentCubesPowered >= maxPower)
                {
                    continue; 
                }

                if (neighbor.isPowered && neighbor.currentPowerColor != powerColor)
                {
                    TriggerGameOver();
                    return;
                }

                if (isNewCube)
                {
                    visitedCubes.Add(neighborCube);
                    currentCubesPowered++;
                }

                SetTriggerPowered(neighbor, current.distanceFromSource + 1, queue, visitedTriggers);
                if (neighborCube != null) neighborCube.MarkFacePowered(neighbor);
            }

            if (currentCube != null)
            {
                PowerConnectionTrigger internalBridge = currentCube.GetInternalNeighbor(current);
                if (internalBridge != null && internalBridge.gameObject.activeInHierarchy && !internalBridge.isObstructed && !visitedTriggers.Contains(internalBridge))
                {
                    SetTriggerPowered(internalBridge, current.distanceFromSource, queue, visitedTriggers);
                    currentCube.MarkFacePowered(internalBridge);
                }

                var faceNeighbors = currentCube.GetConnectedTriggersOnFace(current);
                foreach (var fn in faceNeighbors)
                {
                    if (fn.gameObject.activeInHierarchy && !visitedTriggers.Contains(fn))
                    {
                        SetTriggerPowered(fn, current.distanceFromSource, queue, visitedTriggers);
                    }
                }
            }
        }

        foreach (var cube in visitedCubes)
        {
            cube.RefreshFaceVisuals(powerColor);
        }
    }

    private void EnqueueSourceTrigger(PowerConnectionTrigger trigger, Queue<PowerConnectionTrigger> queue, HashSet<PowerConnectionTrigger> visitedTriggers, HashSet<RunodePower> visitedCubes)
    {
        if (trigger == null || !trigger.gameObject.activeInHierarchy) return;
        
        trigger.isPowered = true;
        trigger.currentPowerColor = powerColor;
        trigger.distanceFromSource = 0;
        trigger.sourceMW = maxPower;
        
        visitedTriggers.Add(trigger);
        queue.Enqueue(trigger);

        if (trigger.parentRunodePower != null && !visitedCubes.Contains(trigger.parentRunodePower))
        {
            visitedCubes.Add(trigger.parentRunodePower);
            currentCubesPowered++;
        }
    }

    private void SetTriggerPowered(PowerConnectionTrigger trigger, int distance, Queue<PowerConnectionTrigger> queue, HashSet<PowerConnectionTrigger> visited)
    {
        trigger.isPowered = true;
        trigger.currentPowerColor = powerColor;
        trigger.distanceFromSource = distance;
        trigger.sourceMW = maxPower;
        
        visited.Add(trigger);
        queue.Enqueue(trigger);
    }

    private PowerConnectionTrigger FindExternalNeighbor(PowerConnectionTrigger source)
    {
        SphereCollider sphere = source.GetComponent<SphereCollider>();
        Vector3 searchPos = sphere != null ? source.transform.TransformPoint(sphere.center) : source.transform.position;

        float queryRadius = 0.1f;
        Collider[] hits = Physics.OverlapSphere(searchPos, queryRadius, -1, QueryTriggerInteraction.Collide);

        foreach (var hit in hits)
        {
            PowerConnectionTrigger neighbor = hit.GetComponent<PowerConnectionTrigger>();
            if (neighbor != null && neighbor != source)
            {
                if (neighbor.gameObject.activeInHierarchy && !neighbor.isObstructed)
                {
                    RunodePower sParent = source.parentRunodePower;
                    if (sParent == null) sParent = source.GetComponentInParent<RunodePower>();
                    
                    RunodePower nParent = neighbor.parentRunodePower;
                    if (nParent == null) nParent = neighbor.GetComponentInParent<RunodePower>();

                    Transform sCube = sParent != null ? sParent.transform : source.transform.root;
                    Transform nCube = nParent != null ? nParent.transform : neighbor.transform.root;
                    float dist = Vector3.Distance(sCube.position, nCube.position);

                    if (dist > 1.1f)
                    {
                        Vector3 sNormal = source.transform.parent.forward;
                        Vector3 nNormal = neighbor.transform.parent.forward;
                        float dot = Mathf.Abs(Vector3.Dot(sNormal, nNormal));

                        if (dot > 0.9f) continue;
                    }

                    if (sParent != nParent || (sParent == null && nParent == null))
                    {
                        return neighbor;
                    }
                }
            }
        }
        return null;
    }

    private void TriggerGameOver()
    {
        Debug.Log("[PowerSource] Game Over - illegal connection detected (loop or color mixing).");
    }
}
