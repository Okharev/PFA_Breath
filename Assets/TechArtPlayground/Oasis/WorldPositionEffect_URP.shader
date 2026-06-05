Shader "Custom/URP/WorldPositionEffect"
{
Properties
    {
        [Header(Base Appearance (Outside))]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Alt Appearance (Inside Swap))]
        _AltMap ("Alt Texture", 2D) = "white" {}
        _AltColor ("Alt Color", Color) = (1, 1, 1, 1)

        [Header(World Position Mask Settings)]
        [Toggle(_DISSOLVE_MODE)] _DissolveMode ("Dissolve Instead of Swap", Float) = 0
        [Toggle(_INVERT_EFFECT)] _InvertEffect ("Invert Mask", Float) = 0
        _EffectFeather ("Transition Feather/Softness", Range(0.001, 5.0)) = 1.0
        

        _NoiseMap ("Noise Map", 2D) = "gray" {}
        _NoiseScale ("Noise Scale (World Space)", Float) = 0.5
        _NoiseStrength ("Noise Strength", Float) = 1.0
        // ADD THIS: Controls the X and Y (world space XZ) scrolling speed
        _NoiseSpeed ("Noise Scroll Speed", Vector) = (0.2, 0.2, 0, 0)

        [Header(PBR Core)]
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }
        LOD 300

        // HLSLINCLUDE block ensures functions and uniform definitions are correctly shared 
        // across Forward, Shadow, and Depth passes without duplicating code.
HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // 1. THIS MUST STRICTLY MATCH THE PROPERTIES BLOCK ABOVE
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _AltColor;
            float4 _BaseMap_ST;
            float4 _AltMap_ST;
            float _EffectFeather;
            float _NoiseScale;
            float _NoiseStrength;
            float _Metallic;
            float _Smoothness;
            float4 _NoiseSpeed;
        CBUFFER_END

        // 2. GLOBAL VARIABLES (Driven exclusively by OasisManager.cs)
        #define MAX_OASES 20
        float4 _OasisData[MAX_OASES]; 
        int _ActiveOasisCount;        
        
        float4 _EdgeColor;            
        float _EdgeWidth;             
        float _DesaturationAmount; // <-- Added to catch your C# script's broadcast!

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_AltMap);  SAMPLER(sampler_AltMap);
        TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

        // Core Mask Logic
// Core Mask Logic
        float2 CalculateEffect(float3 positionWS)
        {
            // Apply _Time.y to animate the UVs over time
            float2 noiseUV = (positionWS.xz * _NoiseScale) + (_Time.y * _NoiseSpeed.xy);
            float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUV).r;
            float noiseOffset = (noise - 0.5) * _NoiseStrength;
            
            float finalMask = 1.0; 
            float finalEdgeGlow = 0.0;

            for (int i = 0; i < _ActiveOasisCount; i++)
            {
                float3 center = _OasisData[i].xyz;
                float currentRadius = _OasisData[i].w;

                float dist = distance(positionWS, center) + noiseOffset;
                float localMask = saturate((dist - currentRadius) / max(0.001, _EffectFeather));
                
                finalMask = min(finalMask, localMask);
                
                float edgeDist = abs(dist - currentRadius);
                float localEdgeGlow = smoothstep(_EdgeWidth, 0.0, edgeDist);
                
                finalEdgeGlow = max(finalEdgeGlow, localEdgeGlow);
            }

            #ifdef _INVERT_EFFECT
                finalMask = 1.0 - finalMask;
            #endif
            
            return float2(finalMask, finalEdgeGlow);
        }
        ENDHLSL
        // ------------------------------------------------------------------
        // PASS 1: Forward Lit (Standard URP rendering)
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            
            // Effect Features
            #pragma shader_feature_local _DISSOLVE_MODE
            #pragma shader_feature_local _INVERT_EFFECT
            
            // URP 6.0 Multi-compiles for performance & compatibility
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                output.uv         = input.uv;
                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                float2 effectInfo = CalculateEffect(input.positionWS);
                float mask = effectInfo.x;
                float edgeGlow = effectInfo.y;

                #ifdef _DISSOLVE_MODE
                    clip(mask - 0.001); // Erase pixels (Inside the sphere normally)
                #endif

                // Sample Textures
// Sample Textures
                float2 uvBase = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                half3 colorOutside = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvBase).rgb * _BaseColor.rgb;
                
                // NEW: Apply your C# Manager's Desaturation to the unhealed area!
                half3 grayscale = dot(colorOutside, half3(0.299, 0.587, 0.114));
                colorOutside = lerp(colorOutside, grayscale, _DesaturationAmount);
                
                float2 uvAlt = input.uv * _AltMap_ST.xy + _AltMap_ST.zw;
                half3 colorInside = SAMPLE_TEXTURE2D(_AltMap, sampler_AltMap, uvAlt).rgb * _AltColor.rgb;

                // Blend Albedo and Emission
                half3 albedo = lerp(colorInside, colorOutside, mask);
                half3 emission = edgeGlow * _EdgeColor.rgb;

                // Assemble standard PBR data
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.fogCoord = ComputeFogFactor(input.positionCS.z);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) && !defined(_SURFACE_TYPE_TRANSPARENT)
                    inputData.shadowCoord = ComputeScreenPos(input.positionCS);
                #else
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.emission = emission;
                surfaceData.occlusion = 1.0;
                surfaceData.alpha = 1.0;
                
                // URP Standard Lighting
                half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
                finalColor.rgb = MixFog(finalColor.rgb, inputData.fogCoord);
                
                return finalColor;
            }
            ENDHLSL
        }
        
        // ------------------------------------------------------------------
        // PASS 2: Shadow Caster (Required for holes in shadows during dissolve)
        // ------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #pragma shader_feature_local _DISSOLVE_MODE
            #pragma shader_feature_local _INVERT_EFFECT
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                // URP Shadow bias logic
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - output.positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, normalWS, lightDirectionWS));
                
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                #ifdef _DISSOLVE_MODE
                    float2 effect = CalculateEffect(input.positionWS);
                    clip(effect.x - 0.001);
                #endif
                
                return 0; // Only depth is output
            }
            ENDHLSL
        }
        
        // ------------------------------------------------------------------
        // PASS 3: Depth Only (Required for depth pre-pass and optimizations)
        // ------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #pragma shader_feature_local _DISSOLVE_MODE
            #pragma shader_feature_local _INVERT_EFFECT

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                #ifdef _DISSOLVE_MODE
                    float2 effect = CalculateEffect(input.positionWS);
                    clip(effect.x - 0.001);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}