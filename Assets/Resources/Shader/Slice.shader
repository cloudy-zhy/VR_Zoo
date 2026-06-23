Shader "Universal Render Pipeline/Custom/Slice"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1, 1, 1, 1)

        [Header(PBR)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0.0

        [Header(Slice)]
        sliceNormal ("Slice Normal (World)", Vector) = (0, 0, 0, 0)
        sliceCentre ("Slice Centre (World)", Vector) = (0, 0, 0, 0)
        sliceOffsetDst ("Slice Offset", Float) = 0

        // Blending state (for URP shader GUI compatibility)
        [HideInInspector] _Surface ("__surface", Float) = 0
        [HideInInspector] _Blend ("__blend", Float) = 0
        [HideInInspector] _Cull ("__cull", Float) = 2
        [HideInInspector] _AlphaClip ("__clip", Float) = 0
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite ("__zw", Float) = 1
        [HideInInspector] _ReceiveShadows ("Receive Shadows", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex LitPassVertex
            #pragma fragment SliceLitPassFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fog

            #define _SPECULAR_SETUP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // --- Material Properties ---
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Smoothness;
                float _Metallic;
                float3 sliceNormal;
                float sliceOffsetDst;
                float3 sliceCentre;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                half3 viewDirWS : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            };

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, float4(1,0,0,1));

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.shadowCoord = TransformWorldToShadowCoord(vertexInput.positionWS);
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 SliceLitPassFragment(Varyings input) : SV_Target
            {
                // === Slice Clip ===
                float3 adjustedCentre = sliceCentre + sliceNormal * sliceOffsetDst;
                float3 offsetToSliceCentre = adjustedCentre - input.positionWS;
                clip(dot(offsetToSliceCentre, sliceNormal));

                // === PBR ===
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                Light mainLight = GetMainLight(input.shadowCoord);
                half3 N = normalize(input.normalWS);
                half3 V = normalize(input.viewDirWS);
                half3 L = normalize(mainLight.direction);
                half3 H = normalize(L + V);

                half NdotL = saturate(dot(N, L));
                half NdotH = saturate(dot(N, H));

                half3 diffuse = albedo.rgb * mainLight.color * NdotL * mainLight.shadowAttenuation;

                half roughness = 1.0 - _Smoothness;
                roughness = max(roughness, 0.002);
                half roughness2 = roughness * roughness;
                half d = roughness2 / (3.14159 * pow(NdotH * NdotH * (roughness2 - 1.0) + 1.0, 2.0));
                half3 F0 = lerp(half3(0.04, 0.04, 0.04), albedo.rgb, _Metallic);
                half3 specular = F0 * d * mainLight.color * NdotL * mainLight.shadowAttenuation;

                half3 ambient = SampleSH(N) * albedo.rgb * (1.0 - _Metallic);

                half3 color = diffuse + specular + ambient;
                color = MixFog(color, input.fogFactor);

                return half4(color, albedo.a);
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
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;
            float3 sliceNormal;
            float sliceOffsetDst;
            float3 sliceCentre;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 posCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

                #if UNITY_REVERSED_Z
                    posCS.z = min(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    posCS.z = max(posCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return posCS;
            }

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                // Slice clip in shadow pass
                float3 adjustedCentre = sliceCentre + sliceNormal * sliceOffsetDst;
                float3 offsetToSliceCentre = adjustedCentre - input.positionWS;
                clip(dot(offsetToSliceCentre, sliceNormal));
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
