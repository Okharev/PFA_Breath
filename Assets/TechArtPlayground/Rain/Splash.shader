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
                float type : TEXCOORD3;
            };

            struct Splash
            {
                float4 posAndLife;
                float4 normalAndMaxLife;
                float4 typeAndPadding;
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
                
                float3 s_pos = s.posAndLife.xyz;
                float s_life = s.posAndLife.w;
                float3 s_normal = s.normalAndMaxLife.xyz;
                float s_type = s.typeAndPadding.x;

                float inverseLife = 1.0 - s_life; 
                float3 worldPos;
                
                if (s_type < 0.5) // FLAT GROUND SPLASH
                {
                    float currentSize = lerp(0.05, _MaxSize, inverseLife);
                    float3 localPos = float3(input.positionOS.x, 0, input.positionOS.y) * currentSize;
                    worldPos = s_pos + localPos + s_normal * 0.02;
                }
                else // DRIP DOWN WALL
                {
                    float3 upAxis = float3(0, 1, 0);
                    float3 rightAxis = normalize(cross(upAxis, s_normal));
                    if (length(rightAxis) < 0.01) rightAxis = float3(1, 0, 0);
                    
                    float3 surfaceUp = normalize(cross(s_normal, rightAxis));

                    // INCREASED SIZES: Make drips thicker and taller
                    float widthScale = 0.12; 
                    float heightScale = 0.6 * s_life; 
                    
                    float3 localPos = (rightAxis * (input.positionOS.x * widthScale)) + 
                                      (surfaceUp * (input.positionOS.y * heightScale));
                    
                    // Push out 0.05 to prevent clipping into wall bumps
                    worldPos = s_pos + localPos + s_normal * 0.05; 
                }

                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;
                output.life = s_life;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.type = s_type;

                if (s_life <= 0.0) output.positionCS = float4(0,0,0,0);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half alpha = 0;
                
                if (input.type < 0.5) // FLAT SPLASH
                {
                    float2 centeredUV = input.uv * 2.0 - 1.0;
                    float dist = length(centeredUV);
                    float ring = smoothstep(0.7, 0.9, dist) - smoothstep(0.9, 1.0, dist);
                    alpha = ring * input.life * _SplashColor.a;
                }
                else // DRIP STREAK
                {
                    float widthMask = 1.0 - abs(input.uv.x * 2.0 - 1.0); 
                    float bottomHeavyMask = smoothstep(1.0, 0.0, input.uv.y); 
                    
                    float shape = smoothstep(0.2, 0.8, widthMask * bottomHeavyMask);
                    
                    // Increased brightness multiplier to combat vertical wall lighting dropoffs
                    alpha = shape * input.life * _SplashColor.a * 2.5; 
                }

                if (alpha < 0.01) discard;

                half4 finalColor = half4(_SplashColor.rgb, alpha);
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }
}