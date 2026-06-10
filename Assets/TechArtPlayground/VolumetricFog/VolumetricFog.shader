Shader "Hidden/Custom/VolumetricFog"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "VolumetricFogRaymarch"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Anisotropy;
            float _Density;
            float _MaxDistance;
            int _MaxSteps;
            float4 _Tint;

            // Particle Data Arrays
            int _ParticleCount;
            float4 _ParticlePositions[32]; // xyz: position, w: radius
            float _ParticleDensities[32];

            float HGPhase(float cosTheta, float g)
            {
                float g2 = g * g;
                return (1.0 - g2) / (4.0 * PI * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
            }

            // Calculates combined Global + Local Particle density at a specific world position
            float GetDynamicDensity(float3 worldPos)
            {
                float accumulatedDensity = _Density;

                // Evaluate analytical sphere particles
                for (int p = 0; p < _ParticleCount; p++)
                {
                    float3 pCenter = _ParticlePositions[p].xyz;
                    float pRadius = _ParticlePositions[p].w;
                    float pDensityMax = _ParticleDensities[p];

                    float d = distance(worldPos, pCenter);
                    
                    if (d < pRadius)
                    {
                        // Cubic smooth step falloff towards particle edge
                        float t = d / pRadius;
                        float falloff = 1.0 - (3.0 * t * t - 2.0 * t * t * t);
                        accumulatedDensity += falloff * pDensityMax;
                    }
                }
                return accumulatedDensity;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 originalColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                
                float rawDepth = SampleSceneDepth(uv);
                
                // Skybox mask optimization
                if (rawDepth < 0.00001 || rawDepth > 0.99999) return originalColor; 

                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP) - rayOrigin);
                float maxDist = min(length(ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP) - rayOrigin), _MaxDistance);
                
                float stepSize = maxDist / (float)_MaxSteps;
                float3 currentPos = rayOrigin;
                
                Light mainLight = GetMainLight();
                float phase = HGPhase(dot(rayDir, mainLight.direction), _Anisotropy);
                
                float3 accumulatedLight = 0.0;
                float transmittance = 1.0;
                const float extinctionCoeff = 0.05;

                [loop]
                for (int i = 0; i < _MaxSteps; i++)
                {
                    // Sample the dynamic density (Global + Local Particles combined)
                    float sampleDensity = GetDynamicDensity(currentPos);

                    if (sampleDensity > 0.0)
                    {
                        float4 shadowCoord = TransformWorldToShadowCoord(currentPos);
                        float shadowAtten = MainLightRealtimeShadow(shadowCoord);
                        
                        float3 stepLight = mainLight.color * shadowAtten * phase * _Tint.rgb;
                        
                        // Accumulate based on local step density
                        float extinction = exp(-sampleDensity * extinctionCoeff * stepSize);
                        accumulatedLight += transmittance * stepLight * sampleDensity * stepSize;
                        transmittance *= extinction;
                        
                        if (transmittance < 0.01) break;
                    }
                    
                    currentPos += rayDir * stepSize;
                }
                
                return float4(originalColor.rgb * transmittance + accumulatedLight, 1.0);
            }
            ENDHLSL
        }
    }
}