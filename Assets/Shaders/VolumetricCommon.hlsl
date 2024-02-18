#ifndef VOLUMETRIC_COMMON
#define VOOLUMETRIC_COMMON

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl" // required by the below file (I believe)
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl" // for TEXTURE2D_X() and RW_TEXTURE2D_X

//https://discussions.unity.com/t/how-do-int-textures-work-in-computeshaders/246832/2
#define EncodeUintToFloat(u) (asfloat(u | 0x40000000))
#define DecodeFloatToUint(f) (asuint(f) & 0xBFFFFFFF)

static const float STEP_DISTANCE = 38;
// static const float LIGHT_STEP_DISTANCE = 19;
static const uint LIGHT_SAMPLES = 25;

static const float LIGHT_STEP_MULTIPLIER = 1.18f;

static const float OVER_STEP = 4;
static const float EMPTY_OVER_STEP = 5;

float3 _CamFrustrum[4];


// float _StepDistance;

float _MinHeight;
float _MaxHeight;
float _Width;

// Texture3D<float> _SDFMap;
// float _SDFMarchDistance; 

// Texture3D<float> _DensityMap;
Texture3D<float2> _DensityMap;
Texture3D<float> _SDF;

Texture3D<float4> _BrickMap;
Texture3D<float> _Bricks;

float _BrickSize;
float _BrickCellSize;

float _SDFNorm;
// Texture3D<float> _DetailDensityMap;

float _Downscale;

float _Density;
float3 _Scale;
float _DetailScale;
float _DetailAmount;
float _BufferDistance;

float _Phase;
float _Phase2;

float3 _SunDirection;
float3 _SunColour;

float3 _AmbientColour;
float3 _TotalAmbientColour;

float3 _InvBrick;


float Random(float seed)
{
    return frac(sin(dot(seed, float2(12.9898, 78.233)))*43758.5453);
}

float3 getViewDir(float2 uv)
{
    return (lerp(lerp(_CamFrustrum[2], _CamFrustrum[3], uv.x), lerp(_CamFrustrum[0], _CamFrustrum[1], uv.x), uv.y));
}

float hgPhase(float cosAngle, float g)
{
    float gSquared = g * g;
    return (1 - gSquared) / pow(1 + gSquared - 2 * g * cosAngle, 1.5f);
}

float phaseFunction(float cosAngle)
{
    return lerp(hgPhase(cosAngle, _Phase), hgPhase(cosAngle, _Phase2), 0.5f);
}

// Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
float2 Intersection(float3 rayOrigin, float3 invRaydir) {
    // Adapted from: http://jcgt.org/published/0007/03/04/
    float3 bmin = float3(- _Width, _MinHeight, -_Width);
    float3 bmax = float3(_Width, _MaxHeight,  _Width);
    float3 t0 = (bmin - rayOrigin) * invRaydir;
    float3 t1 = (bmax - rayOrigin) * invRaydir;
    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);
    
    float dstA = max(max(tmin.x, tmin.y), tmin.z);
    float dstB = min(tmax.x, min(tmax.y, tmax.z));

    // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
    // dstA is dst to nearest intersection, dstB dst to far intersection

    // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
    // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

    // CASE 3: ray misses box (dstA > dstB)

    float dstToBox = max(0, dstA);
    float dstInsideBox = max(0, dstB - dstToBox);
    return float2(dstToBox, dstInsideBox);
}

// float DensityFunction(float density)
// {
//     return max(0, density);
//     return max(0, 2.0f * density - 1.35f);
// }

float2 SampleMajorDensity(float3 pos)
{
    return _DensityMap.SampleLevel(s_linear_repeat_sampler, pos / float3(_Width * 2.0f, _MaxHeight - _MinHeight, _Width * 2.0f), 0);
}

float2 SampleFullDensity(float3 pos)
{
    float mult = pos.y > _MaxHeight ? 0 : 1;
    return SampleMajorDensity(pos) * mult;
}

// float LightBlockWalk(float3 p, float3 worldPos, float3 brick)
// {
//     float3 t0 = (0.f - p) / (_SunDirection + 0.0001f);
//     float3 t1 = (1.0f - p) / (_SunDirection + 0.0001f);
//     float3 tmin = min(t0, t1);
//     float3 tmax = max(t0, t1);
//     float fromDst = max(max(tmin.x, tmin.y), tmin.z) + 1.0f / (_BrickSize);
//     float toDst = min(tmax.x, min(tmax.y, tmax.z));
//     float transmittance = 1;
//     [loop]
//     while (fromDst < toDst)
//     {
//         float3 brickPos = p + _SunDirection * fromDst;
//         fromDst += STEP_DISTANCE / (_BrickCellSize );
//             
//         float density = _Bricks.SampleLevel(s_point_clamp_sampler, brick.xyz + ((brickPos * _BrickSize)) * _InvBrick), 0) * STEP_DISTANCE * _Density;
//         
//         transmittance *= exp(-density);
//     }
//     return transmittance;
// }
//
// float3 FullLightRaymarch(float3 pos)
// {
//     float2 tParams = Intersection((pos - float3(_Width, -_MinHeight, _Width)), 1.0f / _SunDirection);
//
//     float transmittance = 1;
//
//     float3 lightEnergy = 0;
//
//     float t = 0;
//
//     float3 invDir = 1.0f / (abs(_SunDirection));
//
//     pos = ((pos) / _BrickCellSize);
//     float3 startPos = pos;
//
//     float3 initDst = (floor(pos) - pos + (sign(_SunDirection) + 1.0f) / 2.0f) / _SunDirection;
//     float3 dst = initDst;
//
//     pos = floor(pos);
//     
//     [loop]
//     while (t < tParams.y)
//     {
//         float4 brick = _BrickMap.Load(float4(pos, 0));
//         if (brick.a > 0)
//         {
//             float3 p = (startPos + t * _SunDirection / _BrickCellSize) - pos;
//
//             transmittance *= LightBlockWalk(p, pos, brick);
//
//             
//             if (transmittance < 0.01f)
//             {
//                 break;
//             }
//         }
//         
//         //https://www.scratchapixel.com/lessons/3d-basic-rendering/introduction-acceleration-structure/grid.html
//         if (dst.z < min(dst.x, dst.y))
//         {
//             t = dst.z * _BrickCellSize;
//             dst.z += invDir.z;
//             pos.z += sign(_SunDirection.z);
//         }
//         else
//         {
//             if (dst.x < dst.y)
//             {
//                 t = dst.x * _BrickCellSize; // current t, next intersection with cell along ray
//                 dst.x += invDir.x; // increment, next crossing along x
//                 pos.x += sign(_SunDirection.x);
//             }
//             else
//             {
//                 t = dst.y * _BrickCellSize;
//                 dst.y += invDir.y; // increment, next crossing along y
//                 pos.y += sign(_SunDirection.y);
//             }
//         }
//     }
//     return transmittance * _SunColour;
// }

float3 LightRaymarch(float3 pos)
{
    float2 tParams = Intersection((pos - float3(_Width, -_MinHeight, _Width)), 1.0f / _SunDirection);

    float density = 0;

    float t = 0;

    float3 startPos = (pos / (_BrickCellSize));

    float stepDst = STEP_DISTANCE;
    
    [loop]
    while (t < tParams.y)
    {
        stepDst *= LIGHT_STEP_MULTIPLIER;
        t += stepDst;
        pos = floor((startPos + t * _SunDirection / _BrickCellSize));
        float4 brick = (_BrickMap.Load(float4(pos, 0)));
        if (brick.a > 0)
        {
            float3 p = (startPos + t * _SunDirection / _BrickCellSize) - pos;

            density += _Bricks.SampleLevel(s_linear_repeat_sampler, brick.xyz * _InvBrick + (p * (_BrickSize)) * _InvBrick, 0) * stepDst;
        }
    }
    return exp(-density * _Density) * _SunColour;
}



float4 BlockWalk(float3 p, float3 worldPos, float3 dir, float3 brick, float transmittance)
{
    float3 t0 = (0.f - p) / (dir + 0.0001f);
    float3 t1 = (1.0f - p) / (dir + 0.0001f);
    float3 tmin = min(t0, t1);
    float3 tmax = max(t0, t1);
    float fromDst = max(max(tmin.x, tmin.y), tmin.z);
    float toDst = min(tmax.x, min(tmax.y, tmax.z));
    float4 output = float4(0,0,0,transmittance);
    [loop]
    while (fromDst < toDst)
    {
        float3 brickPos = p + dir * fromDst;
        fromDst += STEP_DISTANCE / (_BrickCellSize);
            
        float density = _Bricks.SampleLevel(s_linear_repeat_sampler, brick.xyz + ((brickPos * _BrickSize) * _InvBrick), 0) * STEP_DISTANCE * _Density;
        
        output.xyz += output.w * density * LightRaymarch((worldPos + brickPos) * _BrickCellSize);
        output.w *= exp(-density);
    }
    return output;
}



float4 OctTreeRaymarch(float3 startPos, float3 direction, float distance, float totalDistance)
{
    float transmittance = 1;

    float3 lightEnergy = 0;
    float phase = phaseFunction(dot(direction, _SunDirection));

    float t = 0;

    float3 invDir = 1.0f / (abs(direction));

    startPos = ((startPos + float3(_Width, -_MinHeight, _Width)) / _BrickCellSize);

    float3 pos = startPos;
    float3 initDst = (floor(pos) - pos + (sign(direction) + 1.0f) / 2.0f) / direction;
    float3 dst = initDst;

    pos = floor(pos);

    float maxStep = max(abs(direction.x), max(abs(direction.y), abs(direction.z)));

    
    [loop]
    while (t < totalDistance)
    {
        float4 brick = (_BrickMap.Load(float4(pos, 0)));
        if (brick.a > 0)
        {
            float3 p = (startPos + t * direction / _BrickCellSize) - pos;

            float4 block = BlockWalk(p, pos, direction, brick.xyz * _InvBrick, transmittance);

            lightEnergy += block.xyz;
            transmittance = block.w;
            if (transmittance < 0.01f)
            {
                break;
            }
        }
        
        //https://www.scratchapixel.com/lessons/3d-basic-rendering/introduction-acceleration-structure/grid.html
        if (dst.z < min(dst.x, dst.y))
        {
            t = dst.z * _BrickCellSize;
            dst.z += invDir.z;
            pos.z += sign(direction.z);
        }
        else
        {
            if (dst.x < dst.y)
            {
                t = dst.x * _BrickCellSize; // current t, next intersection with cell along ray
                dst.x += invDir.x; // increment, next crossing along x
                pos.x += sign(direction.x);
            }
            else
            {
                t = dst.y * _BrickCellSize;
                dst.y += invDir.y; // increment, next crossing along y
                pos.y += sign(direction.y);
            }
        }
    }
    return float4(lightEnergy * phase, 1.0f - transmittance);
}



#endif