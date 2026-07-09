using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PowerSource : MonoBehaviour
{
    public int colorIndex;
    [HideInInspector] public Color powerColor;

    public int maxPower = 10;
    public int currentCubesPowered = 0;
    public List<RunodePower> poweredRunodes = new List<RunodePower>();

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

    private struct BFSNode
    {
        public PowerConnectionTrigger trigger;
        public RunodePower.FaceData sourceFace;

        public BFSNode(PowerConnectionTrigger t, RunodePower.FaceData f)
        {
            trigger = t;
            sourceFace = f;
        }
    }

    public void RunBFS()
    {
        Queue<BFSNode> queue = new Queue<BFSNode>();
        HashSet<PowerConnectionTrigger> visitedTriggers = new HashSet<PowerConnectionTrigger>();
        HashSet<RunodePower> visitedCubes = new HashSet<RunodePower>();
        
        currentCubesPowered = 0;
        poweredRunodes.Clear();

        EnqueueSourceTrigger(upTrigger, queue, visitedTriggers, visitedCubes);
        EnqueueSourceTrigger(rightTrigger, queue, visitedTriggers, visitedCubes);
        EnqueueSourceTrigger(downTrigger, queue, visitedTriggers, visitedCubes);
        EnqueueSourceTrigger(leftTrigger, queue, visitedTriggers, visitedCubes);

        while (queue.Count > 0)
        {
            BFSNode node = queue.Dequeue();
            PowerConnectionTrigger current = node.trigger;
            RunodePower currentCube = current.parentRunodePower;

            // Get the face data for the CURRENT trigger to pass it forward as the 'sourceFace'
            RunodePower.FaceData currentFace = null;
            if (currentCube != null) currentFace = currentCube.GetFaceData(current);

            PowerConnectionTrigger neighbor = FindExternalNeighbor(current);
            if (neighbor != null && neighbor.gameObject.activeInHierarchy && !neighbor.isObstructed)
            {
                RunodePower neighborCube = neighbor.parentRunodePower;
                if (neighborCube == null) neighborCube = neighbor.GetComponentInParent<RunodePower>();
                
                if (neighborCube != null)
                {
                    string sender = currentCube != null ? currentCube.name : name;
                    // Check face power BEFORE skipping via visitedTriggers to catch loops. Pass the sourceFace to ignore back-links.
                    if (!neighborCube.MarkFacePowered(neighbor, powerColor, sender, node.sourceFace, this)) return;
                }

                if (!visitedTriggers.Contains(neighbor))
                {
                    bool isNewCube = neighborCube != null && !visitedCubes.Contains(neighborCube);

                    if (isNewCube && currentCubesPowered >= maxPower)
                    {
                        continue; 
                    }

                    if (isNewCube)
                    {
                        visitedCubes.Add(neighborCube);
                        poweredRunodes.Add(neighborCube);
                        currentCubesPowered++;
                    }

                    SetTriggerPowered(neighbor, current.distanceFromSource + 1, queue, visitedTriggers, currentFace);
                }
            }

            if (currentCube != null)
            {
                PowerConnectionTrigger internalBridge = currentCube.GetInternalNeighbor(current);
                if (internalBridge != null && internalBridge.gameObject.activeInHierarchy && !internalBridge.isObstructed)
                {
                    // Check face power for corner wraps
                    if (!currentCube.MarkFacePowered(internalBridge, powerColor, currentCube.name, node.sourceFace, this)) return;

                    if (!visitedTriggers.Contains(internalBridge))
                    {
                        SetTriggerPowered(internalBridge, current.distanceFromSource, queue, visitedTriggers, currentFace);
                    }
                }

                var faceNeighbors = currentCube.GetConnectedTriggersOnFace(current);
                foreach (var fn in faceNeighbors)
                {
                    if (fn.gameObject.activeInHierarchy && !visitedTriggers.Contains(fn))
                    {
                        // Internal propagation on the same face: keep the same sourceFace reference
                        SetTriggerPowered(fn, current.distanceFromSource, queue, visitedTriggers, node.sourceFace);
                    }
                }
            }
        }

        foreach (var cube in visitedCubes)
        {
            cube.RefreshFaceVisuals();
        }
    }

    private void EnqueueSourceTrigger(PowerConnectionTrigger trigger, Queue<BFSNode> queue, HashSet<PowerConnectionTrigger> visitedTriggers, HashSet<RunodePower> visitedCubes)
    {
        if (trigger == null || !trigger.gameObject.activeInHierarchy) return;
        
        trigger.isPowered = true;
        trigger.currentPowerColor = powerColor;
        trigger.distanceFromSource = 0;
        trigger.sourceMW = maxPower;
        
        visitedTriggers.Add(trigger);
        queue.Enqueue(new BFSNode(trigger, null));

        if (trigger.parentRunodePower != null)
        {
            // Initial power from source to cube face
            trigger.parentRunodePower.MarkFacePowered(trigger, powerColor, name, null, this);

            if (!visitedCubes.Contains(trigger.parentRunodePower))
            {
                visitedCubes.Add(trigger.parentRunodePower);
                poweredRunodes.Add(trigger.parentRunodePower);
                currentCubesPowered++;
            }
        }
    }

    private void SetTriggerPowered(PowerConnectionTrigger trigger, int distance, Queue<BFSNode> queue, HashSet<PowerConnectionTrigger> visited, RunodePower.FaceData sourceFace)
    {
        trigger.isPowered = true;
        trigger.currentPowerColor = powerColor;
        trigger.distanceFromSource = distance;
        trigger.sourceMW = maxPower;
        
        visited.Add(trigger);
        queue.Enqueue(new BFSNode(trigger, sourceFace));
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
        Debug.Log("GAME OVER");
        Debug.Log($"[PowerSource] {name} detected illegal connection (loop or collision).");
    }
}
