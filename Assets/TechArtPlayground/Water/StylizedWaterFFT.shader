Shader "Custom/Ocean_FFT_Volumetric"
{
    Properties
    {
        [Header(FFT Data Links)]
        [NoScaleOffset] _DispTex ("Displacement Map (RGB)", 2D) = "black" {}
        [NoScaleOffset] _DerivTex ("Derivatives & Jacobian (RGB)", 2D) = "black" {}
        _FFTScale ("FFT Grid Scale (1 / Ocean Size)", Float) = 0.01
        _Choppiness ("Choppiness Scale", Range(0, 5)) = 1.2

        [Header(Water Colors Volumetrics)]
        _ShallowColor ("Shallow Water Color", Color) = (0.2, 0.6, 0.7, 1.0)
        _DeepColor ("Deep Water Color", Color) = (0.02, 0.1, 0.2, 1.0)
        _DepthAbsorption ("Depth Absorption Rate (Beer's Law)", Range(0.01, 2.0)) = 0.2
        _RefractionStrength ("Refraction Distortion", Range(0.0, 0.2)) = 0.05

        [Header(Subsurface Scattering)]
        [HDR] _SSSColor ("SSS Crest Color", Color) = (0.3, 0.8, 0.6, 1.0)
        _SSSStrength ("SSS Intensity", Range(0.0, 5.0)) = 1.5
        _SSSPower ("SSS Sun Focus (Power)", Range(1.0, 20.0)) = 5.0

        [Header(Foam Controls)]
        _FoamColor ("Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FoamBias ("Jacobian Foam Bias", Range(-1.0, 1.0)) = -0.1
        _FoamBlurLod ("Foam Blur (Mip Level)", Range(0, 8)) = 2.0
        _FoamPower ("Foam Falloff Power", Range(0.1, 5.0)) = 1.5

        [Header(Reflections and Specular)]
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.95
        _SpecularStrength ("Sun Highlight Strength", Range(0.0, 10.0)) = 3.0
        _FresnelPower ("Fresnel Power", Range(1.0, 10.0)) = 5.0

        [Header(Micro Details)]
        [Normal] _DetailNormal ("Micro Normals", 2D) = "bump" {}
        _DetailScale ("Detail Scale", Float) = 10.0
        _DetailSpeed ("Detail Speed", Vector) = (0.5, 0.5, 0, 0)
        _NormalStrength ("Detail Strength", Range(0.0, 1.0)) = 0.3

        [Header(Foam Noise)]
        _FoamNoise ("Foam Breakup Noise", 2D) = "white" {}
        _FoamNoiseScale ("Foam Noise Scale", Float) = 0.1
        _FoamNoiseSpeed ("Foam Noise Speed (X, Z)", Vector) = (0.02, 0.05, 0.0, 0.0)

        [Header(Underwater Caustics)]
        [NoScaleOffset] _CausticsTexture ("Caustics Texture (Grayscale)", 2D) = "black" {}
        _CausticsScale ("Caustics Scale", Float) = 0.5
        _CausticsSpeed ("Caustics Speed", Float) = 0.5
        _CausticsStrength ("Caustics Strength", Range(0, 2)) = 1.0

        [Header(Shoreline Foam)]
        _FoamDistance ("Shoreline Foam Distance", Float) = 1.5
        _FoamCutoff ("Shoreline Foam Cutoff", Range(0.01, 1.0)) = 0.8

        [Header(Shadows)]
        _ShadowStrength ("Water Shadow Strength", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        // Transparent queue is required to sample the opaque background and depth
        Tags
        {
            "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-1"
        }

        Pass
        {
            Name "OceanForward"
            Tags
            {
                "LightMode"="UniversalForward"
            }

            // Do not write to depth, blend standard transparency if needed
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing

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
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 fftUV : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD10;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_DispTex);
            SAMPLER(sampler_DispTex);
            TEXTURE2D(_DerivTex);
            SAMPLER(sampler_DerivTex);

            CBUFFER_START(UnityPerMaterial)
                float _FFTScale;
                float _Choppiness;

                half3 _ShallowColor;
                half3 _DeepColor;
                float _DepthAbsorption;
                float _RefractionStrength;

                half3 _SSSColor;
                float _SSSStrength;
                float _SSSPower;

                half4 _FoamColor;
                float _FoamBias;
                float _FoamBlurLod;
                float _FoamPower;
                float _FoamNoiseScale;
                float2 _FoamNoiseSpeed;


                float _CausticsScale;
                float _CausticsSpeed;
                float _CausticsStrength;

                float _FoamDistance;
                float _FoamCutoff;

                float _ShadowStrength;

                float _Smoothness;
                float _SpecularStrength;
                float _FresnelPower;
                float _DetailScale;
                float2 _DetailSpeed;
                float _NormalStrength;
                TEXTURE2D(_DetailNormal);
                SAMPLER(sampler_DetailNormal);
                TEXTURE2D(_FoamNoise);
                SAMPLER(sampler_FoamNoise);
                TEXTURE2D(_CausticsTexture);
                SAMPLER(sampler_CausticsTexture);


                // CBUFFER_END

            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float2 stableUV = positionWS.xz * _FFTScale;
                output.fftUV = stableUV;

                // Displace Vertices
                float4 disp = SAMPLE_TEXTURE2D_LOD(_DispTex, sampler_DispTex, stableUV, 0);
                positionWS.x += disp.r * _Choppiness;
                positionWS.y += disp.g;
                positionWS.z += disp.b * _Choppiness;

                output.positionWS = positionWS;
                output.positionHCS = TransformWorldToHClip(positionWS);

                // Calculate Screen Position for Depth/Color sampling
                output.screenPos = ComputeScreenPos(output.positionHCS);

                return output;
            }

half4 frag(Varyings input) : SV_Target
{
    // =========================================================================
    // 1. MACRO & MICRO NORMALS
    // =========================================================================
    // Sample FFT spatial derivatives to get our macro wave shape and Jacobian (choppiness/thinness)
    float4 derivatives = SAMPLE_TEXTURE2D_LOD(_DerivTex, sampler_DerivTex, input.fftUV, _FoamBlurLod);
    float jacobian = derivatives.b;
    float3 macroNormalWS = normalize(float3(-derivatives.r, 1.0, -derivatives.g));

    // Extract high-frequency micro normals (ripples) to break up the FFT grid
    float2 panningUV = (input.positionWS.xz * _DetailScale) + (_Time.y * _DetailSpeed);
    float3 tangentNormal = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, panningUV), _NormalStrength);
    
    // Blend macro and micro normals (Mapping Tangent X/Y to World X/Z)
    float3 normalWS = normalize(float3(macroNormalWS.x + tangentNormal.x, macroNormalWS.y, macroNormalWS.z + tangentNormal.y));

    // =========================================================================
    // 2. REFRACTION & WATER DEPTH
    // =========================================================================
    // Setup screen UVs and distort them based on the wave normal
    float2 screenUV = input.screenPos.xy / input.screenPos.w;
    float2 refractUV = screenUV + (normalWS.xz * _RefractionStrength);

    // Sample depth buffer to calculate how thick the water volume is
    float surfaceZ = input.screenPos.w;
    float rawDepth = SampleSceneDepth(refractUV);
    float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);

    // Prevent refraction artifacts if distortion pulls in an object IN FRONT of the water
    if (sceneZ < surfaceZ)
    {
        refractUV = screenUV;
        rawDepth = SampleSceneDepth(refractUV);
        sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
    }

    // Optical transmittance based on Beer's Law
    float waterDepth = max(0.0, sceneZ - surfaceZ);
    float transmittance = exp(-waterDepth * _DepthAbsorption); 
    
    // =========================================================================
    // 3. UNDERWATER CAUSTICS & BASE ALBEDO
    // =========================================================================
    half3 refractColor = SampleSceneColor(refractUV);

    // Reconstruct world position of the underwater floor to project caustics correctly
    #if UNITY_REVERSED_Z
        real depthForWS = rawDepth;
    #else
        real depthForWS = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
    #endif
    float3 backgroundWS = ComputeWorldSpacePosition(refractUV, depthForWS, UNITY_MATRIX_I_VP);

    // Project scrolling caustics, slightly distorted by the water surface
    float2 causticsUV = backgroundWS.xz * _CausticsScale + normalWS.xz * 0.2;
    float2 pan1 = causticsUV + _Time.y * _CausticsSpeed * float2(1.0, 0.5);
    float2 pan2 = (causticsUV * 0.8) - _Time.y * _CausticsSpeed * float2(0.5, 1.0);
    
    half c1 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan1).r;
    half c2 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan2).r;
    
    // Fade caustics in deep water
    half caustics = min(c1, c2) * _CausticsStrength * transmittance;
    refractColor += (caustics * _ShallowColor.rgb);

    // Calculate final albedo before lighting (Gerstner-style replacement blend)
    half3 waterVolumeColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, transmittance);
    half3 albedo = lerp(waterVolumeColor, refractColor, transmittance);

    // =========================================================================
    // 4. FOAM SYSTEM (STRICT CLAMPING)
    // =========================================================================
    float2 foamUVs = (input.positionWS.xz * _FoamNoiseScale) + (_Time.y * _FoamNoiseSpeed);
    float rawNoise = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUVs).r;
    
    // 1. FFT Crest Foam
    // Invert the jacobian so peaks > 0, and flat water = 0.
    float crest = saturate(1.0 - jacobian);
    // Subtract bias. (If bias is 0.1, foam only appears when crests are very sharp).
    float crestFoam = saturate(crest - _FoamBias); 
    // Multiply by noise to break it up, then apply power to sharpen the clumps
    crestFoam = pow(crestFoam * rawNoise, _FoamPower);

    // 2. Shoreline Foam
    // This mask is exactly 1.0 at the beach, and exactly 0.0 in deep water.
    float foamDepthMask = saturate((_FoamDistance - waterDepth) / max(0.01, _FoamDistance));
    // Multiply the depth mask by the noise texture
    float shoreFoam = foamDepthMask * rawNoise;
    // Use a hard cutoff. If shoreFoam is below the cutoff, it gets forced to 0.0.
    shoreFoam = smoothstep(_FoamCutoff, _FoamCutoff + 0.15, shoreFoam);

    // Combine both masks safely
    float foamMask = saturate(crestFoam + shoreFoam);

    // WIPE OUT micro-normals where foam is present so it looks like a soft volume
    normalWS = normalize(lerp(normalWS, macroNormalWS, foamMask));

// =========================================================================
    // 5. PBR LIGHTING (DIFFUSE & SPECULAR)
    // =========================================================================
    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    BRDFData brdfData;
    half alpha = 1.0; 
    
    // Initialize standard URP PBR data. (Water is dielectric: Metallic = 0.0)
    InitializeBRDFData(albedo, 0.0, half3(0.02, 0.02, 0.02), _Smoothness, alpha, brdfData);

    // Setup shadows and main directional light
    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light mainLight = GetMainLight(shadowCoord);

    // --- FIX: APPLY GLOBAL SHADOW STRENGTH ---
    // Lerp between 1.0 (fully lit) and the actual shadow based on your Inspector slider
    float shadowAtten = lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);

    // NdotL calculation: wave faces pointing away from the sun physically darken
    float NdotL = saturate(dot(normalWS, mainLight.direction));
    half3 radiance = mainLight.color * (mainLight.distanceAttenuation * shadowAtten * NdotL);
    
    half3 diffuse = brdfData.diffuse * radiance;
    half3 specular = DirectBRDFSpecular(brdfData, normalWS, mainLight.direction, viewDirWS) * radiance;
    
    half3 finalColor = diffuse + (specular * _SpecularStrength);

    // =========================================================================
    // 6. VOLUMETRIC SUBSURFACE SCATTERING & ENVIRONMENT REFLECTIONS
    // =========================================================================
    float viewLightAlignment = saturate(dot(viewDirWS, -mainLight.direction));
    
    // Dual-Thickness Masks
    float crestThickness = saturate(1.0 - jacobian); 
    float volumeThickness = saturate(crestThickness + 0.25); 

    float inferredSSSPower = lerp(2.0, 12.0, _Smoothness);
    
    float wrap = 0.4;
    float waveBacklight = saturate((dot(macroNormalWS, -mainLight.direction) + wrap) / (1.0 + wrap));
    waveBacklight *= waveBacklight; 
    
    // Calculate Scattering using the broader Volume mask
    float sssScattering = (waveBacklight * volumeThickness) + (pow(viewLightAlignment, inferredSSSPower) * volumeThickness);
    
    // --- FIX: SOFTENED SSS SHADOWS ---
    // SSS is ambient, so it never gets fully dark. We lerp your softened shadowAtten!
    float sssShadow = lerp(0.15, 1.0, shadowAtten);
    float sssMask = sssScattering * _SSSStrength * sssShadow;
    
    // Chromatic Shift
    half3 inferredSSSColor = saturate(_ShallowColor.rgb * 1.5); 
    half3 sssDynamicColor = lerp(_ShallowColor.rgb, inferredSSSColor, crestThickness);
    
    // Filter the sun's light color through our dynamic water volume color
    half3 dynamicSSS = sssDynamicColor * mainLight.color * sssMask;
    
    // Add SSS glow (blocked by foam)
    finalColor += dynamicSSS * (1.0 - foamMask);

    // Environment Reflections (Fresnel)
    float NdotV = saturate(dot(normalWS, viewDirWS));
    float fresnelTerm = brdfData.specular.x + (1.0 - brdfData.specular.x) * pow(1.0 - NdotV, 5.0);
    
    float3 reflectVec = reflect(-viewDirWS, normalWS);
    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVec, (1.0 - _Smoothness) * 6.0);
    half3 reflection = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);

    finalColor += (reflection * fresnelTerm) * (1.0 - foamMask);

    // =========================================================================
    // 7. FOAM OVERLAY COMPOSITE
    // =========================================================================
    half3 litFoam = _FoamColor.rgb + (mainLight.color * 0.2); 
    
    // --- FIX: FOAM SHADOWS ---
    // Foam scatters millions of light bounces, so it resists shadows heavily. 
    litFoam *= lerp(0.7, 1.0, shadowAtten);
    
    // Physically layer the foam over the finished water surface
    finalColor = lerp(finalColor, litFoam, foamMask);

    return half4(finalColor, 1.0);
}
            ENDHLSL
        }
    }
}