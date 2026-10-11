//  정령의 얼굴 — 사각 판(Quad) 하나를 늘 카메라 쪽으로 세워 눈 둘을 그린다. 스크립트 없이 셰이더만으로 돌린다
//  (맵 씬은 서버도 받으므로 클라 전용 스크립트를 넣지 않는다). 판 크기는 트랜스폼 크기를 그대로 쓴다.
Shader "LOP/SpiritFace"
{
    Properties
    {
        _EyeColor ("Eye Color", Color) = (0.22, 0.16, 0.12, 1)
        _EyeGap ("Eye Gap (0..0.5)", Range(0, 0.5)) = 0.17
        _EyeSize ("Eye Size (w, h)", Vector) = (0.07, 0.11, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "DisableBatching" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _EyeColor;
                half _EyeGap;
                half4 _EyeSize;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                //  물체 가운데를 시점 공간으로 옮긴 뒤, 꼭짓점을 화면과 나란하게 펼친다 = 늘 카메라를 본다.
                float3 centerVS = TransformWorldToView(TransformObjectToWorld(float3(0, 0, 0)));
                float scale = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                centerVS.xy += i.positionOS.xy * scale;
                o.positionCS = TransformWViewToHClip(centerVS);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv - 0.5;
                float2 l = (p - float2(-_EyeGap, 0.04)) / _EyeSize.xy;
                float2 r = (p - float2(_EyeGap, 0.04)) / _EyeSize.xy;
                float d = min(length(l), length(r));
                half a = 1.0h - smoothstep(0.85, 1.0, d);   // 가장자리만 살짝 부드럽게
                clip(a - 0.01h);
                return half4(_EyeColor.rgb, a);
            }
            ENDHLSL
        }
    }
}
