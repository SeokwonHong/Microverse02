Shader "Custom/URP/OceanFieldComposite"
{
    Properties
    {
        [MainTexture] _TrailTex("Trail Texture", 2D) = "white" {}
        _PlanktonTex("Plankton Texture", 2D) = "black" {}

        [HDR] _OutlineColor("Outline Color", Color) = (1, 0.5, 0, 1)
        _Thickness("Outline Thickness", Range(0, 10)) = 1
        _Threshold("Alpha Threshold", Range(0, 1)) = 0.5

        [HDR] _PlanktonGlowColor("Plankton Glow Color", Color) = (0.6, 1, 0.7, 1)
        _PlanktonGlowStrength("Plankton Glow Strength", Range(0, 5)) = 1.5
        _PlanktonSoftness("Plankton Softness", Range(0, 4)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "Unlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_TrailTex);
            SAMPLER(sampler_TrailTex);

            TEXTURE2D(_PlanktonTex);
            SAMPLER(sampler_PlanktonTex);

            float4 _TrailTex_ST;
            float4 _TrailTex_TexelSize;

            float4 _OutlineColor;
            float _Thickness;
            float _Threshold;

            float4 _PlanktonGlowColor;
            float _PlanktonGlowStrength;
            float _PlanktonSoftness;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _TrailTex);
                return output;
            }

            float SamplePlanktonSoft(float2 uv)
            {
                float2 texel = _TrailTex_TexelSize.xy * _PlanktonSoftness;

                float a0 = SAMPLE_TEXTURE2D(_PlanktonTex, sampler_PlanktonTex, uv).a;
                float a1 = SAMPLE_TEXTURE2D(_PlanktonTex, sampler_PlanktonTex, uv + float2( texel.x, 0)).a;
                float a2 = SAMPLE_TEXTURE2D(_PlanktonTex, sampler_PlanktonTex, uv + float2(-texel.x, 0)).a;
                float a3 = SAMPLE_TEXTURE2D(_PlanktonTex, sampler_PlanktonTex, uv + float2(0,  texel.y)).a;
                float a4 = SAMPLE_TEXTURE2D(_PlanktonTex, sampler_PlanktonTex, uv + float2(0, -texel.y)).a;

                return max(a0, max(max(a1, a2), max(a3, a4)));
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 trailCol = SAMPLE_TEXTURE2D(_TrailTex, sampler_TrailTex, input.uv);

                float trailMask = trailCol.a;

                float2 texelSize = _TrailTex_TexelSize.xy * _Thickness;

                float aUp    = SAMPLE_TEXTURE2D(_TrailTex, sampler_TrailTex, input.uv + float2(0, texelSize.y)).a;
                float aDown  = SAMPLE_TEXTURE2D(_TrailTex, sampler_TrailTex, input.uv - float2(0, texelSize.y)).a;
                float aLeft  = SAMPLE_TEXTURE2D(_TrailTex, sampler_TrailTex, input.uv - float2(texelSize.x, 0)).a;
                float aRight = SAMPLE_TEXTURE2D(_TrailTex, sampler_TrailTex, input.uv + float2(texelSize.x, 0)).a;

                float edge = max(max(aUp, aDown), max(aLeft, aRight));

                if (trailMask < _Threshold && edge >= _Threshold)
                {
                    return _OutlineColor;
                }

                float planktonA = SamplePlanktonSoft(input.uv);
                float3 planktonRGB = _PlanktonGlowColor.rgb * planktonA * _PlanktonGlowStrength;

                float3 finalRGB = trailCol.rgb + planktonRGB;

                float visibleAlpha = max(
                    trailMask,
                    max(planktonA, max(finalRGB.r, max(finalRGB.g, finalRGB.b)))
                );

                return float4(finalRGB, saturate(visibleAlpha));
            }
            ENDHLSL
        }
    }
}