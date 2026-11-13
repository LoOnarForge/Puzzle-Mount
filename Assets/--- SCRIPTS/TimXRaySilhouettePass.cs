using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class TimXRaySilhouettePass : ScriptableRenderPass
{
    private const int VisibleMaskShaderPass = 0;
    private const int SilhouetteShaderPass = 1;

    private readonly Material material;
    private readonly List<Renderer> renderers = new List<Renderer>();

    public TimXRaySilhouettePass(Material material)
    {
        this.material = material;
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    // Updates the cached renderer list drawn by the X-ray silhouette pass.
    public void SetRenderers(IReadOnlyList<Renderer> sourceRenderers)
    {
        renderers.Clear();
        if (sourceRenderers == null)
            return;

        for (int i = 0; i < sourceRenderers.Count; i++)
        {
            Renderer renderer = sourceRenderers[i];
            if (renderer != null)
                renderers.Add(renderer);
        }
    }

    // Schedules the mask and occluded silhouette passes in URP RenderGraph.
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (material == null || renderers.Count == 0)
            return;

        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        if (cameraData.isPreviewCamera)
            return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        TextureHandle color = resourceData.activeColorTexture;
        TextureHandle depth = resourceData.activeDepthTexture;
        if (!color.IsValid() || !depth.IsValid())
            return;

        using (var builder = renderGraph.AddUnsafePass<PassData>("Tim XRay Silhouette", out PassData passData))
        {
            passData.material = material;
            passData.color = color;
            passData.depth = depth;
            passData.renderers = renderers;

            builder.UseTexture(color, AccessFlags.ReadWrite);
            builder.UseTexture(depth, AccessFlags.ReadWrite);

            builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
            {
                context.cmd.SetRenderTarget(data.color, data.depth);
                DrawRenderersPass(context.cmd, data.renderers, data.material, VisibleMaskShaderPass);
                DrawRenderersPass(context.cmd, data.renderers, data.material, SilhouetteShaderPass);
            });
        }
    }

    private static void DrawRenderersPass(
        UnsafeCommandBuffer cmd,
        List<Renderer> targetRenderers,
        Material passMaterial,
        int shaderPass)
    {
        for (int i = 0; i < targetRenderers.Count; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            int subMeshCount = GetSubMeshCount(renderer);
            for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
                cmd.DrawRenderer(renderer, passMaterial, subMesh, shaderPass);
        }
    }

    private static int GetSubMeshCount(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh != null)
            return skinned.sharedMesh.subMeshCount;

        if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
            return filter.sharedMesh.subMeshCount;

        return 0;
    }

    private class PassData
    {
        public Material material;
        public TextureHandle color;
        public TextureHandle depth;
        public List<Renderer> renderers;
    }
}
