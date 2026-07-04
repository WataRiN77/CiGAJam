Shader "Hidden/CiGAJam/ObraLilaDither"
{
    Properties
    {
        _DarkColor ("Dark Color", Color) = (0.04, 0.035, 0.03, 1)
        _LightColor ("Light Color", Color) = (0.92, 0.88, 0.72, 1)
        _Brightness ("Brightness", Range(-1, 1)) = 0
        _Contrast ("Contrast", Range(0, 4)) = 1.35
        _DitherStrength ("Dither Strength", Range(0, 1)) = 0.75
        _PatternScale ("Pattern Scale", Range(0.25, 8)) = 1
        _PosterizeSteps ("Posterize Steps", Range(2, 16)) = 4
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.25
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
            float _PatternScale;
            float _PosterizeSteps;
            float _VignetteStrength;

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
                float2 uv = input.texcoord;
                float4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float luminance = dot(source.rgb, float3(0.299, 0.587, 0.114));
                luminance = saturate((luminance - 0.5) * _Contrast + 0.5 + _Brightness);

                float steps = max(2.0, _PosterizeSteps);
                luminance = floor(luminance * (steps - 1.0) + 0.5) / (steps - 1.0);

                float2 pixelPosition = uv * _ScreenParams.xy / max(0.0001, _PatternScale);
                float threshold = Bayer4x4((int2)floor(pixelPosition));
                float dithered = step(threshold, luminance);
                float tone = lerp(luminance, dithered, _DitherStrength);

                float2 centeredUv = uv * 2.0 - 1.0;
                float vignette = saturate(1.0 - dot(centeredUv, centeredUv) * _VignetteStrength);
                tone *= vignette;

                float3 color = lerp(_DarkColor.rgb, _LightColor.rgb, tone);
                return float4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
