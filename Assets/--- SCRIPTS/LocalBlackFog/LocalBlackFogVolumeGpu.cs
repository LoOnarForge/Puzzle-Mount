using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct LocalBlackFogVolumeGpu
{
    public Matrix4x4 worldToLocal;
    public Matrix4x4 localToWorld;
    public Vector4 referencePosition;
    public Vector4 fogParams;

    public static LocalBlackFogVolumeGpu FromVolume(LocalBlackFogVolume volume)
    {
        return new LocalBlackFogVolumeGpu
        {
            worldToLocal = volume.WorldToLocalMatrix,
            localToWorld = volume.LocalToWorldMatrix,
            referencePosition = new Vector4(
                volume.ReferenceWorldPosition.x,
                volume.ReferenceWorldPosition.y,
                volume.ReferenceWorldPosition.z,
                0f),
            fogParams = new Vector4(
                volume.FogDensity,
                volume.EdgeSoftness,
                volume.ClearRadius,
                volume.ObscureRadius)
        };
    }
}
