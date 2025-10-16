using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class VolumetricCloudRendererFeature : ScriptableRendererFeature
{
    public class VolumetricCloudPass : ScriptableRenderPass
    {
        const string m_passName = "VolumetricCloudFeature";
        private Material cloudMaterial;

        public VolumetricCloudPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public void Setup(Material mat)
        {
            cloudMaterial = mat;
            requiresIntermediateTexture = true;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            TextureHandle source = resourceData.activeColorTexture;
            TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);

            destinationDesc.name = $"CameraColor-{m_passName}";
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, cloudMaterial, 0);
            renderGraph.AddBlitPass(para, passName: m_passName);

            resourceData.cameraColor = destination;
        }
    }

    public Material cloudMaterial;
    public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

    VolumetricCloudPass m_ScriptablePass;

    public override void Create()
    {
        m_ScriptablePass = new VolumetricCloudPass();

        m_ScriptablePass.renderPassEvent = injectionPoint;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (cloudMaterial == null)
        {
            Debug.LogWarning("VolumetricCloudRendererFeature does not have material !");
            return;
        }

        m_ScriptablePass.Setup(cloudMaterial);
        renderer.EnqueuePass(m_ScriptablePass);
    }
}
