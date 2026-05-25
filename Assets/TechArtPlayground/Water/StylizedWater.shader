Shader "Custom/RealisticWater_Master"
{
    Properties
    {
        [Header(Water Volumetrics and Color)]
        _ShallowColor ("Shallow Water Color", Color) = (0.4, 0.8, 0.9, 1.0)
        _DeepColor ("Deep Water Color", Color) = (0.05, 0.2, 0.4, 1.0)
        _DepthMultiplier ("Depth Absorption Rate (Beer's Law)", Float) = 0.5
        _RefractionStrength ("Refraction Distortion", Range(0, 0.1)) = 0.05
        
        [Header(Gerstner Waves Macro Detail)]
        _WaveScale ("Global Wave Scale", Float) = 1.0
        _Wave1 ("Wave 1 (Dir X, Dir Y, Steepness, Wavelength)", Vector) = (1.0, 0.5, 0.2, 10.0)
        _Wave2 ("Wave 2 (Dir X, Dir Y, Steepness, Wavelength)", Vector) = (0.3, 0.8, 0.15, 6.0)
        _Wave3 ("Wave 3 (Dir X, Dir Y, Steepness, Wavelength)", Vector) = (-0.2, 0.7, 0.1, 3.0)
        _WaveSpeed ("Global Wave Speed", Float) = 1.2
        
        [Header(Subsurface Scattering Wave Crests)]
        [HDR] _CrestGlowColor ("Crest Glow Color", Color) = (0.2, 0.9, 0.8, 1.0)
        _CrestGlowStrength ("Glow Strength", Range(0, 5)) = 2.0
        _CrestGlowPower ("Glow Power (Thinness)", Range(1, 10)) = 5.0
        
        [Header(Underwater Caustics)]
        [NoScaleOffset] _CausticsTexture ("Caustics Texture (Grayscale)", 2D) = "black" {}
        _CausticsScale ("Caustics Scale", Float) = 0.5
        _CausticsSpeed ("Caustics Speed", Float) = 0.5
        _CausticsStrength ("Caustics Strength", Range(0, 2)) = 1.0
        
        [Header(Micro Disturbances Normal Maps)]
        [NoScaleOffset] _NormalMap ("Water Ripples (Normal)", 2D) = "bump" {}
        _NormalScale ("Normal Tiling Scale", Float) = 2.0
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.8
        _WindDirection1 ("Wind Direction 1", Vector) = (0.1, 0.1, 0, 0)
        _WindDirection2 ("Wind Direction 2", Vector) = (-0.05, 0.15, 0, 0)
        
        [Header(Shoreline Foam)]
        _FoamColor ("Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FoamDistance ("Foam Distance (Depth)", Float) = 1.5
        [NoScaleOffset] _FoamNoise ("Foam Noise (Grayscale)", 2D) = "white" {}
        _FoamScale ("Foam Noise Scale", Float) = 2.0
        _FoamSpeed ("Foam Scrolling Speed", Float) = 0.5
        _FoamCutoff ("Foam Cutoff", Range(0.01, 1.0)) = 0.8

        [Header(PBR Properties)]
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.95
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Transparent-1" 
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 screenPos    : TEXCOORD2;
                float3 tangentWS    : TEXCOORD4;
                float3 bitangentWS  : TEXCOORD5;
                float  waveHeight   : TEXCOORD6; // NEW: Passed to fragment for SSS
            };

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamNoise);
            SAMPLER(sampler_FoamNoise);
            
            // NEW: Caustics
            TEXTURE2D(_CausticsTexture);
            SAMPLER(sampler_CausticsTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthMultiplier;
                float _RefractionStrength;
                
                float _WaveScale;
                float4 _Wave1;
                float4 _Wave2;
                float4 _Wave3;
                float _WaveSpeed;
                
                half4 _CrestGlowColor;
                float _CrestGlowStrength;
                float _CrestGlowPower;
                
                float _CausticsScale;
                float _CausticsSpeed;
                float _CausticsStrength;
                
                float _NormalScale;
                float _NormalStrength;
                float2 _WindDirection1;
                float2 _WindDirection2;
                
                half4 _FoamColor;
                float _FoamDistance;
                float _FoamScale;
                float _FoamSpeed;
                float _FoamCutoff;
                
                float _Smoothness;
                float _Metallic;
            CBUFFER_END

            float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 binormal)
            {
                float steepness = wave.z;
                float wavelength = wave.w;
                float w = 2.0 * PI / wavelength;
                float c = sqrt(9.8 / w);
                float2 d = normalize(wave.xy);
                float f = w * (dot(d, p.xz) - c * _Time.y * _WaveSpeed);
                float a = steepness / w;
                
                float cosf = cos(f);
                float sinf = sin(f);
                
                tangent += float3(-d.x * d.x * (steepness * sinf), d.x * (steepness * cosf), -d.x * d.y * (steepness * sinf));
                binormal += float3(-d.x * d.y * (steepness * sinf), d.y * (steepness * cosf), -d.y * d.y * (steepness * sinf));
                
                return float3(d.x * (a * cosf), a * sinf, d.y * (a * cosf));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                float3 gridPoint = positionWS;
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float3 p = gridPoint;
                
                float4 w1 = _Wave1; w1.w *= _WaveScale;
                float4 w2 = _Wave2; w2.w *= _WaveScale;
                float4 w3 = _Wave3; w3.w *= _WaveScale;
                
                p += GerstnerWave(w1, gridPoint, tangent, binormal);
                p += GerstnerWave(w2, gridPoint, tangent, binormal);
                p += GerstnerWave(w3, gridPoint, tangent, binormal);
                
                // NEW: Calculate normalized wave height for crest masking
                float maxAmp = (w1.z / (2.0 * PI / w1.w)) + (w2.z / (2.0 * PI / w2.w)) + (w3.z / (2.0 * PI / w3.w));
                float heightOffset = p.y - gridPoint.y;
                output.waveHeight = saturate(heightOffset / maxAmp); 
                
                output.positionWS = p;
                output.positionHCS = TransformWorldToHClip(p);
                output.screenPos = ComputeScreenPos(output.positionHCS);
                output.normalWS = normalize(cross(binormal, tangent));
                output.tangentWS = normalize(tangent);
                output.bitangentWS = normalize(binormal);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Dual Normal Mapping
                float2 uv1 = (input.positionWS.xz * _NormalScale) + (_Time.y * _WindDirection1);
                float2 uv2 = (input.positionWS.xz * _NormalScale * 0.75) + (_Time.y * _WindDirection2);
                
                half3 normalMap1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1), _NormalStrength);
                half3 normalMap2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2), _NormalStrength);
                
                half3 tangentNormal = normalize(normalMap1 + normalMap2);
                float3x3 tbn = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 finalNormalWS = normalize(mul(tangentNormal, tbn));

                // 2. Screen Space & View
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float3 viewDirWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);

                // 3. Volumetric Refraction & Depth
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceZ = input.screenPos.w;
                float depthDifference = max(0.0, sceneZ - surfaceZ);
                float opticalDepth = exp(-depthDifference * _DepthMultiplier);
                
                // Smoothly fade out refraction near the edges (0.05 margin)
                float2 edgeDist = min(screenUV, 1.0 - screenUV);
                float edgeFade = smoothstep(0.0, 0.05, min(edgeDist.x, edgeDist.y));

                // Apply faded refraction
                float2 refractedUV = screenUV + (tangentNormal.xy * _RefractionStrength * edgeFade);
                half3 refractionColor = SampleSceneColor(clamp(refractedUV, 0.001, 0.999));

                // -------------------------------------------------------------------------
                // 4. Underwater Caustics (World-Space Reconstruction)
                // -------------------------------------------------------------------------
                // Reconstruct the world position of the underwater terrain pixel
                #if UNITY_REVERSED_Z
                    real depth = rawDepth;
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif

                float3 backgroundWS = ComputeWorldSpacePosition(refractedUV, depth, UNITY_MATRIX_I_VP);
                
                // Map UVs based on XZ terrain space and add normal map distortion to make it "dance"
                float2 causticsUV = backgroundWS.xz * _CausticsScale;
                causticsUV += tangentNormal.xy * 0.2; 
                
                // Pan in two intersecting directions
                float2 pan1 = causticsUV + _Time.y * _CausticsSpeed * float2(1.0, 0.5);
                float2 pan2 = (causticsUV * 0.8) - _Time.y * _CausticsSpeed * float2(0.5, 1.0);
                
                // Use minimum blending on two samples of the texture to create web-like intersections
                half c1 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan1).r;
                half c2 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan2).r;
                half caustics = min(c1, c2) * _CausticsStrength;
                
                // Multiply by opticalDepth so caustics fade out completely in deep water
                caustics *= opticalDepth;
                
                // Add the caustics to the background, tinted slightly by the water's color
                refractionColor += (caustics * _ShallowColor.rgb);

                // Combine Volumetrics
                half3 waterVolumeColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, opticalDepth);
                half3 albedo = lerp(waterVolumeColor, refractionColor, opticalDepth);

                // 5. Shoreline Foam
                float foamDepthMask = saturate((_FoamDistance - depthDifference) / _FoamDistance);
                float2 foamUV = (input.positionWS.xz * _FoamScale) + (_Time.y * _WindDirection1 * _FoamSpeed);
                foamUV += tangentNormal.xy * 0.1;
                float foamNoise = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUV).r;
                float finalFoamAmount = smoothstep(_FoamCutoff - 0.1, _FoamCutoff + 0.1, foamDepthMask * foamNoise);
                albedo = lerp(albedo, _FoamColor.rgb, finalFoamAmount);

                // 6. PBR Lighting
                BRDFData brdfData;
                half alpha = 1.0; 
                float finalSmoothness = lerp(_Smoothness, 0.6, finalFoamAmount);
                InitializeBRDFData(albedo, _Metallic, half3(0.02, 0.02, 0.02), finalSmoothness, alpha, brdfData);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 color = LightingPhysicallyBased(brdfData, mainLight, finalNormalWS, viewDirWS);

                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0; i < lightCount; ++i)
                {
                    Light additionalLight = GetAdditionalLight(i, input.positionWS);
                    color += LightingPhysicallyBased(brdfData, additionalLight, finalNormalWS, viewDirWS);
                }

                // -------------------------------------------------------------------------
                // 7. Subsurface Scattering (Wave Crest Glow)
                // -------------------------------------------------------------------------
                // Translucency occurs mostly when we look TOWARDS the light source
                float backLight = saturate(dot(viewDirWS, -mainLight.direction));
                
                // Power function concentrates the glow into the thinnest parts
                float sssIntensity = pow(backLight, _CrestGlowPower) * _CrestGlowStrength;
                
                // Smoothstep isolates only the upper peaks of the waves using the normalized waveHeight
                float crestMask = smoothstep(0.2, 1.0, input.waveHeight); 
                
                // Calculate the final glowing color
                half3 sssColor = _CrestGlowColor.rgb * sssIntensity * crestMask * mainLight.color;
                
                // Add the subsurface scattering to the final lit color
                color += sssColor;

                // 8. Environment Reflection
                float3 reflectionDir = reflect(-viewDirWS, finalNormalWS);
                half3 environmentColor = GlossyEnvironmentReflection(reflectionDir, finalSmoothness, 1.0);
                float NdotV = saturate(dot(finalNormalWS, viewDirWS));
                float fresnelTerm = brdfData.specular.x + (1.0 - brdfData.specular.x) * pow(1.0 - NdotV, 5.0);
                color += (environmentColor * fresnelTerm) * (1.0 - finalFoamAmount);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}