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

// NOUVEAU : J'ai ajouté flapSpeed et flapAmplitude en entrée !
void GetCubePosition_float(float instanceID_input, float3 objectPosition, float time, float flapSpeed,
                           float flapAmplitude, out float3 worldPosition, out float3 boidColor, out float animSpeed)
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

    // --- NOUVEAU : LA MATRICE DE ROULIS ---
    // On calcule le Cosinus et le Sinus de notre angle
    float c = cos(b.roll);
    float s = sin(b.roll);

    // On fait pivoter nos axes "Droite" et "Haut" autour de notre axe "Avant"
    float3 rolledRight = right * c + up * s;
    float3 rolledUp = up * c - right * s;
    // --------------------------------------
    float3 scaledPos = objectPosition * b.size;

    // --- NOUVEAU : Pure Smooth Animation ---
    // We completely drop the "time *" logic and use our safe, GPU-integrated phase.
    // The flapSpeed from Shader Graph is multiplied directly against the phase.

    float wag = sin(b.flapPhase * flapSpeed + scaledPos.z * 5.0) * (
        scaledPos.z < 0 ? -scaledPos.z * flapAmplitude : 0.0);

    scaledPos.x += wag;
    // -------------------------------------------------------------
    // -------------------------------------------------------------

    float3 rotatedPos = rolledRight * scaledPos.x + rolledUp * scaledPos.y + boidDir * scaledPos.z;

    worldPosition = rotatedPos + b.position;

    boidColor = b.color;
    animSpeed = b.currentSpeed;
    #endif
}

#endif
