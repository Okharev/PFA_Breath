Shader "Custom/StillWater_Lake"
{
    Properties
    {
        [Header(Water Volumetrics and Color)]
        _ShallowColor ("Shallow Water Color", Color) = (0.3, 0.6, 0.7, 1.0)
        _DeepColor ("Deep Water Color", Color) = (0.05, 0.15, 0.25, 1.0)
        _DepthMultiplier ("Depth Absorption Rate (Beer's Law)", Float) = 0.8
        _RefractionStrength ("Refraction Distortion", Range(0, 0.1)) = 0.02
        
        [Header(Surface Wind Ripples (Normal Maps))]
        [NoScaleOffset] _NormalMap ("Water Ripples (Normal)", 2D) = "bump" {}
        _NormalScale ("Normal Tiling Scale", Float) = 3.0
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.3
        _WindDirection1 ("Wind Direction 1", Vector) = (0.05, 0.05, 0, 0)
        _WindDirection2 ("Wind Direction 2", Vector) = (-0.02, 0.08, 0, 0)
        
        [Header(Underwater Caustics)]
        [NoScaleOffset] _CausticsTexture ("Caustics Texture (Grayscale)", 2D) = "black" {}
        _CausticsScale ("Caustics Scale", Float) = 0.8
        _CausticsSpeed ("Caustics Speed", Float) = 0.2
        _CausticsStrength ("Caustics Strength", Range(0, 2)) = 0.5
        
        [Header(Shoreline Foam System)]
        _FoamColor ("Foam Color", Color) = (0.9, 0.95, 1.0, 1.0)
        [NoScaleOffset] _FoamNoise ("Shoreline Foam Noise", 2D) = "white" {}
        _FoamScale ("Foam Noise Scale", Float) = 3.0
        _FoamSpeed ("Foam Scrolling Speed", Float) = 0.1
        _FoamDistance ("Shoreline Foam Distance", Float) = 0.8
        _FoamCutoff ("Shoreline Foam Cutoff", Range(0.01, 1.0)) = 0.7

        [Header(PBR and Lighting Controls)]
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.98
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
        _SpecularStrength ("Direct Specular Strength", Range(0.0, 1.0)) = 0.5
        _ReflectionStrength ("Environment Reflection Strength", Range(0.0, 1.0)) = 0.8
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
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            
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
                float3 tangentWS    : TEXCOORD3;
                float3 bitangentWS  : TEXCOORD4;
                float  fogFactor    : TEXCOORD5;
            };

            TEXTURE2D(_NormalMap);          SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamNoise);          SAMPLER(sampler_FoamNoise);
            TEXTURE2D(_CausticsTexture);    SAMPLER(sampler_CausticsTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthMultiplier;
                float _RefractionStrength;
                
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
                float _SpecularStrength;
                float _ReflectionStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Pure transform - No Gerstner calculations for highly optimized still water
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionHCS);
                
                // Derive standard static vectors
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                float tangentSign = input.tangentOS.w * GetOddNegativeScale();
                float3 bitangentWS = cross(normalWS, tangentWS) * tangentSign;
                
                output.normalWS = normalize(normalWS);
                output.tangentWS = normalize(tangentWS);
                output.bitangentWS = normalize(bitangentWS);
                
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Surface Normals (Wind Ripples)
                float2 uv1 = (input.positionWS.xz * _NormalScale) + (_Time.y * _WindDirection1);
                float2 uv2 = (input.positionWS.xz * _NormalScale * 0.75) + (_Time.y * _WindDirection2);
                
                half3 normalMap1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1), _NormalStrength);
                half3 normalMap2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2), _NormalStrength);
                
                half3 tangentNormal = normalize(normalMap1 + normalMap2);
                float3x3 tbn = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 finalNormalWS = normalize(mul(tangentNormal, tbn));

                // 2. Depth and Refraction Setup
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float3 viewDirWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);
                
                float surfaceZ = input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
                
                // Beer's Law Optical Depth
                float depthDifference = max(0.0, sceneZ - surfaceZ);
                float opticalDepth = exp(-depthDifference * _DepthMultiplier);
                
                // Screen-edge fade to prevent refraction artifacts
                float2 edgeDist = min(screenUV, 1.0 - screenUV);
                float edgeFade = smoothstep(0.0, 0.05, min(edgeDist.x, edgeDist.y));
                
                float2 refractedUV = screenUV + (tangentNormal.xy * _RefractionStrength * edgeFade);
                float refractedDepthRaw = SampleSceneDepth(refractedUV);
                float refractedSceneZ = LinearEyeDepth(refractedDepthRaw, _ZBufferParams);
                
                // Prevent refracting objects in front of the water
                float isBehindWater = step(surfaceZ, refractedSceneZ);
                refractedUV = lerp(screenUV, refractedUV, isBehindWater);

                half3 refractionColor = SampleSceneColor(clamp(refractedUV, 0.001, 0.999));

                // 3. Underwater Caustics
                #if UNITY_REVERSED_Z
                    real depth = rawDepth;
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif

                float3 backgroundWS = ComputeWorldSpacePosition(refractedUV, depth, UNITY_MATRIX_I_VP);
                
                float2 causticsUV = backgroundWS.xz * _CausticsScale;
                causticsUV += tangentNormal.xy * 0.1; // Subtle distortion
                
                float2 pan1 = causticsUV + _Time.y * _CausticsSpeed * float2(1.0, 0.5);
                float2 pan2 = (causticsUV * 0.8) - _Time.y * _CausticsSpeed * float2(0.5, 1.0);
                
                half c1 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan1).r;
                half c2 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan2).r;
                half caustics = min(c1, c2) * _CausticsStrength;
                
                caustics *= opticalDepth; // Fade caustics in deep water
                refractionColor += (caustics * _ShallowColor.rgb);

                // 4. Volumetric Base Color
                half3 waterVolumeColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, opticalDepth);
                half3 albedo = lerp(waterVolumeColor, refractionColor, opticalDepth);

                // 5. Shoreline Foam (Depth Based)
                float2 foamUV = (input.positionWS.xz * _FoamScale) + (_Time.y * _WindDirection1 * _FoamSpeed);
                foamUV += tangentNormal.xy * 0.1;
                
                float foamNoise = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUV).r;
                float foamDepthMask = saturate((_FoamDistance - depthDifference) / _FoamDistance);
                
                float totalFoam = smoothstep(_FoamCutoff - 0.1, _FoamCutoff + 0.1, foamDepthMask * foamNoise);
                albedo = lerp(albedo, _FoamColor.rgb, totalFoam);

                // Flatten normals where foam exists to make it look "frothy" rather than smooth
                finalNormalWS = normalize(lerp(finalNormalWS, input.normalWS, totalFoam));

                // 6. PBR Lighting Integration
                BRDFData brdfData;
                half alpha = 1.0; 
                float finalSmoothness = lerp(_Smoothness, 0.1, totalFoam);
                InitializeBRDFData(albedo, _Metallic, half3(0.02, 0.02, 0.02), finalSmoothness, alpha, brdfData);
                
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 color = half3(0.0, 0.0, 0.0);
                
                float NdotL = saturate(dot(finalNormalWS, mainLight.direction));
                half3 radiance = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation * NdotL);
                
                half3 diffuse = brdfData.diffuse * radiance;
                half3 specular = DirectBRDFSpecular(brdfData, finalNormalWS, mainLight.direction, viewDirWS) * radiance;
                
                color += diffuse + (specular * _SpecularStrength);

                // Additional Lights Support
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0; i < lightCount; ++i)
                {
                    Light additionalLight = GetAdditionalLight(i, input.positionWS);
                    float addNdotL = saturate(dot(finalNormalWS, additionalLight.direction));
                    half3 addRadiance = additionalLight.color * (additionalLight.distanceAttenuation * additionalLight.shadowAttenuation * addNdotL);
                    
                    half3 addDiffuse = brdfData.diffuse * addRadiance;
                    half3 addSpecular = DirectBRDFSpecular(brdfData, finalNormalWS, additionalLight.direction, viewDirWS) * addRadiance;
                    
                    color += addDiffuse + (addSpecular * _SpecularStrength);
                }

                // 7. Glossy Environment Reflection (Fresnel)
                float3 reflectionDir = reflect(-viewDirWS, finalNormalWS);
                half3 environmentColor = GlossyEnvironmentReflection(reflectionDir, finalSmoothness, 1.0);
                
                float NdotV = saturate(dot(finalNormalWS, viewDirWS));
                float fresnelTerm = brdfData.specular.x + (1.0 - brdfData.specular.x) * pow(1.0 - NdotV, 5.0);
                
                // Add reflections (masked by foam)
                color += (environmentColor * fresnelTerm * _ReflectionStrength) * (1.0 - totalFoam);

                color = MixFog(color, input.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}