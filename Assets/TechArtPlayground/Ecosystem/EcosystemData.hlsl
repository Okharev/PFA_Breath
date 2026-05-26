#ifndef ECOSYSTEM_DATA_INCLUDED
#define ECOSYSTEM_DATA_INCLUDED

struct EcosystemBoid
{
    float3 position;
    float3 direction;
    float currentSpeed;
    float roll;
    float flapPhase;
    uint speciesID;
};

StructuredBuffer<EcosystemBoid> boidsBuffer;

// speciesOffset allows the shader to read from the correct chunk of the unified buffer
void GetEcosystemPosition_float(float instanceID_input, float speciesOffset, float swimAnimType,
                                float3 objectPosition, float3 objectNormal,
                                out float3 worldPosition, out float3 worldNormal)
{
    #if !defined(SHADERGRAPH_PREVIEW)

    uint instanceID = (uint)instanceID_input + (uint)speciesOffset;
    EcosystemBoid b = boidsBuffer[instanceID];

    float3 boidDir = normalize(b.direction);
    if (length(boidDir) < 0.1) boidDir = float3(0, 0, 1);
    float3 up = float3(0, 1, 0);
    if (abs(dot(boidDir, up)) > 0.99) up = float3(0, 0, 1);
    float3 right = normalize(cross(up, boidDir));
    up = cross(boidDir, right);

    // Roll Matrix
    float c = cos(b.roll);
    float s = sin(b.roll);
    float3 rolledRight = right * c + up * s;
    float3 rolledUp = up * c - right * s;

    float3 scaledPos = objectPosition; // Assume sizing is baked into mesh or handled via C# uniform scale

    // --- BIOMECHANICAL ANIMATION ---
    float animPhase = b.flapPhase + scaledPos.z * 5.0;

    // Type 0: Standard Fish Tail Wag (Yellow Fish, Koi)
    if (swimAnimType < 0.5)
    {
        float wag = sin(animPhase) * (scaledPos.z < 0 ? -scaledPos.z * 0.4 : 0.0);
        scaledPos.x += wag;
    }
    // Type 1: Ray Wing Undulation (Rays)
    else if (swimAnimType < 1.5)
    {
        float flap = sin(b.flapPhase - abs(scaledPos.x) * 3.0) * abs(scaledPos.x) * 0.3;
        scaledPos.y += flap;
    }
    // Type 2: Orca Full Body Wave (Orcas)
    else
    {
        float wave = sin(animPhase * 0.5) * 0.2;
        scaledPos.x += wave;
        scaledPos.y += cos(animPhase * 0.5) * 0.1 * (scaledPos.z < 0 ? -scaledPos.z : 0.0);
    }

    // Transform Position & Normal
    float3 rotatedPos = rolledRight * scaledPos.x + rolledUp * scaledPos.y + boidDir * scaledPos.z;
    worldPosition = rotatedPos + b.position;

    float3 rotatedNormal = rolledRight * objectNormal.x + rolledUp * objectNormal.y + boidDir * objectNormal.z;
    worldNormal = normalize(rotatedNormal);

    #else
    worldPosition = objectPosition;
    worldNormal = objectNormal;
    #endif
}
#endif
