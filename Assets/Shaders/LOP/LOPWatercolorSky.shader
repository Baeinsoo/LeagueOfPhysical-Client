Shader "LOP/WatercolorSky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.373, 0.663, 0.878, 1)
        _HorizonColor ("Horizon", Color) = (0.663, 0.839, 0.941, 1)
        _BottomColor ("Bottom", Color) = (0.957, 0.914, 0.839, 1)
        _CloudColor ("Cloud", Color) = (1, 1, 1, 1)
        _CloudShadow ("Cloud Shadow", Color) = (0.80, 0.84, 0.94, 1)
        _CloudCover ("Cloud Cover", Range(0, 1)) = 0.42
        _CloudScale ("Cloud Scale", Float) = 1.3
        _CloudSoftness ("Cloud Softness", Range(0.01, 0.4)) = 0.14
        _CloudSpeed ("Cloud Speed", Float) = 0.004
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _HorizonColor;
                half4 _BottomColor;
                half4 _CloudColor;
                half4 _CloudShadow;
                half _CloudCover;
                half _CloudSoftness;
                float _CloudScale;
                float _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * valueNoise(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return v;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                half3 col = h > 0 ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(h), 0.6))
                                  : lerp(_HorizonColor.rgb, _BottomColor.rgb, saturate(-h * 4.0));
                if (h > 0.0)
                {
                    //  위로 갈수록 구름이 작아 보이게 평면에 투영한다.
                    float2 uv = d.xz / (h + 0.25) * _CloudScale + _Time.y * _CloudSpeed;
                    float n = fbm(uv);
                    half mask = smoothstep(1.0 - _CloudCover - _CloudSoftness, 1.0 - _CloudCover + _CloudSoftness, n)
                              * saturate(h * 6.0);
                    //  아래쪽이 살짝 어두운 수채화 번짐.
                    half shade = saturate(fbm(uv + float2(0.0, 0.15)) * 1.3);
                    col = lerp(col, lerp(_CloudShadow.rgb, _CloudColor.rgb, shade), mask * 0.9h);
                }
                col += (hash21(d.xz * 900.0) - 0.5) * 0.012;   // 종이 결
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
}
