Shader "Custom/Ocean_FFT_Debug"
{
    Properties
    {
        [Header(FFT Data Links)]
        [NoScaleOffset] _DispTex ("Displacement Map (RGB)", 2D) = "black" {}
        [NoScaleOffset] _DerivTex ("Derivatives & Jacobian (RGB)", 2D) = "black" {}
        _FFTScale ("FFT Grid Scale (1 / Ocean Size)", Float) = 0.01
        _Choppiness ("Choppiness Scale", Range(0, 5)) = 1.2

        [Header(Debug Foam Controls)]
        _FoamColor ("Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _WaterColor ("Base Water Color", Color) = (0.05, 0.2, 0.4, 1.0)
        _FoamBias ("Jacobian Foam Bias", Range(-1.0, 1.0)) = -0.1
        _FoamBlurLod ("Foam Blur (Mip Level)", Range(0, 8)) = 2.0
        _FoamPower ("Foam Falloff Power", Range(0.1, 5.0)) = 1.5
    }

    SubShader
    {
        // Keep it opaque and simple for debugging vertex movement
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "DebugForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 fftUV        : TEXCOORD0;
            };

            TEXTURE2D(_DispTex);  SAMPLER(sampler_DispTex);
            TEXTURE2D(_DerivTex); SAMPLER(sampler_DerivTex);

            CBUFFER_START(UnityPerMaterial)
                float _FFTScale;
                float _Choppiness;
                half4 _FoamColor;
                half4 _WaterColor;
                float _FoamBias;
                float _FoamBlurLod;
                float _FoamPower;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 1. Get absolute world position BEFORE displacement
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                
                // 2. BULLETPROOF WORLD SPACE UV MAPPING
                // By using positionWS, the mesh can slide around the world, but the waves stay anchored.
                float2 stableUV = positionWS.xz * _FFTScale;
                output.fftUV = stableUV;
                
                // 3. Sample displacement data
                float4 disp = SAMPLE_TEXTURE2D_LOD(_DispTex, sampler_DispTex, stableUV, 0);
                
                // 4. Displace the world vertices
                positionWS.x += disp.r * _Choppiness;
                positionWS.y += disp.g;
                positionWS.z += disp.b * _Choppiness;
                
                // 5. Project to clip space
                output.positionHCS = TransformWorldToHClip(positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Sample the derivative/Jacobian texture map
                // Higher _FoamBlurLod values access higher mipmaps for the progressive blur effect
                float4 derivatives = SAMPLE_TEXTURE2D_LOD(_DerivTex, sampler_DerivTex, input.fftUV, _FoamBlurLod);
                float jacobian = derivatives.b;

                // 2. Compute the isolated Jacobian Foam Mask
                // When waves compress, jacobian drops toward/below 0.
                float foamMask = saturate(1.0 - (jacobian + _FoamBias));
                foamMask = pow(foamMask, _FoamPower);
                
                // 3. Output a pure unlit blend between water and foam colors
                half3 finalColor = lerp(_WaterColor.rgb, _FoamColor.rgb, foamMask);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}