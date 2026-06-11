Shader "Hidden/Custom/VolumetricFog"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        // ==============================================================================
        // PASS 0: HALF-RESOLUTION RAYMARCH
        // ==============================================================================
        Pass
        {
            Name "VolumetricFogRaymarch"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTPROBE_SH
            #pragma multi_compile_local _ _VOLUMETRIC_NOISE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Density;
            float _Anisotropy;
            float4 _Tint;
            float4 _EdgeColor;
            float _RayIntensity;
            
            float _BaseHeight;
            float _HeightFalloff; // Pre-multiplied by 1.44269 on the C# side for exp2
            float _AmbientMultiplier;
            
            float _MaxDistance;
            int _MaxSteps;

            TEXTURE3D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            float _NoiseScale;
            float _NoiseIntensity;
            float3 _WindVelocity;

            float HGPhase(float cosTheta, float g)
            {
                float g2 = g * g;
                float num = 1.0 - g2;
                
                // Add an epsilon to the denominator to prevent division by zero near the sun
                float den = max(1.0 + g2 - 2.0 * g * cosTheta, 0.005);
                
                float phase = num / (4.0 * PI * pow(den, 1.5));
                
                // Clamp the absolute maximum brightness to prevent HDR bleeding
                return min(phase, 8.0);
            }

            float4 Frag(Varyings input) : SV_Target
            {
            float2 uv = input.texcoord;
            float rawDepth = SampleSceneDepth(uv);
            
            // FIX: Removed the skybox mask. 
            // The sky must receive fog up to _MaxDistance, otherwise foreground objects 
            // will look like glowing cutouts due to mismatched in-scattering.
            
            float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(worldPos - rayOrigin);
                
                // 1. ISOMETRIC OPTIMIZATION: Ray-Plane Bounding
                // Calculate the logical "top" of the fog where density drops below ~1%
                // using the inverse of our base-2 falloff curve.
                float effectiveMaxHeight = _BaseHeight + (6.6438 / max(0.0001, _HeightFalloff));
                
                // If camera is above the fog looking down, fast-forward the ray to the fog boundary
                if (rayOrigin.y > effectiveMaxHeight && rayDir.y < 0.0)
                {
                    float t = (effectiveMaxHeight - rayOrigin.y) / rayDir.y;
                    rayOrigin += rayDir * max(0.0, t);
                }

                float maxDist = min(length(worldPos - rayOrigin), _MaxDistance);
                
                // FIX: Stop the ray slightly before it hits the physical surface.
                // This prevents the final marching step from landing inside the mesh's shadow bias zone.
                maxDist = max(0.0, maxDist - 0.3);
                
                if (maxDist <= 0.0) return float4(0, 0, 0, 1.0); // Abort if fog is behind or too far
                
                float stepSize = maxDist / (float)_MaxSteps;
                
                // IGN Dithering
                float2 pixelCoord = uv * (_ScreenParams.xy * 0.5); // Adjusted for half-res
                float jitter = frac(52.9829189 * frac(dot(pixelCoord, float2(0.06711056, 0.00583715))));
                float3 currentPos = rayOrigin + rayDir * (stepSize * jitter);
                
                Light mainLight = GetMainLight();
                float cosTheta = dot(rayDir, mainLight.direction);
                
                // 1. DUAL-LOBE PHASE
                float phaseRays = HGPhase(cosTheta, _Anisotropy);
                float phaseCore = HGPhase(cosTheta, 0.99); // Pushed to 0.99 for an ultra-tight core
                float phase = (phaseRays * 0.7) + (phaseCore * 0.3);
                
                float3 scatterColor = lerp(_EdgeColor.rgb, _Tint.rgb, saturate(cosTheta));
                
                // 2. AGGRESSIVE INTENSITY COMPRESSION (Anti-Giant-Sun)
                // 0.80 (~36 degrees away): Start dampening the 19x multiplier.
                // 0.995 (~5 degrees away): Force the multiplier all the way down to a safe 1.0.
                float sunProximity = smoothstep(0.80, 0.995, cosTheta); 
                
                // Lerp from your high _RayIntensity down to 1.0 as the camera looks at the sun
                float dynamicIntensity = lerp(_RayIntensity, 1.0, sunProximity);
                
                float3 accumulatedLight = 0.0;
                float transmittance = 1.0;
                const float extinctionCoeff = 0.05;
                
                [loop]
                for (int i = 0; i < _MaxSteps; i++)
                {
                    // 2. MATH TUNING: Hardware accelerated exp2
                    float heightDensity = exp2(-_HeightFalloff * max(0.0, currentPos.y - _BaseHeight));
                    float finalDensity = _Density * heightDensity;
                    
                    // 3. NOISE TUNING: Single tap, no domain warp
                    #if defined(_VOLUMETRIC_NOISE)
                        float3 baseCoord = currentPos * _NoiseScale + _WindVelocity * _Time.y;
                        float noiseVal = SAMPLE_TEXTURE3D_LOD(_NoiseTex, sampler_LinearRepeat, baseCoord, 0).r;
                        finalDensity *= lerp(1.0, noiseVal, _NoiseIntensity);
                    #endif
                    
                    if (finalDensity > 0.001)
                    {
                        // FIX: Volumetric Shadow Bias
                        // Push the shadow coordinate deeper into the shadow volume (away from the light).
                        // This counteracts Unity's surface bias and kills the glowing edge light leak.
                        float3 shadowSamplePos = currentPos - (mainLight.direction * 0.5);
                        float4 shadowCoord = TransformWorldToShadowCoord(shadowSamplePos);
                        float shadowAtten = MainLightRealtimeShadow(shadowCoord);
                        
                        float3 ambientGI = unity_AmbientSky.rgb;
                        #if defined(LIGHTPROBE_SH)
                            ambientGI = SampleSH(float3(0, 1, 0));
                        #endif
                        
                        float3 stepLight = (mainLight.color * shadowAtten * phase * scatterColor * dynamicIntensity) + (ambientGI * _AmbientMultiplier * _Tint.rgb);
                        
                        float extinction = exp2(-finalDensity * extinctionCoeff * stepSize * 1.44269);
                        accumulatedLight += transmittance * stepLight * finalDensity * stepSize;
                        transmittance *= extinction;
                    }
                    
                    if (transmittance < 0.01) break;
                    currentPos += rayDir * stepSize;
                }
                
                // Output RGB = Accumulated Light, A = Transmittance
                return float4(accumulatedLight, transmittance);
            }
            ENDHLSL
        }

        // ==============================================================================
        // PASS 1: FULL-RESOLUTION COMPOSITE & UPSAMPLE
        // ==============================================================================
        Pass
        {
            Name "VolumetricFogComposite"
            
            // Hardware compositing: (SceneColor * Transmittance) + (AccumulatedLight * 1.0)
            Blend One SrcAlpha 

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                
                // _BlitTexture is currently bound to our Half-Res Fog target
                float2 halfResTexelSize = 2.0 / _ScreenParams.xy; 
                float centerDepth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);

                // 4-Tap Depth-Aware Bilateral Upsampling
                float2 offsets[4] = {
                    float2(-0.5, -0.5), float2(0.5, -0.5),
                    float2(-0.5,  0.5), float2(0.5,  0.5)
                };

                float4 colorSum = 0.0;
                float weightSum = 0.0;
                
                // NEW: Track the best tap in case all weights are rejected
                float minDepthDiff = 100000.0;
                float4 closestColor = 0.0;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float2 sampleUV = uv + offsets[i] * halfResTexelSize;
                    float4 fogSample = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sampleUV);
                    
                    float tapDepth = LinearEyeDepth(SampleSceneDepth(sampleUV), _ZBufferParams);
                    float depthDiff = abs(centerDepth - tapDepth);

                    // Track the geometrically closest tap
                    if (depthDiff < minDepthDiff)
                    {
                        minDepthDiff = depthDiff;
                        closestColor = fogSample;
                    }

                    // Base weight calculation
                    float weight = exp2(-depthDiff * 5.0); 
                    
                    // CRITICAL FIX: Harsh penalty to stop background fog from bleeding onto foreground objects
                    if (tapDepth > centerDepth + 0.5)
                    {
                        weight *= 0.001; 
                    }
                    
                    colorSum += fogSample * weight;
                    weightSum += weight;
                }

                // If we rejected all taps (e.g., a foreground pixel surrounded purely by background taps),
                // safely fall back to the closest tap without dividing by a microscopic weight sum.
                if (weightSum < 0.001)
                {
                    return closestColor;
                }

                return colorSum / weightSum;
            }
            ENDHLSL
        }
    }
}