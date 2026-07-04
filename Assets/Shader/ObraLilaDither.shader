Shader "Hidden/CiGAJam/ObraLilaDither"
{
    Properties
    {
        _DarkColor ("Dark Color", Color) = (0.04, 0.035, 0.03, 1)
        _LightColor ("Light Color", Color) = (0.92, 0.88, 0.72, 1)
        _Brightness ("Brightness", Range(-1, 1)) = 0
        _Contrast ("Contrast", Range(0, 4)) = 1.35
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.75
        _PixelSize ("Pixel Size", Range(1, 8)) = 1
        _PatternScale ("Pattern Scale", Range(0.25, 8)) = 1
        _PosterizeSteps ("Posterize Steps", Range(2, 32)) = 8
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.25
        _EnableScanlines ("Enable Scanlines", Float) = 1
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.18
        _ScanlineFrequency ("Scanline Frequency", Range(60, 1080)) = 360
        _ScanlineScrollSpeed ("Scanline Scroll Speed", Range(-8, 8)) = 0.35
        _EnableWideAngleLens ("Enable Wide Angle Lens", Float) = 1
        _BarrelDistortion ("Barrel Distortion", Range(0, 0.7)) = 0.18
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.2)) = 0.015
        _EdgeFade ("Edge Fade", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "ObraLilaDither"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _DarkColor;
            float4 _LightColor;
            float _Brightness;
            float _Contrast;
            float _DitherStrength;
            float _PixelSize;
            float _PatternScale;
            float _PosterizeSteps;
            float _VignetteStrength;
            float _EnableScanlines;
            float _ScanlineStrength;
            float _ScanlineFrequency;
            float _ScanlineScrollSpeed;
            float _EnableWideAngleLens;
            float _BarrelDistortion;
            float _ChromaticAberration;
            float _EdgeFade;

            float Bayer4x4(int2 position)
            {
                int x = position.x & 3;
                int y = position.y & 3;
                int index = x + y * 4;

                static const float bayer[16] =
                {
                    0.0 / 16.0,  8.0 / 16.0,  2.0 / 16.0, 10.0 / 16.0,
                    12.0 / 16.0, 4.0 / 16.0, 14.0 / 16.0,  6.0 / 16.0,
                    3.0 / 16.0, 11.0 / 16.0,  1.0 / 16.0,  9.0 / 16.0,
                    15.0 / 16.0, 7.0 / 16.0, 13.0 / 16.0,  5.0 / 16.0
                };

                return bayer[index];
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 baseUv = input.texcoord;
                float2 centeredBaseUv = baseUv * 2.0 - 1.0;
                float radiusSquared = dot(centeredBaseUv, centeredBaseUv);
                float distortion = 1.0 + _BarrelDistortion * radiusSquared * step(0.5, _EnableWideAngleLens);
                float2 uv = centeredBaseUv * distortion * 0.5 + 0.5;
                float inBounds = step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);

                float pixelSize = max(1.0, _PixelSize);
                float2 virtualPixel = floor(uv * _ScreenParams.xy / pixelSize);
                float2 snappedUv = (virtualPixel + 0.5) * pixelSize / _ScreenParams.xy;

                float2 chromaDirection = normalize(centeredBaseUv + float2(0.0001, 0.0001));
                float2 chromaOffset = chromaDirection * _ChromaticAberration * 0.01 * step(0.5, _EnableWideAngleLens);
                float red = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, saturate(snappedUv + chromaOffset)).r;
                float green = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, snappedUv).g;
                float blue = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, saturate(snappedUv - chromaOffset)).b;
                float4 source = float4(red, green, blue, 1.0);

                float luminance = dot(source.rgb, float3(0.299, 0.587, 0.114));
                luminance = saturate((luminance - 0.5) * _Contrast + 0.5 + _Brightness);

                float2 patternPixel = floor(virtualPixel / max(0.0001, _PatternScale));
                float threshold = Bayer4x4((int2)patternPixel);
                float steps = max(2.0, _PosterizeSteps);
                float ditherOffset = (threshold - 0.5) / (steps - 1.0);
                float ditheredLuminance = saturate(luminance + ditherOffset * _DitherStrength);
                float tone = floor(ditheredLuminance * (steps - 1.0) + 0.5) / (steps - 1.0);

                float2 centeredUv = baseUv * 2.0 - 1.0;
                float vignette = saturate(1.0 - dot(centeredUv, centeredUv) * _VignetteStrength);
                tone *= vignette;

                float scanlineWave = sin((baseUv.y * _ScanlineFrequency + _Time.y * _ScanlineScrollSpeed) * 6.2831853);
                float scanline = lerp(1.0, 1.0 - _ScanlineStrength, step(0.0, scanlineWave));
                tone *= lerp(1.0, scanline, step(0.5, _EnableScanlines));

                float edge = saturate((1.0 - radiusSquared * 0.55) / max(0.0001, _EdgeFade));
                float edgeMask = lerp(1.0, edge, step(0.5, _EnableWideAngleLens) * _EdgeFade);
                tone *= edgeMask * inBounds;

                float3 color = lerp(_DarkColor.rgb, _LightColor.rgb, tone);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
