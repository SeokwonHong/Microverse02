Shader "Custom/URP/SimpleOutline"
{
    Properties
    {
        [MainTexture] _MainTex("Texture", 2D) = "white" {}
        [HDR] _OutlineColor("Outline Color", Color) = (1, 0.5, 0, 1)
        _Thickness("Outline Thickness", Range(0, 10)) = 1
        _Threshold("Alpha Threshold", Range(0, 1)) = 0.5
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

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            float4 _OutlineColor;
            float _Thickness;
            float _Threshold;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);

                // alpha channel is TRAIL MASK ONLY
                float trailMask = col.a;

                float2 texelSize = _MainTex_TexelSize.xy * _Thickness;

                float aUp    = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(0, texelSize.y)).a;
                float aDown  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(0, texelSize.y)).a;
                float aLeft  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv - float2(texelSize.x, 0)).a;
                float aRight = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv + float2(texelSize.x, 0)).a;

                float edge = max(max(aUp, aDown), max(aLeft, aRight));

                // Only outline TRAIL pixels
                if (trailMask < _Threshold && edge >= _Threshold)
                {
                    return _OutlineColor;
                }

                // Rebuild visible alpha from colour brightness OR trail mask
                // so food/draw remain visible even though their stored alpha is 0
                float visibleAlpha = max(trailMask, max(col.r, max(col.g, col.b)));

                return float4(col.rgb, visibleAlpha);
            }
            ENDHLSL
        }
    }
}