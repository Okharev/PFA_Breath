#ifndef CHIME_WIND_INCLUDED
#define CHIME_WIND_INCLUDED

float3x3 AngleAxis3x3_float(float angle, float3 axis)
{
    float c, s;
    sincos(angle, s, c);
    float t = 1.0 - c;
    float x = axis.x, y = axis.y, z = axis.z;
    return float3x3(
        t * x * x + c,      t * x * y - s * z,  t * x * z + s * y,
        t * x * y + s * z,  t * y * y + c,      t * y * z - s * x,
        t * x * z - s * y,  t * y * z + s * x,  t * z * z + c
    );
}

void CalculateChimeWind_float(
    float3 PositionOS, 
    float3 NormalOS, 
    float3 PivotOS, 
    float4 VertexColor, 
    float3 WindDirectionWS, 
    float WindStrength, 
    float Time,
    float4x4 WorldToObjectMatrix,
    float4x4 ObjectToWorldMatrix,
    out float3 OutPositionOS, 
    out float3 OutNormalOS)
{
    float vMask = VertexColor.r;
    float rigidity = VertexColor.g;
    float weight = max(VertexColor.b, 0.1);
    
    float3 pivotWS = mul(ObjectToWorldMatrix, float4(PivotOS, 1.0)).xyz;
    float3 windDirOS = normalize(mul(WorldToObjectMatrix, float4(WindDirectionWS, 0.0)).xyz);

    // STEP 1: ROPE SWAY 
    float ropePhase = dot(pivotWS, float3(0.1, 0.0, 0.1)) + Time;
    float ropeSwayAmount = sin(ropePhase) * (WindStrength * 0.25);
    
    float3 ropeSwayOffsetOS = windDirOS * ropeSwayAmount;
    float3 displacedPositionOS = PositionOS + ropeSwayOffsetOS;
    float3 displacedPivotOS = PivotOS + ropeSwayOffsetOS;

    // STEP 2: CHIME SWING 
    float chimePhase = dot(pivotWS, float3(0.5, 0.0, 0.5)) + (Time * 2.0);
    float gust = sin(chimePhase) * 0.5 + 0.5;
    float flutter = sin(chimePhase * 4.3) * 0.2;
    float chimeWindForce = (gust + flutter) * (WindStrength / weight);

    // Rigid Math
    float3 rotAxis = normalize(cross(float3(0, 1, 0), windDirOS));
    float angle = chimeWindForce * 1.5; 
    float3x3 rotMatrix = AngleAxis3x3_float(angle, rotAxis);
    float3 rigidPosOS = mul(rotMatrix, displacedPositionOS - displacedPivotOS) + displacedPivotOS;
    float3 rigidNormalOS = mul(rotMatrix, NormalOS);

    // Soft Math
    float bendFactor = vMask * vMask;

    float3 gustNoiseOffset = float3(
        sin(pivotWS.x * 2.1 + Time * 3.3),
        cos(pivotWS.y * 1.7 - Time * 2.8),
        sin(pivotWS.z * 2.5 + Time * 4.1)
    ) * 0.35; 
    float3 localizedWindDir = normalize(windDirOS + gustNoiseOffset);

    float rippleSpeed = Time * 20.0;
    float ripplePhase = (PositionOS.y * 15.0) - rippleSpeed; 
    float3 crossWind = cross(float3(0, 1, 0), localizedWindDir);
    float3 rippleOffset = crossWind * sin(ripplePhase) * (bendFactor * 0.15 * chimeWindForce);

    float twistAngle = sin(Time * 4.0 + pivotWS.x) * bendFactor * chimeWindForce * 1.2;
    float3x3 twistMatrix = AngleAxis3x3_float(twistAngle, float3(0, 1, 0));

    float3 softOffset = localizedWindDir * (chimeWindForce * bendFactor) + rippleOffset;
    float3 softPosOS = displacedPositionOS + softOffset;

    softPosOS = mul(twistMatrix, softPosOS - displacedPivotOS) + displacedPivotOS;
    float3 softNormalOS = mul(twistMatrix, NormalOS);

    float origDist = length(PositionOS - PivotOS);
    float3 bendDir = softPosOS - displacedPivotOS;
    softPosOS = displacedPivotOS + (normalize(bendDir + float3(0, 0.0001, 0)) * origDist);

    softNormalOS = normalize(softNormalOS + (localizedWindDir * bendFactor * chimeWindForce));

    // OUTPUT
    OutPositionOS = lerp(softPosOS, rigidPosOS, rigidity);
    OutNormalOS = normalize(lerp(softNormalOS, rigidNormalOS, rigidity));
}
#endif