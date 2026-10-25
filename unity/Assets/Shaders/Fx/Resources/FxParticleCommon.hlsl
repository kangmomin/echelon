#ifndef ECHELON_FX_PARTICLE_COMMON
#define ECHELON_FX_PARTICLE_COMMON

struct FxAttributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct FxVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : TEXCOORD1;
    float3 particle : TEXCOORD2;
    float fogFactor : TEXCOORD3;
};

UNITY_INSTANCING_BUFFER_START(FxProps)
    UNITY_DEFINE_INSTANCED_PROP(float4, _FxColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _FxParams)
UNITY_INSTANCING_BUFFER_END(FxProps)

float FxHash(float2 p)
{
    float3 p3 = frac(p.xyx * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float FxNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(FxHash(i), FxHash(i + float2(1.0, 0.0)), u.x),
        lerp(FxHash(i + float2(0.0, 1.0)), FxHash(i + float2(1.0, 1.0)), u.x), u.y);
}

FxVaryings FxBillboardVertex(FxAttributes input)
{
    FxVaryings output = (FxVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    float4 color = UNITY_ACCESS_INSTANCED_PROP(FxProps, _FxColor);
    float4 parameters = UNITY_ACCESS_INSTANCED_PROP(FxProps, _FxParams);
    float3 center = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
    float size = length(float3(UNITY_MATRIX_M._m00, UNITY_MATRIX_M._m10, UNITY_MATRIX_M._m20));
    float c = cos(parameters.x);
    float s = sin(parameters.x);
    float2 vertex = float2(c * input.positionOS.x - s * input.positionOS.y,
        s * input.positionOS.x + c * input.positionOS.y) * size;
    float3 cameraRight = float3(UNITY_MATRIX_V._m00, UNITY_MATRIX_V._m01, UNITY_MATRIX_V._m02);
    float3 cameraUp = float3(UNITY_MATRIX_V._m10, UNITY_MATRIX_V._m11, UNITY_MATRIX_V._m12);
    float3 worldPosition = center + cameraRight * vertex.x + cameraUp * vertex.y;
    output.positionCS = TransformWorldToHClip(worldPosition);
    output.uv = input.uv;
    output.color = color;
    output.particle = parameters.xyz;
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}

FxVaryings FxGroundVertex(FxAttributes input)
{
    FxVaryings output = (FxVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    float4 worldPosition = mul(UNITY_MATRIX_M, input.positionOS);
    output.positionCS = TransformWorldToHClip(worldPosition.xyz);
    output.uv = input.uv;
    output.color = UNITY_ACCESS_INSTANCED_PROP(FxProps, _FxColor);
    output.particle = UNITY_ACCESS_INSTANCED_PROP(FxProps, _FxParams).xyz;
    output.fogFactor = ComputeFogFactor(output.positionCS.z);
    return output;
}

#endif
