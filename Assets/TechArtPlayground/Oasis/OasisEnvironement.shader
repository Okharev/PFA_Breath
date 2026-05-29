Shader "Custom/URP/OasisEnvironment"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        
        [Header(Oasis Settings)]
        [Toggle(OASIS_GROWTH)] _OasisGrowth("Enable Vertex Growth (For Props)", Float) = 0
        _VibrancyBoost("Inside Vibrancy Boost", Range(1.0, 3.0)) = 1.5
        _DeadSaturation("Outside Saturation", Range(0.0, 1.0)) = 0.1
        _TransitionWidth("Transition Softness", Range(0.1, 10.0)) = 2.0
        [HDR] _EdgeEmission("Edge Magic Emission", Color) = (0, 1, 0.5, 1)
        _EdgeWidth("Emission Edge Width", Range(0.1, 5.0)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry"
        }
        
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            
            // URP Keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            // Custom Keywords
            #pragma shader_feature_local OASIS_GROWTH

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // -------------------------------------
            // Structs
            // -------------------------------------
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float2 uv           : TEXCOORD1;
                float3 normalWS     : NORMAL;
            };

            // -------------------------------------
            // Properties & Globals
            // -------------------------------------
            // Globals driven by C# (No CBUFFER needed)
            float4 _GlobalOasisCenter;
            float _GlobalOasisRadius;

            // SRP Batcher compatibility
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
                float _VibrancyBoost;
                float _DeadSaturation;
                float _TransitionWidth;
                float4 _EdgeEmission;
                float _EdgeWidth;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // -------------------------------------
            // Vertex Shader
            // -------------------------------------
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                float3 positionOS = input.positionOS.xyz;

                #if defined(OASIS_GROWTH)
                    // Extract World Space Pivot from the Object to World matrix
                    float3 pivotWS = float3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);
                    
                    // Calculate distance from Oasis Center to the Object's Pivot
                    float distToCenter = distance(pivotWS, _GlobalOasisCenter.xyz);
                    
                    // Calculate growth multiplier (0 outside, 1 inside)
                    float growth = smoothstep(_GlobalOasisRadius, _GlobalOasisRadius - _TransitionWidth, distToCenter);
                    
                    // Scale vertex position in Object Space
                    // This creates a "popping up" effect as the radius overtakes the prop
                    positionOS *= growth;
                #endif

                VertexPositionInputs vertexInput = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            // -------------------------------------
            // Fragment Shader
            // -------------------------------------
            half4 frag(Varyings input) : SV_Target
            {
                // 1. Sample Base Texture
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                // 2. Calculate Oasis Mask
                float dist = distance(input.positionWS, _GlobalOasisCenter.xyz);
                
                // mask: 1.0 inside oasis, 0.0 outside oasis
                float mask = smoothstep(_GlobalOasisRadius, _GlobalOasisRadius - _TransitionWidth, dist);


                // 3. Color Logic
                // Calculate standard luminance
                half luminance = dot(albedo.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 grayscale = half3(luminance, luminance, luminance);

                // Lerp towards the original color for dead (e.g., 0.1)
                half3 deadColor = lerp(grayscale, albedo.rgb, _DeadSaturation);

                // THE FIX: Lerp *past* 1.0 to increase actual saturation without blowing out to white
                // Note: We removed saturate() to allow HDR tonemapping to do its job!
                half3 vibrantColor = lerp(grayscale, albedo.rgb, _VibrancyBoost);

                // Lerp between dead world and vibrant world based on mask
                half3 finalColor = lerp(deadColor, vibrantColor, mask);

                // 4. Edge Emission Logic (The "Magic Ring")
                float wobble = sin(input.positionWS.x * 2.0 + _Time.y * 3.0) * cos(input.positionWS.z * 2.0 + _Time.y * 3.0) * 0.5;

                // Apply wobble to the distance check
                float edgeDist = dist + wobble;

                float edgeMask = smoothstep(_GlobalOasisRadius, _GlobalOasisRadius - _EdgeWidth, edgeDist) - 
                                 smoothstep(_GlobalOasisRadius + 0.1, _GlobalOasisRadius - _TransitionWidth, edgeDist);
                                 
                edgeMask = saturate(edgeMask);
                half3 emission = _EdgeEmission.rgb * edgeMask;

                // 5. Basic URP Lighting (Directional only for brevity/optimization)
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half3 lighting = mainLight.color * NdotL;
                
                // Add ambient/SH lighting to prevent pitch black shadows
                lighting += SampleSH(input.normalWS);

                finalColor = (finalColor * lighting) + emission;

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }
        
        // Note: For a complete production shader, you would include ShadowCaster, DepthOnly, 
        // and DepthNormals passes here. They follow the exact same vertex growth logic 
        // to ensure shadows scale appropriately with the props.
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}