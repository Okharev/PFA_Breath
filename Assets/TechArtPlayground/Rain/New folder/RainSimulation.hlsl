#pragma kernel UpdateRain

struct RainDrop
{
    float3 position;
    float randomSeed;
};

RWStructuredBuffer<RainDrop> _RainBuffer;
AppendStructuredBuffer<float3> _SplashRequests;

float _DeltaTime;
float _Time;
float _GridSize;
float3 _RainVelocity; 
float3 _CameraPos;

Texture2D<float> _OcclusionMap;
SamplerState sampler_OcclusionMap; 
float3 _OcclusionCenter;
float _OcclusionOrthoSize;
float _OcclusionCameraY;
float _FarClipPlane;
int _IsReversedZ;

float3 FastTurbulence(float3 pos, float time)
{
    pos *= 0.15;
    return float3(
        sin(pos.y + time) * cos(pos.z * 0.8),
        sin(pos.z - time * 0.8) * cos(pos.x),
        sin(pos.x + time * 1.2) * cos(pos.y * 0.9)
    );
}

[numthreads(128, 1, 1)]
void UpdateRain(uint3 id : SV_DispatchThreadID)
{
    uint index = id.x;
    RainDrop drop = _RainBuffer[index];

    // 1. PHYSICS
    float3 turbulence = FastTurbulence(drop.position, _Time) * 3.0;
    drop.position += (_RainVelocity + turbulence) * drop.randomSeed * _DeltaTime;

    float halfGrid = _GridSize * 0.5;
    bool shouldRespawn = false;

    // 2. ROOF OCCLUSION CHECK
    float2 occUV;
    occUV.x = (drop.position.x - (_OcclusionCenter.x - _OcclusionOrthoSize)) / (_OcclusionOrthoSize * 2.0);
    occUV.y = (drop.position.z - (_OcclusionCenter.z - _OcclusionOrthoSize)) / (_OcclusionOrthoSize * 2.0); 
    
    if (occUV.x >= 0.0 && occUV.x <= 1.0 && occUV.y >= 0.0 && occUV.y <= 1.0)
    {
        float rawDepth = _OcclusionMap.SampleLevel(sampler_OcclusionMap, occUV, 0);
        float linearDepth = (_IsReversedZ == 1) ? (1.0 - rawDepth) : rawDepth;
        float roofHeight = _OcclusionCameraY - (linearDepth * _FarClipPlane);

        if (drop.position.y < roofHeight && linearDepth < 0.99)
        {
            shouldRespawn = true;
            
            // --- TRIGGER THE SPLASH ---
            // Append the exact hit coordinate to the buffer.
            // We use roofHeight for Y so it sits perfectly on the ground/roof.
            _SplashRequests.Append(float3(drop.position.x, roofHeight, drop.position.z));
        }
    }

    // 3. HORIZONTAL WRAPPING 
    // We keep horizontal wrapping independent so rain doesn't vanish if the player runs forward quickly
    if (drop.position.x < _CameraPos.x - halfGrid) drop.position.x += _GridSize;
    if (drop.position.x > _CameraPos.x + halfGrid) drop.position.x -= _GridSize;
    if (drop.position.z < _CameraPos.z - halfGrid) drop.position.z += _GridSize;
    if (drop.position.z > _CameraPos.z + halfGrid) drop.position.z -= _GridSize;

    // 4. VERTICAL VOID CHECK
    if (drop.position.y < _CameraPos.y - halfGrid)
    {
        // Particle fell all the way into the void!
        shouldRespawn = true;
    }
    else if (drop.position.y > _CameraPos.y + halfGrid)
    {
        // Failsafe: Player is falling downwards extremely fast. Just wrap Y.
        drop.position.y -= _GridSize;
    }

    // 5. UNIFIED RESPAWN LOGIC
    if (shouldRespawn)
    {
        // Every single time a particle dies (roof OR void), randomize its X and Z entirely.
        // This completely eliminates the "Particle Sink" bug.
        float randX = frac(sin(drop.randomSeed * 12.9898 + _Time) * 43758.5453);
        float randZ = frac(cos(drop.randomSeed * 78.233 + _Time) * 43758.5453);

        drop.position.x = _CameraPos.x + (randX - 0.5) * _GridSize;
        drop.position.z = _CameraPos.z + (randZ - 0.5) * _GridSize;

        // Teleport to the top of the sky
        drop.position.y = _CameraPos.y + (halfGrid * 0.98);
        
        // Optional: Scramble the seed so it falls at a slightly different speed on its next life
        drop.randomSeed = lerp(0.8, 1.2, frac(drop.randomSeed * 93.213 + _Time));
    }

    // 6. WRITE BACK TO VRAM
    _RainBuffer[index] = drop;
}