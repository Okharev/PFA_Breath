Shader "Hidden/OasisGlobalColor"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    #define MAX_OASES 20 

    int _ActiveOasisCount;
    // xyz = Position, w = Current Radius (The Healing Wave)
    float4 _OasisData[MAX_OASES]; 
    // x = Max Radius (The Dead Zone Boundary)
    float4 _OasisMaxData[MAX_OASES]; 
    
    float _DesaturationAmount;
    float _TransitionWidth;
    float4 _EdgeEmission;
    float _EdgeWidth;

    half4 Fragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

        half4 screenColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);

        #if UNITY_REVERSED_Z
            real depth = SampleSceneDepth(input.texcoord);
        #else
            real depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(input.texcoord));
        #endif
        
        float3 worldPos = ComputeWorldSpacePosition(input.texcoord, depth, UNITY_MATRIX_I_VP);

        float globalCorruption = 0.0;
        float globalHealing = 0.0;
        float combinedGlowMask = 0.0;

        for(int i = 0; i < _ActiveOasisCount; i++)
        {
            float3 center = _OasisData[i].xyz;
            float currentRadius = _OasisData[i].w;
            float maxRadius = _OasisMaxData[i].x;

            float dist = distance(worldPos, center);
            
            // 1. Calculate Dead Zone (1.0 inside the Max Radius)
            float isDeadZone = 1.0 - smoothstep(maxRadius - _TransitionWidth, maxRadius, dist);
            globalCorruption = max(globalCorruption, isDeadZone);

            // 2. Calculate Healing Wave (1.0 inside the Current Radius)
            float isHealed = 1.0 - smoothstep(currentRadius - _TransitionWidth, currentRadius, dist);
            globalHealing = max(globalHealing, isHealed);

            // 3. Expanding Ring Glow (Only visible inside the Dead Zone)
            float distToEdge = abs(dist - currentRadius);
            float currentGlow = 1.0 - saturate(distToEdge / _EdgeWidth);
            currentGlow = pow(currentGlow, 2.0);
            
            // Multiply by isDeadZone so the glow naturally fades out when it hits the edge of the gray area
            combinedGlowMask = max(combinedGlowMask, currentGlow * isDeadZone);
        }

        // Subtract the healing from the corruption. 
        // 1.0 = Needs to be Grey. 0.0 = Needs to be Normal Color.
        float finalGreyMask = saturate(globalCorruption - globalHealing);

        // Desaturation Math
        half luminance = dot(screenColor.rgb, half3(0.2126, 0.7152, 0.0722));
        half3 grayscale = half3(luminance, luminance, luminance);
        half3 grayColor = lerp(screenColor.rgb, grayscale, _DesaturationAmount);
        
        // Final Color
        half3 baseColor = lerp(screenColor.rgb, grayColor, finalGreyMask);
        half3 finalColor = baseColor + (_EdgeEmission.rgb * combinedGlowMask);

        return half4(finalColor, screenColor.a);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "OasisColorPass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            ENDHLSL
        }
    }
}