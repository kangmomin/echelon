#ifndef ECHELON_TERRAIN_COMMON_INCLUDED
#define ECHELON_TERRAIN_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float TerrainHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float TerrainValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = TerrainHash12(i);
    float b = TerrainHash12(i + float2(1.0, 0.0));
    float c = TerrainHash12(i + float2(0.0, 1.0));
    float d = TerrainHash12(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float TerrainFbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    for (int octave = 0; octave < 4; octave++)
    {
        value += amplitude * TerrainValueNoise(p);
        p = p * 2.03 + float2(17.1, 9.7);
        amplitude *= 0.5;
    }
    return value;
}

half4 TerrainShade(
    float3 albedo,
    half alpha,
    float3 positionWS,
    half3 normalWS,
    float4 positionCS,
    half fogCoord,
    half smoothness,
    half3 specular)
{
    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = albedo;
    surfaceData.alpha = alpha;
    surfaceData.smoothness = smoothness;
    surfaceData.specular = specular;
    surfaceData.normalTS = half3(0.0, 0.0, 1.0);
    surfaceData.occlusion = 1.0;

    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.positionCS = positionCS;
    inputData.normalWS = NormalizeNormalPerPixel(normalWS);
    inputData.viewDirectionWS = half3(GetWorldSpaceNormalizeViewDir(positionWS));
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    inputData.fogCoord = fogCoord;
    inputData.bakedGI = SampleSH(inputData.normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowMask = half4(1.0, 1.0, 1.0, 1.0);

    half4 color = UniversalFragmentPBR(inputData, surfaceData);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    return color;
}

#endif
