#pragma kernel ProcessSplashRequests
#pragma kernel UpdateSplashes

struct Splash
{
    float3 position;
    float life;     // 1.0 = newborn, 0.0 = dead
    float maxLife;
};

RWStructuredBuffer<Splash> _SplashPool;
StructuredBuffer<float3> _SplashRequests;
RWStructuredBuffer<uint> _PoolIndex; // Keeps track of where we are in the ring buffer

uint _RequestCount;
uint _MaxSplashes;
float _DeltaTime;

[numthreads(64, 1, 1)]
void ProcessSplashRequests(uint3 id : SV_DispatchThreadID)
{
    uint index = id.x;
    if (index >= _RequestCount) return;

    // Grab a request position
    float3 hitPos = _SplashRequests[index];

    // Atomically claim the next slot in the ring buffer so threads don't overwrite each other
    uint slot;
    InterlockedAdd(_PoolIndex[0], 1, slot);
    slot = slot % _MaxSplashes;

    // Spawn the splash
    Splash s;
    s.position = hitPos;
    s.life = 1.0;
    // Randomize the lifespan slightly based on world position
    s.maxLife = 0.15 + (frac(hitPos.x * 12.34) * 0.15); 
    
    _SplashPool[slot] = s;
}

[numthreads(128, 1, 1)]
void UpdateSplashes(uint3 id : SV_DispatchThreadID)
{
    uint index = id.x;
    if (index >= _MaxSplashes) return;

    Splash s = _SplashPool[index];
    
    // Only update living splashes
    if (s.life > 0.0)
    {
        s.life -= _DeltaTime / s.maxLife;
        if (s.life <= 0.0) s.life = 0.0;
        _SplashPool[index] = s;
    }
}