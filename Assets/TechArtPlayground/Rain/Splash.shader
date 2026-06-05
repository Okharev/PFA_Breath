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
                
                if (s_type < 0.5) // TYPE 0: GROUND RIPPLE (ALIGNED)
                {
                    float currentSize = lerp(0.05, _MaxSize, inverseLife);

                    // 1. Generate an arbitrary Right vector on the tangent plane
                    float3 upAxis = float3(0, 1, 0);
                    float3 rightAxis = cross(upAxis, s_normal);
                    
                    // If normal is perfectly (0,1,0), cross product length is 0. Provide a fallback.
                    if (length(rightAxis) < 0.001) 
                        rightAxis = float3(1, 0, 0);
                    else 
                        rightAxis = normalize(rightAxis);
                    
                    // 2. Generate the orthogonal Forward vector on the tangent plane
                    float3 forwardAxis = normalize(cross(s_normal, rightAxis));

                    // 3. Map the quad's object space X and Y to these new surface-aligned axes
                    float3 localPos = (rightAxis * input.positionOS.x + forwardAxis * input.positionOS.y) * currentSize;
                    
                    // 4. Push off the surface slightly along the normal to prevent z-fighting
                    // (Increased slightly to 0.03 to help clear micro-bumps on rough terrain)
                    worldPos = s_pos + localPos + s_normal * 0.03; 
                }
else if (s_type < 1.5) // TYPE 1: WALL SHATTER
{
    float3 upAxis = float3(0, 1, 0);
    float3 rightAxis = normalize(cross(upAxis, s_normal));
    if (length(rightAxis) < 0.01) rightAxis = float3(1, 0, 0);
    float3 surfaceUp = normalize(cross(s_normal, rightAxis));

    // [FIX]: Drastically increased the multipliers! 
    // Now it expands from a small pop to 1.5x / 2.5x your _MaxSize
    float width = lerp(0.1, _MaxSize * 1.5, inverseLife);
    float height = lerp(0.1, _MaxSize * 2.5, inverseLife);

    float3 localPos = (rightAxis * input.positionOS.x * width) + 
                      (surfaceUp * input.positionOS.y * height);

    float bottomPush = (input.positionOS.y < 0.0) ? 0.08 : 0.01;
    
    // Ensure it pushes far enough out of the wall geometry
    worldPos = s_pos + localPos + s_normal * (0.15 + bottomPush * inverseLife);
}
                else // [NEW] TYPE 2: EDGE DRIP
                {
                    // Camera-facing Billboard
                    float3 forward = normalize(_WorldSpaceCameraPos - s_pos);
                    float3 rightAxis = normalize(cross(float3(0, 1, 0), forward));
                    float3 upAxis = float3(0, 1, 0);
                    
                    // Thicker and taller than normal rain, tapering off as it dies
                    float width = 0.06;
                    float height = 0.6 * s_life; 
                    
                    float3 localPos = (rightAxis * input.positionOS.x * width) + 
                                      (upAxis * input.positionOS.y * height);
                                      
                    // Push out so it clears the ledge physically
                    worldPos = s_pos + localPos + s_normal * 0.1; 
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
                
                if (input.type < 0.5) // TYPE 0: FLAT SPLASH
                {
                    float2 centeredUV = input.uv * 2.0 - 1.0;
                    float dist = length(centeredUV);
                    float ring = smoothstep(0.7, 0.9, dist) - smoothstep(0.9, 1.0, dist);
                    alpha = ring * input.life * _SplashColor.a;
                }

                else // [NEW] TYPE 2: EDGE DRIP
                {
                    // Bottom-heavy tear shape
                    float widthMask = 1.0 - abs(input.uv.x * 2.0 - 1.0);
                    float bottomHeavyMask = smoothstep(1.0, 0.0, input.uv.y); 
                    
                    float shape = smoothstep(0.2, 0.8, widthMask * bottomHeavyMask);
                    alpha = shape * input.life * _SplashColor.a * 2.0;
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