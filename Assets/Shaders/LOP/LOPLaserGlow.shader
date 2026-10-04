//  레이저 빛 번짐 — 가산 합성. 원기둥 가장자리로 갈수록 옅어져(시선과 법선의 각) 둥근 빛 줄기로 보인다.
//  심지(_Falloff 0 — 고르게 밝음)·번짐 껍데기·창 테두리가 같이 쓴다. 굵기 = 판정 굵기(SkydiveLaserView.GlowThickness).
//  깊이 옅어짐: 그물이 여러 장 겹치면 원근만으로는 앞뒤를 못 가른다 — 내 캐릭터(_LaserFadeOrigin, 없으면 카메라)보다 아래로 멀수록 옅게,
//  지나온 위쪽은 거의 지운다. 밝기가 곧 깊이다(셰이더라 빔 수와 무관하게 공짜).
//  계단(10m 간격)에서 바로 아래 한 장만 밝고 다음 장은 1/3이 되게 짧게 옅어진다. 가장 먼 것도 0.18은 남겨 윤곽은 보인다
//  (피라미드처럼 멀리 미리 읽어야 하는 맵에선 약하다 — 그 맵은 이 시제품 결과로 다시 짠다).
Shader "LOP/LaserGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2.4, 0.3, 0.4, 1)
        _Falloff ("Edge Falloff", Range(0, 6)) = 1.6
        _FadeNear ("Fade Start (m below player)", Float) = 2
        _FadeFar ("Fade End (m below player)", Float) = 20
        _FadeMin ("Far Brightness", Range(0, 1)) = 0.18
        _FadeAbove ("Passed (above player) Brightness", Range(0, 1)) = 0.05
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

            float4 _LaserFadeOrigin;   // 전역(SkydiveLaserView) — w=1 유효

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Falloff;
                float _FadeNear;
                float _FadeFar;
                half _FadeMin;
                half _FadeAbove;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; half fog : TEXCOORD2; float worldY : TEXCOORD3; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 pWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(pWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(pWS);
                o.worldY = pWS.y;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half facing = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half glow = _Falloff > 0.001h ? pow(facing, _Falloff) : 1.0h;

                float originY = _LaserFadeOrigin.w > 0.5 ? _LaserFadeOrigin.y : _WorldSpaceCameraPos.y;
                float below = originY - i.worldY;   // 양수 = 기준보다 아래
                half depth = lerp(1.0h, _FadeMin, saturate((below - _FadeNear) / max(_FadeFar - _FadeNear, 0.01)));
                //  위로 3m 넘게 지나온 것은 거의 지운다(몸 높이·한 틱 이동 여유).
                depth = below < -3.0 ? _FadeAbove : depth;
                glow *= depth;
                //  가산이라 안개는 색을 0 쪽으로 섞는다(멀면 사라진다).
                half3 c = MixFogColor(_Color.rgb * glow, half3(0, 0, 0), i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
