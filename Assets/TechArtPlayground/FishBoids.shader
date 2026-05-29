Shader "Custom/URP/FishBoids"
{
    Properties
    {
        [Header(Surface)]
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.8
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0

        [Header(Boid Flocking)]
        _BaseSpeed("Base Speed", Float) = 4.0

        [Header(Animation)]
        _FlapSpeed("Flap Speed", Float) = 1.8
        _FlapAmplitude("Flap Amplitude", Float) = 1.2

        [Header(Colors)]
        _ColorA("Color A", Color) = (1,1,1,1)
        _ColorB("Color B", Color) = (0,0,1,1)

        [Header(Emission)]
        _CalmGlow("Calm Glow", Range(0,1)) = 0.5
        [HDR] _PanicColor("Panic Color", Color) = (2.73, 0, 0, 1)
        _PanicGlow("Panic Glow", Float) = 5.0
        
        [Header(Translucency  Backlight)]
        _TranslucencyPower("Broad Glow Thickness", Range(1.0, 20.0)) = 5.0
        _TranslucencyScale("Broad Glow Intensity", Range(0.0, 5.0)) = 1.0
        
        [Header(Sun Hotspot)]
        _HotspotPower("Core Hotspot Size (Higher = Smaller)", Range(10.0, 200.0)) = 80.0
        _HotspotIntensity("Core HDR Bloom Intensity", Float) = 15.0
        
        _TranslucencyDistortion("Normal Distortion", Range(0.0, 1.0)) = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"
        }
        LOD 300

        // ==========================================
        // PASS 1: MAIN FORWARD LIT
        // ==========================================
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode"="UniversalForward"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // URP Keywords for lighting, shadows, and GI
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            // Crucial for Graphics.DrawMeshInstancedIndirect
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // --- PROPERTIES ---
            CBUFFER_START(UnityPerMaterial)
                float _Smoothness;
                float _Metallic;

                float _BaseSpeed;

                float _FlapSpeed;
                float _FlapAmplitude;

                float4 _ColorA;
                float4 _ColorB;

                float _CalmGlow;
                float4 _PanicColor;
                float _PanicGlow;
            
                float _TranslucencyPower;
                float _TranslucencyScale;
                float _HotspotPower;
                float _HotspotIntensity;
                float _TranslucencyDistortion;
            CBUFFER_END

            // --- BOID DATA (Matched to our optimized 32-byte struct) ---
            struct Boid
            {
                float3 position;
                float randomSeed;
                float3 velocity;
                float colorSeed;
                uint packedData;
                float splineT;
                float pad1;
                float pad2;
            };
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            StructuredBuffer<Boid> boidsBuffer;
            #endif

            // --- PROCEDURAL RECONSTRUCTION ---
            inline float CustHash(uint s)
            {
                s ^= 2747636419u;
                s *= 2654435769u;
                s ^= s >> 16;
                s *= 2654435769u;
                s ^= s >> 16;
                s *= 2654435769u;
                return float(s) / 4294967295.0;
            }

            // Dummy setup function required by Unity's procedural instancing pragma
            void setup()
            {
            }

            // --- SHADER STRUCTS ---
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
                float3 boidColor : TEXCOORD2;
                float animSpeed : TEXCOORD3;
                float fogCoord : TEXCOORD4;
            };

            // --- VERTEX SHADER ---
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                float3 worldPos = input.positionOS.xyz;
                float3 worldNormal = input.normalOS;
                float3 boidColor = float3(1, 1, 1);
                float animSpeed = _BaseSpeed;

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                uint bufferIndex = input.instanceID;
                Boid b = boidsBuffer[bufferIndex];

                uint persistentID = b.packedData >> 16;
                float roll = ((float)(b.packedData & 0xFFFF) / 65535.0) * 6.28318 - 3.14159;

                // ZERO-COST LOOKUP: We no longer hash the persistent ID!
                float size = lerp(0.5, 1.5, b.randomSeed);
                boidColor = lerp(_ColorA.rgb, _ColorB.rgb, b.colorSeed);

                animSpeed = length(b.velocity);
                float3 boidDir = animSpeed > 0.001 ? (b.velocity / animSpeed) : float3(0, 0, 1);

                float flapPhase = _Time.y * _BaseSpeed * 2.0 + (b.randomSeed * 100.0);
                float effortRatio = clamp(animSpeed / max(_BaseSpeed, 0.01), 0.2, 2.0);
                float dynamicAmplitude = _FlapAmplitude * effortRatio;

                // 1. Vertex Offset (Wag)
                float3 scaledPos = input.positionOS.xyz * size;
                float wag = sin(flapPhase * _FlapSpeed + scaledPos.z * 5.0) * (scaledPos.z < 0
                                              ? -scaledPos.z * dynamicAmplitude
                                              : 0.0);
                scaledPos.x += wag;

                // 2. Build TBN Rotation Matrix
                float3 globalUp = float3(0, 1, 0);
                if (abs(dot(boidDir, globalUp)) > 0.999) globalUp = float3(0, 0, 1);

                float3 right = normalize(cross(globalUp, boidDir));
                float3 up = cross(boidDir, right);

                // Apply Banking/Roll
                float s, c;
                sincos(roll, s, c);
                float3 rolledRight = right * c + up * s;
                float3 rolledUp = normalize(cross(boidDir, rolledRight));

                // 3. Apply Rotations
                float3 rotatedPos = rolledRight * scaledPos.x + rolledUp * scaledPos.y + boidDir * scaledPos.z;
                worldPos = rotatedPos + b.position;

                float3 rotatedNormal = rolledRight * input.normalOS.x + rolledUp * input.normalOS.y + boidDir * input.
                                                                  normalOS.z;
                worldNormal = normalize(rotatedNormal);
                #else
                worldPos = TransformObjectToWorld(input.positionOS.xyz);
                worldNormal = TransformObjectToWorldNormal(input.normalOS);
                #endif

                // Pass to Fragment
                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.normalWS = worldNormal;
                output.boidColor = boidColor;
                output.animSpeed = animSpeed;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            // --- FRAGMENT SHADER ---
// --- FRAGMENT SHADER ---
// --- FRAGMENT SHADER ---
            half4 frag(Varyings input) : SV_Target
            {
                // 1. Procedural Color Only
                half3 finalAlbedo = input.boidColor;

                // 2. Dynamic Emission (Calm vs Panic Burst)
                float speedRatio = saturate((input.animSpeed - _BaseSpeed) / max(_BaseSpeed, 0.01));
                half3 calmEmission = input.boidColor * _CalmGlow;
                half3 panicEmission = _PanicColor.rgb * _PanicGlow;
                half3 finalEmission = lerp(calmEmission, panicEmission, speedRatio);

                // ==========================================
                // 3. URP PBR Lighting Setup (MOVED UP)
                // ==========================================
                // We must initialize this FIRST so we have access to shadows and view direction!
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                
                // ==========================================
                // 4. TRANSLUCENCY & SUN HOTSPOT
                // ==========================================
                Light mainLight = GetMainLight(inputData.shadowCoord);
                
                half3 backLightVector = normalize(mainLight.direction + inputData.normalWS * _TranslucencyDistortion);
                half VdotL = saturate(dot(inputData.viewDirectionWS, -backLightVector));
                
                // Layer A: The broad, soft glow of the fish body
                half broadGlow = pow(VdotL, _TranslucencyPower) * _TranslucencyScale;
                
                // Layer B: The "Ball" of light. A much higher power makes it a tiny dot, 
                // and a high intensity pushes it into HDR for the URP Bloom to catch.
                half coreHotspot = pow(VdotL, _HotspotPower) * _HotspotIntensity;
                
                // Combine them and mask out areas in the shadow of rocks/environment
                half totalTranslucency = (broadGlow + coreHotspot) * mainLight.shadowAttenuation;
                
                // Apply the fish's procedural color to the glowing light!
                half3 translucencyGlow = input.boidColor * totalTranslucency;
                
                finalEmission += translucencyGlow;
                // ==========================================

                // 5. Apply Surface Data
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalAlbedo;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.emission = finalEmission; // Glow is now safely applied!
                surfaceData.alpha = 1.0;

                // 6. Calculate Final Lit Pixel
                half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
                finalColor.rgb = MixFog(finalColor.rgb, input.fogCoord);
                
                return finalColor;
            }
            ENDHLSL
        }

        // ==========================================
        // PASS 2: SHADOW CASTER
        // ==========================================
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode"="ShadowCaster"
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:setup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Boid
            {
                float3 position;
                float randomSeed;
                float3 velocity;
                float colorSeed;
                uint packedData;
                float splineT;
                float pad1;
                float pad2;
            };
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            StructuredBuffer<Boid> boidsBuffer;
            #endif

            inline float CustHash(uint s)
            {
                s ^= 2747636419u;
                s *= 2654435769u;
                s ^= s >> 16;
                s *= 2654435769u;
                s ^= s >> 16;
                s *= 2654435769u;
                return float(s) / 4294967295.0;
            }

            void setup()
            {
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 worldPos = input.positionOS.xyz;

                #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                uint bufferIndex = input.instanceID;
                Boid b = boidsBuffer[bufferIndex];

                // ZERO-COST LOOKUP
                float size = lerp(0.5, 1.5, b.randomSeed);
                float animSpeed = length(b.velocity);
                float3 boidDir = animSpeed > 0.001 ? (b.velocity / animSpeed) : float3(0, 0, 1);

                float3 scaledPos = input.positionOS.xyz * size;

                // Simplified Look-At Rotation (No Banking)
                float3 globalUp = float3(0, 1, 0);
                if (abs(dot(boidDir, globalUp)) > 0.999) globalUp = float3(0, 0, 1);
                float3 right = normalize(cross(globalUp, boidDir));
                float3 up = cross(boidDir, right);

                // Apply Rotation & Position
                float3 rotatedPos = right * scaledPos.x + up * scaledPos.y + boidDir * scaledPos.z;
                worldPos = rotatedPos + b.position;
                #else
                worldPos = TransformObjectToWorld(input.positionOS.xyz);
                #endif

                output.positionCS = TransformWorldToHClip(worldPos);
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}