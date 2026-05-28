Shader "Custom/URP_ProceduralCloth"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.8, 0.1, 0.2, 1)
        _MainTex("Albedo Map", 2D) = "white" {}
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }
        
         Cull Off 

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            
            // Unity 6.4 URP Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct VertexData {
                float3 position;
                float3 normal;
                float2 uv;
            };

            // Bound from a C# script using material.SetBuffer()
            StructuredBuffer<VertexData> _VertexDataBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MainTex_ST;
                float _Smoothness;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD3;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                Varyings output;
                
                // Fetch vertex data mapped 1:1 from compute shader
                VertexData vData = _VertexDataBuffer[vertexID];

                // Procedural generation assumes world space positions from simulation
                float3 positionWS = vData.position;
                float3 normalWS = vData.normal;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldDir(normalWS);
                output.uv = TRANSFORM_TEX(vData.uv, _MainTex);
                
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Sample your texture
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;
                
                // --- 1. MANUALLY POPULATE SURFACE DATA ---
                // Casting from 0 zeroes out the struct, preventing undefined behavior
                SurfaceData surfaceData = (SurfaceData)0; 
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = albedo.a;
                surfaceData.metallic = 0.0h;
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h); // Using WS normals directly below
                surfaceData.emission = half3(0.0h, 0.0h, 0.0h);
                surfaceData.occlusion = 1.0h;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                // --- 2. MANUALLY POPULATE INPUT DATA ---
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS); // Ensure normals are perfectly normalized after interpolation
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                // Essential for URP to cast/receive shadows and ambient light (GI) properly
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = ComputeFogFactor(input.positionCS.z);
                inputData.vertexLighting = half3(0.0h, 0.0h, 0.0h);
                inputData.bakedGI = SampleSH(inputData.normalWS); 
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1.0h, 1.0h, 1.0h, 1.0h);

                // Output final color calculated with URP lighting
                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }
    }
}