Shader "Custom/StillWater_URP"
{
    Properties
    {
        [Header(Water Volumetrics and Color)]
        [MainColor] _ShallowColor ("Shallow Water Color", Color) = (0.4, 0.8, 0.9, 1.0)
        _DeepColor ("Deep Water Color", Color) = (0.05, 0.2, 0.4, 1.0)
        _DepthMultiplier ("Depth Absorption Rate", Float) = 0.5
        _RefractionStrength ("Refraction Distortion", Range(0, 0.1)) = 0.05
        _EdgeFadeDistance ("Edge Fade Distance", Range(0.01, 0.5)) = 0.05

        [Header(Underwater Caustics)]
        [NoScaleOffset] _CausticsTexture ("Caustics Texture (Grayscale)", 2D) = "black" {}
        _CausticsScale ("Caustics Scale", Float) = 0.5
        _CausticsSpeed ("Caustics Speed", Float) = 0.5
        _CausticsStrength ("Caustics Strength", Range(0, 2)) = 1.0
        _CausticsDistortion ("Caustics Distortion", Range(0, 1)) = 0.2

        [Header(Surface Ripples (Normals))]
        [MainTexture] [NoScaleOffset] _NormalMap ("Water Ripples (Normal)", 2D) = "bump" {}
        _NormalScale ("Normal Tiling Scale", Float) = 2.0
        _Ripple2Scale ("Secondary Ripple Scale", Float) = 0.75
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.3
        _WindDirection1 ("Wind Direction 1", Vector) = (0.05, 0.05, 0, 0)
        _WindDirection2 ("Wind Direction 2", Vector) = (-0.02, 0.08, 0, 0)

        [Header(Shoreline Foam)]
        [MainColor] _FoamColor ("Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FoamDistance ("Foam Maximum Distance", Float) = 2.0
        _FoamSpeed ("Foam Ripple Speed", Float) = 1.5
        _FoamDistortion ("Foam Distortion Strength", Range(0, 1)) = 0.5
        _FoamLines ("Foam Line Count", Float) = 15.0

        [Header(PBR and Lighting Controls)]
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.95
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
            Tags
            {
                "LightMode"="UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float fogFactor : TEXCOORD5;
                float4 rippleUVs : TEXCOORD6;
            };

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_CausticsTexture);
            SAMPLER(sampler_CausticsTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _DepthMultiplier;
                float _RefractionStrength;
                float _EdgeFadeDistance;

                float _CausticsScale;
                float _CausticsSpeed;
                float _CausticsStrength;
                float _CausticsDistortion;

                float _NormalScale;
                float _Ripple2Scale;
                float _NormalStrength;
                float2 _WindDirection1;
                float2 _WindDirection2;

                half4 _FoamColor;
                float _FoamDistance;
                float _FoamSpeed;
                float _FoamDistortion;
                float _FoamLines;

                half _Smoothness;
                half _Metallic;
                half _SpecularStrength;
                half _ReflectionStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionHCS);

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;

                output.fogFactor = ComputeFogFactor(output.positionHCS.z);

                output.rippleUVs.xy = (output.positionWS.xz * _NormalScale) + (_Time.y * _WindDirection1);
                output.rippleUVs.zw = (output.positionWS.xz * _NormalScale * _Ripple2Scale) + (_Time.y *
                    _WindDirection2);

                return output;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Surface Ripples
                half3 normalMap1 = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.rippleUVs.xy), _NormalStrength);
                half3 normalMap2 = UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.rippleUVs.zw), _NormalStrength);

                half3 tangentNormal = normalize(normalMap1 + normalMap2);
                float3x3 tbn = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 finalNormalWS = normalize(mul(tangentNormal, tbn));

                // 2. Base Depth Calculation (For Volumetrics)
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float surfaceZ = input.screenPos.w;
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneZ = LinearEyeDepth(rawDepth, _ZBufferParams);

                float depthDifference = max(0.0, sceneZ - surfaceZ);
                float opticalDepth = exp(-depthDifference * _DepthMultiplier);

                // 3. Refraction
                float2 edgeDist = min(screenUV, 1.0 - screenUV);
                float edgeFade = smoothstep(0.0, _EdgeFadeDistance, min(edgeDist.x, edgeDist.y));
                float2 refractedUV = screenUV + (tangentNormal.xy * _RefractionStrength * edgeFade);

                float refractedDepthRaw = SampleSceneDepth(refractedUV);
                float refractedSceneZ = LinearEyeDepth(refractedDepthRaw, _ZBufferParams);

                // Prevent refracting objects that are IN FRONT of the water
                float isBehindWater = step(surfaceZ, refractedSceneZ);
                refractedUV = lerp(screenUV, refractedUV, isBehindWater);

                half3 refractionColor = SampleSceneColor(clamp(refractedUV, 0.001, 0.999));

                // 4. World Space Reconstruction (Shared for Foam and Caustics to save performance)
                #if UNITY_REVERSED_Z
                real depth = refractedDepthRaw;
                #else
                real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, refractedDepthRaw);
                #endif

                // Reconstruct the 3D position of the opaque object behind the water
                float3 backgroundWS = ComputeWorldSpacePosition(refractedUV, depth, UNITY_MATRIX_I_VP);

                // 5. Shoreline Foam (FIXED: Using True Vertical Depth)
                // We calculate the strict Y-axis difference between the water surface and the ground below
                float verticalDepth = max(0.0, input.positionWS.y - backgroundWS.y);

                float normalizedDepth = saturate(verticalDepth / _FoamDistance);
                float distortedDepth = normalizedDepth + (tangentNormal.x * _FoamDistortion);

                float foamSine = sin((distortedDepth * _FoamLines) - (_Time.y * _FoamSpeed));
                half foamLines = smoothstep(0.8, 0.95, foamSine);
                half foamMask = 1.0 - normalizedDepth;
                half finalFoam = foamLines * foamMask;

                // 6. Caustics Mapping
                float2 causticsUV = backgroundWS.xz * _CausticsScale + tangentNormal.xy * _CausticsDistortion;

                float2 pan1 = causticsUV + _Time.y * _CausticsSpeed * float2(1.0, 0.5);
                float2 pan2 = (causticsUV * 0.8) - _Time.y * _CausticsSpeed * float2(0.5, 1.0);

                half c1 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan1).r;
                half c2 = SAMPLE_TEXTURE2D(_CausticsTexture, sampler_CausticsTexture, pan2).r;
                half caustics = min(c1, c2) * _CausticsStrength * opticalDepth;

                refractionColor += (caustics * _ShallowColor.rgb);

                // 7. Base Albedo & Foam Compositing
                half3 waterVolumeColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, opticalDepth);
                half3 baseWaterAlbedo = lerp(waterVolumeColor, refractionColor, opticalDepth);
                half3 albedo = lerp(baseWaterAlbedo, _FoamColor.rgb, finalFoam);

                // 8. Lighting and PBR
                half alpha = 1.0;
                BRDFData brdfData;

                InitializeBRDFData(albedo, _Metallic, kDielectricSpec.rgb, _Smoothness, alpha, brdfData);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 viewDirWS = SafeNormalize(GetCameraPositionWS() - input.positionWS);

                float NdotL = saturate(dot(finalNormalWS, mainLight.direction));
                half3 radiance = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation *
                    NdotL);

                half3 diffuse = brdfData.diffuse * radiance;
                half3 specular = DirectBRDFSpecular(brdfData, finalNormalWS, mainLight.direction, viewDirWS) * radiance;

                half3 color = diffuse + (specular * _SpecularStrength);

                // Additional Lights Support
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half3 lightRadiance = light.color * (light.distanceAttenuation * light.shadowAttenuation * saturate(
                        dot(finalNormalWS, light.direction)));

                    half3 lightDiffuse = brdfData.diffuse * lightRadiance;
                    half3 lightSpecular = DirectBRDFSpecular(brdfData, finalNormalWS, light.direction, viewDirWS) *
                        lightRadiance;

                    color += lightDiffuse + (lightSpecular * _SpecularStrength);
                }
                #endif

                // 9. Environment Reflections (Fresnel)
                float3 reflectionDir = reflect(-viewDirWS, finalNormalWS);
                half3 environmentColor = GlossyEnvironmentReflection(reflectionDir, _Smoothness, 1.0);
                float NdotV = saturate(dot(finalNormalWS, viewDirWS));
                float fresnelTerm = brdfData.specular.x + (1.0 - brdfData.specular.x) * pow(1.0 - NdotV, 5.0);

                color += environmentColor * fresnelTerm * _ReflectionStrength;
                color = MixFog(color, input.fogFactor);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}