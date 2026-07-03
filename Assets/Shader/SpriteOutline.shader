Shader "Custom/SpriteOutline"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.01
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float _OutlineWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // 简单的外发光：检测周围像素透明度
                float outline = 0;
                float2 offsets[8] = {
                    float2(-1, 1), float2(0, 1), float2(1, 1),
                    float2(-1, 0),               float2(1, 0),
                    float2(-1,-1), float2(0,-1), float2(1,-1)
                };
                for (int j = 0; j < 8; j++)
                {
                    float2 sampleUV = i.uv + offsets[j] * _MainTex_TexelSize.xy * _OutlineWidth * 50;
                    outline += tex2D(_MainTex, sampleUV).a;
                }
                outline = saturate(outline - col.a * 8); // 只在不透明区域外描边

                col.rgb = lerp(col.rgb, _OutlineColor.rgb, outline * _OutlineColor.a);
                col.a = max(col.a, outline * _OutlineColor.a);
                return col;
            }
            ENDCG
        }
    }
}