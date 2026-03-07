Shader "Custom/URP_Sprite_JellyPackedCells_Static"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        _FillAlpha ("Fill Alpha", Range(0,1)) = 1.0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.08)) = 0.01

        _OutlineColor ("Outline Color", Color) = (0.35,0.28,0.08,1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.08)) = 0.015

        // xy = neighbour dir, z = cut position in local radius space, w unused
        [PerRendererData] _JellyContact0 ("Jelly Contact 0", Vector) = (0,0,1,0)
        [PerRendererData] _JellyContact1 ("Jelly Contact 1", Vector) = (0,0,1,0)
        [PerRendererData] _JellyContact2 ("Jelly Contact 2", Vector) = (0,0,1,0)
        [PerRendererData] _JellyContact3 ("Jelly Contact 3", Vector) = (0,0,1,0)
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
                float _FillAlpha;
                float _EdgeSoftness;

                float4 _OutlineColor;
                float _OutlineWidth;

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

            float HalfPlaneMask(float2 p, float4 contact, float softness)
            {
                float2 dir = contact.xy;
                float cutPos = contact.z;

                float len2 = dot(dir, dir);
                if (len2 < 1e-6) return 1.0;

                dir = normalize(dir);

                float s = dot(p, dir);

                return 1.0 - smoothstep(cutPos - softness, cutPos + softness, s);
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // local coordinates: centre = 0, edge radius = 1
                float2 p = (IN.uv - 0.5) * 2.0;
                float r = length(p);

                // full circle
                float circle = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0 + _EdgeSoftness, r);

                // clipped cell body
                float bodyMask = circle;
                bodyMask *= HalfPlaneMask(p, _JellyContact0, _EdgeSoftness);
                bodyMask *= HalfPlaneMask(p, _JellyContact1, _EdgeSoftness);
                bodyMask *= HalfPlaneMask(p, _JellyContact2, _EdgeSoftness);
                bodyMask *= HalfPlaneMask(p, _JellyContact3, _EdgeSoftness);

                // inner mask for outer outline
                float innerCircle = 1.0 - smoothstep(
                    1.0 - _OutlineWidth - _EdgeSoftness,
                    1.0 - _OutlineWidth + _EdgeSoftness,
                    r
                );

                float innerMask = innerCircle;
                innerMask *= HalfPlaneMask(p, _JellyContact0, _EdgeSoftness);
                innerMask *= HalfPlaneMask(p, _JellyContact1, _EdgeSoftness);
                innerMask *= HalfPlaneMask(p, _JellyContact2, _EdgeSoftness);
                innerMask *= HalfPlaneMask(p, _JellyContact3, _EdgeSoftness);

                // outer border ring
                float outlineMask = saturate(bodyMask - innerMask);

                // overlap seam: where neighbour clipping planes pass through the cell interior
                float seam0 = bodyMask * (1.0 - HalfPlaneMask(p, _JellyContact0, _EdgeSoftness * 1.2));
                float seam1 = bodyMask * (1.0 - HalfPlaneMask(p, _JellyContact1, _EdgeSoftness * 1.2));
                float seam2 = bodyMask * (1.0 - HalfPlaneMask(p, _JellyContact2, _EdgeSoftness * 1.2));
                float seam3 = bodyMask * (1.0 - HalfPlaneMask(p, _JellyContact3, _EdgeSoftness * 1.2));

                float seamMask = max(max(seam0, seam1), max(seam2, seam3));
                seamMask = saturate(seamMask);

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // no tint property: use sprite texture * renderer colour only
                half4 baseCol = tex * IN.color;
                baseCol.a *= _FillAlpha;

                half4 finalCol = baseCol * bodyMask;

                // apply same colour to outer outline and overlap seam
                float borderMask = saturate(max(outlineMask, seamMask));
                finalCol.rgb = lerp(finalCol.rgb, _OutlineColor.rgb, borderMask * _OutlineColor.a);
                finalCol.a = max(finalCol.a, borderMask * _OutlineColor.a);

                return finalCol;
            }
            ENDHLSL
        }
    }
}