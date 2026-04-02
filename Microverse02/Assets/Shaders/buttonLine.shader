Shader "Custom/URP/buttonLine"
{
    Properties
    {
        [HDR] _BaseColor("Base Color", Color) = (0.1, 1, 0.2, 1)
        [HDR] _GlowColor("Glow Color", Color) = (0.3, 1, 0.4, 1)

        _Alpha("Alpha", Range(0,1)) = 1
        _CoreBrightness("Core Brightness", Range(0,4)) = 1.8
        _EdgeDarkness("Edge Darkness", Range(0,4)) = 1.2
        _RimPower("Rim Power", Range(0.1,8)) = 2.5
        _GlowIntensity("Glow Intensity", Range(0,6)) = 2.0

        _PulseSpeed("Pulse Speed", Range(0,10)) = 0
        _PulseStrength("Pulse Strength", Range(0,2)) = 0.25
        _PulseScale("Pulse Scale", Range(0,20)) = 6
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _GlowColor;
                float _Alpha;
                float _CoreBrightness;
                float _EdgeDarkness;
                float _RimPower;
                float _GlowIntensity;
                float _PulseSpeed;
                float _PulseStrength;
                float _PulseScale;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // LineRenderer UV:
                // x = along the line
                // y = across the width
                float across = abs(IN.uv.y * 2.0 - 1.0);   // 0 in centre, 1 at edges
                float centreMask = 1.0 - saturate(across);

                // fake cylindrical shading
                float core = pow(centreMask, 0.65) * _CoreBrightness;
                float edgeShade = saturate(1.0 - across * _EdgeDarkness);

                // rim glow near edges
                float rim = pow(saturate(1.0 - centreMask), _RimPower) * _GlowIntensity;

                // optional pulse along the wire
                float pulse = 1.0;
                if (_PulseSpeed > 0.001)
                {
                    pulse += sin(IN.uv.x * _PulseScale - _Time.y * _PulseSpeed) * _PulseStrength;
                }

                float3 col = IN.color.rgb * core * edgeShade * pulse;
                col += IN.color.rgb * rim * pulse;

                float alpha = saturate(_Alpha * IN.color.a * max(core * 0.7, 0.2));

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}