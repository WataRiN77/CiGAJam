using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ObraLilaDitherFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRendering;
        public Shader shader;
        public bool applyInSceneView;

        [Header("Palette")]
        public Color darkColor = new Color(0.04f, 0.035f, 0.03f, 1f);
        public Color lightColor = new Color(0.92f, 0.88f, 0.72f, 1f);

        [Header("Tone")]
        [Range(-1f, 1f)] public float brightness = 0f;
        [Range(0f, 4f)] public float contrast = 1.35f;
        [Range(0f, 1f)] public float ditherStrength = 0.75f;
        [Range(1f, 8f)] public float pixelSize = 1f;
        [Range(0.25f, 8f)] public float patternScale = 1f;
        [Range(2f, 32f)] public float posterizeSteps = 8f;
        [Range(0f, 1f)] public float vignetteStrength = 0.25f;

        [Header("CRT Scanlines")]
        public bool enableScanlines = true;
        [Range(0f, 1f)] public float scanlineStrength = 0.18f;
        [Range(60f, 1080f)] public float scanlineFrequency = 360f;
        [Range(-8f, 8f)] public float scanlineScrollSpeed = 0.35f;

        [Header("Surveillance Lens")]
        public bool enableWideAngleLens = true;
        [Range(0f, 0.7f)] public float barrelDistortion = 0.18f;
        [Range(0f, 0.2f)] public float chromaticAberration = 0.015f;
        [Range(0f, 1f)] public float edgeFade = 0.2f;

        [Header("Unprocessed Layer")]
        public bool redrawUnprocessedLayer = true;
        public string unprocessedLayerName = "Gizmo";
        public LayerMask unprocessedLayerMask;
    }

    [SerializeField] private Settings settings = new Settings();

    private Material material;
    private ObraLilaDitherPass pass;
    private UnprocessedLayerPass unprocessedLayerPass;

    public override void Create()
    {
        EnsureMaterial();

        pass = new ObraLilaDitherPass(settings)
        {
            renderPassEvent = settings.renderPassEvent
        };

        unprocessedLayerPass = new UnprocessedLayerPass(settings)
        {
            renderPassEvent = GetAfterPostProcessEvent()
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
        unprocessedLayerPass.SetTarget(renderer.cameraColorTargetHandle);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!CanRender(renderingData))
        {
            return;
        }

        pass.SetMaterial(material);
        renderer.EnqueuePass(pass);

        if (settings.redrawUnprocessedLayer && GetUnprocessedLayerMask() != 0)
        {
            renderer.EnqueuePass(unprocessedLayerPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        CoreUtils.Destroy(material);
    }

    private bool CanRender(RenderingData renderingData)
    {
        if (pass == null || unprocessedLayerPass == null)
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

    private int GetUnprocessedLayerMask()
    {
        if (settings.unprocessedLayerMask.value != 0)
        {
            return settings.unprocessedLayerMask.value;
        }

        int layer = LayerMask.NameToLayer(settings.unprocessedLayerName);
        return layer >= 0 ? 1 << layer : 0;
    }

    private RenderPassEvent GetAfterPostProcessEvent()
    {
        return (RenderPassEvent)((int)settings.renderPassEvent + 1);
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
        private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
        private static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");
        private static readonly int PosterizeStepsId = Shader.PropertyToID("_PosterizeSteps");
        private static readonly int VignetteStrengthId = Shader.PropertyToID("_VignetteStrength");
        private static readonly int EnableScanlinesId = Shader.PropertyToID("_EnableScanlines");
        private static readonly int ScanlineStrengthId = Shader.PropertyToID("_ScanlineStrength");
        private static readonly int ScanlineFrequencyId = Shader.PropertyToID("_ScanlineFrequency");
        private static readonly int ScanlineScrollSpeedId = Shader.PropertyToID("_ScanlineScrollSpeed");
        private static readonly int EnableWideAngleLensId = Shader.PropertyToID("_EnableWideAngleLens");
        private static readonly int BarrelDistortionId = Shader.PropertyToID("_BarrelDistortion");
        private static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
        private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");

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
            material.SetFloat(PixelSizeId, settings.pixelSize);
            material.SetFloat(PatternScaleId, settings.patternScale);
            material.SetFloat(PosterizeStepsId, settings.posterizeSteps);
            material.SetFloat(VignetteStrengthId, settings.vignetteStrength);
            material.SetFloat(EnableScanlinesId, settings.enableScanlines ? 1f : 0f);
            material.SetFloat(ScanlineStrengthId, settings.scanlineStrength);
            material.SetFloat(ScanlineFrequencyId, settings.scanlineFrequency);
            material.SetFloat(ScanlineScrollSpeedId, settings.scanlineScrollSpeed);
            material.SetFloat(EnableWideAngleLensId, settings.enableWideAngleLens ? 1f : 0f);
            material.SetFloat(BarrelDistortionId, settings.barrelDistortion);
            material.SetFloat(ChromaticAberrationId, settings.chromaticAberration);
            material.SetFloat(EdgeFadeId, settings.edgeFade);
        }
    }

    private class UnprocessedLayerPass : ScriptableRenderPass
    {
        private static readonly List<ShaderTagId> ShaderTagIds = new List<ShaderTagId>
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit")
        };

        private readonly Settings settings;
        private readonly ProfilingSampler profilingSampler = new ProfilingSampler("Redraw Unprocessed Layer");
        private RTHandle target;

        public UnprocessedLayerPass(Settings settings)
        {
            this.settings = settings;
        }

        public void SetTarget(RTHandle target)
        {
            this.target = target;
            renderPassEvent = (RenderPassEvent)((int)settings.renderPassEvent + 1);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (target != null)
            {
                ConfigureTarget(target);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            int layerMask = GetLayerMask();

            if (layerMask == 0)
            {
                return;
            }

            SortingCriteria sortingCriteria = SortingCriteria.CommonTransparent;
            DrawingSettings drawingSettings = CreateDrawingSettings(ShaderTagIds, ref renderingData, sortingCriteria);
            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all, layerMask);
            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private int GetLayerMask()
        {
            if (settings.unprocessedLayerMask.value != 0)
            {
                return settings.unprocessedLayerMask.value;
            }

            int layer = LayerMask.NameToLayer(settings.unprocessedLayerName);
            return layer >= 0 ? 1 << layer : 0;
        }
    }
}
