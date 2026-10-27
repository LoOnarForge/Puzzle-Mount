using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class LocalBlackFogRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader fogShader;

    private LocalBlackFogRenderPass pass;
    private Material material;

    public override void Create()
    {
        if (fogShader == null)
            fogShader = Shader.Find("Custom/LocalBlackFog");

        if (fogShader == null)
        {
            Debug.LogError("LocalBlackFogRendererFeature: shader Custom/LocalBlackFog not found.");
            return;
        }

        material = CoreUtils.CreateEngineMaterial(fogShader);
        pass = new LocalBlackFogRenderPass(material);
        pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.DisposePassResources();
        CoreUtils.Destroy(material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (pass == null || material == null)
            return;

        if (!renderingData.cameraData.isPreviewCamera)
            renderer.EnqueuePass(pass);
    }
}
