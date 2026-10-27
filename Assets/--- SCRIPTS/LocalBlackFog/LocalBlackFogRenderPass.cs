using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class LocalBlackFogRenderPass : ScriptableRenderPass
{
    public const int MaxVolumes = 16;

    private static readonly List<LocalBlackFogVolume> ActiveVolumes = new();
    private static readonly List<LocalBlackFogVolume> EnabledVolumes = new();
    private static readonly LocalBlackFogVolumeGpu[] GpuScratch = new LocalBlackFogVolumeGpu[MaxVolumes];

    private readonly Material material;
    private readonly ComputeBuffer volumeBuffer;

    private static readonly int FogVolumesBufferId = Shader.PropertyToID("_FogVolumes");
    private static readonly int FogVolumeCountId = Shader.PropertyToID("_FogVolumeCount");
    private static readonly int FogSceneColorId = Shader.PropertyToID("_FogSceneColor");
    private static readonly int FogSceneDepthId = Shader.PropertyToID("_FogSceneDepth");

    public LocalBlackFogRenderPass(Material material)
    {
        this.material = material;
        volumeBuffer = new ComputeBuffer(MaxVolumes, Marshal.SizeOf<LocalBlackFogVolumeGpu>());
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        requiresIntermediateTexture = true;
    }

    public void DisposePassResources()
    {
        volumeBuffer?.Release();
    }

    public static void Register(LocalBlackFogVolume volume)
    {
        if (volume != null && !ActiveVolumes.Contains(volume))
            ActiveVolumes.Add(volume);
    }

    public static void Unregister(LocalBlackFogVolume volume)
    {
        ActiveVolumes.Remove(volume);
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (material == null)
            return;

        EnabledVolumes.Clear();
        for (int i = 0; i < ActiveVolumes.Count; i++)
        {
            LocalBlackFogVolume volume = ActiveVolumes[i];
            if (volume != null && volume.isActiveAndEnabled)
                EnabledVolumes.Add(volume);
        }

        if (EnabledVolumes.Count == 0)
            return;

        if (EnabledVolumes.Count > MaxVolumes)
            Debug.LogWarning($"Local Black Fog: {EnabledVolumes.Count} volumes active; only the first {MaxVolumes} are rendered.");

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        if (cameraData.isPreviewCamera)
            return;

        TextureHandle color = resourceData.activeColorTexture;
        TextureHandle depth = resourceData.activeDepthTexture;
        if (!color.IsValid() || !depth.IsValid())
            return;

        int volumeCount = Mathf.Min(EnabledVolumes.Count, MaxVolumes);
        for (int i = 0; i < volumeCount; i++)
            GpuScratch[i] = LocalBlackFogVolumeGpu.FromVolume(EnabledVolumes[i]);

        RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
        TextureDesc copyTextureDesc = new TextureDesc(desc.width, desc.height)
        {
            colorFormat = desc.graphicsFormat,
            depthBufferBits = 0,
            msaaSamples = MSAASamples.None,
            name = "_LocalBlackFogSceneCopy"
        };
        TextureHandle sceneCopy = renderGraph.CreateTexture(copyTextureDesc);

        using (var builder = renderGraph.AddUnsafePass<PassData>("Local Black Fog", out PassData passData))
        {
            builder.AllowGlobalStateModification(true);

            passData.material = material;
            passData.color = color;
            passData.depth = depth;
            passData.sceneCopy = sceneCopy;
            passData.volumeBuffer = volumeBuffer;
            passData.volumeCount = volumeCount;

            builder.UseTexture(color, AccessFlags.ReadWrite);
            builder.UseTexture(depth, AccessFlags.Read);
            builder.UseTexture(sceneCopy, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
            {
                context.cmd.CopyTexture(data.color, data.sceneCopy);
                context.cmd.SetGlobalTexture(FogSceneDepthId, data.depth);
                context.cmd.SetGlobalTexture(FogSceneColorId, data.sceneCopy);

                data.volumeBuffer.SetData(GpuScratch, 0, 0, data.volumeCount);
                data.material.SetBuffer(FogVolumesBufferId, data.volumeBuffer);
                data.material.SetInt(FogVolumeCountId, data.volumeCount);

                context.cmd.SetRenderTarget(data.color);
                context.cmd.DrawProcedural(Matrix4x4.identity, data.material, 0, MeshTopology.Triangles, 3, 1);
            });
        }
    }

    private class PassData
    {
        public Material material;
        public TextureHandle color;
        public TextureHandle depth;
        public TextureHandle sceneCopy;
        public ComputeBuffer volumeBuffer;
        public int volumeCount;
    }
}
