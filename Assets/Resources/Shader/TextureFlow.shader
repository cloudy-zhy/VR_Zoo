Shader "Custom/TextureFlow"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _FlowSpeedX ("Flow Speed X", Float) = 0.5
        _FlowSpeedY ("Flow Speed Y", Float) = 0.0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4  _BaseColor;
                float  _FlowSpeedX;
                float  _FlowSpeedY;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                // 模型空间坐标当 UV（不管模型有没有 UV 都能看到纹理）
                OUT.uv = IN.positionOS.xy * 0.5 + 0.5;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 流动偏移
                float2 flowUV = IN.uv + float2(_FlowSpeedX, _FlowSpeedY) * _Time.y;
                flowUV = frac(flowUV);

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flowUV);
                return tex * _BaseColor;
            }
            ENDHLSL
        }

        // ShadowCaster
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 pos : POSITION; float3 nrm : NORMAL; };
            struct Varyings { float4 pos : SV_POSITION; };

            float4 GetShadowPositionHClip(float3 posWS, float3 normalWS)
            {
                float3 lightDir = _LightDirection;
                float4 posCS = TransformWorldToHClip(posWS);
                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return posCS;
            }

            Varyings ShadowVert(Attributes IN)
            {
                Varyings OUT;
                float3 ws = TransformObjectToWorld(IN.pos.xyz);
                float3 wn = TransformObjectToWorldNormal(IN.nrm);
                OUT.pos = GetShadowPositionHClip(ws, wn);
                return OUT;
            }
            half4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}