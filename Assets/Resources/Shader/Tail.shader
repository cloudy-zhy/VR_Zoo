Shader "Custom/URP/CometTailPlane"
{
    Properties
    {
        _CoreTex ("Core Streak Texture", 2D) = "white" {}
        _WispTex ("Wisp Texture", 2D) = "gray" {}
        _NoiseTex ("Noise Texture", 2D) = "gray" {}

        [HDR]_CoreColor ("Core Color", Color) = (1.0, 0.48, 0.08, 1)
        [HDR]_WispColorA ("Wisp Color A", Color) = (1.0, 0.22, 0.04, 1)
        [HDR]_WispColorB ("Wisp Color B", Color) = (0.45, 0.18, 1.0, 1)

        _CoreSpeed ("Core Speed", Vector) = (0.8, 0, 0, 0)
        _WispSpeed ("Wisp Speed", Vector) = (0.35, 0.05, 0, 0)
        _NoiseSpeed ("Noise Speed", Vector) = (0.15, -0.05, 0, 0)

        _DistortStrength ("Distort Strength", Range(0, 0.2)) = 0.035
        _CoreWidth ("Core Width", Range(0.01, 0.5)) = 0.13
        _WispWidth ("Wisp Width", Range(0.05, 0.8)) = 0.42

        _LengthFade ("Length Fade", Range(0.2, 5)) = 1.8
        _EdgeFade ("Edge Fade", Range(0.01, 0.5)) = 0.18

        _CoreIntensity ("Core Intensity", Range(0, 20)) = 6
        _WispIntensity ("Wisp Intensity", Range(0, 10)) = 3
        _Alpha ("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "CometTail"
            Tags { "LightMode"="UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CoreTex);
            SAMPLER(sampler_CoreTex);

            TEXTURE2D(_WispTex);
            SAMPLER(sampler_WispTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreTex_ST;
                float4 _WispTex_ST;
                float4 _NoiseTex_ST;

                float4 _CoreColor;
                float4 _WispColorA;
                float4 _WispColorB;

                float4 _CoreSpeed;
                float4 _WispSpeed;
                float4 _NoiseSpeed;

                float _DistortStrength;
                float _CoreWidth;
                float _WispWidth;
                float _LengthFade;
                float _EdgeFade;
                float _CoreIntensity;
                float _WispIntensity;
                float _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y;

                // uv.x = 0 靠近星星，uv.x = 0 是尾端
                float lengthMask = pow(saturate(uv.x), _LengthFade);

                // 上下边缘柔化，避免看到矩形平面
                float edgeMask =
                    smoothstep(0.0, _EdgeFade, uv.y) *
                    smoothstep(0.0, _EdgeFade, 1.0 - uv.y);

                float centerDist = abs(uv.y - 0.5);

                float coreBand = smoothstep(_CoreWidth, 0.0, centerDist);
                float wispBand = smoothstep(_WispWidth, 0.0, centerDist);

                float2 noiseUV = uv * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
                noiseUV += _NoiseSpeed.xy * t;

                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                float2 distort = float2(noise - 0.5, noise - 0.5) * _DistortStrength;

                float2 coreUV = uv * _CoreTex_ST.xy + _CoreTex_ST.zw;
                coreUV += _CoreSpeed.xy * t;
                coreUV += distort * 0.35;

                float2 wispUV = uv * _WispTex_ST.xy + _WispTex_ST.zw;
                wispUV += _WispSpeed.xy * t;
                wispUV += distort;

                float coreTex = SAMPLE_TEXTURE2D(_CoreTex, sampler_CoreTex, coreUV).r;
                float wispTex = SAMPLE_TEXTURE2D(_WispTex, sampler_WispTex, wispUV).r;

                float core = coreTex * coreBand * lengthMask;
                float wisp = wispTex * wispBand * lengthMask * edgeMask;

                float3 wispColor = lerp(
                    _WispColorA.rgb,
                    _WispColorB.rgb,
                    saturate(uv.y + noise * 0.35)
                );

                float3 color =
                    _CoreColor.rgb * core * _CoreIntensity +
                    wispColor * wisp * _WispIntensity;

                color *= edgeMask;
                color *= _Alpha * input.color.a;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}