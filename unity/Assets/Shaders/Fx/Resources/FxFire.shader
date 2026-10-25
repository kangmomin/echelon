Shader "Echelon/Fx/Fire"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent+1" }
        Pass
        {
            Name "FxFire"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "FxParticleCommon.hlsl"

            FxVaryings Vert(FxAttributes input)
            {
                return FxBillboardVertex(input);
            }

            float4 Frag(FxVaryings input) : SV_Target
            {
                float2 q = input.uv * 2.0 - 1.0;
                float n = FxNoise(q * 3.0 + input.particle.y * 23.0);
                float r = length(q) + (n - 0.5) * 0.35;
                float glow = pow(saturate(1.0 - r), 1.6);
                float core = pow(saturate(1.0 - r * 1.8), 2.0) * (1.0 - input.particle.z);
                float3 color = input.color.rgb + core * 0.8;
                float alpha = saturate(input.color.a * glow);
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
