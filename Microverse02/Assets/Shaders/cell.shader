// Shader "Custom/URP_Sprite_DetectRadial_2022"
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
//                 float r = length(p) * 2.0;

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

        // --- Fake 3D lighting ---
        _LightDir ("Light Dir (XY)", Vector) = (-0.35, 0.65, 0, 0)
        _LightStrength ("Light Strength", Range(0, 2)) = 1.0
        _Ambient ("Ambient", Range(0, 1)) = 0.35

        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.35
        _HighlightStrength ("Highlight Strength", Range(0, 2)) = 0.65
        _HighlightSize ("Highlight Size", Range(0.05, 1)) = 0.35

        _RimStrength ("Rim Strength", Range(0, 2)) = 0.25
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.0
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

                float4 _LightDir;
                float _LightStrength;
                float _Ambient;

                float _ShadowStrength;
                float _HighlightStrength;
                float _HighlightSize;

                float _RimStrength;
                float _RimPower;
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
                // UV centred
                float2 p = IN.uv - 0.5;
                float r = length(p) * 2.0; // 0 centre -> 1 edge

                // Circle mask
                float circle = 1.0 - smoothstep(1.0 - _CircleSoftness, 1.0 + _CircleSoftness, r);

                // Your radial alpha ramp (inner->outer)
                float a = min(_Inner, _Outer);
                float b = max(_Inner, _Outer);
                float t = smoothstep(a, b, r);
                t = pow(saturate(t), _Power);

                // Sample sprite (if you want pure flat disc, replace tex with 1)
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half4 col = tex * _Color * IN.color;

                // ===== Fake sphere normal from UV =====
                // Map p into [-1,1] radius space
                float2 xy = p * 2.0;
                float rr = dot(xy, xy);

                // If rr>1, we're outside the disc; keep stable (mask will kill alpha anyway)
                float z = sqrt(saturate(1.0 - rr));
                float3 n = normalize(float3(xy.x, -xy.y, z)); // -y so "top" is positive in screen space feel

                // Light direction from property (XY only, Z inferred)
                float2 ldxy = normalize(_LightDir.xy);
                float3 l = normalize(float3(ldxy.x, ldxy.y, 1.0));

                // Basic light
                float ndl = saturate(dot(n, l));
                float lit = _Ambient + ndl * _LightStrength;

                // Shadow bias: darker as you go away from the light direction
                float shadow = (1.0 - ndl) * _ShadowStrength;

                // Small spec-ish highlight near the top
                // Use ndl and a tight power controlled by HighlightSize
                float highlightPower = lerp(32.0, 4.0, _HighlightSize); // smaller size -> tighter highlight
                float spec = pow(ndl, highlightPower) * _HighlightStrength;

                // Rim light near edge (good for readability)
                float rim = pow(saturate(1.0 - n.z), _RimPower) * _RimStrength;

                // Apply shading to RGB (keep alpha logic separate)
                float shade = saturate(lit - shadow);
                col.rgb = col.rgb * shade + spec + rim;

                // Final alpha uses your existing mask + ramp
                col.a = circle * t * _EdgeAlpha * _Color.a * IN.color.a * tex.a;

                return col;
            }
            ENDHLSL
        }
    }
}