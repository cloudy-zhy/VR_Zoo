Shader "Custom/URP/Animal Silhouette Galaxy Skybox"
{
    Properties
    {
        [Header(Gradient)]
        _TopColor ("Top Color", Color) = (0.015, 0.035, 0.12, 1)
        _MiddleColor ("Middle Color", Color) = (0.035, 0.055, 0.18, 1)
        _BottomColor ("Bottom Color", Color) = (0.0, 0.0, 0.025, 1)
        _MiddlePosition ("Middle Position", Range(0.05, 0.95)) = 0.5
        _GradientSmoothness ("Gradient Smoothness", Range(0, 1)) = 0.6
        _SkyIntensity ("Sky Intensity", Range(0, 3)) = 1

        [Header(Animal Silhouette Texture)]
        _SilhouetteTex ("Black White Silhouette Texture", 2D) = "black" {}
        _SilhouetteColor ("Silhouette Star Color", Color) = (1, 0.92, 0.72, 1)
        _SilhouetteStrength ("Silhouette Sample Strength", Range(0, 1)) = 1
        _SilhouetteThreshold ("Silhouette Threshold", Range(0, 1)) = 0.5
        _SilhouetteSoftness ("Silhouette Edge Softness", Range(0.001, 0.5)) = 0.05
        _SilhouetteEmission ("Silhouette Emission", Range(0, 5)) = 1.5
        _SilhouetteRotation ("Silhouette Horizontal Rotation", Range(0, 1)) = 0

        [Header(Procedural Stars)]
        _StarStrength ("Star Toggle Strength", Range(0, 1)) = 1
        _StarCount ("Star Count", Range(0, 800)) = 280
        _StarSize ("Star Size", Range(0.001, 0.08)) = 0.015
        _StarTwinkleFrequency ("Twinkle Frequency", Range(0, 20)) = 3
        _StarTwinkleStrength ("Twinkle Strength", Range(0, 1)) = 0.45
        _StarColor ("Star Color", Color) = (1, 0.96, 0.82, 1)
        _StarEmission ("Star Emission", Range(0, 5)) = 1.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "PreviewType" = "Skybox"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off

        Pass
        {
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define PI 3.14159265359
            #define TWO_PI 6.28318530718

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldDir : TEXCOORD0;
            };

            TEXTURE2D(_SilhouetteTex);
            SAMPLER(sampler_SilhouetteTex);
            float4 _SilhouetteTex_ST;

            half3 _TopColor;
            half3 _MiddleColor;
            half3 _BottomColor;
            half _MiddlePosition;
            half _GradientSmoothness;
            half _SkyIntensity;

            half3 _SilhouetteColor;
            half _SilhouetteStrength;
            half _SilhouetteThreshold;
            half _SilhouetteSoftness;
            half _SilhouetteEmission;
            half _SilhouetteRotation;

            half _StarStrength;
            half _StarCount;
            half _StarSize;
            half _StarTwinkleFrequency;
            half _StarTwinkleStrength;
            half3 _StarColor;
            half _StarEmission;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.worldDir = TransformObjectToWorldDir(input.positionOS.xyz);
                return output;
            }

            float2 DirectionToEquirectUV(float3 dir)
            {
                dir = normalize(dir);
                float u = atan2(dir.x, dir.z) / TWO_PI + 0.5;
                float v = asin(clamp(dir.y, -1.0, 1.0)) / PI + 0.5;
                return float2(u, v);
            }

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            half3 SampleGradient(float y)
            {
                float t = saturate(y * 0.5 + 0.5);
                float mid = saturate(_MiddlePosition);
                float lowerT = saturate(t / max(mid, 0.001));
                float upperT = saturate((t - mid) / max(1.0 - mid, 0.001));

                float lowerSmooth = lowerT * lowerT * (3.0 - 2.0 * lowerT);
                float upperSmooth = upperT * upperT * (3.0 - 2.0 * upperT);
                lowerT = lerp(lowerT, lowerSmooth, _GradientSmoothness);
                upperT = lerp(upperT, upperSmooth, _GradientSmoothness);

                half3 lowerColor = lerp(_BottomColor, _MiddleColor, lowerT);
                half3 upperColor = lerp(_MiddleColor, _TopColor, upperT);
                return lerp(lowerColor, upperColor, step(mid, t)) * _SkyIntensity;
            }

            half SampleSilhouette(float2 skyUV)
            {
                float2 uv = skyUV;
                uv.x = frac(uv.x + _SilhouetteRotation);
                uv = uv * _SilhouetteTex_ST.xy + _SilhouetteTex_ST.zw;

                half4 tex = SAMPLE_TEXTURE2D(_SilhouetteTex, sampler_SilhouetteTex, uv);
                half luminance = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                half mask = smoothstep(_SilhouetteThreshold, saturate(_SilhouetteThreshold + _SilhouetteSoftness), luminance);
                return mask * _SilhouetteStrength;
            }

            half SampleProceduralStars(float2 skyUV)
            {
                float density = max(_StarCount, 0.0);
                float2 gridSize = float2(max(density * 0.08, 1.0), max(density * 0.04, 1.0));
                float2 gridUV = skyUV * gridSize;
                float2 cell = floor(gridUV);
                float2 localUV = frac(gridUV);

                float visibility = step(0.35, Hash12(cell));
                float2 starPos = Hash22(cell + 17.0);
                float2 delta = localUV - starPos;
                delta.x *= gridSize.x / max(gridSize.y, 0.001);

                float dist = length(delta);
                float star = 1.0 - smoothstep(0.0, _StarSize, dist);

                float phase = Hash12(cell + 91.0) * TWO_PI;
                float twinkle = 1.0 - _StarTwinkleStrength;
                twinkle += _StarTwinkleStrength * (sin(_Time.y * _StarTwinkleFrequency + phase) * 0.5 + 0.5);

                return star * visibility * twinkle * _StarStrength * step(0.001, _StarCount);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.worldDir);
                float2 skyUV = DirectionToEquirectUV(dir);

                half3 color = SampleGradient(dir.y);

                half silhouette = SampleSilhouette(skyUV);
                color += _SilhouetteColor * silhouette * _SilhouetteEmission;

                half stars = SampleProceduralStars(skyUV);
                color += _StarColor * stars * _StarEmission;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
