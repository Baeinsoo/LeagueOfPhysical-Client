// 투석기 표적이 터질 때의 파티클·고리. 텍스처 없이 UV로 모양을 만든다(꽉 찬 조각·부드러운 점·고리).
// 섞기는 재질 값으로 고른다 — 불티는 더하기(SrcAlpha, One), 나머지는 보통(SrcAlpha, OneMinusSrcAlpha).
// 정점 색(파티클의 색·알파 사라짐) × _Color. 번쩍임은 정점 색이 없는 표적 메시에 씌우므로 _UseVertexColor = 0으로 정점 색을 무시한다(곱하면 검게 나온다).
Shader "LOP/FxParticle"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        [Enum(Solid, 0, Dot, 1, Ring, 2)] _Shape ("Shape", Float) = 1
        _RingWidth ("Ring Width", Range(0.02, 0.5)) = 0.12
        [ToggleUI] _UseVertexColor ("Use Vertex Color", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Shape;
                float _RingWidth;
                float _UseVertexColor;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = lerp(half4(1, 1, 1, 1), v.color, (half)_UseVertexColor) * _Color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;   // 가운데 0, 가장자리 1
                half a = 1;
                if (_Shape > 1.5)
                {
                    float inner = 1.0 - _RingWidth * 2.0;
                    a = smoothstep(1.0, 0.92, d) * smoothstep(inner - 0.08, inner, d);
                }
                else if (_Shape > 0.5)
                {
                    a = saturate(1.0 - d);
                    a *= a;
                }
                return half4(i.color.rgb, i.color.a * a);
            }
            ENDHLSL
        }
    }
}
