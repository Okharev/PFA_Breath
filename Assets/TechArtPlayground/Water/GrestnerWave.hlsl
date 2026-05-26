// GerstnerWave.hlsl
// Set node inputs: Position(Vector3), Direction(Vector2), Steepness(Float), Wavelength(Float), Speed(Float)
// Set node outputs: OutPosition(Vector3)

void CalculateGerstnerWave_float(float3 Position, float2 Direction, float WaveHeight, float Wavelength, float Steepness, float Speed, float Time, out float3 OutPosition)
{
    // Calculate the wave number (k) based on Wavelength
    float k = 2.0 * 3.14159265 / Wavelength;
    
    // Normalize the direction to ensure consistent speeds
    float2 d = normalize(Direction);
    
    // Calculate the phase of the wave (f)
    float f = k * (dot(d, Position.xz) - Speed * Time);
    
    // Displacement Mathematics:
    // Y is displaced vertically by the WaveHeight.
    // X and Z are pulled backward/forward by the Steepness to create sharp, sweeping crests.
    OutPosition.x = Position.x + d.x * (Steepness * WaveHeight * cos(f));
    OutPosition.y = WaveHeight * sin(f);
    OutPosition.z = Position.z + d.y * (Steepness * WaveHeight * cos(f));
}