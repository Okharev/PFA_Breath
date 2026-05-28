Shader "Custom/URP/ProceduralChimes"
{
    Properties
    {
        [Header(Surface Textures)]
        _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _MetallicGlossMap("Metallic (R) Smoothness (A)", 2D) = "white" {}
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _BumpMap("Normal Map", 2D) = "bump" {}
        
        [Header(Local Wind Overrides (Optional))]
        _WindStrengthMultiplier("Wind Strength Multiplier", Range(0, 5)) = 1.0
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

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Metallic;
            float _Smoothness;
            float _WindStrengthMultiplier;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        // --- GLOBAL WIND VARIABLES ---
        float4 _GlobalWindDirection; 
        float _GlobalWindStrength;
        float4 _GlobalWindMapping; 
        
        TEXTURE2D(_GlobalWindNoise); 
        SAMPLER(sampler_GlobalWindNoise);

        // --- GLOBAL GUST FUNCTION ---
        float GetGlobalGust(float3 positionWS)
        {
            float2 worldUV = positionWS.xz * _GlobalWindMapping.z;
            float2 scrolledUV = worldUV - _GlobalWindMapping.xy;
            float gustRaw = SAMPLE_TEXTURE2D_LOD(_GlobalWindNoise, sampler_GlobalWindNoise, scrolledUV, 0).r;
            return smoothstep(0.2, 0.8, gustRaw);
        }

        float3x3 AngleAxis3x3(float angle, float3 axis)
        {
            float c, s;
            sincos(angle, s, c);
            float t = 1.0 - c;
            float x = axis.x, y = axis.y, z = axis.z;
            return float3x3(
                t * x * x + c,      t * x * y - s * z,  t * x * z + s * y,
                t * x * y + s * z,  t * y * y + c,      t * y * z - s * x,
                t * x * z - s * y,  t * y * z + s * x,  t * z * z + c
            );
        }

        void ApplyChimeWind(inout float3 positionOS, inout float3 normalOS, float4 pivotData, float4 vertexColor)
        {
            float3 pivotOS = pivotData.xyz;
            float ropeT = pivotData.w; 
            
            float vMask = vertexColor.r;
            float rigidity = vertexColor.g;         
            float weight = max(vertexColor.b, 0.1); 
            
            float3 pivotWS = TransformObjectToWorld(pivotOS);
            
            // --- MACRO GUST APPLICATION ---
            float baseWindStrength = (_GlobalWindStrength > 0.0 ? _GlobalWindStrength : 1.0) * _WindStrengthMultiplier;
            float macroGustMultiplier = GetGlobalGust(pivotWS);
            float windStrength = baseWindStrength * macroGustMultiplier;

            float3 windDirWS = length(_GlobalWindDirection.xyz) > 0.1 ? normalize(_GlobalWindDirection.xyz) : float3(1,0,0);
            float3 windDirOS = normalize(TransformWorldToObjectDir(windDirWS));

            // ==========================================
            // 1. Rope Sway 
            // ==========================================
            float ropePhase = dot(pivotWS, float3(0.1, 0.0, 0.1)) + _Time.y;
            float anchorMask = sin(ropeT * 3.14159265);
            float ropeSwayAmount = sin(ropePhase) * (windStrength * 0.25) * anchorMask;
            float3 ropeSwayOffsetOS = windDirOS * ropeSwayAmount;
            
            float3 displacedPosOS = positionOS + ropeSwayOffsetOS;
            float3 displacedPivotOS = pivotOS + ropeSwayOffsetOS;

            // ==========================================
            // 2. Chime Swing
            // ==========================================
            float chimePhase = dot(pivotWS, float3(0.5, 0.0, 0.5)) + (_Time.y * 2.0);
            float gust = sin(chimePhase) * 0.5 + 0.5;
            float flutter = sin(chimePhase * 4.3) * 0.2;
            float windForce = (gust + flutter) * (windStrength / weight);

            // Rigid Math (Bells)
            float3 rotAxis = normalize(cross(float3(0, 1, 0), windDirOS));
            float angle = windForce * 1.5; 
            float3x3 rotMatrix = AngleAxis3x3(angle, rotAxis);
            float3 rigidPosOS = mul(rotMatrix, displacedPosOS - displacedPivotOS) + displacedPivotOS;
            float3 rigidNormalOS = mul(rotMatrix, normalOS);

            // ==========================================
            // UPGRADED Soft Math (Ribbons) 
            // ==========================================
// ==========================================
            // UPGRADED Soft Math (Ribbons) - STABILIZED
            // ==========================================
            float bendFactor = vMask * vMask;

            // 1. LOCALIZED TURBULENCE (Smoothed & Slowed)
            // Lowered frequencies and reduced the amplitude (0.15 down from 0.35) 
            // so it sways organically rather than snapping instantly.
            float3 gustNoiseOffset = float3(
                sin(pivotWS.x * 0.5 + _Time.y * 1.2),
                cos(pivotWS.y * 0.4 - _Time.y * 1.0),
                sin(pivotWS.z * 0.6 + _Time.y * 1.4)
            ) * 0.15;
            float3 localizedWindDir = normalize(windDirOS + gustNoiseOffset);

            // 2. VORTEX SHEDDING (Edge Rippling)
            // FIX: Using 'vMask' (0.0 to 1.0) instead of raw 'positionOS.y'.
            // This ensures the wave scales perfectly no matter how tall your 3D model is.
            float rippleSpeed = _Time.y * 10.0;
            float ripplePhase = (vMask * 6.0) - rippleSpeed;
            float3 crossWind = cross(float3(0, 1, 0), localizedWindDir);
            
            // Lowered the ripple intensity heavily to prevent zig-zag contortions
            float3 rippleOffset = crossWind * sin(ripplePhase) * (bendFactor * 0.05 * windForce);

            // 3. TORSIONAL TWIST
            // FIX: We use min() to strictly cap the rotation angle to 0.8 radians (~45 degrees).
            // No matter how high the wind force goes, it will never spiral into a full corkscrew.
            float twistPhase = _Time.y * 2.0 + (pivotWS.x * 0.5);
            float twistAngle = sin(twistPhase) * bendFactor * min(windForce * 0.3, 0.8);
            float3x3 twistMatrix = AngleAxis3x3(twistAngle, float3(0, 1, 0));

            // --- APPLY DEFORMATIONS ---
            
            // Apply directional bend + ripple
            float3 softOffset = localizedWindDir * (windForce * bendFactor) + rippleOffset;
            float3 softPosOS = displacedPosOS + softOffset;
            
            // Apply twist relative to the displaced pivot
            softPosOS = mul(twistMatrix, softPosOS - displacedPivotOS) + displacedPivotOS;
            float3 softNormalOS = mul(twistMatrix, normalOS);

            // 4. AAA LENGTH PRESERVATION
            float origDist = length(positionOS - pivotOS);
            float3 bendDir = softPosOS - displacedPivotOS;
            softPosOS = displacedPivotOS + (normalize(bendDir + float3(0, 0.0001, 0)) * origDist);

            // Blend normals to face the wind slightly
            softNormalOS = normalize(softNormalOS + (localizedWindDir * bendFactor * windForce));


            // Output Blend
            positionOS = lerp(softPosOS, rigidPosOS, rigidity);
            normalOS = normalize(lerp(softNormalOS, rigidNormalOS, rigidity));
        }
        ENDHLSL

        // =========================================================================
        // PASS 1: Forward Lit
        // =========================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float4 uv2_pivot    : TEXCOORD2; 
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float2 uv           : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                ApplyChimeWind(input.positionOS.xyz, input.normalOS, input.uv2_pivot, input.color);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half4 metallicGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                
                half metallic = metallicGloss.r * _Metallic;
                half smoothness = metallicGloss.a * _Smoothness;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.metallic = metallic;
                surfaceData.smoothness = smoothness;
                surfaceData.alpha = albedo.a;

                return UniversalFragmentPBR(inputData, surfaceData);
            }
            ENDHLSL
        }

        // =========================================================================
        // PASS 2: Shadow Caster
        // =========================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 uv2_pivot    : TEXCOORD2;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                ApplyChimeWind(input.positionOS.xyz, input.normalOS, input.uv2_pivot, input.color);
                
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // =========================================================================
        // PASS 3: Depth Only
        // =========================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 uv2_pivot    : TEXCOORD2;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                ApplyChimeWind(input.positionOS.xyz, input.normalOS, input.uv2_pivot, input.color);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}