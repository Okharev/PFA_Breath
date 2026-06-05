Shader "Custom/URP/ComputeRain"
{
    Properties
    {
        _MainTex ("Rain Drop Texture", 2D) = "white" {}
        [HDR] _RainColor ("Rain Color", Color) = (0.8, 0.9, 1.0, 0.5)
        _DropSize ("Base Drop Size (Width, Height)", Vector) = (0.05, 0.5, 0, 0)
        _RainVelocity ("Rain Velocity", Vector) = (0.5, -20.0, 0.2, 0)
        _DepthFade ("Depth Fade Distance", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off 

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            // URP Fog Support
            #pragma multi_compile_fog 

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // Required for reading the scene depth buffer (Soft Particles)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float4 screenPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
                float distanceAlpha : TEXCOORD3;
            };

            struct RainDrop
            {
                float3 position;
                float randomSeed;
            };

            StructuredBuffer<RainDrop> _RainBuffer;
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _RainColor;
                float2 _DropSize;
                float3 _RainVelocity;
                float _DepthFade;
                float _GridSize; // Pushed from C#
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                RainDrop drop = _RainBuffer[input.instanceID];

                // 1. VELOCITY-BASED MOTION STRETCH
                float speed = length(_RainVelocity);
                // Stretch multiplier: base size + (speed * factor)
                float stretch = max(1.0, speed * 0.05); 
                
                float sizeNoise = drop.randomSeed;
                float2 individualizedSize = _DropSize * float2(
                    lerp(0.6, 1.2, frac(sizeNoise * 15.43)), 
                    lerp(0.7, 1.5, frac(sizeNoise * 93.21)) * stretch // Apply stretch to Y
                );

                // 2. ANGULAR ALIGNMENT
                float3 fallDir = normalize(_RainVelocity);
                float3 right = normalize(cross(fallDir, float3(0, 0, 1)));
                if (length(right) < 0.01) right = normalize(cross(fallDir, float3(1, 0, 0)));
                float3 forward = cross(right, fallDir);

                float3 localPos = input.positionOS.xyz;
                float3 rotatedPos = (localPos.x * right * individualizedSize.x) +
                                    (localPos.y * fallDir * individualizedSize.y) +
                                    (localPos.z * forward * individualizedSize.x);

                float3 worldPos = drop.position + rotatedPos;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = input.uv;
                
                // 3. FOG AND DEPTH PREP
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.screenPos = ComputeScreenPos(output.positionCS);

                // 4. DISTANCE FADING (Smooth out at the edges of the simulation grid)
                float distFromCam = distance(_WorldSpaceCameraPos, worldPos);
                // Fade from opaque at 30% of grid size to fully transparent at 45% grid size
                output.distanceAlpha = smoothstep(_GridSize * 0.45, _GridSize * 0.3, distFromCam);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 finalColor = texColor * _RainColor;

                // 5. SOFT PARTICLES (Depth Fading)
                #if !defined(SHADER_API_GLES)
                    float2 screenUV = input.screenPos.xy / input.screenPos.w;
                    float sceneDepth = SampleSceneDepth(screenUV);
                    float linearSceneDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
                    float linearParticleDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                    
                    // Smoothly fade the alpha based on distance to intersecting object
                    float depthFade = saturate((linearSceneDepth - linearParticleDepth) / _DepthFade);
                    finalColor.a *= depthFade;
                #endif

                // Apply distance edge-fading
                finalColor.a *= input.distanceAlpha;

                // Discard completely empty pixels early
                if (finalColor.a < 0.01) discard;

                // 6. URP FOG BLENDING
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);

                return finalColor;
            }
            ENDHLSL
        }
    }
}