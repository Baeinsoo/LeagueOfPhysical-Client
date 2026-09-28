#ifndef LOP_TOON_LIGHTING_INCLUDED
#define LOP_TOON_LIGHTING_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

//  원신식 소프트 툰: 반쯤 램버트 × 그림자를 3단(그림자·중간·밝음)으로 끊고 경계만 살짝 흐린다.
//  그림자는 검정이 아니라 푸른 보라(shadowColor)로 떨어진다. 림은 밝은 쪽에만.
half3 LOPToonShade(float3 positionWS, half3 normalWS, half3 viewDirWS, half3 albedo,
                   half3 shadowColor, half midThreshold, half lightThreshold, half softness,
                   half3 rimColor, half rimPower, half rimStrength)
{
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
    Light light = GetMainLight(shadowCoord);
    half ndl = dot(normalWS, light.direction) * 0.5h + 0.5h;
    half lit = ndl * lerp(0.35h, 1.0h, light.shadowAttenuation);
    half b1 = smoothstep(midThreshold - softness, midThreshold + softness, lit);
    half b2 = smoothstep(lightThreshold - softness, lightThreshold + softness, lit);
    half3 midTint = lerp(shadowColor, half3(1.0h, 1.0h, 1.0h), 0.6h);
    half3 tint = lerp(shadowColor, lerp(midTint, half3(1.0h, 1.0h, 1.0h), b2), b1);

    half3 color = albedo * tint * light.color;
    color += albedo * SampleSH(normalWS) * 0.35h;
    half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), rimPower) * rimStrength * b1;
    color += rimColor * rim;
    return color;
}

#endif
