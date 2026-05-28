Shader "Custom/URP/BannerWindVertexColor"
{
    Properties
    {
        [Header(Surface Properties)]
        _BaseColor ("Base Color", Color) = (0.8, 0.1, 0.1, 1.0)
        _SSSColor ("Subsurface Scattering Color", Color) = (1.0, 0.3, 0.2, 1.0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        
        [Header(Wind Simulation Constraints)]
        _AnchorGradient ("Tension Power", Range(0.1, 5.0)) = 1.5
                
        [Header(The BIG Movements (Mass and Gusts))]
        _SwayAmplitude ("Heavy Sway (Big Gusts)", Float) = 2.5
        _FlapAmplitude ("Tail Snap (Whip)", Float) = 1.0
        _FlapFrequency ("Snap Frequency", Float) = 6.0
        
        [Header(Primary Wind (Billow))]
        _WindDirection ("Wind Direction", Vector) = (0, 0, 1, 0)
        _WindSpeed ("Wind Speed", Float) = 3.0
        _BillowAmplitude ("Billow Amplitude", Float) = 0.4
        _BillowFrequency ("Billow Frequency", Vector) = (2.0, 1.5, 0, 0)
        
        [Header(Secondary Wind (Ripples))]
        _RippleAmplitude ("Ripple Amplitude", Float) = 0.08
        _RippleFrequency ("Ripple Frequency", Vector) = (8.0, 12.0, 0, 0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry" 
        }
        
        // Culling disabled so we can see both sides of the banner
        Cull Off 

        // ==================================================================
        // FORWARD LIT PASS
        // Handles the actual drawing, lighting, and colors of the banner
        // ==================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            
            // URP specific keywords for shadows
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Data pulled directly from the Mesh
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;     // <-- Reads our painted Vertex Colors
            };

            // Data passed from the Vertex Shader to the Fragment Shader
            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD3;
            };

            // Material properties grouped for SRP Batcher compatibility (Performance)
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SSSColor;
                half _Smoothness;
                
                float _AnchorGradient;
                float4 _WindDirection;
                float _WindSpeed;
                
                float _SwayAmplitude;   // <-- ADD THIS
                float _FlapAmplitude;
                float _FlapFrequency;
                
                float _BillowAmplitude;
                float4 _BillowFrequency;
                
                float _RippleAmplitude;
                float4 _RippleFrequency;
            CBUFFER_END

// --- PROCEDURAL WIND, WHIP, AND MASS FUNCTION ---
            float3 CalculateWindDisplacement(float3 positionOS, float anchorWeight)
            {
                float3 windDir = normalize(_WindDirection.xyz);
                float t = _Time.y * _WindSpeed;

                // Base Axes (Up/Down, Left/Right relative to wind)
                float3 globalUp = float3(0, 1, 0);
                float3 sideAxis = normalize(cross(windDir, globalUp));
                if (length(sideAxis) < 0.01) sideAxis = float3(1, 0, 0); 
                float3 verticalAxis = normalize(cross(sideAxis, windDir));

                // -------------------------------------------------------------
                // FORCE 1: LOCAL RIPPLES (The fabric surface texture)
                // -------------------------------------------------------------
                float rippleMask = pow(saturate(anchorWeight), _AnchorGradient);
                float billow = sin(positionOS.x * _BillowFrequency.x + positionOS.y * _BillowFrequency.y - t) * _BillowAmplitude;
                float ripple = cos(positionOS.x * _RippleFrequency.x + positionOS.y * _RippleFrequency.y - t * 2.5) * _RippleAmplitude;
                float3 waveDisplacement = windDir * ((billow + ripple) * rippleMask);


                // -------------------------------------------------------------
                // FORCE 2: THE HEAVY HEAVE (The BIG, dramatic movements)
                // -------------------------------------------------------------
                // We use a drastically slowed down time variable (30% speed). 
                // Big mass moves slowly. When this hits, it swings the entire flag.
                float gustTime = t * 0.3; 
                float bigGust = sin(gustTime) * cos(gustTime * 0.73) * _SwayAmplitude;
                
                // We use a lower power (1.5 instead of 3.0) so the middle of the flag 
                // gets pulled along smoothly, creating a massive, sweeping curve.
                float heaveMask = pow(saturate(anchorWeight), 1.5); 
                float3 heavyDisplacement = sideAxis * (bigGust * heaveMask);


                // -------------------------------------------------------------
                // FORCE 3: THE TAIL SNAP (Fast, violent whipping)
                // -------------------------------------------------------------
                // The energy travels down the fabric, snapping only at the very end
                float spatialOffset = dot(positionOS, windDir) * 1.5;
                float flapTime = (_Time.y * _FlapFrequency) - spatialOffset;
                
                float sideWhip = sin(flapTime) * cos(flapTime * 0.43);
                float verticalWhip = cos(flapTime * 1.2) * sin(flapTime * 0.77);
                
                // Cubed mask (3.0) means this violent energy ONLY affects the final 30% of the tail
                float whipMask = pow(saturate(anchorWeight), 3.0) * _FlapAmplitude;
                float3 snapDisplacement = (sideAxis * sideWhip + verticalAxis * verticalWhip) * whipMask;

                // Combine all three physical forces
                return waveDisplacement + heavyDisplacement + snapDisplacement;
            }
            
            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // 1. Move the main vertex
                float3 displacement = CalculateWindDisplacement(input.positionOS.xyz, input.color.r);
                float3 displacedPosOS = input.positionOS.xyz + displacement;

                // 2. Finite Difference Method: Recalculate Normals for accurate lighting
                // We fake-move vertices slightly to the right and up, then create a new normal
                float epsilon = 0.01;
                
                // Sample Right
                float3 posRightOS = input.positionOS.xyz + input.tangentOS.xyz * epsilon;
                float3 displacedRightOS = posRightOS + CalculateWindDisplacement(posRightOS, input.color.r);

                // Sample Up
                float3 bitangentOS = cross(input.normalOS, input.tangentOS.xyz) * input.tangentOS.w;
                float3 posUpOS = input.positionOS.xyz + bitangentOS * epsilon;
                float3 displacedUpOS = posUpOS + CalculateWindDisplacement(posUpOS, input.color.r);

                // Cross product constructs the new mathematically perfect normal
                float3 newTangentOS = normalize(displacedRightOS - displacedPosOS);
                float3 newBitangentOS = normalize(displacedUpOS - displacedPosOS);
                float3 newNormalOS = normalize(cross(newTangentOS, newBitangentOS));

                // 3. Transform geometry data to World Space for the camera
                output.positionWS = TransformObjectToWorld(displacedPosOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(newNormalOS);
                output.uv = input.uv;

                return output;
            }

half4 LitPassFragment(Varyings input, float facing : VFACE) : SV_Target            {
                // If viewing the back of the flag, invert the normal so lighting isn't black
// Remove "input." from input.facing
            float3 normalWS = normalize(input.normalWS) * (facing > 0 ? 1.0 : -1.0);
            float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                        
                // Fetch Unity Main Directional Light & Shadow data
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lightColor = mainLight.color;
                half3 lightDir = mainLight.direction;
                half shadowAttenuation = mainLight.shadowAttenuation;

                // --- PBR Lighting Model ---
                half NdotL = saturate(dot(normalWS, lightDir));
                
                // Soft wrap lighting (prevents harsh shadows on soft fabric)
                half wrap = 0.5;
                half diffuseWrap = saturate((dot(normalWS, lightDir) + wrap) / ((1.0 + wrap) * (1.0 + wrap)));
                
                // Subsurface Scattering (SSS)
                // Glows brightly when the sun shines through the fabric directly at the camera
                half translucency = saturate(dot(viewDirWS, -lightDir)) * (1.0 - NdotL);
                translucency = pow(translucency, 4.0) * shadowAttenuation;

                // Combine Light Components
                half3 diffuseLighting = lightColor * (diffuseWrap * shadowAttenuation);
                half3 sssLighting = lightColor * translucency * _SSSColor.rgb;
                half3 ambientLighting = SampleSH(normalWS); // Unity ambient/skybox light

                // Final Output
                half3 finalColor = _BaseColor.rgb * (diffuseLighting + ambientLighting) + sssLighting;

                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }

        // ==================================================================
        // SHADOW CASTER PASS
        // Casts accurate shadows based on the flapping geometry
        // ==================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;     // <-- Needs Vertex Colors too!
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float _AnchorGradient;
                float4 _WindDirection;
                float _WindSpeed;
                float _BillowAmplitude;
                float4 _BillowFrequency;
                float _RippleAmplitude;
                float4 _RippleFrequency;
            CBUFFER_END

            // Identical to ForwardLit: Keeps the shadow perfectly synced with the visible mesh
            float3 CalculateWindDisplacement(float3 positionOS, float anchorWeight)
            {
                float anchorMask = pow(saturate(anchorWeight), _AnchorGradient);
                float t = _Time.y * _WindSpeed;

                float billow = sin(positionOS.x * _BillowFrequency.x + positionOS.y * _BillowFrequency.y - t) * _BillowAmplitude;
                float ripple = cos(positionOS.x * _RippleFrequency.x + positionOS.y * _RippleFrequency.y - t * 2.5) * _RippleAmplitude;

                float3 windDir = normalize(_WindDirection.xyz);
                return windDir * ((billow + ripple) * anchorMask);
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                
                // Displace shadow geometry
                float3 displacedPosOS = input.positionOS.xyz + CalculateWindDisplacement(input.positionOS.xyz, input.color.r);
                
                // Transform for shadow mapping
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(displacedPosOS));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                // Shadow casters just write depth, no color needed
                return 0;
            }
            ENDHLSL
        }
    }
}