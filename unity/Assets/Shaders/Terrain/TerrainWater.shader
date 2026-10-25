Shader "Echelon/Terrain/Water"
{
    Properties
    {
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 tangentWS : TEXCOORD3;
                half3 bitangentWS : TEXCOORD4;
                half fogCoord : TEXCOORD5;
            };

            Varyings TerrainVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                output.bitangentWS = cross(output.normalWS, output.tangentWS) * input.tangentOS.w;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 TerrainFragment(Varyings input) : SV_Target
            {
                float2 p = float2(input.uv.x * 30.0, input.uv.y);
                float time = _Time.y;
                float ripple = TerrainValueNoise(p * 0.3 + float2(0.0, -time * 1.2)) * 0.6
                    + TerrainValueNoise(p * 0.9 + float2(time * 0.5, -time * 2.0)) * 0.4;
                float edge = min(input.uv.x, 1.0 - input.uv.x);
                float3 deep = float3(0.08, 0.20, 0.26);
                float3 shallow = float3(0.22, 0.36, 0.36);
                float3 color = lerp(shallow, deep, smoothstep(0.0, 0.35, edge));
                color += float3(0.05, 0.06, 0.06) * ripple;
                color = lerp(color, float3(0.62, 0.66, 0.64), (1.0 - smoothstep(0.0, 0.06, edge)) * 0.5);

                half3 rippleNormal = normalize(half3(ripple - 0.5, 0.5 - TerrainValueNoise(p * 1.7 + time), 2.0));
                half3 normalWS = normalize(
                    rippleNormal.x * input.tangentWS
                    + rippleNormal.y * input.bitangentWS
                    + rippleNormal.z * input.normalWS);
                half alpha = smoothstep(0.0, 0.08, edge) * 0.92;
                return TerrainShade(color, alpha, input.positionWS, normalWS, input.positionCS, input.fogCoord, 0.88 - 0.1 * ripple, half3(0.6, 0.6, 0.6));
            }
            ENDHLSL
        }
    }
}
