Shader "Custom/URP/ComputePhysicsBanner"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.8, 0.1, 0.1, 1.0)
        _SSSColor ("Subsurface Scattering Color", Color) = (1.0, 0.3, 0.2, 1.0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry"
        }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // La structure EXACTE de ton Compute Shader
            struct VertexData
            {
                float3 position;
                float3 normal;
                float2 uv;
            };

            StructuredBuffer<VertexData> _VertexDataBuffer;

            struct Attributes
            {
                uint vertexID : SV_VertexID; // Identifiant magique pour lire le buffer
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SSSColor;
                half _Smoothness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                // On lit la physique ! (Déjà en World Space grâce au Compute Shader)
                VertexData data = _VertexDataBuffer[input.vertexID];

                output.positionWS = data.position;
                output.positionCS = TransformWorldToHClip(data.position);
                output.normalWS = normalize(data.normal);
                output.uv = data.uv;

                return output;
            }

            half4 Frag(Varyings input, float facing : VFACE) : SV_Target
            {
                // Inversion de la normale pour le dos du tissu
                float3 normalWS = normalize(input.normalWS) * (facing > 0 ? 1.0 : -1.0);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half NdotL = saturate(dot(normalWS, mainLight.direction));

                // Wrap Lighting (lumière douce)
                half wrap = 0.5;
                half diffuseWrap =
                    saturate((dot(normalWS, mainLight.direction) + wrap) / ((1.0 + wrap) * (1.0 + wrap)));

                // Subsurface Scattering (lumière qui traverse le tissu)
                half translucency = saturate(dot(viewDirWS, -mainLight.direction)) * (1.0 - NdotL);
                translucency = pow(translucency, 4.0) * mainLight.shadowAttenuation;

                half3 diffuseLighting = mainLight.color * (diffuseWrap * mainLight.shadowAttenuation);
                half3 sssLighting = mainLight.color * translucency * _SSSColor.rgb;
                half3 ambientLighting = SampleSH(normalWS);

                half3 finalColor = _BaseColor.rgb * (diffuseLighting + ambientLighting) + sssLighting;
                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct VertexData
            {
                float3 position;
                float3 normal;
                float2 uv;
            };

            StructuredBuffer<VertexData> _VertexDataBuffer;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                VertexData data = _VertexDataBuffer[input.vertexID];
                output.positionCS = TransformWorldToHClip(data.position);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}