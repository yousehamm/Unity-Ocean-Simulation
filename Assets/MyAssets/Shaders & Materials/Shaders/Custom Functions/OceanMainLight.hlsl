#ifndef OCEAN_MAINLIGHT_INCLUDED
#define OCEAN_MAINLIGHT_INCLUDED

#if !defined(SHADERGRAPH_PREVIEW)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

void OceanMainLight_float(float3 PositionWS, out float3 Direction, out float3 Color, out float ShadowAtten)
{
#if defined(SHADERGRAPH_PREVIEW)
    Direction = normalize(float3(0.5, 0.5, 0.5));
    Color = float3(1.0, 1.0, 1.0);
    ShadowAtten = 1.0;
#else
    #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
        float4 shadowCoord = TransformWorldToShadowCoord(PositionWS);
        Light mainLight = GetMainLight(shadowCoord);
        ShadowAtten = mainLight.shadowAttenuation;
    #else
        Light mainLight = GetMainLight();
        ShadowAtten = 1.0;
    #endif

    Direction = mainLight.direction;
    Color = mainLight.color;
#endif
}

#endif