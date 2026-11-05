Shader "LOP/ToonDecal"
{
    Properties
    {
        _FaceMap ("Face Atlas", 2D) = "white" {}
        //  몸과 같은 렌더러 블록으로 들어오는 값 — 나간 선수 회색(_BaseColor)·위험도 빛(_Rim*). 기본값이면 지금 그대로 그린다.
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _RimColor ("Rim Color", Color) = (1, 0.96, 0.88, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimStrength ("Rim Strength", Range(0, 1)) = 0
        _ShadowColor ("Shadow Color", Color) = (0.62, 0.64, 0.86, 1)
        _MidThreshold ("Mid Threshold", Range(0, 1)) = 0.45
        _LightThreshold ("Light Threshold", Range(0, 1)) = 0.72
        _Softness ("Softness", Range(0.001, 0.2)) = 0.04
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _FaceMap_ST;
            half4 _BaseColor;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _ShadowColor;
            half _MidThreshold;
            half _LightThreshold;
            half _Softness;
        CBUFFER_END
        TEXTURE2D(_FaceMap);
        SAMPLER(sampler_FaceMap);
        ENDHLSL

        //  얼굴 판: 머리와 같은 음영을 받아야 판만 떠 보이지 않는다.
        Pass
        {
            Name "ForwardDecal"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #include "LOPToonLighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.uv = TRANSFORM_TEX(i.uv, _FaceMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_FaceMap, sampler_FaceMap, i.uv);
                half3 n = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half3 c = LOPToonShade(i.positionWS, n, v, tex.rgb * _BaseColor.rgb, _ShadowColor.rgb, _MidThreshold, _LightThreshold,
                                       _Softness, _RimColor.rgb, _RimPower, _RimStrength);
                return half4(MixFog(c, i.fog), tex.a);
            }
            ENDHLSL
        }
    }
}
