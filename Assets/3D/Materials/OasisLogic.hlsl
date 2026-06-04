// 1. Guard against the file being included multiple times by Shader Graph passes
#ifndef OASIS_EFFECT_INCLUDED
#define OASIS_EFFECT_INCLUDED

// 2. Declare globals. We do not initialize them here so C# can control them.
int _ActiveOasisCount;
float4 _OasisData[20];

// 3. The Custom Function
void CalculateOasisLoops_float(float3 PositionWS, float NoiseOffset, float EffectFeather, float EdgeWidth, out float Mask, out float EdgeGlow)
{
    // ALWAYS initialize 'out' variables at the very start to satisfy D3D11
    Mask = 1.0;
    EdgeGlow = 0.0;

    // 4. PREVIEW SAFEGUARD: Skip the C# array logic if compiling for the node preview
    #ifdef SHADERGRAPH_PREVIEW
    // Just output a flat color in the graph preview to prevent compiler crashes
    Mask = 1.0;
    EdgeGlow = 0.0;
    #else
    // ACTUAL GAME LOGIC: This only compiles for the real material
    int safeCount = min(_ActiveOasisCount, 20);

    for (int i = 0; i < safeCount; i++)
    {
        float3 center = _OasisData[i].xyz;
        float currentRadius = _OasisData[i].w;

        float dist = distance(PositionWS, center) + NoiseOffset;
            
        float localMask = saturate((dist - currentRadius) / max(0.001, EffectFeather));
        Mask = min(Mask, localMask);
            
        float edgeDist = abs(dist - currentRadius);
        float localEdgeGlow = smoothstep(EdgeWidth, 0.0, edgeDist);
        EdgeGlow = max(EdgeGlow, localEdgeGlow);
    }
    #endif
}

#endif