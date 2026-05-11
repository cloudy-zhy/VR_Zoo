Shader "Custom/IceSurface_URP"
{
    Properties
    {
        [Header(Base)]
        _BaseColor ("Base Color", Color) = (0.8, 0.9, 1.0, 1.0)
        _MainTex ("Main Tex", 2D) = "white" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.85
        _FresnelTerm ("Fresnel", Range(0, 5)) = 1.5

        [Header(Multi-Layer Ice Crystals)]
        _MindTexure ("Ice Crystal Tex", 2D) = "white" {}
        _MindColor ("Ice Color", Color) = (0.6, 0.7, 1.0, 1.0)
        _MindDepth ("Parallax Depth", Range(0, 0.1)) = 0.02

        [Header(Deep Layer)]
        _LowIceColor ("Deep Ice Color", Color) = (0.4, 0.5, 0.7, 1.0)

        [Header(Refraction)]
        _RefractionStrength ("Refraction Strength", Range(0, 0.2)) = 0.08
        _WaterDepth ("Refraction Depth", Float) = 1.0
        _WaterFalloff ("Refraction Falloff", Float) = 2.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float4 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float4 shadowCoord : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float3 normalWS : TEXCOORD4;
                float3 viewDirWS : TEXCOORD5;
                float4 tangentWS : TEXCOORD6;
                float fogCoord : TEXCOORD7;
            };

            TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);        SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MindTexure);       SAMPLER(sampler_MindTexure);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _MainTex_ST;
                float4 _NormalMap_ST;
                float4 _MindTexure_ST;
                half _Smoothness;
                half _FresnelTerm;
                half4 _MindColor;
                half _MindDepth;
                half4 _LowIceColor;
                half _RefractionStrength;
                half _WaterDepth;
                half _WaterFalloff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = SafeNormalize(_WorldSpaceCameraPos - output.positionWS);
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                
                output.uv.xy = TRANSFORM_TEX(input.uv.xy, _MainTex);
                output.uv.zw = TRANSFORM_TEX(input.uv.xy, _MindTexure);
                
                output.shadowCoord = GetShadowCoord(vertexInput);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            half3 ComputeReflection(half3 viewDirWS, half3 normalWS, half3 positionWS, half3 directLighting)
            {
                half3 reflectVector = reflect(-viewDirWS, normalWS);
                half3 envReflection = GlossyEnvironmentReflection(reflectVector, positionWS, 1.0, 1.0);
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelTerm);
                return lerp(0, envReflection, fresnel) * directLighting;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 viewDirWS = normalize(input.viewDirWS);
                half3 normalWS = normalize(input.normalWS);
                
                half2 parallaxOffset = viewDirWS.xz * _MindDepth;
                half2 mindUV = input.uv.zw + parallaxOffset;
                half4 mindIceColor = SAMPLE_TEXTURE2D(_MindTexure, sampler_MindTexure, mindUV) * _MindColor;
                
                half4 normalMap = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv.xy);
                half3 tangentNormal = UnpackNormal(normalMap);
                half3x3 tangentToWorld = CreateTangentToWorld(normalWS, input.tangentWS.xyz, input.tangentWS.w);
                half3 normalWS_Detail = normalize(mul(tangentNormal, tangentToWorld));
                
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
                half3 iceColor = mainTex.rgb * _BaseColor.rgb;
                
                Light mainLight = GetMainLight(input.shadowCoord);
                half3 directLighting = mainLight.color * mainLight.distanceAttenuation;
                
                InputData inputData;
                inputData.positionWS = input.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.viewDirectionWS = viewDirWS;
                
                half3 brdfData, specular;
                InitializeBRDFData(half3(0,0,0), 0.0, half3(1,1,1), _Smoothness, 1.0, brdfData, specular);
                half3 spec = LightingPhysicallyBased(brdfData, specular, mainLight.direction, normalWS_Detail, viewDirWS, mainLight.color, 1.0);
                
                half3 GI = SampleSH(normalWS_Detail) * iceColor;
                half3 reflection = ComputeReflection(viewDirWS, normalWS_Detail, input.positionWS, directLighting);
                
                half3 finalAlbedo = directLighting * spec + directLighting * (iceColor + GI + mindIceColor.rgb + _LowIceColor.rgb);
                
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                #if UNITY_UV_STARTS_AT_TOP
                screenUV.y = 1.0 - screenUV.y;
                #endif
                
                half2 bumpOffset = tangentNormal.xy * _RefractionStrength;
                float2 refractiveUV = screenUV + bumpOffset;
                
                half rawDepth = SampleSceneDepth(screenUV);
                half sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                half surfaceDepth = input.screenPos.w;
                half depthDelta = saturate(pow(abs(sceneDepth - surfaceDepth) * _WaterDepth, _WaterFalloff));
                half3 opaqueColor = SampleSceneColor(refractiveUV);
                half3 refractionColor = opaqueColor * (1.0 - depthDelta);
                
                half3 finalColor = finalAlbedo + refractionColor;
                finalColor = MixFog(finalColor, input.fogCoord);
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}
            HLSLPROGRAM
            #pragma vertex vert_shadow
            #pragma fragment frag_shadow
            #pragma multi_compile_shadowcaster
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct AttributesShadow { float4 positionOS : POSITION; };
            struct VaryingsShadow { float4 positionCS : SV_POSITION; };
            
            VaryingsShadow vert_shadow(AttributesShadow input)
            {
                VaryingsShadow output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            
            half4 frag_shadow(VaryingsShadow input) : SV_TARGET { return 0; }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}