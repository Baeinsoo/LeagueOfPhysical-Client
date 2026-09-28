Shader "LOP/Toon"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowColor ("Shadow Color", Color) = (0.62, 0.64, 0.86, 1)
        _MidThreshold ("Mid Threshold", Range(0, 1)) = 0.45
        _LightThreshold ("Light Threshold", Range(0, 1)) = 0.72
        _Softness ("Softness", Range(0.001, 0.2)) = 0.04
        _RimColor ("Rim Color", Color) = (1, 0.96, 0.88, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.35
        _OutlineWidth ("Outline Width (px)", Range(0, 4)) = 1.5
        _OutlineColor ("Outline Color", Color) = (0.25, 0.2, 0.3, 1)
        [Toggle] _UseRegions ("Use Vertex Regions", Float) = 0
        _SkinColor ("Region Skin", Color) = (0.97, 0.82, 0.68, 1)
        _TopColor ("Region Top", Color) = (1, 1, 1, 1)
        _SleeveColor ("Region Sleeve", Color) = (1, 1, 1, 1)
        _BottomColor ("Region Bottom", Color) = (0.23, 0.2, 0.31, 1)
        _ShoeColor ("Region Shoe", Color) = (1, 1, 1, 1)
        _HairColor ("Region Hair", Color) = (0.29, 0.2, 0.13, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        //  SRP 배처: 모든 패스가 같은 재질 버퍼를 본다.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half4 _RimColor;
            half4 _OutlineColor;
            half _MidThreshold;
            half _LightThreshold;
            half _Softness;
            half _RimPower;
            half _RimStrength;
            half _OutlineWidth;
            half _UseRegions;
            half4 _SkinColor;
            half4 _TopColor;
            half4 _SleeveColor;
            half4 _BottomColor;
            half4 _ShoeColor;
            half4 _HairColor;
        CBUFFER_END

        //  정점 색 빨강 채널 = 옷 영역(0 피부 · 1 윗옷 · 2 소매 끝 · 3 바지 · 4 신발 · 5 머리카락). LookDevRegions와 같은 규칙.
        half3 LOPRegionColor(half code)
        {
            half r = round(code * 5.0h);
            half3 c = _SkinColor.rgb;
            c = r > 0.5h ? _TopColor.rgb : c;
            c = r > 1.5h ? _SleeveColor.rgb : c;
            c = r > 2.5h ? _BottomColor.rgb : c;
            c = r > 3.5h ? _ShoeColor.rgb : c;
            c = r > 4.5h ? _HairColor.rgb : c;
            return c;
        }
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "LOPToonLighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
                nointerpolation half region : TEXCOORD4;   // 영역 번호는 섞지 않는다 — 섞으면 사이 영역 색이 줄무늬로 낀다
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.region = i.color.r;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseColor.rgb;
                albedo *= _UseRegions > 0.5h ? LOPRegionColor(i.region) : half3(1.0h, 1.0h, 1.0h);
                half3 n = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 c = LOPToonShade(i.positionWS, n, v, albedo, _ShadowColor.rgb, _MidThreshold, _LightThreshold,
                                       _Softness, _RimColor.rgb, _RimPower, _RimStrength);
                return half4(MixFog(c, i.fog), 1.0h);
            }
            ENDHLSL
        }

        //  외곽선: 뒷면을 법선 방향으로 화면 픽셀만큼 밀어 칠한다. 캐릭터 아닌 재질은 이 패스를 끈다.
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half fog : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float4 cs = TransformObjectToHClip(i.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(i.normalOS);
                float2 nCS = mul((float3x3)GetWorldToHClipMatrix(), nWS).xy;
                float len = max(length(nCS), 1e-5);
                cs.xy += nCS / len * (_OutlineWidth * 2.0 / _ScreenParams.xy) * cs.w;
                o.positionCS = cs;
                o.fog = ComputeFogFactor(cs.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, i.fog), 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

            float4 vert(Attributes i) : SV_POSITION
            {
                float3 ws = TransformObjectToWorld(i.positionOS.xyz);
                float3 nws = TransformObjectToWorldNormal(i.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - ws);
            #else
                float3 ld = _LightDirection;
            #endif
                float4 p = TransformWorldToHClip(ApplyShadowBias(ws, nws, ld));
            #if UNITY_REVERSED_Z
                p.z = min(p.z, UNITY_NEAR_CLIP_VALUE);
            #else
                p.z = max(p.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return p;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; };
            float4 vert(Attributes i) : SV_POSITION { return TransformObjectToHClip(i.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
