#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

void GetShadowAttenuation_float(float3 Position, out float Attenuation) {

    float4 positionCS = TransformObjectToHClip(Position);
    VertexPositionInputs positions = GetVertexPositionInputs(Position.xyz);
    float4 shadowCoordinates = GetShadowCoord(positions);
    Light mainLight = GetMainLight(shadowCoordinates);    
    //Attenuation = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
    Attenuation = mainLight.shadowAttenuation;
}

#endif