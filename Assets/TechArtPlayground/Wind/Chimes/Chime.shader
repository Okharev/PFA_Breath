Shader "Custom/InstancedChime"
{
    Properties
    {
        _BaseColor("Color", Color) = (0.8, 0.6, 0.2, 1)
        _Metallic("Metallic", Range(0,1)) = 1.0
        _Smoothness("Smoothness", Range(0,1)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }

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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct ChimeData
            {
                float3 pivotPosition;
                float mass;
                float2 angle;
                float2 velocity;
                float length;
                float3 padding;
                float4x4 transformMatrix;
            };

            StructuredBuffer<ChimeData> _ChimeDataBuffer;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Metallic;
                float _Smoothness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float4x4 instanceMatrix = _ChimeDataBuffer[input.instanceID].transformMatrix;

                float3 positionWS = mul(instanceMatrix, float4(input.positionOS.xyz, 1.0)).xyz;
                float3 normalWS = mul((float3x3)instanceMatrix, input.normalOS);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = _BaseColor.rgb;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.alpha = 1.0;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }


    }
}