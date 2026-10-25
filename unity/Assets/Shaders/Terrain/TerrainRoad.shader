Shader "Echelon/Terrain/Road"
{
    Properties
    {
        _Shoulder("Shoulder Fraction", Range(0, 0.5)) = 0.16
        _CenterDash("Center Dash", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
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

            CBUFFER_START(UnityPerMaterial)
                float _Shoulder;
                float _CenterDash;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogCoord : TEXCOORD3;
            };

            Varyings TerrainVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 TerrainFragment(Varyings input) : SV_Target
            {
                float x = input.uv.x;
                float along = input.uv.y;
                float noise = TerrainValueNoise(float2(x * 8.0, along * 0.5));
                float3 asphalt = lerp(float3(0.19, 0.19, 0.20), float3(0.24, 0.24, 0.24), noise);
                float3 dirt = lerp(float3(0.50, 0.45, 0.35), float3(0.42, 0.40, 0.30), noise);
                float edge = min(x, 1.0 - x);
                float paved = smoothstep(_Shoulder - 0.02, _Shoulder + 0.02, edge + (noise - 0.5) * 0.04);
                float3 color = lerp(dirt, asphalt, paved);
                float edgeLine = 1.0 - smoothstep(0.0, 0.025, abs(edge - _Shoulder - 0.04));
                color = lerp(color, float3(0.78, 0.78, 0.74), edgeLine * 0.7 * paved);
                float dash = step(0.5, frac(along / 12.0)) * (1.0 - smoothstep(0.0, 0.02, abs(x - 0.5))) * _CenterDash;
                color = lerp(color, float3(0.85, 0.80, 0.55), dash * 0.8);
                float alpha = smoothstep(0.0, 0.05, edge);
                clip(alpha - 0.5);
                return TerrainShade(color, 1.0, input.positionWS, input.normalWS, input.positionCS, input.fogCoord, 0.15, half3(0.25, 0.25, 0.25));
            }
            ENDHLSL
        }
    }
}
