Shader "UI/NeuralArcHUD_UITK"
{
    Properties
    {
        _NodeCount("NodeCount", Float) = 4
        _Node0("Node0", Vector) = (0, 0, 0, 0)
        _Node1("Node1", Vector) = (0, 0, 0, 0)
        _Node2("Node2", Vector) = (0, 0, 0, 0)
        _Node3("Node3", Vector) = (0, 0, 0, 0)
        _Node4("Node4", Vector) = (0, 0, 0, 0)
        _Node5("Node5", Vector) = (0, 0, 0, 0)
        _Node6("Node6", Vector) = (0, 0, 0, 0)
        _Node7("Node7", Vector) = (0, 0, 0, 0)

        _Thickness("Thickness", Float) = 0.015
        _Smoothness("Smoothness", Float) = 0.1
        _NoiseScale("Noise Scale", Float) = 15.0
        _OrganicSpeed("Animation Speed", Float) = 1.5

        [HDR] _CoreColor("Core Color", Color) = (0.9, 1.0, 1.0, 1.0)
        [HDR] _GlowColor("Glow Color", Color) = (0.02, 1.0, 0.93, 1.0)

        // Required by UITK internally
        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "isCustomUITKShader"="true" // CRUCIAL: Tells UITK this is a valid shader
            "Queue"="Transparent"
            "ShaderGraphShader"="true" // Required by UITK shim
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "Default"

            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex uie_custom_vert
            #pragma fragment uie_custom_frag

            // UI Toolkit Keywords
            #pragma multi_compile_local _ _UIE_FORCE_GAMMA
            #pragma multi_compile_local _ _UIE_TEXTURE_SLOT_COUNT_4 _UIE_TEXTURE_SLOT_COUNT_2 _UIE_TEXTURE_SLOT_COUNT_1
            #pragma multi_compile_local _ _UIE_RENDER_TYPE_SOLID _UIE_RENDER_TYPE_TEXTURE _UIE_RENDER_TYPE_TEXT _UIE_RENDER_TYPE_GRADIENT

            #define UITK_SHADERGRAPH
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define ATTRIBUTES_NEED_TEXCOORD0
            #define ATTRIBUTES_NEED_COLOR
            #define VARYINGS_NEED_TEXCOORD0
            #define VARYINGS_NEED_COLOR
            #define FEATURES_GRAPH_VERTEX
            #define REQUIRE_DEPTH_TEXTURE
            #define REQUIRE_NORMAL_TEXTURE
            #define SHADERPASS SHADERPASS_CUSTOM_UI

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
            #include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/UIShim.hlsl"

            // --------------------------------------------------
            // CBUFFER Properties

            CBUFFER_START(UnityPerMaterial)
                float _NodeCount;
                float4 _Node0, _Node1, _Node2, _Node3, _Node4, _Node5, _Node6, _Node7;
                float _Thickness;
                float _Smoothness;
                float _NoiseScale;
                float _OrganicSpeed;
                float4 _CoreColor;
                float4 _GlowColor;
            CBUFFER_END

            // --------------------------------------------------
            // Math & Organic Functions

            float random(float2 st)
            {
                return frac(sin(dot(st.xy, float2(12.9898, 78.233))) * 43758.5453123);
            }

            float noise(float2 st)
            {
                float2 i = floor(st);
                float2 f = frac(st);
                float a = random(i);
                float b = random(i + float2(1.0, 0.0));
                float c = random(i + float2(0.0, 1.0));
                float d = random(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            float sdSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = clamp(dot(pa, ba) / max(dot(ba, ba), 0.0001), 0.0, 1.0);
                return length(pa - ba * h);
            }

            float smin(float a, float b, float k)
            {
                float h = max(k - abs(a - b), 0.0) / k;
                return min(a, b) - h * h * h * k * (1.0 / 6.0);
            }

            // --------------------------------------------------
            // UITK Core Structs

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(ATTRIBUTES_NEED_INSTANCEID)
                uint instanceID : INSTANCEID_SEMANTIC;
                #endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 texCoord0;
                float4 color;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
            };

            struct SurfaceDescriptionInputs
            {
                float4 uv0;
                float4 color;
            };

            struct VertexDescriptionInputs
            {
                float4 vertexPosition;
                float4 vertexColor;
                float4 uv;
            };

            struct PackedVaryings
            {
                float4 positionCS : SV_POSITION;
                float4 texCoord0 : INTERP0;
                float4 color : INTERP1;
                #if UNITY_ANY_INSTANCING_ENABLED || defined(VARYINGS_NEED_INSTANCEID)
                uint instanceID : CUSTOM_INSTANCE_ID;
                #endif
                #if (defined(UNITY_STEREO_INSTANCING_ENABLED))
                uint stereoTargetEyeIndexAsRTArrayIdx : SV_RenderTargetArrayIndex;
                #endif
            };

            PackedVaryings PackVaryings(Varyings input)
            {
                PackedVaryings output;
                ZERO_INITIALIZE(PackedVaryings, output);
                output.positionCS = input.positionCS;
                output.texCoord0 = input.texCoord0;
                output.color = input.color;
                return output;
            }

            Varyings UnpackVaryings(PackedVaryings input)
            {
                Varyings output;
                    ZERO_INITIALIZE(Varyings, output);
                output.positionCS = input.positionCS;
                output.texCoord0 = input.texCoord0;
                output.color = input.color;
                return output;
            }

            // --------------------------------------------------
            // Shader Logic

            struct VertexDescription
            {
            };

            VertexDescription VertexDescriptionFunction(VertexDescriptionInputs IN)
            {
                return (VertexDescription)0;
            }

            struct SurfaceDescription
            {
                float3 BaseColor;
                float Alpha;
            };

            SurfaceDescription SurfaceDescriptionFunction(SurfaceDescriptionInputs IN)
            {
                SurfaceDescription surface = (SurfaceDescription)0;

                float2 uv = IN.uv0.xy;
                float time = _Time.y * _OrganicSpeed;

                float2 nodes[8] = {
                    _Node0.xy, _Node1.xy, _Node2.xy, _Node3.xy,
                    _Node4.xy, _Node5.xy, _Node6.xy, _Node7.xy
                };

                int count = clamp((int)_NodeCount, 1, 8);

                // Add organic wobble to the UV field
                float2 distortedUV = uv + float2(
                    noise(uv * _NoiseScale + time) * 0.02,
                    noise(uv * _NoiseScale - time) * 0.02
                );

                // Find central hub anchor
                float2 centerAnchor = float2(0, 0);
                for (int j = 0; j < count; j++)
                {
                    centerAnchor += nodes[j];
                }
                centerAnchor /= max((float)count, 1.0);
                centerAnchor = lerp(centerAnchor, float2(0.5, 0.5), 0.3);

                float d = 100.0; // Base distance to nothing

                for (int i = 0; i < count; i++)
                {
                    // Path to next node
                    if (i < count - 1)
                    {
                        float distEdge = sdSegment(distortedUV, nodes[i], nodes[i + 1]);
                        d = smin(d, distEdge, _Smoothness);
                    }

                    // Path to central anchor
                    float distCenter = sdSegment(distortedUV, nodes[i], centerAnchor);
                    d = smin(d, distCenter, _Smoothness * 1.5);
                }

                // Render core line and glow falloff
                float core = smoothstep(_Thickness, _Thickness * 0.1, d);
                float glow = smoothstep(_Thickness * 4.0, 0.0, d);

                float3 finalColor = lerp(_GlowColor.rgb, _CoreColor.rgb, core);
                float alpha = max(core, glow * 0.4) * _GlowColor.a;

                // Output to UI Toolkit
                surface.BaseColor = finalColor;
                surface.Alpha = alpha;

                return surface;
            }

            VertexDescriptionInputs BuildVertexDescriptionInputs(Attributes input)
            {
                VertexDescriptionInputs output;
                ZERO_INITIALIZE(VertexDescriptionInputs, output);
                return output;
            }

            SurfaceDescriptionInputs BuildSurfaceDescriptionInputs(Varyings input)
            {
                SurfaceDescriptionInputs output;
                ZERO_INITIALIZE(SurfaceDescriptionInputs, output);
                output.uv0 = input.texCoord0;
                output.color = input.color;
                return output;
            }

            // --------------------------------------------------
            // UITK execution shim (MANDATORY for compilation)
            #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/UITKPass.hlsl"
            ENDHLSL
        }
    }
}