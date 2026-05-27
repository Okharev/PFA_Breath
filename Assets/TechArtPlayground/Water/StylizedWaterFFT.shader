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
        _SSSEmission ("SSS HDR Emission (Triggers Bloom)", Range(1.0, 10.0)) = 3.0

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
        
        _MaxWaveHeight ("Max Wave Height (Driven by C#)", Float) = 2.0
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
                float  waveHeight  : TEXCOORD2;
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
                float _SSSEmission;

                half4 _FoamColor;
                float _FoamBias;
                float _FoamBlurLod;
                float _FoamPower;
                float _FoamNoiseScale;
                float2 _FoamNoiseSpeed;

                float _MaxWaveHeight;
            
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

                // NEW: Store the localized vertical displacement
                output.waveHeight = disp.g; 

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
    // 1. MACRO & MICRO NORMALS (Dual-Frequency Detail)
    // =========================================================================
    // Sample FFT spatial derivatives at LOD 0 for sharp, accurate wave geometry
    float4 crispDerivs = SAMPLE_TEXTURE2D_LOD(_DerivTex, sampler_DerivTex, input.fftUV, 0);
    float jacobian = crispDerivs.b;
    float3 macroNormalWS = normalize(float3(-crispDerivs.r, 1.0, -crispDerivs.g));

    // Calculate distance for detail fading
    float viewDist = length(GetCameraPositionWS() - input.positionWS);
    float detailFade = smoothstep(150.0, 50.0, viewDist); 

    // STYLIZED FIX: Multi-Frequency Micro-Normals
    // We sample the detail normal twice at different scales/speeds to break up 
    // visible tiling and create chaotic, organic surface interference.
    float2 panningUV1 = (input.positionWS.xz * _DetailScale) + (_Time.y * _DetailSpeed);
    float2 panningUV2 = (input.positionWS.xz * _DetailScale * 0.5) + (_Time.y * float2(-_DetailSpeed.y, _DetailSpeed.x) * 0.7);

    float3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, panningUV1), _NormalStrength * detailFade);
    float3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormal, sampler_DetailNormal, panningUV2), _NormalStrength * detailFade * 0.8);
    
    // Blend the two normal maps (Whiteout blend) and combine with macro normal
    float3 tangentNormal = normalize(float3(n1.xy + n2.xy, n1.z * n2.z));
    float3 normalWS = normalize(float3(macroNormalWS.x + tangentNormal.x, macroNormalWS.y, macroNormalWS.z + tangentNormal.y));

    // Blurred Jacobian strictly for soft foam masking
    float blurredJacobian = SAMPLE_TEXTURE2D_LOD(_DerivTex, sampler_DerivTex, input.fftUV, _FoamBlurLod).b;

    // =========================================================================
    // 2. REFRACTION & STYLIZED DEPTH BANDING
    // =========================================================================
    float2 screenUV = input.screenPos.xy / input.screenPos.w;
    float2 refractUV = screenUV + (normalWS.xz * _RefractionStrength);

    float surfaceZ = input.screenPos.w;
    float rawDepth = SampleSceneDepth(refractUV);
    float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);

    // Prevent refraction artifacts if object is in front of the water
    if (sceneZ < surfaceZ)
    {
        refractUV = screenUV;
        rawDepth = SampleSceneDepth(refractUV);
        sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
    }

    float waterDepth = max(0.0, sceneZ - surfaceZ);
    float transmittance = exp(-waterDepth * _DepthAbsorption); 

    // STYLIZED FIX: Non-Linear Depth Banding
    // We compress the transmittance curve so shallow water remains vibrantly colored 
    // near the shore, then abruptly drops off into dark, deep ocean water.
    float stylizedTransmittance = smoothstep(0.05, 0.8, transmittance);

    // =========================================================================
    // 3. UNDERWATER CAUSTICS (Chromatic Aberration)
    // =========================================================================
    half3 refractColor = SampleSceneColor(refractUV);

    #if UNITY_REVERSED_Z
        real depthForWS = rawDepth;
    #else
        real depthForWS = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, rawDepth);
    #endif
    float3 backgroundWS = ComputeWorldSpacePosition(refractUV, depthForWS, UNITY_MATRIX_I_VP);

    float2 causticsUV = backgroundWS.xz * _CausticsScale + normalWS.xz * 0.2;
    float2 pan1 = causticsUV + _Time.y * _CausticsSpeed * float2(1.0, 0.5);
    float2 pan2 = (causticsUV * 0.8) - _Time.y * _CausticsSpeed * float2(0.5, 1.0);
    
    // STYLIZED FIX: Chromatic Aberration
    // We offset the UVs slightly for Red and Blue channels to create a glassy, prismatic effect
    float2 causticsOffset = float2(0.015, 0.0) * _CausticsScale;

    half r = min(SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan1 + causticsOffset).r, SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan2 + causticsOffset).r);
    half g = min(SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan1).r, SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan2).r);
    half b = min(SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan1 - causticsOffset).r, SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan2 - causticsOffset).r);
    
    half3 caustics = half3(r, g, b) * _CausticsStrength * transmittance;
    refractColor += (caustics * _ShallowColor.rgb);

    // Calculate final albedo using the stylized transmittance
    half3 waterVolumeColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, stylizedTransmittance);
    half3 albedo = lerp(waterVolumeColor, refractColor, transmittance);

    // =========================================================================
    // 4. FOAM SYSTEM (Dual-Layer Cellular Breakup)
    // =========================================================================
    // STYLIZED FIX: Subtracting a second noise creates dynamic, morphing gaps
    // so the foam looks like bursting bubbles rather than a sliding texture.
// =========================================================================
    // 4. FOAM SYSTEM (Stylized & Solidified)
    // =========================================================================
    float2 foamUV1 = (input.positionWS.xz * _FoamNoiseScale) + (_Time.y * _FoamNoiseSpeed);
    float2 foamUV2 = (input.positionWS.xz * _FoamNoiseScale * 0.75) + (_Time.y * _FoamNoiseSpeed * 0.5);

    float noise1 = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUV1).r;
    float noise2 = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUV2).r;
    
    // FIX 1: High-Contrast Noise
    // Instead of subtracting, we multiply them and boost the intensity. 
    // This creates sharp, cellular bubble shapes with peak whites intact.
    float rawNoise = saturate((noise1 * noise2) * 2.5);
    
    // 1. Crest Foam
    float crest = saturate(1.0 - blurredJacobian);
    
    // Create a much broader base mask for the foam to live in
    float crestBase = smoothstep(_FoamBias, _FoamBias + 0.6, crest);
    
    // FIX 2: The "Stylized Cut"
    // We subtract the inverted noise from the crest base, then use a tight smoothstep 
    // to create a hard, solid, cartoony edge instead of a soft transparent fade.
    float foamShape = saturate(crestBase - (1.0 - rawNoise));
    float crestFoam = smoothstep(0.1, 0.2, foamShape); 

    // 2. Shoreline Foam
    float foamDepthMask = saturate((_FoamDistance - waterDepth) / max(0.01, _FoamDistance));
    
    // Apply the same hard-cut stylized logic to the shoreline
    float shoreShape = saturate(foamDepthMask - (1.0 - rawNoise));
    float shoreFoam = smoothstep(_FoamCutoff, _FoamCutoff + 0.1, shoreShape);

    // Combine masks safely
    float foamMask = saturate(crestFoam + shoreFoam);
    
    // Wipe out micro-normals where foam is present so it looks like a flat, thick fluid
    normalWS = normalize(lerp(normalWS, macroNormalWS, foamMask));

    // =========================================================================
    // 5. PBR LIGHTING (DIFFUSE & SPECULAR)
    // =========================================================================
    float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
    BRDFData brdfData;
    
    float alpha = 1.0f;
    InitializeBRDFData(albedo, 0.0, half3(0.02, 0.02, 0.02), _Smoothness, alpha, brdfData);

    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
    Light mainLight = GetMainLight(shadowCoord);
    
    float shadowAtten = lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);
    float NdotL = saturate(dot(normalWS, mainLight.direction));
    
    half3 radiance = mainLight.color * (mainLight.distanceAttenuation * shadowAtten * NdotL);
    half3 diffuse = brdfData.diffuse * radiance;
    half3 specular = DirectBRDFSpecular(brdfData, normalWS, mainLight.direction, viewDirWS) * radiance;
    
    half3 finalColor = diffuse + (specular * _SpecularStrength);

    // =========================================================================
    // 6. STYLIZED "SEA OF THIEVES" VOLUMETRIC SSS
    // =========================================================================
    float viewLightAlignment = saturate(dot(viewDirWS, -mainLight.direction));
    float sssShadow = lerp(0.15, 1.0, shadowAtten);

    // Thickness Masks (Relies on input.waveHeight mapped dynamically to _MaxWaveHeight)
    float crestThickness = saturate(1.0 - jacobian); 
    float localHeightMask = saturate(input.waveHeight / max(0.1, _MaxWaveHeight)); 
    float volumeThickness = saturate(crestThickness + localHeightMask * 0.5 + 0.1);

    // Lobe 1: Direct Sun Scattering (Sharp & Luminous Highlight)
    float directPhase = pow(viewLightAlignment, lerp(4.0, 16.0, _Smoothness));
    float directSSS = directPhase * volumeThickness * sssShadow;
    half3 directSSSColor = mainLight.color * directSSS;

    // Lobe 2: Ambient Multi-Scattering (Soft & Omni-directional)
    // We wrap the normal away from the light to simulate light wrapping around the wave
    float wrap = 0.6; // Higher wrap = more gummy/soft
    float waveBacklight = saturate((dot(macroNormalWS, -mainLight.direction) + wrap) / (1.0 + wrap));
    waveBacklight = smoothstep(0.0, 1.0, waveBacklight); // Smooth the curve
    float ambientSSS = waveBacklight * volumeThickness;

    // Sample Sky Ambient (SH) to ensure water glows even in shadows/overcast
    half3 ambientSkyLight = SampleSH(macroNormalWS);
    half3 ambientSSSColor = ambientSkyLight * ambientSSS;

    // Combine & Apply Color (Chromatic shift at peaks)
// Combine & Apply Color (Chromatic shift at peaks)
    half3 sssDynamicColor = lerp(_ShallowColor.rgb, saturate(_ShallowColor.rgb * 1.5), crestThickness);
    half3 totalSSS = (directSSSColor + ambientSSSColor) * sssDynamicColor * _SSSStrength;

    // --- NEW: PUSH INTO HDR SPACE ---
    // This forces the brightness of the wave crests well above 1.0, 
    // guaranteeing that the URP Post-Processing Bloom will grab it and make it glow.
    totalSSS *= _SSSEmission;

    // Add SSS (blocked by foam)
    finalColor += totalSSS * (1.0 - foamMask);;

    // =========================================================================
    // 7. STYLIZED ENVIRONMENT REFLECTIONS (Aggressive Fresnel)
    // =========================================================================
    // STYLIZED FIX: We override PBR Fresnel with an exaggerated curve that forces 
    // the water to reflect the sky heavily at grazing angles, creating high contrast.
    float NdotV = saturate(dot(macroNormalWS, viewDirWS));
    
    // Custom Schlick Fresnel with adjustable exponent for stylization
    float customFresnel = saturate(pow(1.0 - NdotV, _FresnelPower * 0.8));
    customFresnel = lerp(0.02, 1.0, customFresnel); // Base reflectivity so it isn't matte

    // Sample Environment (Unity's Specular Cube)
    float3 reflectVec = reflect(-viewDirWS, normalWS); 
    half4 encodedIrradiance = SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectVec, (1.0 - _Smoothness) * 6.0);
    half3 reflection = DecodeHDREnvironment(encodedIrradiance, unity_SpecCube0_HDR);

    finalColor += (reflection * customFresnel) * (1.0 - foamMask);

    // =========================================================================
    // 8. FOAM OVERLAY COMPOSITE
    // =========================================================================
    half3 litFoam = _FoamColor.rgb + (mainLight.color * 0.2);
    
    // Foam scatters millions of light bounces, so it resists shadows heavily
    litFoam *= lerp(0.7, 1.0, shadowAtten);
    
    // Layer the foam over the finished water surface
    finalColor = lerp(finalColor, litFoam, foamMask);

    return half4(finalColor, 1.0);
}
            
            ENDHLSL
        }
    }
}