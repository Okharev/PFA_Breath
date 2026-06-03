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
        _GlobalEffectCenter ("Effect Center (Fallback/Inspector)", Vector) = (0, 0, 0, 0)
        _EffectRadius ("Effect Radius", Float) = 5.0
        _EffectFeather ("Transition Feather/Softness", Range(0.001, 5.0)) = 1.0
        
        [Header(Edge Distortion (Noise))]
        _NoiseMap ("Noise Map", 2D) = "gray" {}
        _NoiseScale ("Noise Scale (World Space)", Float) = 0.5
        _NoiseStrength ("Noise Strength", Float) = 1.0

        [Header(Edge Emission Glowing Border)]
        [HDR] _EdgeColor ("Edge Glow Color", Color) = (0, 2, 4, 1)
        _EdgeWidth ("Edge Glow Width", Range(0.0, 2.0)) = 0.2

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

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _AltColor;
            float4 _BaseMap_ST;
            float4 _AltMap_ST;
            // ---> I removed float _EffectRadius from here! <---
            float _EffectFeather;
            float _NoiseScale;
            float _NoiseStrength;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _Metallic;
            float _Smoothness;
        CBUFFER_END

        // Declared outside CBUFFER to allow easy global override via Shader.SetGlobal...
        float4 _GlobalEffectCenter;
        float _EffectRadius; // ---> I moved it down here! <---

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_AltMap);  SAMPLER(sampler_AltMap);
        TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

        // Core Mask Logic: Returns x = Mask (0 to 1), y = Edge Glow Emission
        float2 CalculateEffect(float3 positionWS)
        {
            float2 noiseUV = positionWS.xz * _NoiseScale;
            float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUV).r;
            
            // Perturb distance via noise to create a stylized organic transition edge
            float dist = distance(positionWS, _GlobalEffectCenter.xyz) + (noise - 0.5) * _NoiseStrength;
            
            // Calculate the 0-1 mask
            float mask = saturate((dist - _EffectRadius) / max(0.001, _EffectFeather));
            
            #ifdef _INVERT_EFFECT
                mask = 1.0 - mask;
            #endif
            
            // Edge calculation based strictly on the boundary
            float edgeDist = abs(dist - _EffectRadius);
            float edgeGlow = smoothstep(_EdgeWidth, 0.0, edgeDist); // Peaks precisely at the boundary
            
            return float2(mask, edgeGlow);
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
                float2 uvBase = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                half3 colorOutside = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvBase).rgb * _BaseColor.rgb;
                
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