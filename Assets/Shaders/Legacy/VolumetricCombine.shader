Shader "FullScreen/VolumetricCombine"
{
    HLSLINCLUDE

    #pragma vertex Vert

    #pragma target 4.5
    #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

    #include "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/CustomPass/CustomPassCommon.hlsl"

    TEXTURE2D_X(_Volumetrics);

    float _ScreenScale;
    
    float4 FullScreenPass(Varyings varyings) : SV_Target
    {
        //UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(varyings);
        // float depth = LoadCameraDepth(varyings.positionCS.xy);
        //PositionInputs posInput = GetPositionInput(varyings.positionCS.xy, _ScreenSize.zw, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);
        // float3 viewDirection = GetWorldSpaceNormalizeViewDir(posInput.positionWS);
         float4 color = float4(1.0, 0.0, 1.0, 1.0);
        //
        // // Load the camera color buffer at the mip 0 if we're not at the before rendering injection point
        if (_CustomPassInjectionPoint != CUSTOMPASSINJECTIONPOINT_BEFORE_RENDERING)
             color = float4(CustomPassLoadCameraColor(varyings.positionCS.xy, 0), 1);
        //float4 color = SAMPLE_TEXTURE2D_X_LOD(_ColorPyramidTexture, s_linear_clamp_sampler, uv, 0).rgba;
        float width, height;
        float elements;
        _Volumetrics.GetDimensions(width, height, elements);
        float2 uv = varyings.positionCS.xy * _ScreenScale;
        float4 volume = SAMPLE_TEXTURE2D_X_LOD(_Volumetrics, s_trilinear_clamp_sampler,  uv / float2(width, height), 0).rgba;
        return float4(lerp(color.rgb, volume.rgb, volume.a), color.a);
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
