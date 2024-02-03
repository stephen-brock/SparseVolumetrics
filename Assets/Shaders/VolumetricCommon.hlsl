#ifndef VOLUMETRIC_COMMON
#define VOOLUMETRIC_COMMON

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl" // required by the below file (I believe)
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl" // for TEXTURE2D_X() and RW_TEXTURE2D_X


static const float STEP_DISTANCE = 10;
static const uint LIGHT_SAMPLES = 5;

float3 _CamFrustrum[4];

float _MinHeight;
float _MaxHeight;
float _Width;

// Texture3D<float> _SDFMap;
// float _SDFMarchDistance;

Texture3D<float> _DensityMap;
// Texture3D<float2> _DensityMap;
Texture3D<float> _DetailDensityMap;

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

float SampleMajorDensity(float3 pos)
{
    return _DensityMap.SampleLevel(s_linear_repeat_sampler, pos / float3(_Width, _MaxHeight - _MinHeight, _Width), 0);
}

float SampleDensity(float3 pos, float density)
{
    return max(0, density + _DetailDensityMap.SampleLevel(s_linear_repeat_sampler, pos * _DetailScale, 0) * _DetailAmount);
}

float3 LightRaymarch(float3 pos)
{
    float2 tParams = Intersection(pos, 1.0f / _SunDirection);
    if (tParams.y < tParams.x)
    {
        return 0;
    }
    float stepDistance = tParams.y / (LIGHT_SAMPLES + 1);
    float3 step = _SunDirection * stepDistance;

    float totalDensity = SampleDensity(pos, _DensityMap.SampleLevel(s_linear_repeat_sampler, pos * _Scale, 0).x);
    pos += step;
    totalDensity = (SampleDensity(pos, _DensityMap.SampleLevel(s_linear_repeat_sampler, pos * _Scale, 0).x) + totalDensity) / 2;
    
    [unroll]
    for (uint i = 1; i < LIGHT_SAMPLES; i++)
    {
        pos += step;
        totalDensity += SampleDensity(pos, _DensityMap.SampleLevel(s_linear_repeat_sampler, pos * _Scale, 0).x);
    }
    
    return exp(-totalDensity * stepDistance * _Density) * _SunColour;
}

// float SDFRaymarch(float3 startPos, float3 direction, float totalDistance)
// {
//     float3 pos = startPos;
//     float dst = 1.0f - _DensityMap.SampleLevel(s_linear_repeat_sampler, pos * _Scale, 0).y;
//     
//     float t = 0;
//
//     float stepDst = _SDFMarchDistance * dst;
//     [loop]
//     while (t < totalDistance)
//     {
//         t += stepDst;
//         pos += direction * stepDst;
//         dst = 1.0f - _DensityMap.SampleLevel(s_linear_repeat_sampler, pos * _Scale, 0).y;
//
//         if (dst <= 0.1f)
//         {
//             break;
//         }
//         stepDst = _SDFMarchDistance * dst;
//     }
//     return max(0, t - stepDst);
// }

float4 Raymarch(float3 startPos, float3 direction, float distance, float totalDistance)
{
    float3 pos = startPos;
    float transmittance = 1;

    float3 lightEnergy = 0;
    float phase = phaseFunction(dot(direction, _SunDirection));

    float t = 0;
    // reproj = 0;

    [loop]
    while (t < totalDistance)
    {
        t += STEP_DISTANCE;
        pos += direction * STEP_DISTANCE;
        float smp = SampleMajorDensity(pos);
        float density = SampleDensity(pos, smp.x) * STEP_DISTANCE * _Density;

        if (density > 0)
        {
            lightEnergy += (LightRaymarch(pos)) * density * transmittance + _AmbientColour * transmittance;
            transmittance *= exp(-density);

            // if (transmittance > 0.4f && reproj.y < 0.5f)
            // {
            //     reproj = float2((t + distance) / _ProjectionParams.z, 1);
            // }

            if (transmittance < 0.01)
            {
                transmittance = 0;
                break;
            }
        }
        // float dst = 1.0f - smp.y;
        // if (dst > 0)
        // {
        //     float extend = SDFRaymarch(pos, direction, totalDistance - t);
        //     t += extend;
        //     pos += direction * extend; 
        // }
    }
    return float4(lightEnergy * phase + _TotalAmbientColour, 1.0f - transmittance);
}


#endif