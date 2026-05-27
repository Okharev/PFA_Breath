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

        [Header(Micro Vertex Disturbances (LOD))]
        _MicroWaveIntensity ("Micro Wave Global Intensity", Range(0, 1)) = 1.0
        _MicroWaveFadeDistance ("Distance Fade (LOD)", Float) = 30.0
        _MicroWave1 ("Micro 1 (Dir X, Y, Steepness, Wavelength)", Vector) = (0.5, 0.2, 0.4, 1.5)
        _MicroWave2 ("Micro 2 (Dir X, Y, Steepness, Wavelength)", Vector) = (-0.3, 0.6, 0.3, 0.8)
        
        [Header(Subsurface Scattering Wave Crests)]
        _CrestGlowStrength ("Glow Strength", Range(0, 5)) = 2.0
        _CrestGlowPower ("Glow Power (Thinness)", Range(1, 10)) = 5.0
        // NOUVEAU : Contrôle de la longueur de diffusion sur la vague
        _CrestGlowSpread ("Glow Spread (Length)", Range(0.01, 1.0)) = 0.8
        
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
        
        [Header(Foam System (Shoreline and Whitecaps))]
        _FoamColor ("Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [NoScaleOffset] _FoamNoise ("Shoreline Foam Noise", 2D) = "white" {}
        _FoamScale ("Foam Noise Scale", Float) = 2.0
        _FoamSpeed ("Foam Scrolling Speed", Float) = 0.5
        _FoamDistance ("Shoreline Foam Distance", Float) = 1.5
        _FoamCutoff ("Shoreline Foam Cutoff", Range(0.01, 1.0)) = 0.8
        
        _WhitecapThreshold ("Whitecaps Steepness Threshold", Range(0.01, 1.0)) = 0.3
        _WhitecapStrength ("Whitecaps Strength", Range(0.0, 2.0)) = 1.0

        [Header(PBR and Lighting Controls)]
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.95
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
        _SpecularStrength ("Direct Specular Strength", Range(0.0, 1.0)) = 0.15
        _ReflectionStrength ("Environment Reflection Strength", Range(0.0, 1.0)) = 0.5
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

            // Add this line to enable GPU Instancing
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
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float4 screenPos    : TEXCOORD2;
                float3 tangentWS    : TEXCOORD4;
                float3 bitangentWS  : TEXCOORD5;
                float  waveHeight   : TEXCOORD6; 
                float  fogFactor    : TEXCOORD7; 
                float  crestFactor  : TEXCOORD8; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamNoise);
            SAMPLER(sampler_FoamNoise);
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
                
                float _MicroWaveIntensity;
                float _MicroWaveFadeDistance;
                float4 _MicroWave1;
                float4 _MicroWave2;
                
                float _CrestGlowStrength;
                float _CrestGlowPower;
                // NOUVEAU : Déclaration de la variable Spread
                float _CrestGlowSpread;
                
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
                
                float _WhitecapThreshold;
                float _WhitecapStrength;
                
                float _Smoothness;
                float _Metallic;
                float _SpecularStrength;
                float _ReflectionStrength;
            CBUFFER_END

            float3 GerstnerWave(float4 wave, float3 p, inout float3 tangent, inout float3 binormal, inout float crest)
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
                
                crest += pow(max(0.0, sinf), 3.0) * steepness;
                
                return float3(d.x * (a * cosf), a * sinf, d.y * (a * cosf));
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                float3 gridPoint = positionWS;
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float3 p = gridPoint;
                
                float currentCrest = 0.0;
                
                float4 w1 = _Wave1; w1.w *= _WaveScale;
                float4 w2 = _Wave2; w2.w *= _WaveScale;
                float4 w3 = _Wave3; w3.w *= _WaveScale;
                
                p += GerstnerWave(w1, gridPoint, tangent, binormal, currentCrest);
                p += GerstnerWave(w2, gridPoint, tangent, binormal, currentCrest);
                p += GerstnerWave(w3, gridPoint, tangent, binormal, currentCrest);

                float distanceToCam = distance(GetCameraPositionWS(), gridPoint);
                float microFade = saturate(1.0 - (distanceToCam / _MicroWaveFadeDistance));
                microFade *= _MicroWaveIntensity;

                float4 mw1 = _MicroWave1; 
                float4 mw2 = _MicroWave2;
                mw1.z *= microFade;
                mw2.z *= microFade;

                p += GerstnerWave(mw1, gridPoint, tangent, binormal, currentCrest);
                p += GerstnerWave(mw2, gridPoint, tangent, binormal, currentCrest);
                
                float maxAmp = (w1.z / (2.0 * PI / w1.w)) + (w2.z / (2.0 * PI / w2.w)) + (w3.z / (2.0 * PI / w3.w));
                float heightOffset = p.y - gridPoint.y;
                
                output.waveHeight = saturate(heightOffset / maxAmp); 
                output.crestFactor = currentCrest; 
                
                output.positionWS = p;
                output.positionHCS = TransformWorldToHClip(p);
                output.screenPos = ComputeScreenPos(output.positionHCS);
                
                output.normalWS = normalize(cross(binormal, tangent));
                output.tangentWS = normalize(tangent);
                output.bitangentWS = normalize(binormal);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv1 = (input.positionWS.xz * _NormalScale) + (_Time.y * _WindDirection1);
                float2 uv2 = (input.positionWS.xz * _NormalScale * 0.75) + (_Time.y * _WindDirection2);
                
                half3 normalMap1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv1), _NormalStrength);
                half3 normalMap2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv2), _NormalStrength);
                
                half3 tangentNormal = normalize(normalMap1 + normalMap2);
                float3x3 tbn = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 finalNormalWS = normalize(mul(tangentNormal, tbn));

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float3 viewDirWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);

                float surfaceZ = input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);
                
                float depthDifference = max(0.0, sceneZ - surfaceZ);
                float opticalDepth = exp(-depthDifference * _DepthMultiplier);
                
                float2 edgeDist = min(screenUV, 1.0 - screenUV);
                float edgeFade = smoothstep(0.0, 0.05, min(edgeDist.x, edgeDist.y));

                float2 refractedUV = screenUV + (tangentNormal.xy * _RefractionStrength * edgeFade);
                float refractedDepthRaw = SampleSceneDepth(refractedUV);
                float refractedSceneZ = LinearEyeDepth(refractedDepthRaw, _ZBufferParams);
                
                float isBehindWater = step(surfaceZ, refractedSceneZ);
                refractedUV = lerp(screenUV, refractedUV, isBehindWater);

                half3 refractionColor = SampleSceneColor(clamp(refractedUV, 0.001, 0.999));

                #if UNITY_REVERSED_Z
                    real depth = rawDepth;
                #else
                    real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, rawDepth);
                #endif

                float3 backgroundWS = ComputeWorldSpacePosition(refractedUV, depth, UNITY_MATRIX_I_VP);
                float2 causticsUV = backgroundWS.xz * _CausticsScale;
                causticsUV += tangentNormal.xy * 0.2; 
                
                float2 pan1 = causticsUV + _Time.y * _CausticsSpeed * float2(1.0, 0.5);
                float2 pan2 = (causticsUV * 0.8) - _Time.y * _CausticsSpeed * float2(0.5, 1.0);
                
                half c1 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan1).r;
                half c2 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan2).r;
                half caustics = min(c1, c2) * _CausticsStrength;
                
                caustics *= opticalDepth;
                refractionColor += (caustics * _ShallowColor.rgb);

                half3 waterVolumeColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, opticalDepth);
                half3 albedo = lerp(waterVolumeColor, refractionColor, opticalDepth);

                float2 foamUV = (input.positionWS.xz * _FoamScale) + (_Time.y * _WindDirection1 * _FoamSpeed);
                foamUV += tangentNormal.xy * 0.1;
                float foamNoise = SAMPLE_TEXTURE2D(_FoamNoise, sampler_FoamNoise, foamUV).r;
                
                float foamDepthMask = saturate((_FoamDistance - depthDifference) / _FoamDistance);
                float shoreFoam = smoothstep(_FoamCutoff - 0.1, _FoamCutoff + 0.1, foamDepthMask * foamNoise);
                
                float crestMask = smoothstep(_WhitecapThreshold - 0.15, _WhitecapThreshold + 0.15, input.crestFactor);
                float crestFoam = crestMask * input.crestFactor * _WhitecapStrength;
                
                float totalFoam = saturate(shoreFoam + crestFoam);
                albedo = lerp(albedo, _FoamColor.rgb, totalFoam);

                finalNormalWS = normalize(lerp(finalNormalWS, input.normalWS, totalFoam));

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

                // =========================================================================
                // NOUVEAU : Application de la Longueur du Glow (Spread)
                // =========================================================================
                float backLight = saturate(dot(viewDirWS, -mainLight.direction));
                float sssIntensity = pow(backLight, _CrestGlowPower) * _CrestGlowStrength;
                
                // On inverse _CrestGlowSpread pour que 1 = lumière descend tout en bas de la vague
                float sssMask = smoothstep(1.0 - _CrestGlowSpread, 1.0, input.waveHeight);
                
                half3 inferredSSSColor = saturate(_ShallowColor.rgb * 1.5); 
                half3 sssColor = inferredSSSColor * sssIntensity * sssMask * mainLight.color;
                
                color += sssColor * (1.0 - totalFoam); 

                float3 reflectionDir = reflect(-viewDirWS, finalNormalWS);
                half3 environmentColor = GlossyEnvironmentReflection(reflectionDir, finalSmoothness, 1.0);
                float NdotV = saturate(dot(finalNormalWS, viewDirWS));
                float fresnelTerm = brdfData.specular.x + (1.0 - brdfData.specular.x) * pow(1.0 - NdotV, 5.0);
                
                color += (environmentColor * fresnelTerm * _ReflectionStrength) * (1.0 - totalFoam);

                color = MixFog(color, input.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}