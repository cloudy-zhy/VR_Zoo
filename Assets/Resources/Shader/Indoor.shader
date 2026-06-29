Shader "Custom/URP/GalaxyFlowProjectedUV"
{
    Properties
    {
        _NebulaTex ("Nebula Texture", 2D) = "white" {}
        _StarTex ("Star Mask Texture", 2D) = "black" {}
        _NoiseTex ("Noise Distortion Texture", 2D) = "gray" {}

        _BackgroundColor ("Background Color", Color) = (0, 0, 0, 1)
        _BackgroundAlpha ("Background Alpha", Range(0, 1)) = 1

        [HDR]_ColorA ("Nebula Color A", Color) = (0.2, 0.4, 1.0, 1)
        [HDR]_ColorB ("Nebula Color B", Color) = (1.0, 0.25, 0.85, 1)
        [HDR]_StarColor ("Star Color", Color) = (1.0, 0.85, 0.55, 1)

        _NebulaSpeedA ("Nebula Speed A", Vector) = (0.03, 0.01, 0, 0)
        _NebulaSpeedB ("Nebula Speed B", Vector) = (-0.015, 0.025, 0, 0)
        _StarSpeed ("Star Speed", Vector) = (0.01, 0.035, 0, 0)
        _NoiseSpeed ("Noise Speed", Vector) = (0.02, -0.015, 0, 0)

        _DistortStrength ("Distort Strength", Range(0, 0.25)) = 0.04

        _SwirlCenter ("Swirl Center", Vector) = (0.5, 0.5, 0, 0)
        _SwirlStrength ("Swirl Strength", Range(-4, 4)) = 1.2
        _SwirlSpeed ("Swirl Speed", Range(-2, 2)) = 0.25

        _NebulaIntensity ("Nebula Intensity", Range(0, 20)) = 4
        _StarIntensity ("Star Intensity", Range(0, 40)) = 12
        _StarSharpness ("Star Sharpness", Range(0.5, 12)) = 4
        _TwinkleStrength ("Twinkle Strength", Range(0, 1)) = 0.35

        _EdgeFade ("Edge Fade", Range(0.001, 0.5)) = 0.03
        _Alpha ("Galaxy Alpha", Range(0, 1)) = 1
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
            Name "GalaxyFlow"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NebulaTex);
            SAMPLER(sampler_NebulaTex);

            TEXTURE2D(_StarTex);
            SAMPLER(sampler_StarTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NebulaTex_ST;
                float4 _StarTex_ST;
                float4 _NoiseTex_ST;

                float4 _BackgroundColor;
                float _BackgroundAlpha;

                float4 _ColorA;
                float4 _ColorB;
                float4 _StarColor;

                float4 _NebulaSpeedA;
                float4 _NebulaSpeedB;
                float4 _StarSpeed;
                float4 _NoiseSpeed;

                float4 _SwirlCenter;

                float _DistortStrength;
                float _SwirlStrength;
                float _SwirlSpeed;

                float _NebulaIntensity;
                float _StarIntensity;
                float _StarSharpness;
                float _TwinkleStrength;

                float _EdgeFade;
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

            float2 RotateUV(float2 uv, float2 center, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);

                float2 p = uv - center;
                float2 r;
                r.x = p.x * c - p.y * s;
                r.y = p.x * s + p.y * c;

                return r + center;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float t = _Time.y;

                float edgeMask =
                    smoothstep(0.0, _EdgeFade, uv.x) *
                    smoothstep(0.0, _EdgeFade, 1.0 - uv.x) *
                    smoothstep(0.0, _EdgeFade, uv.y) *
                    smoothstep(0.0, _EdgeFade, 1.0 - uv.y);

                float2 center = _SwirlCenter.xy;
                float2 fromCenter = uv - center;
                float radius = length(fromCenter);

                float2 noiseUV = uv * _NoiseTex_ST.xy + _NoiseTex_ST.zw;
                noiseUV += _NoiseSpeed.xy * t;

                float noiseA = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                float noiseB = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV * 1.73 + 0.37).g;

                float2 distortion = float2(noiseA - 0.5, noiseB - 0.5) * _DistortStrength;

                float swirlMask = saturate(1.0 - radius);
                float swirlAngle = (_SwirlStrength * swirlMask + _SwirlSpeed * t) * 0.6;

                float2 flowUV = RotateUV(uv + distortion, center, swirlAngle);

                float2 nebulaUVA = flowUV * _NebulaTex_ST.xy + _NebulaTex_ST.zw;
                nebulaUVA += _NebulaSpeedA.xy * t;

                float2 nebulaUVB = RotateUV(flowUV, center, -swirlAngle * 0.7);
                nebulaUVB = nebulaUVB * _NebulaTex_ST.xy + _NebulaTex_ST.zw;
                nebulaUVB += _NebulaSpeedB.xy * t;

                float4 nebulaA = SAMPLE_TEXTURE2D(_NebulaTex, sampler_NebulaTex, nebulaUVA);
                float4 nebulaB = SAMPLE_TEXTURE2D(_NebulaTex, sampler_NebulaTex, nebulaUVB);

                float nebulaMaskA = nebulaA.r;
                float nebulaMaskB = nebulaB.g;
                float nebulaMask = saturate(nebulaMaskA * 0.75 + nebulaMaskB * 0.65);

                float3 nebulaColor = lerp(
                    _ColorA.rgb,
                    _ColorB.rgb,
                    saturate(nebulaB.b + noiseA * 0.45)
                );

                float2 starUV = flowUV * _StarTex_ST.xy + _StarTex_ST.zw;
                starUV += _StarSpeed.xy * t;
                starUV += distortion * 0.45;

                float starRaw = SAMPLE_TEXTURE2D(_StarTex, sampler_StarTex, starUV).r;
                float stars = pow(saturate(starRaw), _StarSharpness);

                float twinkle = 1.0 + sin((noiseA + noiseB + starRaw) * 18.0 + t * 5.0) * _TwinkleStrength;
                stars *= twinkle;

                float3 galaxyColor =
                    nebulaColor * nebulaMask * _NebulaIntensity +
                    _StarColor.rgb * stars * _StarIntensity;

                galaxyColor *= edgeMask;
                galaxyColor *= _Alpha * input.color.a;

                float backgroundAlpha = _BackgroundAlpha * edgeMask * input.color.a;

                float3 color = _BackgroundColor.rgb + galaxyColor;
                float alpha = saturate(backgroundAlpha);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}