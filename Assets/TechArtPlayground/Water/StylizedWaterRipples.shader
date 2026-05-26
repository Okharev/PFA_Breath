Shader "Custom/StylizedWaterRipples"
{
    Properties
    {
        [Header(Base Colors)]
        _WaterColor ("Water Color", Color) = (0.0, 0.5, 1.0, 1.0)
        _FoamColor ("Foam Color", Color) = (1.0, 1.0, 1.0, 1.0)
        
        [Header(Proximity Data)]
        [Tooltip("Pure white at intersection, blurred outwards to black")]
        _IntersectionMap ("Intersection Map (Blurred)", 2D) = "black" {}

        [Header(Ripple Dynamics)]
        _RippleSpeed ("Ripple Speed", Float) = 3.0
        _RippleFrequency ("Ripple Frequency", Float) = 15.0
        _RippleThickness ("Ripple Thickness", Range(0, 1)) = 0.8
        _FoamFade ("Foam Fade Distance", Range(0.01, 1)) = 0.5
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            Name "ForwardLit"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Unity 6.4 Core URP Library
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            // Using CBUFFER for SRP Batcher compatibility (Crucial for performance)
            CBUFFER_START(UnityPerMaterial)
                float4 _WaterColor;
                float4 _FoamColor;
                float _RippleSpeed;
                float _RippleFrequency;
                float _RippleThickness;
                float _FoamFade;
            CBUFFER_END

            TEXTURE2D(_IntersectionMap);
            SAMPLER(sampler_IntersectionMap);

            Varyings vert(Attributes input)
            {
                Varyings output;
                // Transform Object to Clip Space using Unity 6 macros
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. Sample the baked proximity map (1 at shore, fading to 0 away from shore)
                float proximity = SAMPLE_TEXTURE2D(_IntersectionMap, sampler_IntersectionMap, input.uv).r;

                // 2. Generate moving ripples using a sine wave. 
                // Adding time forces the wave crests to move toward the lower proximity values (outwards).
                float wavePhase = (proximity * _RippleFrequency) + (_Time.y * _RippleSpeed);
                float sineWave = sin(wavePhase);

                // 3. Stylize into distinct, hard bands. 
                // We use smoothstep instead of 'step' to prevent jagged pixel aliasing.
                float rippleBand = smoothstep(_RippleThickness - 0.05, _RippleThickness + 0.05, sineWave);

                // 4. Fade out the ripples as they move further from the obstacle.
                float fadeMask = smoothstep(0.0, _FoamFade, proximity);
                
                // Final foam multiplier
                float finalFoam = rippleBand * fadeMask;

                // 5. Composite water and foam
                float4 finalColor = lerp(_WaterColor, _FoamColor, finalFoam);

                return finalColor;
            }
            ENDHLSL
        }
    }
}