using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-50)]
public class PowerSource : MonoBehaviour
{
    public int colorIndex;
    [HideInInspector] public Color powerColor;

    public int maxPower = 10;
    public int currentFacesPowered = 0;
    public List<RunodePower.FaceData> poweredFaces = new List<RunodePower.FaceData>();

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

    public System.Collections.IEnumerator RunBFS(float delay)
    {
        WaitForSeconds wait = new WaitForSeconds(delay);
        
        // 1. Snapshot old state for capacity theft cleanup
        List<RunodePower.FaceData> previouslyPowered = new List<RunodePower.FaceData>(poweredFaces);
        
        // 2. Logic Clear: Reset flags for our territory ONLY (No visual updates)
        foreach (var face in previouslyPowered)
        {
            if (face != null && face.cube != null)
            {
                face.cube.ClearFace(face.faceIndex, false);
            }
        }

        Queue<BFSNode> queue = new Queue<BFSNode>();
        HashSet<PowerConnectionTrigger> visitedTriggers = new HashSet<PowerConnectionTrigger>();
        HashSet<RunodePower.FaceData> visitedFaces = new HashSet<RunodePower.FaceData>();
        
        currentFacesPowered = 0;
        poweredFaces.Clear();

        EnqueueSourceTrigger(upTrigger, queue, visitedTriggers, visitedFaces);
        EnqueueSourceTrigger(rightTrigger, queue, visitedTriggers, visitedFaces);
        EnqueueSourceTrigger(downTrigger, queue, visitedTriggers, visitedFaces);
        EnqueueSourceTrigger(leftTrigger, queue, visitedTriggers, visitedFaces);

        while (queue.Count > 0)
        {
            BFSNode node = queue.Dequeue();
            PowerConnectionTrigger current = node.trigger;
            RunodePower currentCube = current.parentRunodePower;

            // Get the face data for the CURRENT trigger
            RunodePower.FaceData currentFace = null;
            if (currentCube != null) currentFace = currentCube.GetFaceData(current);

            // PRIORITY: Discover all internal connections on the SAME cube first
            if (currentCube != null && currentFace != null)
            {
                // Internal bridges (corner wraps)
                PowerConnectionTrigger internalBridge = currentCube.GetInternalNeighbor(current);
                if (internalBridge != null && internalBridge.gameObject.activeInHierarchy && !internalBridge.isObstructed)
                {
                    if (currentCube.MarkFacePowered(internalBridge, powerColor, currentCube.name, node.sourceFace, this, currentFace.distanceFromSource))
                    {
                        if (!visitedTriggers.Contains(internalBridge))
                        {
                            RunodePower.FaceData nextFace = currentCube.GetFaceData(internalBridge);
                            if (nextFace != null && !visitedFaces.Contains(nextFace))
                            {
                                if (currentFacesPowered < maxPower)
                                {
                                    visitedFaces.Add(nextFace);
                                    poweredFaces.Add(nextFace);
                                    currentFacesPowered++;
                                    currentCube.RefreshFaceVisuals();
                                    yield return wait;
                                }
                                else continue;
                            }
                            SetTriggerPowered(internalBridge, currentFace.distanceFromSource, queue, visitedTriggers, currentFace);
                        }
                    }
                }

                // Face neighbors (same face)
                var faceNeighbors = currentCube.GetConnectedTriggersOnFace(current);
                foreach (var fn in faceNeighbors)
                {
                    if (fn.gameObject.activeInHierarchy && !visitedTriggers.Contains(fn))
                    {
                        SetTriggerPowered(fn, currentFace.distanceFromSource, queue, visitedTriggers, node.sourceFace);
                    }
                }
            }

            // THEN Discover External Neighbors
            PowerConnectionTrigger neighbor = FindExternalNeighbor(current);
            if (neighbor != null && neighbor.gameObject.activeInHierarchy && !neighbor.isObstructed)
            {
                RunodePower neighborCube = neighbor.parentRunodePower ?? neighbor.GetComponentInParent<RunodePower>();
                
                if (neighborCube != null)
                {
                    int nextDist = currentFace != null ? currentFace.distanceFromSource + 1 : 1;
                    string sender = currentCube != null ? currentCube.name : name;
                    if (!neighborCube.MarkFacePowered(neighbor, powerColor, sender, currentFace, this, nextDist)) yield break;
                    
                    RunodePower.FaceData nextFace = neighborCube.GetFaceData(neighbor);
                    if (nextFace != null && !visitedTriggers.Contains(neighbor))
                    {
                        if (!visitedFaces.Contains(nextFace))
                        {
                            if (currentFacesPowered < maxPower)
                            {
                                visitedFaces.Add(nextFace);
                                poweredFaces.Add(nextFace);
                                currentFacesPowered++;
                                neighborCube.RefreshFaceVisuals();
                                yield return wait;
                            }
                            else continue;
                        }
                        SetTriggerPowered(neighbor, nextDist, queue, visitedTriggers, currentFace);
                    }
                }
            }
        }

        // 3. CAPACITY THEFT: Clear any faces that were powered but are no longer in the set
        foreach (var face in previouslyPowered)
        {
            if (face != null && !visitedFaces.Contains(face) && face.cube != null)
            {
                face.cube.ClearFace(face.faceIndex, true);
            }
        }
    }

    private void EnqueueSourceTrigger(PowerConnectionTrigger trigger, Queue<BFSNode> queue, HashSet<PowerConnectionTrigger> visitedTriggers, HashSet<RunodePower.FaceData> visitedFaces)
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
            trigger.parentRunodePower.MarkFacePowered(trigger, powerColor, name, null, this, 0);

            RunodePower.FaceData targetFace = trigger.parentRunodePower.GetFaceData(trigger);
            if (targetFace != null && !visitedFaces.Contains(targetFace))
            {
                visitedFaces.Add(targetFace);
                poweredFaces.Add(targetFace);
                currentFacesPowered++;
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
