Shader "Unlit/VolumetricVertFrag"
{
    Properties
    {
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent"}
        LOD 100
        
        ZWrite On
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
                float4 vertex : SV_POSITION;
                float3 viewDir : TEXCOORD0;
                float maxDistance : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                float3 worldPos = mul(UNITY_MATRIX_M, v.vertex);
                o.viewDir = GetWorldSpaceViewDir(worldPos);
                o.maxDistance = v.colour * 8000.0f;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 worldPos = _WorldSpaceCameraPos - i.viewDir;
                float3 viewDirection = -normalize(i.viewDir);
                float depth = LinearEyeDepth(SampleCameraDepth((i.vertex.xy + 1.0f) / 2.0f), _ZBufferParams);
                
                float2 tParams = Intersection(worldPos, 1.0f / viewDirection);
                
                // float depthExtended = depth >= _ProjectionParams.z - 1.0f ? 10000000 : depth;
                // tParams.y = min(tParams.y + tParams.x, depthExtended) - tParams.x;
                float3 startPos = worldPos - viewDirection * (tParams.x + Random(depth + _Time.y) * _BufferDistance);
                
                float2 reproj;
                float4 col = Raymarch(startPos, viewDirection, 0, min(i.maxDistance, tParams.y), reproj);
                return col;
            }
            ENDHLSL
        }
    }
}
