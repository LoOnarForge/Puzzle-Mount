using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PowerSource : MonoBehaviour
{
    public int colorIndex;
    [HideInInspector] public Color powerColor;

    public int maxPower = 10;
    public int currentFacesPowered = 0;
    public List<RunodeFace> poweredFaces = new List<RunodeFace>();

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
        public RunodeFace sourceFace;

        public BFSNode(PowerConnectionTrigger t, RunodeFace f)
        {
            trigger = t;
            sourceFace = f;
        }
    }

    public void RunBFS()
    {
        // Snapshot old state for capacity theft cleanup
        List<RunodeFace> previouslyPowered = new List<RunodeFace>(poweredFaces);
        
        Queue<BFSNode> queue = new Queue<BFSNode>();
        HashSet<PowerConnectionTrigger> visitedTriggers = new HashSet<PowerConnectionTrigger>();
        HashSet<RunodeFace> visitedFaces = new HashSet<RunodeFace>();
        
        // Note: tracking is now handled globally by PowerManager.

        EnqueueSourceTrigger(upTrigger, queue, visitedTriggers, visitedFaces);
        EnqueueSourceTrigger(rightTrigger, queue, visitedTriggers, visitedFaces);
        EnqueueSourceTrigger(downTrigger, queue, visitedTriggers, visitedFaces);
        EnqueueSourceTrigger(leftTrigger, queue, visitedTriggers, visitedFaces);

        while (queue.Count > 0)
        {
            BFSNode node = queue.Dequeue();
            PowerConnectionTrigger current = node.trigger;
            RunodeFace currentFace = current.parentRunodeFace;

            // PRIORITY: Discover all internal connections on the SAME cube first
            if (currentFace != null)
            {
                // Internal bridges (corner wraps)
                PowerConnectionTrigger internalBridge = currentFace.GetInternalNeighbor(current);
                if (internalBridge != null && internalBridge.gameObject.activeInHierarchy && !internalBridge.isObstructed)
                {
                    RunodeFace bridgeFace = internalBridge.parentRunodeFace;
                    if (bridgeFace != null && bridgeFace.MarkPowered(powerColor, node.sourceFace, this, currentFace.distanceFromSource))
                    {
                        if (!visitedTriggers.Contains(internalBridge))
                        {
                            if (!visitedFaces.Contains(bridgeFace))
                            {
                                if (currentFacesPowered < maxPower)
                                {
                                    visitedFaces.Add(bridgeFace);
                                    poweredFaces.Add(bridgeFace);
                                    currentFacesPowered++;
                                    bridgeFace.ApplyColor(powerColor);
                                }
                                else continue;
                            }
                            SetTriggerPowered(internalBridge, currentFace.distanceFromSource, queue, visitedTriggers, currentFace);
                        }
                    }
                }

                // Face neighbors (same face)
                var faceNeighbors = currentFace.GetConnectedTriggersOnFace(current);
                foreach (var fn in faceNeighbors)
                {
                    if (fn.gameObject.activeInHierarchy && !visitedTriggers.Contains(fn))
                        SetTriggerPowered(fn, currentFace.distanceFromSource, queue, visitedTriggers, node.sourceFace);
                }
            }

            // THEN Discover External Neighbors
            PowerConnectionTrigger neighbor = FindExternalNeighbor(current);
            if (neighbor != null && neighbor.gameObject.activeInHierarchy && !neighbor.isObstructed)
            {
                RunodeFace neighborFace = neighbor.parentRunodeFace;

                if (neighborFace != null)
                {
                    int nextDist = currentFace != null ? currentFace.distanceFromSource + 1 : 1;
                    if (!neighborFace.MarkPowered(powerColor, currentFace, this, nextDist)) return;

                    if (!visitedTriggers.Contains(neighbor))
                    {
                        if (!visitedFaces.Contains(neighborFace))
                        {
                            if (currentFacesPowered < maxPower)
                            {
                                visitedFaces.Add(neighborFace);
                                poweredFaces.Add(neighborFace);
                                currentFacesPowered++;
                                neighborFace.ApplyColor(powerColor);
                            }
                            else continue;
                        }
                        SetTriggerPowered(neighbor, nextDist, queue, visitedTriggers, currentFace);
                    }
                }
            }
        }
    }

    private void EnqueueSourceTrigger(PowerConnectionTrigger trigger, Queue<BFSNode> queue, HashSet<PowerConnectionTrigger> visitedTriggers, HashSet<RunodeFace> visitedFaces)
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
            RunodeFace targetFace = trigger.parentRunodeFace;
            if (targetFace != null)
            {
                targetFace.MarkPowered(powerColor, null, this, 0);
                if (!visitedFaces.Contains(targetFace))
                {
                    visitedFaces.Add(targetFace);
                    poweredFaces.Add(targetFace);
                    currentFacesPowered++;
                }
            }
        }
    }

    private void SetTriggerPowered(PowerConnectionTrigger trigger, int distance, Queue<BFSNode> queue, HashSet<PowerConnectionTrigger> visited, RunodeFace sourceFace)
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
