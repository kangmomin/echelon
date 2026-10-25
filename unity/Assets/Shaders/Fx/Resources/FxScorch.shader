Shader "Echelon/Fx/Scorch"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-1" }
        Pass
        {
            Name "FxScorch"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FxParticleCommon.hlsl"

            FxVaryings Vert(FxAttributes input)
            {
                return FxGroundVertex(input);
            }

            float4 Frag(FxVaryings input) : SV_Target
            {
                float2 q = input.uv * 2.0 - 1.0;
                float seed = input.particle.y;
                float n = FxNoise(q * 3.0 + seed * 41.0) * 0.6 + FxNoise(q * 7.0 - seed * 13.0) * 0.4;
                float r = length(q) + (n - 0.5) * 0.6;
                float a = 1.0 - smoothstep(0.2, 0.9, r);
                float3 color = lerp(float3(0.05, 0.045, 0.04), float3(0.22, 0.19, 0.15), smoothstep(0.1, 0.8, r));
                color = MixFog(color, input.fogFactor);
                return float4(color, input.color.a * a * 0.85);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
