Shader "Unlit/VolumetricVertFrag"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent"}
        LOD 100
        
        ZWrite Off
        Cull Front
        Blend SrcAlpha OneMinusSrcAlpha 
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "HLSLSupport.cginc"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariablesFunctions.hlsl"
            #include "VolumetricCommon.hlsl"
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float colour : COLOR;
            };

            struct v2f
            {
                float4 viewDirDst : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            float _Downsample;

            #define V2F_SCREEN_TYPE float4
            inline float4 ComputeScreenPos (float4 pos) {
              float4 o = pos * 0.5f; //why myltiply by .5f
              #if defined(UNITY_HALF_TEXEL_OFFSET)
              o.xy = float2(o.x, o.y*_ProjectionParams.x) + o.w * _ScreenParams.zw;
              #else
              o.xy = float2(o.x, o.y*_ProjectionParams.x) + o.w;
              #endif
             
              #if defined(SHADER_API_FLASH)
              o.xy *= unity_NPOTScale.xy;
              #endif
             
              o.zw = pos.zw;
              return o;
            }
            
            v2f vert (appdata v, out float4 outpos : SV_POSITION)
            {
                v2f o;
                outpos = TransformObjectToHClip(v.vertex);
                float3 worldPos = mul(UNITY_MATRIX_M, v.vertex);
                o.viewDirDst.xyz = GetWorldSpaceViewDir(worldPos);
                o.viewDirDst.w = pow(v.colour, 2.2f) * 8000.0f;
                return o;
            }

            float4 frag (v2f i, UNITY_VPOS_TYPE screenPos : VPOS) : SV_Target
            {
                float3 worldPos = _WorldSpaceCameraPos - i.viewDirDst.xyz;
                float3 viewDirection = -normalize(i.viewDirDst.xyz);
                float depth = LinearEyeDepth(SampleCameraDepth(_Downsample * screenPos.xy / _ScreenParams), _ZBufferParams);
                
                
                float2 tParams = Intersection(worldPos, 1.0f / viewDirection);
                
                float depthExtended = depth >= _ProjectionParams.z - 1.0f ? 10000000 : depth;
                tParams.y = min(tParams.y + tParams.x, depthExtended - LinearEyeDepth(screenPos.zzz, _ZBufferParams)) - tParams.x;
                if (tParams.y <= 0.f)
                {
                    return float4(0,0,0,0);
                }

                float3 startPos = worldPos - viewDirection * (tParams.x + Random(depth + _Time.y) * _BufferDistance);
                
                // float2 reproj;
                float4 col = Raymarch(startPos, viewDirection, 0, min(i.viewDirDst.w, tParams.y));
                return col;
            }
            ENDHLSL
        }
    }
}
