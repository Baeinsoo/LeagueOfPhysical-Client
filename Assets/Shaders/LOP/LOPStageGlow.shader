//  로비 무대의 빛줄기·플래시 — 조명 없이 색 × 불투명도를 더해 그린다(겹칠수록 밝아짐). 깊이는 쓰지 않는다.
Shader "LOP/StageGlow"
{
    Properties
    {
        _BaseColor ("Color (alpha = 세기)", Color) = (1, 1, 1, 0.2)
        _FadeAlongV ("Fade Along V (0 = 균일, 1 = 위로 갈수록 흐려짐)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "StageGlow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _FadeAlongV;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half fade = lerp(1.0h, 1.0h - (half)i.uv.y, _FadeAlongV);
                return half4(_BaseColor.rgb * _BaseColor.a * fade, 1.0h);
            }
            ENDHLSL
        }
    }
}
