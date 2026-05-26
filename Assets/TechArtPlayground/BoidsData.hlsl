#ifndef BOIDS_DATA_INCLUDED
#define BOIDS_DATA_INCLUDED

struct Boid
{
    float3 position;
    float3 velocity; 
    uint packedData; // Replaces roll
    float splineT;
};

StructuredBuffer<Boid> boidsBuffer;

float CustHash(uint s)
{
    s ^= 2747636419u;
    s *= 2654435769u;
    s ^= s >> 16;
    s *= 2654435769u;
    s ^= s >> 16;
    s *= 2654435769u;
    return float(s) / 4294967295.0;
}

void GetCubePosition_float(float instanceID_input, float3 objectPosition, float3 objectNormal, float time, 
                           float flapSpeed, float flapAmplitude, float3 colorA, float3 colorB, 
                           out float3 worldPosition, out float3 worldNormal, out float3 boidColor, out float animSpeed)
{
    #ifndef SHADERGRAPH_PREVIEW
    uint id = (uint)instanceID_input;
    Boid b = boidsBuffer[id];
    
    // UNPACK ID and ROLL
    uint persistentID = b.packedData >> 16;
    float roll = ((float)(b.packedData & 0xFFFF) / 65535.0) * 6.28318 - 3.14159;
    
    // Procedural Reconstruction using our rock-solid persistentID
    float randomSeed = CustHash(persistentID);
    float size = lerp(0.5, 1.5, randomSeed);
    boidColor = lerp(colorA, colorB, CustHash(id + 1));
    
    animSpeed = length(b.velocity);
    float3 boidDir = animSpeed > 0.001 ? (b.velocity / animSpeed) : float3(0,0,1);
    float flapPhase = time * animSpeed * 2.0 + (randomSeed * 100.0);

    // 2. Vertex Offset (Wag)
    float3 scaledPos = objectPosition * size;
    float wag = sin(flapPhase * flapSpeed + scaledPos.z * 5.0) * (scaledPos.z < 0 ? -scaledPos.z * flapAmplitude : 0.0);
    scaledPos.x += wag;

    // 3. Build Rotation Matrix (Compute Right and Up vectors)
    float3 globalUp = float3(0, 1, 0);
    // Fallback if pointing straight up/down
    if (abs(dot(boidDir, globalUp)) > 0.999) globalUp = float3(0, 0, 1); 
    
    float3 right = normalize(cross(globalUp, boidDir));
    float3 up = cross(boidDir, right);
    
    // Apply Roll
    float s, c;
    sincos(roll, s, c);
    float3 rolledRight = right * c + up * s;
    float3 rolledUp = normalize(cross(boidDir, rolledRight));

    // 4. Apply Rotations
    float3 rotatedPos = rolledRight * scaledPos.x + rolledUp * scaledPos.y + boidDir * scaledPos.z;
    worldPosition = rotatedPos + b.position;

    float3 rotatedNormal = rolledRight * objectNormal.x + rolledUp * objectNormal.y + boidDir * objectNormal.z;
    worldNormal = normalize(rotatedNormal);

    #else
    worldPosition = objectPosition;
    boidColor = float3(1,1,1);
    animSpeed = 1.0;
    worldNormal = objectNormal;
    #endif
}
#endif