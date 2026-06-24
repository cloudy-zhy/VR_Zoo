Shader "Custom/URP/Transparent Edge Glow"
{
    Properties
    {
        [HDR]_BaseColor ("Base Color", Color) = (0.55, 1.0, 0.55, 1.0)
        _Alpha ("Transparency", Range(0, 1)) = 0.35

        [HDR]_EdgeColor ("Edge Glow Color", Color) = (0.45, 1.0, 0.25, 1.0)
        _EdgeIntensity ("Edge Glow Intensity", Range(0, 20)) = 3.0
        _EdgeWidth ("Edge Glow Width", Range(0, 1)) = 0.55
        _EdgeAlphaBoost ("Edge Alpha Boost", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Alpha;
                half4 _EdgeColor;
                half _EdgeIntensity;
                half _EdgeWidth;
                half _EdgeAlphaBoost;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.normalWS = normalInputs.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(positionInputs.positionWS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                half fresnel = 1.0h - saturate(dot(normalWS, viewDirWS));

                // Higher width values should feel wider in the material inspector.
                half edgePower = lerp(8.0h, 0.35h, _EdgeWidth);
                half edgeMask = pow(fresnel, edgePower);

                half alpha = saturate(_Alpha * _BaseColor.a + edgeMask * _EdgeAlphaBoost * _EdgeColor.a);
                half3 baseColor = _BaseColor.rgb * (_Alpha * _BaseColor.a);
                half3 edgeGlow = _EdgeColor.rgb * edgeMask * _EdgeIntensity;

                return half4(baseColor + edgeGlow, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
