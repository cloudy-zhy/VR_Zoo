Shader "Custom/InfiniteBeamShader"
{
    Properties
    {
        [HDR] _BaseColor("Base Color", Color) = (0, 1, 1, 0.25)
        _BeamRadius("Beam Radius (World Space)", Float) = 0.15
        _UpHeightScale("Up Height Scale", Float) = 500.0
        _DownHeightScale("Down Height Scale", Float) = 500.0
        _FadePower("Fade Power", Float) = 2.0
        _RimPower("Rim Power", Float) = 1.5
        _GlowIntensity("Glow Intensity", Float) = 1.5
        _GlowSpeed("Glow Speed", Float) = 2.0
        _GlowFrequency("Glow Frequency", Float) = 5.0
        _GlowStrength("Glow Strength", Float) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 viewDirWS    : TEXCOORD3;
                float  origY        : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _BeamRadius;
                float _UpHeightScale;
                float _DownHeightScale;
                float _FadePower;
                float _RimPower;
                float _GlowIntensity;
                float _GlowSpeed;
                float _GlowFrequency;
                float _GlowStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // 保存原始本地Y坐标，范围 [-1.0, 1.0]
                output.origY = input.positionOS.y;

                // 1. 获取物体的世界中心位置
                float3 centerWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));

                // 2. 根据本地 XZ 归一化方向和给定的世界空间半径，计算世界空间 XZ 偏移
                // 这样彻底抛弃了物体本身的 Rotation 和 Scale 影响，保证了直立和固定粗细
                float2 dir = input.positionOS.xz;
                float len = length(dir);
                float2 xzOffset = float2(0.0, 0.0);
                if (len > 0.001)
                {
                    xzOffset = (dir / len) * _BeamRadius;
                }

                // 3. 构建初始世界坐标
                float3 finalPosWS = float3(centerWS.x + xzOffset.x, centerWS.y, centerWS.z + xzOffset.y);

                // 4. 双向拉伸 Y 轴高度（世界空间下永远保持垂直）
                if (input.positionOS.y > 0.0)
                {
                    finalPosWS.y = centerWS.y + input.positionOS.y * _UpHeightScale;
                }
                else if (input.positionOS.y < 0.0)
                {
                    finalPosWS.y = centerWS.y + input.positionOS.y * _DownHeightScale;
                }
                else
                {
                    finalPosWS.y = centerWS.y;
                }

                // 5. 变换到裁剪空间
                output.positionCS = TransformWorldToHClip(finalPosWS);
                output.uv = input.uv;

                // 6. 重建世界竖立法线（由于圆柱体直立，世界法线即是侧面水平外扩的方向）
                float3 normalWS = float3(0.0, 0.0, 0.0);
                if (len > 0.001)
                {
                    normalWS = float3(dir.x / len, 0.0, dir.y / len);
                }
                output.normalWS = normalWS;

                // 7. 计算视线方向
                output.viewDirWS = _WorldSpaceCameraPos.xyz - finalPosWS;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. 对称渐隐（以物体位置 origY = 0 最亮，两端 origY = 1 和 -1 为透明）
                float absY = saturate(abs(input.origY));
                float fade = pow(1.0 - absY, _FadePower);

                // 2. 边缘虚化 (Fresnel)
                float3 normal = normalize(input.normalWS);
                float3 viewDir = normalize(input.viewDirWS);
                float rim = 1.0 - saturate(abs(dot(normal, viewDir)));
                rim = pow(rim, _RimPower);

                // 3. 动态流光
                float flow = sin(input.origY * _GlowFrequency - _Time.y * _GlowSpeed) * 0.5 + 0.5;
                float glow = flow * fade * _GlowStrength;

                // 4. 最终透明度
                float alpha = saturate(rim * fade + glow) * _BaseColor.a;

                // 5. 最终自发光颜色
                float3 rgb = _BaseColor.rgb * _GlowIntensity;

                return float4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
