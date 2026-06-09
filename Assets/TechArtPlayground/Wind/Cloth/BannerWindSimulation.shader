Shader "Custom/URP/ComputePhysicsBanner"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.8, 0.1, 0.1, 1.0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _TranslucencyStrength ("Translucency Strength", Range(0, 5)) = 1.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull Off // Double-sided rendering

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // URP Keywords for Shadows and Additional Lights
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Structure of Arrays (SoA) Buffers
            StructuredBuffer<float3> _PositionsBuffer;
            StructuredBuffer<float3> _NormalsBuffer;
            StructuredBuffer<float2> _UVsBuffer;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3; // Calculated in Vertex for performance
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                half _TranslucencyStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // Read from separated buffers
                float3 position = _PositionsBuffer[input.vertexID];
                float3 normal = _NormalsBuffer[input.vertexID];
                float2 uv = _UVsBuffer[input.vertexID];

                output.positionWS = position;
                output.positionCS = TransformWorldToHClip(position);
                output.normalWS = normal;
                output.uv = uv;

                // Optimization: Calculate shadow coordinate per-vertex, not per-pixel
                output.shadowCoord = TransformWorldToShadowCoord(position);

                return output;
            }

            // Helper function for Cloth Lighting (Diffuse + SSS + Specular)
            half3 CalculateClothLighting(Light light, float3 normalWS, float3 viewDirWS, float facing)
            {
                half NdotL = saturate(dot(normalWS, light.direction));
                
                // 1. Wrap Diffuse (softens harsh shadows on fabric)
                half wrap = 0.5;
                half diffuseWrap = saturate((dot(normalWS, light.direction) + wrap) / ((1.0 + wrap) * (1.0 + wrap)));
                
                // 2. Dynamic Subsurface Scattering (Inferred from Light Color + Base Color)
                half translucency = saturate(dot(viewDirWS, -light.direction)) * (1.0 - NdotL);
                translucency = pow(translucency, 4.0) * light.shadowAttenuation * light.distanceAttenuation;
                half3 sssLighting = light.color * translucency * _BaseColor.rgb * _TranslucencyStrength;

                // 3. Specular Highlight (Driven by _Smoothness)
                half3 halfVector = normalize(light.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfVector));
                half specularModifier = pow(NdotH, exp2(10.0 * _Smoothness + 1.0));
                half3 specularLighting = light.color * specularModifier * light.shadowAttenuation * light.distanceAttenuation;

                // 4. Combine
                half3 diffuseLighting = light.color * (diffuseWrap * light.shadowAttenuation * light.distanceAttenuation);
                return _BaseColor.rgb * diffuseLighting + sssLighting + specularLighting;
            }

            half4 Frag(Varyings input, float facing : VFACE) : SV_Target
            {
                // Ensure normal faces the camera for the back sides of the cloth
                float3 normalWS = normalize(input.normalWS) * (facing > 0 ? 1.0 : -1.0);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                // --- MAIN LIGHT (Sun) ---
                Light mainLight = GetMainLight(input.shadowCoord);
                half3 finalLighting = CalculateClothLighting(mainLight, normalWS, viewDirWS, facing);

                // --- AMBIENT LIGHT (Skybox) ---
                finalLighting += _BaseColor.rgb * SampleSH(normalWS);

                // --- ADDITIONAL LIGHTS (Torches, Point Lights, etc.) ---
                #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light addLight = GetAdditionalLight(lightIndex, input.positionWS);
                    finalLighting += CalculateClothLighting(addLight, normalWS, viewDirWS, facing);
                }
                #endif

                return half4(finalLighting, _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            // Required for URP shadow math
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // MUST declare buffers again in this pass!
            StructuredBuffer<float3> _PositionsBuffer;
            StructuredBuffer<float3> _NormalsBuffer;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = _PositionsBuffer[input.vertexID];
                float3 normalWS = _NormalsBuffer[input.vertexID];

                // Safely applies URP shadow bias based on the light direction to prevent Shadow Acne
                float3 lightDirectionWS = _MainLightPosition.xyz;
                float3 biasedPositionWS = ApplyShadowBias(positionWS, normalWS, lightDirectionWS);
                
                output.positionCS = TransformWorldToHClip(biasedPositionWS);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0; // The shadow map only cares about depth, not color
            }
            ENDHLSL
        }

// --- DEPTH ONLY PASS (For Depth Prepass, DoF, SSAO) ---
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0 // We only care about writing to the depth buffer

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> _PositionsBuffer;

            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = _PositionsBuffer[input.vertexID];
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // --- DEPTH NORMALS PASS (For SSAO and Decals) ---
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            StructuredBuffer<float3> _PositionsBuffer;
            StructuredBuffer<float3> _NormalsBuffer;

            struct Attributes { uint vertexID : SV_VertexID; };
            
            struct Varyings 
            { 
                float4 positionCS : SV_POSITION; 
                float3 normalWS : TEXCOORD0;
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = _PositionsBuffer[input.vertexID];
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = _NormalsBuffer[input.vertexID];
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                // Output normal mapped to 0-1 range for the DepthNormals texture
                return half4(input.normalWS * 0.5 + 0.5, 1.0);
            }
            ENDHLSL
        }
    }
}