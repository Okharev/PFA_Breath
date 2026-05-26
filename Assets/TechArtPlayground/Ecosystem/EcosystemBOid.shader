Shader "TechArt/EcosystemBoid_URP"
{
    Properties
    {
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Header(Animation Settings)]
        [KeywordEnum(Fish, Ray, Orca)] _AnimType ("Swim Animation Type", Float) = 0

        // This is set automatically by our C# script using mat.SetFloat()
        [HideInInspector] _SpeciesOffset ("Species Offset", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // --- FORWARD LIT PASS ---
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP Keywords for Lighting and Shadows
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT

            // Unity Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // --- DATA STRUCTURES ---
            struct EcosystemBoid
            {
                float3 position;
                float3 direction;
                float currentSpeed;
                float roll;
                float flapPhase;
                float flapAmplitude; // <-- Check here!
                float size; // <-- Check here!
                uint schoolID;
            };

            StructuredBuffer<EcosystemBoid> boidsBuffer;

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _AnimType;
                float _SpeciesOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID; // Injected by Graphics.RenderMeshIndirect
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            // --- VERTEX SHADER ---
            Varyings vert(Attributes input)
            {
                Varyings output;

                // 1. Fetch Boid Data
                uint id = input.instanceID + (uint)_SpeciesOffset;
                EcosystemBoid b = boidsBuffer[id];

                // 2. Orientation & Roll Matrix
                float3 boidDir = normalize(b.direction);
                if (length(boidDir) < 0.1) boidDir = float3(0, 0, 1);

                float3 up = float3(0, 1, 0);
                if (abs(dot(boidDir, up)) > 0.99) up = float3(0, 0, 1);

                float3 right = normalize(cross(up, boidDir));
                up = cross(boidDir, right);

                float c = cos(b.roll);
                float s = sin(b.roll);
                float3 rolledRight = right * c + up * s;
                float3 rolledUp = up * c - right * s;

                // 3. Biomechanical Animation (Vertex Deformation)

                // NEW: Apply Scale Heterogeneity before any animation or rotation!
                float3 localPos = input.positionOS.xyz * b.size;

                float animPhase = b.flapPhase + localPos.z * 5.0;

                if (_AnimType < 0.5) // 0 = Fish (Tail Wag)
                {
                    // NEW: Multiply the wag amount by the boid's effort. 
                    // When gliding, amplitude drops to 0.2, straightening the tail!
                    float wag = sin(animPhase) * (localPos.z < 0 ? -localPos.z * 0.4 : 0.0) * b.flapAmplitude;
                    localPos.x += wag;
                }
                else if (_AnimType < 1.5) // 1 = Ray (Wing Undulation)
                {
                    // Rays always glide a little bit, so we soften the amplitude drop
                    float smoothedAmplitude = lerp(0.5, 1.0, b.flapAmplitude);
                    float flap = sin(b.flapPhase - abs(localPos.x) * 3.0) * abs(localPos.x) * 0.3 * smoothedAmplitude;
                    localPos.y += flap;
                }
                else // 2 = Orca (Full Body Wave)
                {
                    // Orcas hold their massive bodies very stiff when striking/gliding
                    float wave = sin(animPhase * 0.5) * 0.2 * b.flapAmplitude;
                    localPos.x += wave;
                    localPos.y += cos(animPhase * 0.5) * 0.1 * (localPos.z < 0 ? -localPos.z : 0.0) * b.flapAmplitude;
                }

                // 4. Transform Position to World Space
                float3 rotatedPos = rolledRight * localPos.x + rolledUp * localPos.y + boidDir * localPos.z;
                output.positionWS = rotatedPos + b.position;

                // 5. Transform Normal to World Space
                float3 rotatedNormal = rolledRight * input.normalOS.x + rolledUp * input.normalOS.y + boidDir * input.
                                                                          normalOS.z;
                output.normalWS = normalize(rotatedNormal);

                // 6. Project to Clip Space
                output.positionCS = TransformWorldToHClip(output.positionWS);

                return output;
            }

            // --- FRAGMENT SHADER ---
            half4 frag(Varyings input) : SV_Target
            {
                // Simple URP Lighting calculation
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);

                // Get Main Light (Sun)
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));

                // Diffuse Lighting (N dot L)
                half NdotL = saturate(dot(inputData.normalWS, mainLight.direction));
                half3 lighting = mainLight.color * (NdotL * mainLight.shadowAttenuation);

                // Add simple ambient light to prevent pitch-black shadows underwater
                half3 ambient = half3(0.1, 0.2, 0.3);

                half3 finalColor = _BaseColor.rgb * (lighting + ambient);

                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }

        // --- SHADOW CASTER PASS ---
        // Crucial for letting the schools cast dynamic shadows onto the sea floor and each other
        // --- SHADOW CASTER PASS ---
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow

            // Required to differentiate between Directional (Sun) and Point/Spot lights
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // --- THE FIX: Explicitly declare the variables Unity's CPU is trying to send ---
            float3 _LightDirection;
            float3 _LightPosition;

            // --- DATA STRUCTURES ---
            struct EcosystemBoid
            {
                float3 position;
                float3 direction;
                float currentSpeed;
                float roll;
                float flapPhase;
                float flapAmplitude; // <-- Check here too!
                float size; // <-- Check here too!
                uint schoolID;
            };

            StructuredBuffer<EcosystemBoid> boidsBuffer;

            CBUFFER_START(UnityPerMaterial)
                float _AnimType;
                float _SpeciesOffset;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vertShadow(Attributes input)
            {
                Varyings output;

                uint id = input.instanceID + (uint)_SpeciesOffset;
                EcosystemBoid b = boidsBuffer[id];

                // Orientation & Roll Matrix
                float3 boidDir = normalize(b.direction);
                if (length(boidDir) < 0.1) boidDir = float3(0, 0, 1);

                float3 up = float3(0, 1, 0);
                if (abs(dot(boidDir, up)) > 0.99) up = float3(0, 0, 1);
                float3 right = normalize(cross(up, boidDir));
                up = cross(boidDir, right);

                float c = cos(b.roll);
                float s = sin(b.roll);
                float3 rolledRight = right * c + up * s;
                float3 rolledUp = up * c - right * s;

                // Biomechanical Animation
                float3 localPos = input.positionOS.xyz;
                float animPhase = b.flapPhase + localPos.z * 5.0;

                if (_AnimType < 0.5)
                {
                    float wag = sin(animPhase) * (localPos.z < 0 ? -localPos.z * 0.4 : 0.0);
                    localPos.x += wag;
                }
                else if (_AnimType < 1.5)
                {
                    float flap = sin(b.flapPhase - abs(localPos.x) * 3.0) * abs(localPos.x) * 0.3;
                    localPos.y += flap;
                }
                else
                {
                    float wave = sin(animPhase * 0.5) * 0.2;
                    localPos.x += wave;
                    localPos.y += cos(animPhase * 0.5) * 0.1 * (localPos.z < 0 ? -localPos.z : 0.0);
                }

                // Transform Position & Normal
                float3 rotatedPos = rolledRight * localPos.x + rolledUp * localPos.y + boidDir * localPos.z;
                float3 positionWS = rotatedPos + b.position;

                float3 rotatedNormal = rolledRight * input.normalOS.x + rolledUp * input.normalOS.y + boidDir * input.
                    normalOS.z;
                float3 normalWS = normalize(rotatedNormal);

                // --- Calculate correct light direction for shadow bias ---
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                // Apply shadow bias to prevent acne
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                return output;
            }

            half4 fragShadow(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}