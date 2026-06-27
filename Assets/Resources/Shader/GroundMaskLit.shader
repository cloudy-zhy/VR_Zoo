Shader "Universal Render Pipeline/Custom/GroundMaskLit"
{
    Properties
    {
        [Header(Mask Color)]
        [MainTexture] _MaskTex ("Mask Texture", 2D) = "white" {}
        _MaskColorBlack ("Black Area Color", Color) = (1.0, 0.82, 0.18, 1.0)
        _MaskColorWhite ("White Area Color", Color) = (0.28, 0.72, 0.22, 1.0)
        _BaseColor ("Base Tint", Color) = (1.0, 1.0, 1.0, 1.0)

        [Header(Detail)]
        [NoScaleOffset] _DetailTex ("Detail Texture", 2D) = "white" {}
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.0
        [NoScaleOffset] [Normal] _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Scale", Range(0, 2)) = 1.0

        [Header(World UV)]
        [Toggle(_USE_WORLD_UV)] _UseWorldUV ("Use World UV", Float) = 0
        _WorldScale ("World UV Scale", Float) = 10.0
        _WorldRotation ("World UV Rotation", Range(0, 360)) = 0.0

        [Header(Lighting)]
        _DirectLightIntensity ("Direct Light Intensity", Range(0, 2)) = 1.0
        _EnvironmentColor ("Environment Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _EnvironmentIntensity ("Environment Intensity", Range(0, 3)) = 1.0

        [Header(Toon Shadow)]
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 1.0
        _ShadowColor ("Shadow Color", Color) = (0.45, 0.45, 0.45, 1.0)
        _ShadowStep ("Shadow Step", Range(0, 1)) = 0.5
        _ShadowFeather ("Shadow Feather", Range(0, 0.2)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MaskTex_ST;
            float4 _MaskColorBlack;
            float4 _MaskColorWhite;
            float4 _BaseColor;
            float _DetailStrength;
            float _NormalScale;
            float _UseWorldUV;
            float _WorldScale;
            float _WorldRotation;
            float _DirectLightIntensity;
            float4 _EnvironmentColor;
            float _EnvironmentIntensity;
            float _ReceiveShadows;
            float _ShadowStrength;
            float4 _ShadowColor;
            float _ShadowStep;
            float _ShadowFeather;
        CBUFFER_END

        TEXTURE2D(_MaskTex);
        SAMPLER(sampler_MaskTex);
        TEXTURE2D(_DetailTex);
        SAMPLER(sampler_DetailTex);
        TEXTURE2D(_NormalMap);
        SAMPLER(sampler_NormalMap);

        float2 RotateUV(float2 uv, float degrees)
        {
            float radians = degrees * PI / 180.0;
            float s;
            float c;
            sincos(radians, s, c);
            return mul(float2x2(c, -s, s, c), uv);
        }

        float2 GetGroundUV(float2 meshUV, float3 positionWS)
        {
        #if defined(_USE_WORLD_UV)
            float2 uv = positionWS.xz / max(_WorldScale, 0.001);
            return RotateUV(uv, _WorldRotation);
        #else
            return TRANSFORM_TEX(meshUV, _MaskTex);
        #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma prefer_hlslcc gles
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma shader_feature_local _USE_WORLD_UV
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float fogCoord : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInput.positionCS;
                output.positionWS = positionInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = normalInput.bitangentWS;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(positionInput.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float2 groundUV = GetGroundUV(input.uv, input.positionWS);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, groundUV), _NormalScale);
                float3 normalWS = TransformTangentToWorld(normalTS, half3x3(input.tangentWS, input.bitangentWS, input.normalWS));
                normalWS = normalize(normalWS);

                half mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, groundUV).r;
                half3 maskColor = lerp(_MaskColorBlack.rgb, _MaskColorWhite.rgb, mask);

                half3 detail = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, groundUV).rgb;
                detail = lerp(half3(1.0, 1.0, 1.0), detail, _DetailStrength);

                half3 baseColor = maskColor * _BaseColor.rgb * detail;

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));

                half3 bakedAmbient = SampleSH(normalWS) * _EnvironmentColor.rgb * _EnvironmentIntensity;
                half3 ambientColor = baseColor * bakedAmbient;

                half receiveShadow = saturate(_ReceiveShadows);
                half feather = max(_ShadowFeather, 0.0001h);
                half toonRamp = smoothstep(_ShadowStep - feather, _ShadowStep + feather, ndotl);
                half castShadowMask = smoothstep(0.001h, feather, 1.0h - mainLight.shadowAttenuation) * receiveShadow;

                half3 litDirect = baseColor * mainLight.color * _DirectLightIntensity;
                half3 shadowDirect = litDirect * _ShadowColor.rgb;
                half3 toonDirect = lerp(shadowDirect, litDirect, toonRamp);
                toonDirect = lerp(litDirect, toonDirect, _ShadowStrength);
                toonDirect = lerp(toonDirect, shadowDirect, castShadowMask * _ShadowStrength);

                half3 additionalDirect = 0.0h;
            #if defined(_ADDITIONAL_LIGHTS)
                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                    half additionalNdotL = saturate(dot(normalWS, additionalLight.direction));
                    half additionalRamp = smoothstep(_ShadowStep - feather, _ShadowStep + feather, additionalNdotL);
                    half additionalShadow = lerp(1.0h, additionalLight.shadowAttenuation, receiveShadow);
                    half additionalAttenuation = additionalLight.distanceAttenuation * additionalShadow;

                    half3 additionalLit = baseColor * additionalLight.color * _DirectLightIntensity * additionalAttenuation;
                    half3 additionalShadowed = additionalLit * _ShadowColor.rgb;
                    half3 additionalToon = lerp(additionalShadowed, additionalLit, additionalRamp);
                    additionalDirect += lerp(additionalLit, additionalToon, _ShadowStrength);
                LIGHT_LOOP_END
            #endif

                half3 finalColor = ambientColor + toonDirect + additionalDirect;

                finalColor = MixFog(finalColor, input.fogCoord);
                return half4(finalColor, _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif

                output.positionCS = positionCS;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
