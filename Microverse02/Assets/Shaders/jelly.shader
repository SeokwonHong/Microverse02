Shader "Custom/URP_Sprite_JellyRadial_2022_WiggleCut"
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

        // Wiggle
        _WiggleAmp   ("Wiggle Amplitude", Range(0, 0.2)) = 0.04
        _WiggleFreq  ("Wiggle Frequency", Range(0, 24))  = 10
        _WiggleSpeed ("Wiggle Speed", Range(0, 10))      = 2
        _WiggleRadial("Wiggle Radial Bias", Range(0, 4)) = 1.5

        // Contact cutting
        _CutStrength   ("Cut Strength", Range(0, 4)) = 1.0
        _CutCone       ("Cut Cone", Range(0, 1)) = 0.75
        _DepthScale    ("Depth Scale", Range(0, 10)) = 2.0
        _CutOuterStart ("Cut Outer Start", Range(0, 1)) = 0.55

        // Per-cell contact data: xy = direction, z = depth, w unused
        [PerRendererData] _JellyContact0 ("Jelly Contact 0", Vector) = (0,0,0,0)
        [PerRendererData] _JellyContact1 ("Jelly Contact 1", Vector) = (0,0,0,0)
        [PerRendererData] _JellyContact2 ("Jelly Contact 2", Vector) = (0,0,0,0)
        [PerRendererData] _JellyContact3 ("Jelly Contact 3", Vector) = (0,0,0,0)
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

                float _WiggleAmp;
                float _WiggleFreq;
                float _WiggleSpeed;
                float _WiggleRadial;

                float _CutStrength;
                float _CutCone;
                float _DepthScale;
                float _CutOuterStart;

                float4 _JellyContact0;
                float4 _JellyContact1;
                float4 _JellyContact2;
                float4 _JellyContact3;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            float ContactCut(float2 radialDir, float rBase, float4 contact)
            {
                float2 cdir = contact.xy;
                float depth = contact.z;

                float len2 = dot(cdir, cdir);
                if (len2 < 1e-6 || depth <= 0.0)
                    return 0.0;

                cdir = normalize(cdir);

                // 1 when pixel points toward the neighbour, 0 elsewhere
                float alignment = dot(radialDir, cdir);

                // only affect a cone facing the neighbour
                float coneMask = smoothstep(_CutCone, 1.0, alignment);

                // cut mainly near the outer jelly edge
                float outerMask = smoothstep(_CutOuterStart, 1.0, rBase);

                // deeper overlap = stronger cut
                float depthMask = saturate(depth * _DepthScale);

                return coneMask * outerMask * depthMask * _CutStrength;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 p = IN.uv - 0.5;
                float rLen = length(p);

                float2 radialDir = (rLen > 1e-5) ? (p / rLen) : float2(1, 0);

                // base radius: 0 centre -> 1 edge
                float rBase = rLen * 2.0;

                // wiggle
                float ang = atan2(p.y, p.x);
                float time = _Time.y * _WiggleSpeed;

                float wiggle =
                    sin(ang * _WiggleFreq + time) *
                    (1.0 + rBase * _WiggleRadial) *
                    _WiggleAmp;

                float r = rBase + wiggle;

                // circle mask
                float circle = 1.0 - smoothstep(1.0 - _CircleSoftness, 1.0 + _CircleSoftness, r);

                // radial alpha ramp
                float a = min(_Inner, _Outer);
                float b = max(_Inner, _Outer);
                float t = smoothstep(a, b, r);
                t = pow(saturate(t), _Power);

                // neighbour cuts
                float cut = 0.0;
                cut += ContactCut(radialDir, rBase, _JellyContact0);
                cut += ContactCut(radialDir, rBase, _JellyContact1);
                cut += ContactCut(radialDir, rBase, _JellyContact2);
                cut += ContactCut(radialDir, rBase, _JellyContact3);
                cut = saturate(cut);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                half4 col = tex * _Color * IN.color;
                col.a = circle * t * (1.0 - cut) * _EdgeAlpha * _Color.a * IN.color.a;

                return col;
            }
            ENDHLSL
        }
    }
}