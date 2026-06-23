Shader "Universal Render Pipeline/Custom/RevealWorldY"
{
    Properties
    {
        [Header(Base Color)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1.0, 1.0, 1.0, 1.0)

        [Header(Reveal Settings)]
        _RevealWorldY ("Reveal World Y", Float) = 0.0
        _EdgeWidth ("Edge Width", Range(0, 2)) = 0.1
        [HDR] _EdgeColor ("Edge Color", Color) = (0.0, 1.0, 0.5, 1.0)
        _EdgePower ("Edge Power", Range(1, 10)) = 2.0

        [Header(Lighting)]
        _DirectLightIntensity ("Direct Light Intensity", Range(0, 2)) = 1.0
        _EnvironmentIntensity ("Environment Intensity", Range(0, 3)) = 1.0

        [Header(Shadow)]
        [Toggle] _ReceiveShadows ("Receive Shadows", Float) = 1
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 1.0
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
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _RevealWorldY;
            float _EdgeWidth;
            float4 _EdgeColor;
            float _EdgePower;
            float _DirectLightIntensity;
            float _EnvironmentIntensity;
            float _ReceiveShadows;
            float _ShadowStrength;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogCoord : TEXCOORD3;
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
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInput.positionCS;
                output.positionWS = positionInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = input.uv;
                output.fogCoord = ComputeFogFactor(positionInput.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float worldY = input.positionWS.y;
                
                // 1. 世界坐标 Y 轴裁剪
                // 当 worldY > _RevealWorldY 时裁切
                clip(_RevealWorldY - worldY);

                float3 normalWS = normalize(input.normalWS);
                float2 uv = TRANSFORM_TEX(input.uv, _BaseMap);
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half3 baseColor = texColor.rgb * _BaseColor.rgb;

                // 计算光照
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndotl = saturate(dot(normalWS, mainLight.direction));

                // 阴影与环境光
                half receiveShadow = saturate(_ReceiveShadows);
                half shadowAtten = lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength * receiveShadow);
                half3 litDirect = baseColor * mainLight.color * ndotl * shadowAtten * _DirectLightIntensity;
                
                half3 bakedAmbient = SampleSH(normalWS) * _EnvironmentIntensity;
                half3 ambientColor = baseColor * bakedAmbient;

                half3 finalColor = ambientColor + litDirect;

                // 2. 边缘发光效果计算
                // 仅当 worldY 在 [_RevealWorldY - _EdgeWidth, _RevealWorldY] 区间时计算发光
                float distToReveal = _RevealWorldY - worldY;
                if (distToReveal < _EdgeWidth)
                {
                    float edgeFactor = 1.0 - (distToReveal / max(_EdgeWidth, 0.0001));
                    edgeFactor = pow(saturate(edgeFactor), _EdgePower);
                    half3 edgeGlow = edgeFactor * _EdgeColor.rgb * _EdgeColor.a;
                    finalColor += edgeGlow;
                }

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
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
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif

                output.positionCS = positionCS;
                output.positionWS = positionWS;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                // 影子也随着高度显现进行裁剪，保证影子不会提前穿帮
                clip(_RevealWorldY - input.positionWS.y);
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
                float3 positionWS : TEXCOORD0;
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
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                clip(_RevealWorldY - input.positionWS.y);
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
