Shader "Echelon/Terrain/Ground"
{
    Properties
    {
        _TypeMask("Terrain Type Mask", 2D) = "white" {}
        _TypeMaskBlur("Terrain Type Mask Mips", 2D) = "white" {}
        _MapSize("Map Size", Vector) = (4000, 4000, 0, 0)
        _HeightMin("Minimum Height", Float) = 20
        _HeightMax("Maximum Height", Float) = 110
        _FieldAngle("Field Angle", Float) = 0.35
        _FieldSize("Field Size", Vector) = (230, 150, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TerrainVertex
            #pragma fragment TerrainFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "TerrainCommon.hlsl"

            TEXTURE2D(_TypeMask);
            SAMPLER(sampler_TypeMask);
            TEXTURE2D(_TypeMaskBlur);
            SAMPLER(sampler_TypeMaskBlur);

            CBUFFER_START(UnityPerMaterial)
                float4 _MapSize;
                float _HeightMin;
                float _HeightMax;
                float _FieldAngle;
                float4 _FieldSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogCoord : TEXCOORD2;
            };

            Varyings TerrainVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            float2 TerrainFieldCell(float2 p, out float2 local, out float rowDirection)
            {
                float c = cos(_FieldAngle);
                float s = sin(_FieldAngle);
                float2 r = float2(c * p.x + s * p.y, -s * p.x + c * p.y);
                float column = floor(r.x / _FieldSize.x);
                r.y += frac(column * 0.6180339) * _FieldSize.y;
                float2 cell = float2(column, floor(r.y / _FieldSize.y));
                local = float2(frac(r.x / _FieldSize.x) * _FieldSize.x, frac(r.y / _FieldSize.y) * _FieldSize.y);
                float edge = min(min(local.x, _FieldSize.x - local.x), min(local.y, _FieldSize.y - local.y));
                float h = TerrainHash12(cell + float2(11.0, 5.0));
                rowDirection = step(0.5, TerrainHash12(cell + 2.0));
                float crop = h < 0.32 ? -1.0 : floor(TerrainHash12(cell * 1.7 + 4.0) * 5.0);
                return float2(crop, edge);
            }

            half4 TerrainFragment(Varyings input) : SV_Target
            {
                float2 simulationPosition = float2(input.positionWS.x, input.positionWS.z);
                float2 uv = simulationPosition / _MapSize.xy;
                float4 mask = SAMPLE_TEXTURE2D_LOD(_TypeMask, sampler_TypeMask, uv, 0.0);
                float wobble = (TerrainValueNoise(simulationPosition * 0.09) - 0.5) * 0.35;
                float water = smoothstep(0.3, 0.55, mask.g + wobble);
                float forest = smoothstep(0.25, 0.6, mask.b + wobble);
                float urban = smoothstep(0.3, 0.6, mask.a + wobble);
                float nearRoad = smoothstep(0.02, 0.18, SAMPLE_TEXTURE2D_LOD(_TypeMaskBlur, sampler_TypeMaskBlur, uv, 3.0).r);
                float nearForest = smoothstep(0.05, 0.4, SAMPLE_TEXTURE2D_LOD(_TypeMaskBlur, sampler_TypeMaskBlur, uv, 2.0).b);

                float noiseBig = TerrainFbm(simulationPosition * 0.0035);
                float noiseMid = TerrainFbm(simulationPosition * 0.02);
                float noiseFine = TerrainValueNoise(simulationPosition * 0.45);
                float heightFactor = saturate((input.positionWS.y - _HeightMin) / max(_HeightMax - _HeightMin, 1.0));

                float3 grassA = float3(0.27, 0.37, 0.17);
                float3 grassB = float3(0.37, 0.43, 0.22);
                float3 color = lerp(grassA, grassB, noiseBig);
                color = lerp(color, color * float3(1.08, 1.04, 0.86), noiseMid * 0.6);
                color = lerp(color, float3(0.55, 0.55, 0.36), heightFactor * 0.25);

                float2 local;
                float rowDirection;
                float2 field = TerrainFieldCell(simulationPosition, local, rowDirection);
                if (field.x >= 0.0)
                {
                    float3 crop;
                    if (field.x < 1.0) crop = float3(0.62, 0.54, 0.28);
                    else if (field.x < 2.0) crop = float3(0.36, 0.28, 0.19);
                    else if (field.x < 3.0) crop = float3(0.30, 0.42, 0.17);
                    else if (field.x < 4.0) crop = float3(0.48, 0.46, 0.28);
                    else crop = float3(0.42, 0.40, 0.24);
                    float rows = 0.5 + 0.5 * sin((rowDirection > 0.5 ? local.x : local.y) * 2.2);
                    crop *= 0.94 + 0.07 * rows;
                    crop *= 0.92 + 0.16 * noiseMid;
                    float inField = smoothstep(2.0, 5.0, field.y) * (1.0 - nearRoad) * (1.0 - nearForest);
                    color = lerp(color, crop, inField * 0.95);
                }
                float margin = (1.0 - smoothstep(0.6, 2.0, field.y)) * (1.0 - nearRoad) * (1.0 - nearForest);
                color = lerp(color, float3(0.30, 0.34, 0.20), margin * 0.7);

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                float slope = 1.0 - saturate(normalWS.y);
                color = lerp(color, float3(0.45, 0.40, 0.33), smoothstep(0.12, 0.35, slope));
                float3 forestFloor = lerp(float3(0.16, 0.22, 0.12), float3(0.24, 0.27, 0.15), noiseMid);
                color = lerp(color, forestFloor, forest);
                float3 town = lerp(float3(0.46, 0.45, 0.40), float3(0.38, 0.40, 0.30), noiseMid);
                color = lerp(color, town, urban * lerp(0.45, 0.8, noiseFine));
                color = lerp(color, color * float3(1.06, 1.05, 0.92), nearRoad * 0.5);
                float bank = smoothstep(0.12, 0.3, mask.g + wobble) - water;
                color = lerp(color, float3(0.36, 0.32, 0.24), saturate(bank) * 0.8);
                color = lerp(color, float3(0.20, 0.22, 0.18), water);
                color *= 0.94 + 0.12 * noiseFine;
                return TerrainShade(color, 1.0, input.positionWS, normalWS, input.positionCS, input.fogCoord, 0.05, half3(0.25, 0.25, 0.25));
            }
            ENDHLSL
        }
    }
}
