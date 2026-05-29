Shader "Custom/URP/RopeWind"
{
    Properties
    {
        [Header(Surface)]
        _BaseMap("Rope Texture (Albedo)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Header(Wind Synchronization)]
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

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _WindStrengthMultiplier;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        void ApplyRopeWind(inout float3 positionOS, float2 uv)
        {
            float3 positionWS = TransformObjectToWorld(positionOS);
            
            // --- MACRO GUST APPLICATION ---
            float baseWindStrength = (_GlobalWindStrength > 0.0 ? _GlobalWindStrength : 1.0) * _WindStrengthMultiplier;
            float macroGustMultiplier = GetGlobalGust(positionWS);
            float windStrength = baseWindStrength * macroGustMultiplier;

            float3 windDirWS = length(_GlobalWindDirection.xyz) > 0.1 ? normalize(_GlobalWindDirection.xyz) : float3(1, 0, 0);
            float3 windDirOS = normalize(TransformWorldToObjectDir(windDirWS));

            float ropePhase = dot(positionWS, float3(0.1, 0.0, 0.1)) + _Time.y;
            float ropeSwayAmount = sin(ropePhase) * (windStrength * 0.25);

            float anchorMask = sin(uv.x * 3.14159265);
            float3 ropeSwayOffsetOS = windDirOS * (ropeSwayAmount * anchorMask);
            
            positionOS += ropeSwayOffsetOS;
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                ApplyRopeWind(input.positionOS.xyz, input.uv);

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalize(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
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
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                ApplyRopeWind(input.positionOS.xyz, input.uv);

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
    }
}