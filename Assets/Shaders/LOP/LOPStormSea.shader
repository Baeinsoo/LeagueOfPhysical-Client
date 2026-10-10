//  번개 치는 먹구름 바다 — 정령 아래 바닥 구름. 어두운 보랏빛 구름 무늬 위로, 칸마다 정해진 무작위 순간에
//  속에서 번개가 번쩍인다(시간을 칸 번호와 섞은 해시라 같은 시각엔 늘 같은 칸이 켜진다). 그림만 — 판정과 무관.
Shader "LOP/StormSea"
{
    Properties
    {
        _DarkColor ("Cloud Shadow", Color) = (0.13, 0.11, 0.18, 1)
        _LightColor ("Cloud Top", Color) = (0.36, 0.30, 0.42, 1)
        _Scale ("Cloud Size (m)", Float) = 50
        [HDR] _FlashColor ("Flash", Color) = (2.2, 2.2, 3.0, 1)
        _FlashCell ("Flash Cell (m)", Float) = 70
        _FlashRate ("Flash Slots per Second", Float) = 1.5
        _FlashChance ("Flash Chance per Slot", Range(0, 1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardStormSea"
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
                half4 _FlashColor;
                float _FlashCell;
                float _FlashRate;
                float _FlashChance;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half fog : TEXCOORD1; };

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, amp = 0.5;
                for (int k = 0; k < 4; k++) { v += amp * Noise(p); p = p * 2.03 + 17.1; amp *= 0.5; }
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
                float2 xz = i.positionWS.xz;
                float c = Fbm(xz / _Scale + float2(_Time.y * 0.02, 0.0));   // 아주 천천히 흘러간다
                half3 col = lerp(_DarkColor.rgb, _LightColor.rgb, smoothstep(0.35, 0.8, c));
                col *= GetMainLight().color * 0.6 + 0.4;   // 위에서 금빛 노을을 받는다

                //  번개: 칸마다, 시간 칸마다 한 번 제비뽑기 → 뽑히면 확 켜졌다 빠르게 꺼진다. 칸 가운데서 둥글게 퍼진다.
                float2 cell = floor(xz / _FlashCell);
                float slot = fmod(floor(_Time.y * _FlashRate), 997.0);   // 해시에 넣기 전에 작게 — 커지면 소수 정밀도가 깨져 번쩍임이 규칙적으로 변한다
                float pick = step(1.0 - _FlashChance, Hash(cell + slot * float2(0.37, 1.91)));
                float fade = 1.0 - frac(_Time.y * _FlashRate);
                float2 local = frac(xz / _FlashCell) - 0.5;
                float flash = pick * fade * fade * fade * saturate(1.0 - length(local) * 2.0) * (0.5 + c);
                col += _FlashColor.rgb * flash;

                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
