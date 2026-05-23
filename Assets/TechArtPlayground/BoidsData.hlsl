#ifndef BOIDS_DATA_INCLUDED
#define BOIDS_DATA_INCLUDED

struct Boid
{
    float3 position;
    float3 direction;
    float3 color;
    float size;
    float currentSpeed;
};

StructuredBuffer<Boid> boidsBuffer;

// NOUVEAU : J'ai ajouté flapSpeed et flapAmplitude en entrée !
void GetCubePosition_float(float instanceID_input, float3 objectPosition, float time, float flapSpeed, float flapAmplitude, out float3 worldPosition, out float3 boidColor, out float animSpeed)
{
    #if defined(SHADERGRAPH_PREVIEW)
    worldPosition = objectPosition;
    boidColor = float3(1, 1, 1);
    animSpeed = 1.0;
    #else
    uint instanceID = (uint)instanceID_input;
    Boid b = boidsBuffer[instanceID]; 
    
    float3 boidDir = normalize(b.direction);
    if (length(boidDir) < 0.1) boidDir = float3(0, 0, 1);

    float3 up = float3(0, 1, 0);
    if (abs(dot(boidDir, up)) > 0.99) up = float3(0, 0, 1); 
    
    float3 right = normalize(cross(up, boidDir));
    up = cross(boidDir, right);

    float3 scaledPos = objectPosition * b.size;

    // --- NOUVEAU : Le Chaos Organique ---
    // On crée un décalage unique pour chaque poisson basé sur son numéro d'instance.
    // Le "12.345" est juste un nombre aléatoire pour bien casser la symétrie.
    float randomOffset = instanceID_input * 12.345;
    
    // On intègre le décalage, la vitesse (flapSpeed) et l'amplitude (flapAmplitude)
    float wag = sin(time * b.currentSpeed * flapSpeed + scaledPos.z * 5.0 + randomOffset) * (scaledPos.z < 0 ? -scaledPos.z * flapAmplitude : 0.0);
    scaledPos.x += wag; 
    // ------------------------------------

    float3 rotatedPos = right * scaledPos.x + up * scaledPos.y + boidDir * scaledPos.z;

    worldPosition = rotatedPos + b.position;
    
    boidColor = b.color;
    animSpeed = b.currentSpeed;
    #endif
}

#endif