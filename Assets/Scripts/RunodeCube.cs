using System.Collections.Generic;
using UnityEngine;

public enum RunodeLineType
{
    Empty,
    [InspectorName("─ Horizontal")]
    Horizontal,
    [InspectorName("│ Vertical")]
    Vertical,
    [InspectorName("────────")]
    Separator1,
    [InspectorName("┘ Corner Left Top")]
    CornerLeftTop,
    [InspectorName("└ Corner Top Right")]
    CornerTopRight,
    [InspectorName("┌ Corner Right Bottom")]
    CornerRightBottom,
    [InspectorName("┐ Corner Bottom Left")]
    CornerBottomLeft,
    [InspectorName("──────────")]
    Separator2,
    [InspectorName("┴ T Section Left")]
    TSectionLeft,
    [InspectorName("├ T Section Top")]
    TSectionTop,
    [InspectorName("┬ T Section Right")]
    TSectionRight,
    [InspectorName("┤ T Section Bottom")]
    TSectionBottom,
    [InspectorName("────────────")]
    Separator3,
    [InspectorName("┼ Cross")]
    Cross
}

public class RunodeCube : MonoBehaviour
{
    public RunodeLineType topFace    = RunodeLineType.Empty;
    public RunodeLineType bottomFace = RunodeLineType.Empty;
    public RunodeLineType northFace  = RunodeLineType.Empty;
    public RunodeLineType southFace  = RunodeLineType.Empty;
    public RunodeLineType eastFace   = RunodeLineType.Empty;
    public RunodeLineType westFace   = RunodeLineType.Empty;

    public Transform topFaceTransform;
    public Transform bottomFaceTransform;
    public Transform northFaceTransform;
    public Transform southFaceTransform;
    public Transform eastFaceTransform;
    public Transform westFaceTransform;

    public Sprite horizontalSprite;
    public Sprite verticalSprite;
    public Sprite cornerSprite;
    public Sprite tSectionSprite;
    public Sprite crossSprite;


    // Refreshes obstruction state and then connections on this cube and its neighbours.
    public void RefreshCubeAndAdjacentConnections()
    {
        RunodeCube[] affectedCubes = FindAffectedCubes();

        RefreshObstructionsOnAffectedCubes(affectedCubes);
        RefreshPortObstructionsOnAffectedCubes(affectedCubes);
        RefreshConnectionsOnAffectedCubes(affectedCubes);
    }

    // Finds this cube plus every face- or edge-adjacent cube within the refresh radius.
    private RunodeCube[] FindAffectedCubes()
    {
        List<RunodeCube> affectedCubes = new List<RunodeCube> { this };
        RunodeCube[] cubes = FindObjectsByType<RunodeCube>(FindObjectsSortMode.None);

        foreach (RunodeCube cube in cubes)
        {
            if (cube == this)
                continue;

            Vector3 offset = cube.transform.position - transform.position;

            if (Mathf.Abs(offset.x) <= 2.1f
                && Mathf.Abs(offset.y) <= 2.1f
                && Mathf.Abs(offset.z) <= 2.1f)
            {
                affectedCubes.Add(cube);
            }
        }

        return affectedCubes.ToArray();
    }

    // Refreshes obstruction state on every active line in the affected cubes.
    private void RefreshObstructionsOnAffectedCubes(RunodeCube[] affectedCubes)
    {
        foreach (RunodeCube cube in affectedCubes)
        {
            RunodeLine[] lines = cube.GetComponentsInChildren<RunodeLine>();

            foreach (RunodeLine line in lines)
                line.RefreshFaceObstructionState();
        }
    }

    private void RefreshPortObstructionsOnAffectedCubes(RunodeCube[] affectedCubes)
    {

        foreach (RunodeCube cube in affectedCubes)
        {
            RunodeLine[] lines = cube.GetComponentsInChildren<RunodeLine>();

            foreach (RunodeLine line in lines)
                line.RefreshPortObstructions();
        }
    }

    // Refreshes connections on every unobstructed active line in the affected cubes.
    private void RefreshConnectionsOnAffectedCubes(RunodeCube[] affectedCubes)
    {
        foreach (RunodeCube cube in affectedCubes)
        {
            RunodeLine[] lines = cube.GetComponentsInChildren<RunodeLine>();

            foreach (RunodeLine line in lines)
            {
                line.RefreshPortConnections();
            }
        }
    }

    // Returns the face index (0:Top, 1:Bottom, 2:North, 3:South, 4:East, 5:West)
    // for the given world point relative to the cube's visual parent. Used for the Highlighter script
    public int GetFaceIndexFromPoint(Vector3 worldPoint)
    {
        Transform vParent = transform.GetChild(0);
        Vector3 localPoint = vParent.InverseTransformPoint(worldPoint);
        float lx = Mathf.Abs(localPoint.x), ly = Mathf.Abs(localPoint.y), lz = Mathf.Abs(localPoint.z);

        if (ly * 1.1f > lx && ly * 1.1f > lz) return localPoint.y > 0 ? 0 : 1;
        if (lx >= lz) return localPoint.x > 0 ? 4 : 5;
        return localPoint.z > 0 ? 2 : 3;
    }
}
