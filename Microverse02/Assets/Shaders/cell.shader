Shader "Custom/URP_Sprite_DetectRadial_2022"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _EdgeAlpha ("Edge Alpha", Range(0,1)) = 0.5
        _Power ("Falloff Power", Range(0.1, 8)) = 3.0
        _Inner ("Inner (0-1)", Range(0,1)) = 0.0
        _Outer ("Outer (0-1)", Range(0,1)) = 1.0
        _CircleSoftness ("Circle Softness", Range(0.001, 0.2)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "SpriteUnlit"
            Tags { "LightMode"="Universal2D" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _EdgeAlpha;
                float _Power;
                float _Inner;
                float _Outer;
                float _CircleSoftness;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // radial distance 0 centre -> 1 edge
                float2 p = IN.uv - 0.5;
                float r = length(p) * 2.0;

                // circle cutout, independent of texture alpha
                float circle = 1.0 - smoothstep(1.0 - _CircleSoftness, 1.0 + _CircleSoftness, r);

                // outer stronger alpha ramp
                float a = min(_Inner, _Outer);
                float b = max(_Inner, _Outer);
                float t = smoothstep(a, b, r);
                t = pow(saturate(t), _Power);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                half4 col = tex * _Color * IN.color;
                col.a = circle * t * _EdgeAlpha * _Color.a * IN.color.a;
                return col;
            }
            ENDHLSL
        }
    }
}