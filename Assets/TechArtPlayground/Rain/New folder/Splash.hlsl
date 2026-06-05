Shader "Custom/URP/Splash"
{
    Properties
    {
        [HDR] _SplashColor ("Splash Color", Color) = (0.9, 0.95, 1.0, 0.8)
        _MaxSize ("Max Splash Size", Float) = 0.4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off 

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float life : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            struct Splash
            {
                float3 position;
                float life;
                float maxLife;
            };

            StructuredBuffer<Splash> _SplashPool;
            
            CBUFFER_START(UnityPerMaterial)
                half4 _SplashColor;
                float _MaxSize;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                Splash s = _SplashPool[input.instanceID];
                
                // Inverse life: 0 is newborn, 1 is dead
                float inverseLife = 1.0 - s.life; 
                
                // Expand the quad outwards as it dies
                float currentSize = lerp(0.05, _MaxSize, inverseLife);

                // Align flat to the ground (XZ plane)
                float3 localPos = float3(input.positionOS.x, 0, input.positionOS.y) * currentSize;
                
                // Add a tiny Y offset to prevent Z-fighting with the ground
                float3 worldPos = s.position + localPos + float3(0, 0.02, 0);

                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;
                output.life = s.life;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                // Hide dead particles instantly by collapsing them
                if (s.life <= 0.0) output.positionCS = float4(0,0,0,0);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Procedural Ring generation using UVs
                float2 centeredUV = input.uv * 2.0 - 1.0;
                float dist = length(centeredUV);
                
                // Create a hard ring shape
                float ring = smoothstep(0.7, 0.9, dist) - smoothstep(0.9, 1.0, dist);
                
                // Fade out based on particle life and the procedural ring
                half alpha = ring * input.life * _SplashColor.a;
                if (alpha < 0.01) discard;

                half4 finalColor = half4(_SplashColor.rgb, alpha);
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }
}