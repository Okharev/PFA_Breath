Shader "URP/Procedural/KelpLit"
{
    Properties
    {
        _Smoothness ("Smoothness", Range(0, 1)) = 0.6
        _Subsurface ("Subsurface SSS (Fake)", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off 

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ProceduralVertex
            #pragma fragment Fragment
            
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing 
            #pragma instancing_options procedural:SetupInstancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
                float3 leafColor  : TEXCOORD3; // <--- FIX: Passing color safely per-vertex
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // STRICT ALIGNED STRUCTS
            struct KelpType {
                float4 colorBase; float4 colorTip; float windStrength; float windScale;
                float gravity; float leafScale; float stalkThickness; float3 padding;
            };

            struct LeafObject {
                int stalkNodeIndex; int leafNodeStartIndex; int typeIndex; float colorGradientLerp;
                float4 restRotation; float coneAngle; float3 padding;
            };
            
            struct LeafNode {
                float3 position; float3 prevPosition; float2 padding;
            };

            StructuredBuffer<KelpType> _KelpTypes;
            StructuredBuffer<LeafObject> _LeafObjects;
            StructuredBuffer<LeafNode> _LeafNodes;

            float _Smoothness;
            float _Subsurface;
            float4x4 _ProceduralObjectToWorld;

            void SetupInstancing()
            {
                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                LeafObject leaf = _LeafObjects[unity_InstanceID];
                KelpType typeData = _KelpTypes[leaf.typeIndex];
                LeafNode rootNode = _LeafNodes[leaf.leafNodeStartIndex];
                LeafNode tipNode = _LeafNodes[leaf.leafNodeStartIndex + 2];

                // 1. Find the distance between the root and tip of the physics simulation
                float3 forward = tipNode.position - rootNode.position;
                float dist = max(length(forward), 0.001);
                forward /= dist; // Normalize

                // 2. Kelp blades hang vertically. We must map the Mesh's X-axis (Width) 
                // to point Upwards, and its Y-axis (Thickness) horizontally.
                float3 worldUp = float3(0,1,0); 
                if (abs(dot(forward, worldUp)) > 0.999) { worldUp = float3(1, 0, 0); }

                float3 meshY = normalize(cross(worldUp, forward)); // Horizontal vector
                float3 meshX = cross(forward, meshY); // Vertical vector

                float s = typeData.leafScale; 

                // 3. Construct Matrix: We stretch the Z-axis by 'dist' so the geometry 
                // physically stretches and compresses with the wind simulation!
                _ProceduralObjectToWorld = float4x4(
                    meshX.x * s, meshY.x * s, forward.x * dist, rootNode.position.x,
                    meshX.y * s, meshY.y * s, forward.y * dist, rootNode.position.y,
                    meshX.z * s, meshY.z * s, forward.z * dist, rootNode.position.z,
                    0,           0,           0,                1
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
                
                // FIX: Calculate Color here and pack it into the struct!
                LeafObject leaf = _LeafObjects[unity_InstanceID];
                KelpType typeData = _KelpTypes[leaf.typeIndex];
                output.leafColor = lerp(typeData.colorBase.rgb, typeData.colorTip.rgb, leaf.colorGradientLerp);
                #else
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.leafColor = float3(0.2, 0.8, 0.2); // Fallback
                #endif

                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = normalize(normalWS);
                output.uv = input.uv;
                
                return output;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = input.normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                
                // FIX: Read safe interpolated color
                float3 albedo = lerp(input.leafColor * 0.4, input.leafColor, input.uv.y);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                
                float sss = saturate(dot(inputData.viewDirectionWS, -mainLight.direction)) * _Subsurface;
                float3 lighting = mainLight.color * (saturate(dot(inputData.normalWS, mainLight.direction)) + sss);
                float3 ambient = SampleSH(inputData.normalWS);

                return float4(albedo * (lighting + ambient), 1.0);
            }
            ENDHLSL
        }
    }
}