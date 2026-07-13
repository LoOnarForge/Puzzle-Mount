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

    private Queue<BFSNode> bfsQueue = new Queue<BFSNode>();
    private HashSet<PowerConnectionTrigger> visitedTriggers = new HashSet<PowerConnectionTrigger>();
    private HashSet<RunodeFace> visitedFaces = new HashSet<RunodeFace>();

    public bool HasPendingSteps => bfsQueue.Count > 0;

    // Seeds the BFS queue with all source triggers. Called by PowerManager before the interleaved loop.
    public void InitBFS()
    {
        bfsQueue.Clear();
        visitedTriggers.Clear();
        visitedFaces.Clear();

        EnqueueSourceTrigger(upTrigger);
        EnqueueSourceTrigger(rightTrigger);
        EnqueueSourceTrigger(downTrigger);
        EnqueueSourceTrigger(leftTrigger);
    }

    // Processes one node from the BFS queue. Returns false if a short circuit is detected.
    public bool StepBFS()
    {
        if (bfsQueue.Count == 0) return true;

        BFSNode node = bfsQueue.Dequeue();
        PowerConnectionTrigger current = node.trigger;
        RunodeFace currentFace = current.parentRunodeFace;

        if (currentFace != null)
        {
            // Internal bridge (corner/edge wrap)
            PowerConnectionTrigger internalBridge = currentFace.GetInternalNeighbor(current);
            if (internalBridge != null && internalBridge.gameObject.activeInHierarchy && !internalBridge.isObstructed)
            {
                RunodeFace bridgeFace = internalBridge.parentRunodeFace;
                if (bridgeFace != null)
                {
                    bool isNewBridgeFace = !visitedFaces.Contains(bridgeFace);
                    if (isNewBridgeFace && currentFacesPowered >= maxPower) return true;

                    if (!bridgeFace.MarkPowered(powerColor, node.sourceFace, this, currentFace.distanceFromSource))
                        return false;

                    if (!visitedTriggers.Contains(internalBridge))
                    {
                        if (isNewBridgeFace)
                        {
                            visitedFaces.Add(bridgeFace);
                            poweredFaces.Add(bridgeFace);
                            currentFacesPowered++;
                            bridgeFace.ApplyColor(powerColor);
                        }
                        SetTriggerPowered(internalBridge, currentFace.distanceFromSource, currentFace);
                    }
                }
            }

            // Face neighbors (same face)
            var faceNeighbors = currentFace.GetConnectedTriggersOnFace(current);
            foreach (var fn in faceNeighbors)
            {
                if (fn.gameObject.activeInHierarchy && !visitedTriggers.Contains(fn))
                    SetTriggerPowered(fn, currentFace.distanceFromSource, node.sourceFace);
            }
        }

        // External neighbor (adjacent cube)
        PowerConnectionTrigger neighbor = FindExternalNeighbor(current);
        if (neighbor != null && neighbor.gameObject.activeInHierarchy && !neighbor.isObstructed)
        {
            RunodeFace neighborFace = neighbor.parentRunodeFace;
            if (neighborFace != null)
            {
                int nextDist = currentFace != null ? currentFace.distanceFromSource + 1 : 1;
                bool isNewNeighborFace = !visitedFaces.Contains(neighborFace);
                if (isNewNeighborFace && currentFacesPowered >= maxPower) return true;

                if (!neighborFace.MarkPowered(powerColor, currentFace, this, nextDist))
                    return false;

                if (!visitedTriggers.Contains(neighbor))
                {
                    if (isNewNeighborFace)
                    {
                        visitedFaces.Add(neighborFace);
                        poweredFaces.Add(neighborFace);
                        currentFacesPowered++;
                        neighborFace.ApplyColor(powerColor);
                    }
                    SetTriggerPowered(neighbor, nextDist, currentFace);
                }
            }
        }

        return true;
    }

    private void EnqueueSourceTrigger(PowerConnectionTrigger trigger)
    {
        if (trigger == null || !trigger.gameObject.activeInHierarchy) return;

        trigger.isPowered = true;
        trigger.currentPowerColor = powerColor;
        trigger.distanceFromSource = 0;
        trigger.sourceMW = maxPower;

        visitedTriggers.Add(trigger);
        bfsQueue.Enqueue(new BFSNode(trigger, null));

        if (trigger.parentRunodePower != null)
        {
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

    private void SetTriggerPowered(PowerConnectionTrigger trigger, int distance, RunodeFace sourceFace)
    {
        trigger.isPowered = true;
        trigger.currentPowerColor = powerColor;
        trigger.distanceFromSource = distance;
        trigger.sourceMW = maxPower;

        visitedTriggers.Add(trigger);
        bfsQueue.Enqueue(new BFSNode(trigger, sourceFace));
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
}
