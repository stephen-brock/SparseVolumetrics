Shader "FullScreen/VolumetricRaymarcher"
{
    HLSLINCLUDE

    static const float STEP_DISTANCE = 10;
    static const uint LIGHT_SAMPLES = 5;

    float _MinHeight;
    float _MaxHeight;
    float _Width;

    Texture3D _DensityMap;
    Texture3D _DetailDensityMap;
    float _Density;
    float _LightDensity;
    float3 _Scale;
    float _DetailScale;
    float _DetailAmount;
    float _BufferDistance;

    float _Phase;
    float _Phase2;

    float3 _SunDirection;
    float3 _SunColour;

    float3 _AmbientColour;

    float _ScreenScale;

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    float Random(float seed)
    {
        return frac(sin(dot(seed, float2(12.9898, 78.233)))*43758.5453);
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
    
    // //https://tavianator.com/2011/ray_box.html
    // float Intersection(float3 p, float3 d, out float tmax) {
    //     float3 bmin = float3(_WorldSpaceCameraPos.x - _Width, _MinHeight, _WorldSpaceCameraPos.z - _Width);
    //     float3 bmax = float3(_WorldSpaceCameraPos.x + _Width, _MaxHeight, _WorldSpaceCameraPos.z + _Width);
    //     float tx1 = (bmin.x - p.x) / d.x;
    //     float tx2 = (bmax.x - p.x) / d.x;
    //
    //     float tmin = min(tx1, tx2);
    //     tmax = max(tx1, tx2);
    //
    //     float ty1 = (bmin.y - p.y) / d.y;
    //     float ty2 = (bmax.y - p.y) / d.y;
    //
    //     tmin = max(tmin, min(ty1, ty2));
    //     tmax = min(tmax, max(ty1, ty2));
    //
    //     float tz1 = (bmin.z - p.z) / d.z;
    //     float tz2 = (bmax.z - p.z) / d.z;
    //
    //     tmin = max(tmin, min(tz1, tz2));
    //     tmax = min(tmax, max(tz1, tz2));
    //
    //     return tmin;
    // }

    // Returns (dstToBox, dstInsideBox). If ray misses box, dstInsideBox will be zero
    float2 Intersection(float3 rayOrigin, float3 invRaydir) {
        // Adapted from: http://jcgt.org/published/0007/03/04/
        float3 bmin = float3(_WorldSpaceCameraPos.x - _Width, _MinHeight, _WorldSpaceCameraPos.z - _Width);
        float3 bmax = float3(_WorldSpaceCameraPos.x + _Width, _MaxHeight, _WorldSpaceCameraPos.z + _Width);
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

    float SampleDensity(float3 pos)
    {
        return max(0,max(0, 2.0f * _DensityMap.SampleLevel(s_linear_repeat_sampler, pos * _Scale, 0) - 1.25f) + _DetailDensityMap.SampleLevel(s_linear_repeat_sampler, pos * _DetailScale, 0) * _DetailAmount);
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
        
        float totalDensity = SampleDensity(pos);
        pos += step;
        totalDensity = (SampleDensity(pos) + totalDensity) / 2;
        
        [unroll]
        for (uint i = 1; i < LIGHT_SAMPLES; i++)
        {
            pos += step;
            totalDensity += SampleDensity(pos);
        }
        
        return exp(-totalDensity * stepDistance * _LightDensity) * _SunColour;
    }

    float4 Raymarch(float3 startPos, float3 direction, float totalDistance)
    {
        float3 pos = startPos;
        float transmittance = 1;

        float3 lightEnergy = 0;
        float phase = phaseFunction(dot(direction, _SunDirection));

        float t = 0;

        [loop]
        while (t < totalDistance)
        {
            t += STEP_DISTANCE;
            pos += direction * STEP_DISTANCE;
            float density = SampleDensity(pos) * STEP_DISTANCE * _Density;

            if (density > 0)
            {
                lightEnergy += LightRaymarch(pos) * density * transmittance;
                transmittance *= exp(-density);

                if (transmittance < 0.01)
                {
                    break;
                }
            }
        }

        return float4(lightEnergy * phase + _AmbientColour, 1.0f - transmittance);
    }
    
    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        
        varyings.positionCS.xy *= _ScreenScale;
        float depth = LoadCameraDepth(varyings.positionCS.xy);
        PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        float3 viewDirection = -GetWorldSpaceNormalizeViewDir(posInput.positionWS);
        
        float2 tParams = Intersection(_WorldSpaceCameraPos, 1.0f / viewDirection);

        float depthExtended = posInput.linearDepth >= _ProjectionParams.z ? 1000000 : posInput.linearDepth; 
        tParams.y = min(tParams.y + tParams.x, depthExtended) - tParams.x;
        if (tParams.y < 0)
        {
            return float4(0,0,0,0);
        }
        return Raymarch(_WorldSpaceCameraPos + viewDirection * (tParams.x + Random(posInput.positionSS.x + posInput.positionSS.y * 100.0f - _Time.y * 1000.0f) * _BufferDistance), viewDirection, tParams.y);
    }

    ENDHLSL

    SubShader
    {
        Tags{ "RenderPipeline" = "HDRenderPipeline" }
        Pass
        {
            Name "Custom Pass 0"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
                #pragma fragment FullScreenPass
            ENDHLSL
        }
    }
    Fallback Off
}
