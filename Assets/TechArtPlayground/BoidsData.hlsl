#ifndef BOIDS_DATA_INCLUDED
#define BOIDS_DATA_INCLUDED

struct Boid
{
    float3 position;
    float3 direction;
    float3 color;
    float size;
    float currentSpeed;
    float roll;
    float flapPhase;
    float splineT;
};

StructuredBuffer<Boid> boidsBuffer;

// Notice how the inputs and outputs now perfectly match the top-to-bottom order in your Shader Graph node!
void GetCubePosition_float(float instanceID_input, float3 objectPosition, float time, float flapSpeed,
                           float flapAmplitude, float3 objectNormal, out float3 worldPosition, out float3 boidColor, out float animSpeed, out float3 worldNormal)
{
    #if !defined(SHADERGRAPH_PREVIEW)
    uint instanceID = (uint)instanceID_input;
    Boid b = boidsBuffer[instanceID];

    float3 boidDir = normalize(b.direction);
    if (length(boidDir) < 0.1) boidDir = float3(0, 0, 1);

    float3 up = float3(0, 1, 0);
    if (abs(dot(boidDir, up)) > 0.99) up = float3(0, 0, 1);

    float3 right = normalize(cross(up, boidDir));
    up = cross(boidDir, right);

    // --- ROLL MATRIX ---
    float c = cos(b.roll);
    float s = sin(b.roll);
    float3 rolledRight = right * c + up * s;
    float3 rolledUp = up * c - right * s;

    float3 scaledPos = objectPosition * b.size;
    
    // --- Smooth Animation ---
    float wag = sin(b.flapPhase * flapSpeed + scaledPos.z * 5.0) * (scaledPos.z < 0 ? -scaledPos.z * flapAmplitude : 0.0);
    scaledPos.x += wag;

    // 1. ROTATE POSITION
    float3 rotatedPos = rolledRight * scaledPos.x + rolledUp * scaledPos.y + boidDir * scaledPos.z;
    worldPosition = rotatedPos + b.position;

    // 2. ROTATE NORMAL
    float3 rotatedNormal = rolledRight * objectNormal.x + rolledUp * objectNormal.y + boidDir * objectNormal.z;
    worldNormal = normalize(rotatedNormal);

    // 3. PASS DATA
    boidColor = b.color;
    animSpeed = b.currentSpeed;
    
    #else
    // Dummy outputs so Shader Graph preview doesn't crash
    worldPosition = objectPosition;
    boidColor = float3(1,1,1);
    animSpeed = 1.0;
    worldNormal = objectNormal;
    #endif
}
#endif