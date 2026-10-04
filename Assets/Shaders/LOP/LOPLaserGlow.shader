//  레이저 빛 번짐 — 가산 합성. 원기둥 가장자리로 갈수록 옅어져(시선과 법선의 각) 둥근 빛 줄기로 보인다.
//  심지(가는 불투명 원기둥)를 감싸는 껍데기로 쓴다. 굵기 = 판정 굵기(SkydiveLaserView.GlowThickness).
Shader "LOP/LaserGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.4, 0.3, 0.4, 1)
        _Falloff ("Edge Falloff", Range(0.5, 6)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Falloff;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; half fog : TEXCOORD2; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 pWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(pWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(pWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half facing = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half glow = pow(facing, _Falloff);
                //  가산이라 안개는 색을 0 쪽으로 섞는다(멀면 사라진다).
                half3 c = MixFogColor(_Color.rgb * glow, half3(0, 0, 0), i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
