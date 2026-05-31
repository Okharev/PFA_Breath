Shader "URP/Procedural/StalkLit"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            
            
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ProceduralVertex
            #pragma fragment Fragment
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_instancing 
            #pragma instancing_options procedural:SetupInstancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 stalkColor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct KelpType {
                float4 colorBase; float4 colorTip; float windStrength; float windScale;
                float gravity; float leafScale; float stalkThickness; float3 padding;
            };

            struct StalkNode {
                float3 position; float3 prevPosition; float3 normal; float3 tangent;
                int typeIndex; float3 padding;
            };

            StructuredBuffer<KelpType> _KelpTypes;
            StructuredBuffer<StalkNode> _StalkNodes;

            uint _NodesPerStalk;
            float4x4 _ProceduralObjectToWorld;

            void SetupInstancing()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                // Calculate which nodes this stalk segment connects based purely on the Instance ID
                uint segmentsPerStalk = _NodesPerStalk - 1;
                uint stalkIdx = unity_InstanceID / segmentsPerStalk;
                uint segIdx = unity_InstanceID % segmentsPerStalk;
                
                uint nodeA_Idx = stalkIdx * _NodesPerStalk + segIdx;
                uint nodeB_Idx = nodeA_Idx + 1;

                StalkNode nodeA = _StalkNodes[nodeA_Idx];
                StalkNode nodeB = _StalkNodes[nodeB_Idx];
                KelpType typeData = _KelpTypes[nodeA.typeIndex];

                float3 forward = nodeB.position - nodeA.position;
                float dist = max(length(forward), 0.001);
                forward /= dist; // Normalize

                float3 up = float3(0,1,0);
                if (abs(dot(forward, up)) > 0.999) { up = float3(1, 0, 0); }
                float3 right = normalize(cross(up, forward));
                up = cross(forward, right);

                // Stretch Z to match distance, shrink XY to match thickness
                float r = typeData.stalkThickness * 0.5;

                _ProceduralObjectToWorld = float4x4(
                    right.x * r, up.x * r, forward.x * dist, nodeA.position.x,
                    right.y * r, up.y * r, forward.y * dist, nodeA.position.y,
                    right.z * r, up.z * r, forward.z * dist, nodeA.position.z,
                    0,           0,        0,                1
                );
                #endif
            }

            Varyings ProceduralVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output); 

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                float3 positionWS = mul(_ProceduralObjectToWorld, input.positionOS).xyz;
                float3 normalWS = mul((float3x3)_ProceduralObjectToWorld, input.normalOS);
                
                // Stalk takes the base color of the type
                uint segmentsPerStalk = _NodesPerStalk - 1;
                uint nodeIdx = (unity_InstanceID / segmentsPerStalk) * _NodesPerStalk;
                output.stalkColor = _KelpTypes[_StalkNodes[nodeIdx].typeIndex].colorBase.rgb * 0.7; // Darker than leaves
                #else
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.stalkColor = float3(0.2, 0.4, 0.2);
                #endif

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalize(normalWS);
                return output;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = input.normalWS;
                
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 lighting = mainLight.color * saturate(dot(inputData.normalWS, mainLight.direction));
                float3 ambient = SampleSH(inputData.normalWS);

                return float4(input.stalkColor * (lighting + ambient), 1.0);
            }
            ENDHLSL
        }
    }
}