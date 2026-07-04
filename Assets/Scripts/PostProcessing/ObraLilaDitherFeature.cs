using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ObraLilaDitherFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Shader shader;
        public bool applyInSceneView;

        [Header("Palette")]
        public Color darkColor = new Color(0.04f, 0.035f, 0.03f, 1f);
        public Color lightColor = new Color(0.92f, 0.88f, 0.72f, 1f);

        [Header("Tone")]
        [Range(-1f, 1f)] public float brightness = 0f;
        [Range(0f, 4f)] public float contrast = 1.35f;
        [Range(0f, 1f)] public float ditherStrength = 0.75f;
        [Range(0.25f, 8f)] public float patternScale = 1f;
        [Range(2f, 16f)] public float posterizeSteps = 4f;
        [Range(0f, 1f)] public float vignetteStrength = 0.25f;
    }

    [SerializeField] private Settings settings = new Settings();

    private Material material;
    private ObraLilaDitherPass pass;

    public override void Create()
    {
        EnsureMaterial();

        pass = new ObraLilaDitherPass(settings)
        {
            renderPassEvent = settings.renderPassEvent
        };
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (!CanRender(renderingData))
        {
            return;
        }

        pass.SetMaterial(material);
        pass.SetTarget(renderer.cameraColorTargetHandle);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!CanRender(renderingData))
        {
            return;
        }

        pass.SetMaterial(material);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        CoreUtils.Destroy(material);
    }

    private bool CanRender(RenderingData renderingData)
    {
        if (pass == null)
        {
            Create();
        }

        if (!EnsureMaterial())
        {
            return false;
        }

        if (!settings.applyInSceneView && renderingData.cameraData.isSceneViewCamera)
        {
            return false;
        }

        CameraType cameraType = renderingData.cameraData.camera.cameraType;
        return cameraType == CameraType.Game || cameraType == CameraType.SceneView;
    }

    private bool EnsureMaterial()
    {
        if (settings.shader == null)
        {
            settings.shader = Shader.Find("Hidden/CiGAJam/ObraLilaDither");
        }

        if (settings.shader == null)
        {
            return false;
        }

        if (material == null || material.shader != settings.shader)
        {
            CoreUtils.Destroy(material);
            material = CoreUtils.CreateEngineMaterial(settings.shader);
        }

        return material != null;
    }

    private class ObraLilaDitherPass : ScriptableRenderPass
    {
        private static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        private static readonly int LightColorId = Shader.PropertyToID("_LightColor");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int DitherStrengthId = Shader.PropertyToID("_DitherStrength");
        private static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");
        private static readonly int PosterizeStepsId = Shader.PropertyToID("_PosterizeSteps");
        private static readonly int VignetteStrengthId = Shader.PropertyToID("_VignetteStrength");

        private readonly Settings settings;
        private readonly ProfilingSampler profilingSampler = new ProfilingSampler("Obra Lila Dither");

        private Material material;
        private RTHandle source;
        private RTHandle temporaryColorTexture;

        public ObraLilaDitherPass(Settings settings)
        {
            this.settings = settings;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public void SetMaterial(Material material)
        {
            this.material = material;
            renderPassEvent = settings.renderPassEvent;
        }

        public void SetTarget(RTHandle source)
        {
            this.source = source;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref temporaryColorTexture,
                descriptor,
                FilterMode.Point,
                TextureWrapMode.Clamp,
                name: "_ObraLilaDitherTemp"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null || source == null || temporaryColorTexture == null)
            {
                return;
            }

            UpdateMaterial();

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, profilingSampler))
            {
                Blitter.BlitCameraTexture(cmd, source, temporaryColorTexture);
                Blitter.BlitCameraTexture(cmd, temporaryColorTexture, source, material, 0);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        public void Dispose()
        {
            temporaryColorTexture?.Release();
        }

        private void UpdateMaterial()
        {
            material.SetColor(DarkColorId, settings.darkColor);
            material.SetColor(LightColorId, settings.lightColor);
            material.SetFloat(BrightnessId, settings.brightness);
            material.SetFloat(ContrastId, settings.contrast);
            material.SetFloat(DitherStrengthId, settings.ditherStrength);
            material.SetFloat(PatternScaleId, settings.patternScale);
            material.SetFloat(PosterizeStepsId, settings.posterizeSteps);
            material.SetFloat(VignetteStrengthId, settings.vignetteStrength);
        }
    }
}
