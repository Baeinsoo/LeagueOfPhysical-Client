//  태풍 구름 벽 — 원통 둘레를 따라 천천히 흐르는 구름 무늬. 시간만 쓰는 그림이라 판정과 무관하다.
//  무늬는 월드 각도로 깐다(벽 조각 48장이 이어져 보이게). 둘레를 정수 칸으로 나눠 노이즈를 감싸서
//  ±180° 자리에 세로 이음매가 생기지 않는다. 밝기는 해(메인 라이트) 색을 따라가 높이별 분위기와 같이 어두워진다.
Shader "LOP/StormWall"
{
    Properties
    {
        _DarkColor ("Cloud Shadow", Color) = (0.32, 0.35, 0.42, 1)
        _LightColor ("Cloud Light", Color) = (0.85, 0.88, 0.94, 1)
        _Scale ("Cloud Size (m)", Float) = 40
        _FlowSpeed ("Flow (deg/s)", Float) = 6
        _Radius ("Cylinder Radius (m)", Float) = 60
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardStorm"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                half4 _LightColor;
                float _Scale;
                float _FlowSpeed;
                float _Radius;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half fog : TEXCOORD1; };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            //  x 방향으로 period 칸마다 되풀이되는 값 노이즈 — 원통을 한 바퀴 돌면 처음 칸과 이어진다.
            float WrapNoise(float2 p, float period)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                //  fmod는 GPU에서 나눗셈 근사라 6.9999995처럼 나온다 — 해시에 들어가면 칸 경계마다 값이 끊겨 세로 줄이 생긴다. 반올림해 늘 정수로 고정.
                float x0 = round(i.x - period * floor(i.x / period));
                x0 = (x0 >= period) ? x0 - period : x0;
                float x1 = (x0 + 1.0 >= period) ? 0.0 : x0 + 1.0;
                float a = Hash(float2(x0, i.y));
                float b = Hash(float2(x1, i.y));
                float c = Hash(float2(x0, i.y + 1.0));
                float d = Hash(float2(x1, i.y + 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float WrapFbm(float2 p, float period)
            {
                float v = 0.0, amp = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += amp * WrapNoise(p, period);
                    p = float2(p.x * 2.0, p.y * 2.0 + 17.1);   // x는 정확히 두 배라 주기도 두 배로 맞물린다
                    period *= 2.0;
                    amp *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                float3 pWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionWS = pWS;
                o.positionCS = TransformWorldToHClip(pWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float cells = 2.0 * max(1.0, round(PI * _Radius / _Scale));   // 항상 짝수 — 노이즈 주기를 절반으로 줄여도 정수라 이음매가 없다
                float turn = atan2(i.positionWS.z, i.positionWS.x) / (2.0 * PI) + _Time.y * _FlowSpeed / 360.0;   // 바퀴 수
                float2 p = float2(turn * cells, i.positionWS.y * 1.8 / _Scale);   // 세로로 눌러 옆으로 길게 — 휘감기는 띠처럼
                float warp = WrapFbm(p * 0.5, cells * 0.5);
                float c = WrapFbm(p + float2(0.0, warp * 1.5), cells);
                half3 col = lerp(_DarkColor.rgb, _LightColor.rgb, smoothstep(0.3, 0.75, c));
                col *= GetMainLight().color * 0.8 + 0.2;   // 높이별 해 색·세기를 따라간다
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
