Shader "Echelon/Fx/Smoke"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "FxSmoke"
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
                return FxBillboardVertex(input);
            }

            float4 Frag(FxVaryings input) : SV_Target
            {
                float2 q = input.uv * 2.0 - 1.0;
                float seed = input.particle.y * 37.0;
                float n = FxNoise(q * 2.2 + seed) * 0.6 + FxNoise(q * 5.0 - seed) * 0.4;
                float r = length(q) + (n - 0.5) * 0.55;
                float a = 1.0 - smoothstep(0.35, 0.95, r);
                float fadeIn = smoothstep(0.0, 0.06, input.particle.z);
                float3 color = input.color.rgb * (0.82 + 0.32 * (1.0 - input.uv.y)) * (0.9 + 0.2 * n);
                color = MixFog(color, input.fogFactor);
                float alpha = saturate(input.color.a * a * fadeIn);
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
