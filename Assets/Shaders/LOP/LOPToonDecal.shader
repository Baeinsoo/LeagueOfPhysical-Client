Shader "LOP/ToonDecal"
{
    Properties
    {
        _FaceMap ("Face Atlas", 2D) = "white" {}
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
                half3 c = LOPToonShade(i.positionWS, n, v, tex.rgb, _ShadowColor.rgb, _MidThreshold, _LightThreshold,
                                       _Softness, half3(0, 0, 0), 1.0h, 0.0h);
                return half4(MixFog(c, i.fog), tex.a);
            }
            ENDHLSL
        }
    }
}
