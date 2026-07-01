Shader "Custom/AnimeSkyboxURP"
{
    Properties
    {
        [Header(Sky_Colors)]
        _TopColor("Top Color (Sky Zenith)", Color) = (0.08, 0.23, 0.53, 1)
        _MidColor("Middle Color (Horizon High)", Color) = (0.32, 0.61, 0.89, 1)
        _BottomColor("Bottom Color (Horizon Low)", Color) = (0.91, 0.78, 0.78, 1)
        _MidPoint("Middle Color Position", Range(-1.0, 1.0)) = 0.0
        
        [Header(CloudLayer1_Slow_Soft)]
        _CloudColor1("Cloud Color 1", Color) = (1, 1, 1, 0.5)
        _CloudScale1("Cloud Scale 1", Float) = 3.0
        _CloudSpeed1("Cloud Speed 1 (XY)", Vector) = (0.01, 0.005, 0, 0)
        _CloudCutoff1("Cloud Threshold 1", Range(0.0, 1.0)) = 0.45
        _CloudFeather1("Cloud Feather 1", Range(0.001, 0.5)) = 0.02

        [Header(CloudLayer2_Fast_Crisp)]
        _CloudColor2("Cloud Color 2", Color) = (1, 1, 1, 0.8)
        _CloudScale2("Cloud Scale 2", Float) = 5.0
        _CloudSpeed2("Cloud Speed 2 (XY)", Vector) = (-0.02, 0.01, 0, 0)
        _CloudCutoff2("Cloud Threshold 2", Range(0.0, 1.0)) = 0.5
        _CloudFeather2("Cloud Feather 2", Range(0.001, 0.5)) = 0.01
    }

    SubShader
    {
        // 声明 URP 管线支持
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // 引入 URP 核心着色器库
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 viewDirWS    : TEXCOORD0;
            };

            float4 _TopColor;
            float4 _MidColor;
            float4 _BottomColor;
            float _MidPoint;

            float4 _CloudColor1;
            float _CloudScale1;
            float4 _CloudSpeed1;
            float _CloudCutoff1;
            float _CloudFeather1;

            float4 _CloudColor2;
            float _CloudScale2;
            float4 _CloudSpeed2;
            float _CloudCutoff2;
            float _CloudFeather2;

            // 简易伪随机噪声
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(hash(i + float2(0.0, 0.0)), hash(i + float2(1.0, 0.0)), u.x),
                            lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            // 多层分形噪声
            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2 shift = float2(100.0, 100.0);
                for (int i = 0; i < 3; ++i) {
                    v += a * noise(p);
                    p = p * 2.0 + shift;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                // 优化后的 URP 跨版本裁剪空间坐标转换宏
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDirWS = input.positionOS.xyz; 
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 1. 天空多段渐变
                float3 dir = normalize(input.viewDirWS);
                float y = dir.y; 

                float4 skyColor;
                if (y > _MidPoint)
                {
                    float t = (y - _MidPoint) / (1.0 - _MidPoint);
                    skyColor = lerp(_MidColor, _TopColor, t);
                }
                else
                {
                    float t = (y - (-1.0)) / (_MidPoint - (-1.0));
                    skyColor = lerp(_BottomColor, _MidColor, t);
                }

                float4 finalColor = skyColor;

                // 2. 双层赛璐璐动漫云
                if (y > -0.1)
                {
                    // 消除极点拉伸的经典平面投影
                    float2 skyUV = dir.xz / (max(0.01, dir.y) + 0.3);

                    // --- 第一层云 (远景慢云) ---
                    float2 uv1 = skyUV * _CloudScale1 + _Time.y * _CloudSpeed1.xy;
                    float n1 = fbm(uv1);
                    float cloudAlpha1 = smoothstep(_CloudCutoff1, _CloudCutoff1 + _CloudFeather1, n1);
                    cloudAlpha1 *= smoothstep(-0.1, 0.1, y); 
                    finalColor = lerp(finalColor, _CloudColor1, cloudAlpha1 * _CloudColor1.a);

                    // --- 第二层云 (近景快云) ---
                    float2 uv2 = skyUV * _CloudScale2 + _Time.y * _CloudSpeed2.xy;
                    float n2 = fbm(uv2);
                    float cloudAlpha2 = smoothstep(_CloudCutoff2, _CloudCutoff2 + _CloudFeather2, n2);
                    cloudAlpha2 *= smoothstep(-0.1, 0.1, y); 
                    finalColor = lerp(finalColor, _CloudColor2, cloudAlpha2 * _CloudColor2.a);
                }

                return finalColor;
            }
            ENDHLSL
        }
    }
}