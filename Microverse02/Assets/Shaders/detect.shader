// Shader "Custom/URP_Sprite_DetectRadial_2022_Wiggle"
// {
//     Properties
//     {
//         [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
//         _Color ("Tint", Color) = (1,1,1,1)

//         _EdgeAlpha ("Edge Alpha", Range(0,1)) = 0.5
//         _Power ("Falloff Power", Range(0.1, 8)) = 3.0
//         _Inner ("Inner (0-1)", Range(0,1)) = 0.0
//         _Outer ("Outer (0-1)", Range(0,1)) = 1.0
//         _CircleSoftness ("Circle Softness", Range(0.001, 0.2)) = 0.02

//         // Wiggle controls
//         _WiggleAmp   ("Wiggle Amplitude", Range(0, 0.2)) = 0.04
//         _WiggleFreq  ("Wiggle Frequency", Range(0, 24))  = 10
//         _WiggleSpeed ("Wiggle Speed", Range(0, 10))      = 2
//         _WiggleRadial("Wiggle Radial Bias", Range(0, 4))  = 1.5
//     }

//     SubShader
//     {
//         Tags
//         {
//             "RenderPipeline"="UniversalPipeline"
//             "Queue"="Transparent"
//             "RenderType"="Transparent"
//             "IgnoreProjector"="True"
//             "CanUseSpriteAtlas"="True"
//         }

//         Pass
//         {
//             Name "SpriteUnlit"
//             Tags { "LightMode"="Universal2D" }

//             Blend SrcAlpha OneMinusSrcAlpha
//             ZWrite Off
//             Cull Off

//             HLSLPROGRAM
//             #pragma vertex vert
//             #pragma fragment frag

//             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

//             TEXTURE2D(_MainTex);
//             SAMPLER(sampler_MainTex);

//             struct Attributes
//             {
//                 float4 positionOS : POSITION;
//                 float2 uv         : TEXCOORD0;
//                 float4 color      : COLOR;
//             };

//             struct Varyings
//             {
//                 float4 positionHCS : SV_POSITION;
//                 float2 uv          : TEXCOORD0;
//                 float4 color       : COLOR;
//             };

//             CBUFFER_START(UnityPerMaterial)
//                 float4 _Color;
//                 float _EdgeAlpha;
//                 float _Power;
//                 float _Inner;
//                 float _Outer;
//                 float _CircleSoftness;

//                 float _WiggleAmp;
//                 float _WiggleFreq;
//                 float _WiggleSpeed;
//                 float _WiggleRadial;
//             CBUFFER_END

//             Varyings vert (Attributes IN)
//             {
//                 Varyings OUT;
//                 OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
//                 OUT.uv = IN.uv;
//                 OUT.color = IN.color;
//                 return OUT;
//             }

//             half4 frag (Varyings IN) : SV_Target
//             {
//                 // radial distance 0 centre -> 1 edge
//                 float2 p = IN.uv - 0.5;

//                 // base radius (0..~1)
//                 float rBase = length(p) * 2.0;

//                 // angle around centre (-pi..pi)
//                 float ang = atan2(p.y, p.x);

//                 // time
//                 float time = _Time.y * _WiggleSpeed;

//                 // wiggle: sine travelling around the circumference, plus slight radial dependency
//                 // (radial dependency helps avoid the entire disc shifting as one piece)
//                 float wiggle =
//                     sin(ang * _WiggleFreq + time) *
//                     (1.0 + rBase * _WiggleRadial) *
//                     _WiggleAmp;

//                 float r = rBase + wiggle;

//                 // circle cutout, independent of texture alpha
//                 float circle = 1.0 - smoothstep(1.0 - _CircleSoftness, 1.0 + _CircleSoftness, r);

//                 // outer stronger alpha ramp
//                 float a = min(_Inner, _Outer);
//                 float b = max(_Inner, _Outer);
//                 float t = smoothstep(a, b, r);
//                 t = pow(saturate(t), _Power);

//                 half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

//                 half4 col = tex * _Color * IN.color;
//                 col.a = circle * t * _EdgeAlpha * _Color.a * IN.color.a;
//                 return col;
//             }
//             ENDHLSL
//         }
//     }
// }
Shader "Custom/URP_Microbe_MicroscopeBlob_Wiggle"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Shape
        _Inner ("Inner (0-1)", Range(0,1)) = 0.00
        _Outer ("Outer (0-1)", Range(0,1)) = 1.00
        _Power ("Falloff Power", Range(0.1, 10)) = 2.5
        _CircleSoftness ("Circle Softness", Range(0.0005, 0.2)) = 0.02

        // Wiggle
        _WiggleAmp   ("Wiggle Amplitude", Range(0, 0.25)) = 0.05
        _WiggleFreq  ("Wiggle Frequency", Range(0, 40))  = 14
        _WiggleSpeed ("Wiggle Speed", Range(0, 10))      = 2
        _WiggleRadial("Wiggle Radial Bias", Range(0, 6))  = 2.0

        // Microscope look
        _FluidAlpha ("Fluid Alpha", Range(0,1)) = 0.70
        _BaseCol    ("Base Colour", Color) = (0.10, 0.35, 0.35, 1)
        _InnerCol   ("Inner Colour", Color) = (0.25, 0.55, 0.35, 1)
        _RimCol     ("Membrane Rim Colour", Color) = (0.95, 0.90, 0.70, 1)
        _RimWidth   ("Rim Width", Range(0.001, 0.25)) = 0.06
        _RimPower   ("Rim Power", Range(0.5, 10)) = 4.0

        _SpecStrength ("Spec Strength", Range(0, 8)) = 0.9
        _SpecSharpness("Spec Sharpness", Range(2, 128)) = 32

        _GrainScale    ("Grain Scale", Range(1, 200)) = 65
        _GrainStrength ("Grain Strength", Range(0, 2)) = 0.55
        _ClumpScale    ("Clump Scale", Range(1, 40)) = 10
        _ClumpStrength ("Clump Strength", Range(0, 2)) = 0.65

        _ChromAber ("Chromatic Aberration", Range(0, 0.01)) = 0.003
        _EdgeGlow  ("Edge Glow", Range(0, 2)) = 0.6
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

                float _Inner;
                float _Outer;
                float _Power;
                float _CircleSoftness;

                float _WiggleAmp;
                float _WiggleFreq;
                float _WiggleSpeed;
                float _WiggleRadial;

                float _FluidAlpha;
                float4 _BaseCol;
                float4 _InnerCol;
                float4 _RimCol;
                float _RimWidth;
                float _RimPower;

                float _SpecStrength;
                float _SpecSharpness;

                float _GrainScale;
                float _GrainStrength;
                float _ClumpScale;
                float _ClumpStrength;

                float _ChromAber;
                float _EdgeGlow;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            // ---- tiny hash/value noise (fast, no textures) ----
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1,0));
                float c = hash21(i + float2(0,1));
                float d = hash21(i + float2(1,1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.55;
                for (int i = 0; i < 4; i++)
                {
                    v += a * noise(p);
                    p *= 2.02;
                    a *= 0.5;
                }
                return v;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 p  = uv - 0.5;

                // radial distance 0 centre -> 1 edge
                float rBase = length(p) * 2.0;
                float ang   = atan2(p.y, p.x);
                float time  = _Time.y * _WiggleSpeed;

                float wiggle =
                    sin(ang * _WiggleFreq + time) *
                    (1.0 + rBase * _WiggleRadial) *
                    _WiggleAmp;

                float r = rBase + wiggle;

                // circle mask (procedural)
                float circle = 1.0 - smoothstep(1.0 - _CircleSoftness, 1.0 + _CircleSoftness, r);

                // optional sprite alpha mask (keeps your sprite workflow)
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                float shapeMask = circle * tex.a;

                // edge ramp (your original idea, but used for body thickness)
                float a0 = min(_Inner, _Outer);
                float b0 = max(_Inner, _Outer);
                float tEdge = smoothstep(a0, b0, r);
                tEdge = pow(saturate(tEdge), _Power);

                // fake “spherical” normal for wet lighting
                float rr = saturate(r);                 // 0..1
                float z  = sqrt(saturate(1.0 - rr*rr)); // hemisphere
                float3 N = normalize(float3(p.x, p.y, z));
                float3 V = float3(0,0,1);

                // light coming from top-left like your photo
                float3 L = normalize(float3(-0.45, 0.65, 0.6));

                float ndl = saturate(dot(N, L));

                // membrane rim (brighter near edge + slight glow)
                float rimBand = smoothstep(1.0 - _RimWidth, 1.0, rr);
                float rim = pow(rimBand, _RimPower);

                // internal grain + clumps (microscope debris / granules)
                float2 nUV = uv;
                nUV += 0.02 * float2(sin(time * 0.7), cos(time * 0.9)); // slow drift

                float grain = fbm(nUV * _GrainScale);
                float clump = fbm(nUV * _ClumpScale);

                // bias clumps towards centre, like your reference
                float centreBias = pow(saturate(1.0 - rr), 0.65);
                float interior = (grain * _GrainStrength) + (clump * _ClumpStrength * centreBias);

                // colour blend: base -> inner colour with interior texture
                float3 baseCol  = _BaseCol.rgb;
                float3 innerCol = _InnerCol.rgb;
                float3 fluidCol = lerp(baseCol, innerCol, saturate(0.35 + interior));

                // subtle chromatic aberration at edge (colour fringe)
                float fringe = smoothstep(0.65, 1.0, rr) * _ChromAber;
                float2 dir = (rBase > 1e-5) ? (p / (rBase*0.5)) : float2(0,0);
                float2 uvR = uv + dir * fringe;
                float2 uvB = uv - dir * fringe;

                half aR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvR).a;
                half aB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvB).a;

                // spec highlight (wet shine)
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), _SpecSharpness) * _SpecStrength;

                // final shading composition
                float edgeGlow = rim * _EdgeGlow;
                float bodyLight = lerp(0.55, 1.15, ndl); // soft contrast

                float3 col =
                    fluidCol * bodyLight +
                    _RimCol.rgb * (rim + edgeGlow) +
                    spec;

                // make alpha thicker towards edge + preserve sprite alpha
                float alpha =
                    shapeMask *
                    _FluidAlpha *
                    saturate(0.25 + 0.75 * (1.0 - rr)) * // more opaque in the middle
                    (0.6 + 0.4 * tEdge);

                // add a touch of “membrane opacity” at rim
                alpha = saturate(alpha + rim * 0.25);

                // apply per-sprite tint/vertex colour
                col *= (_Color.rgb * IN.color.rgb);
                alpha *= (_Color.a * IN.color.a);

                // tiny edge fringing via alpha differences (R/B sample)
                // (kept subtle so it doesn’t look like a bug)
                alpha *= saturate(0.85 + 0.15 * (aR + aB) * 0.5);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
}