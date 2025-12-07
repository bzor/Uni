using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class RaymarchSDFFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material raymarchMaterial;
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingSkybox;
    }

    class RaymarchSDFPass : ScriptableRenderPass
    {
        private Material _material;
        private static readonly string ProfilerTag = "Raymarch SDF Fullscreen";

        public RaymarchSDFPass(Material material, RenderPassEvent passEvent)
        {
            _material = material;
            this.renderPassEvent = passEvent;
        }

        // NOTE: Unity 6 / newer URP uses this signature:
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Get the renderer for this camera
            var renderer = renderingData.cameraData.renderer;

            // Configure our pass to render into the camera's color + depth
            // These are RTHandles in newer URP; ConfigureTarget handles them.
            ConfigureTarget(renderer.cameraColorTargetHandle, renderer.cameraDepthTargetHandle);

            // No clear – we’re just drawing over what’s already there.
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null)
                return;

            // Optional: skip SceneView if you only care about the LKG/game camera
            if (renderingData.cameraData.isSceneViewCamera)
                return;

            CommandBuffer cmd = CommandBufferPool.Get(ProfilerTag);

            // We already configured the target in OnCameraSetup,
            // so we can just draw a fullscreen quad into it.
            CoreUtils.DrawFullScreen(cmd, _material);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // Nothing to clean up for this simple pass.
        }
    }

    public Settings settings = new Settings();
    private RaymarchSDFPass _pass;

    public override void Create()
    {
        _pass = new RaymarchSDFPass(settings.raymarchMaterial, settings.passEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.raymarchMaterial == null)
            return;

        renderer.EnqueuePass(_pass);
    }
}
