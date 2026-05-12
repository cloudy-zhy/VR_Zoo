Shader "Custom/ice-surface"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (0.5, 0.8, 1.0, 0.6)
        [MainTexture] _BaseMap("Albedo (RGB) / Alpha (A)", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _HeightMap("Height Map (R)", 2D) = "gray" {}
        _SmoothnessMap("Smoothness Map (R)", 2D) = "white" {}
        _NoiseMap("Noise Map (冰裂纹感)", 2D) = "gray" {}
        
        _Glossiness("光滑度", Range(0, 1)) = 0.85
        _Metallic("金属度", Range(0, 1)) = 0.0
        _ParallaxScale("视差强度", Range(-0.1, 0.1)) = 0.025
        _FresnelPower("菲涅尔强度", Range(0, 5)) = 1.8
        _FresnelColor("菲涅尔颜色", Color) = (1, 1, 1, 1)
        _SpecularColor("高光颜色", Color) = (0.9, 0.95, 1, 1)
        _EmissionIntensity("自发光强度", Range(0, 0.5)) = 0.1
        _NoiseIntensity("冰裂纹强度", Range(0, 0.4)) = 0.15
        _AlphaFresnelPower("透明度菲涅尔", Range(0, 3)) = 1.2
    }
    
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            TEXTURE2D(_BaseMap);          SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);        SAMPLER(sampler_NormalMap);
            TEXTURE2D(_HeightMap);        SAMPLER(sampler_HeightMap);
            TEXTURE2D(_SmoothnessMap);    SAMPLER(sampler_SmoothnessMap);
            TEXTURE2D(_NoiseMap);         SAMPLER(sampler_NoiseMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NormalMap_ST;
                float4 _HeightMap_ST;
                float4 _SmoothnessMap_ST;
                float4 _NoiseMap_ST;
                half4 _BaseColor;
                half4 _FresnelColor;
                half4 _SpecularColor;
                half _Glossiness;
                half _Metallic;
                half _ParallaxScale;
                half _FresnelPower;
                half _EmissionIntensity;
                half _NoiseIntensity;
                half _AlphaFresnelPower;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 uv_Tex : TEXCOORD1; // xy = base, zw = height
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float4 tangentWS : TEXCOORD4;
                float3 viewDirWS : TEXCOORD5;
            };
            
            // 切线空间视线偏移（视差映射）
            float2 ParallaxOffset(half2 uv, half3 viewDirTS, half scale, half height)
            {
                float2 offset = viewDirTS.xy * (height * scale);
                return uv - offset;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = float4(normalInput.tangentWS, input.tangentOS.w);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                
                output.uv = input.uv;
                output.uv_Tex.xy = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                output.uv_Tex.zw = input.uv * _HeightMap_ST.xy + _HeightMap_ST.zw;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 构建 TBN 矩阵
                half3 normalWS = normalize(input.normalWS);
                half3 tangentWS = normalize(input.tangentWS.xyz);
                half3 bitangentWS = cross(normalWS, tangentWS) * input.tangentWS.w;
                float3x3 TBN = float3x3(tangentWS, bitangentWS, normalWS);
                
                // 切线空间视线方向
                half3 viewDirTS = mul(TBN, normalize(input.viewDirWS));
                viewDirTS = normalize(viewDirTS);
                
                // 采样高度图并偏移 UV
                half height = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, input.uv_Tex.zw).r;
                float2 offsetUV = ParallaxOffset(input.uv_Tex.xy, viewDirTS, _ParallaxScale, height);
                float2 finalUV = offsetUV;
                
                // 采样基础贴图
                half4 albedoAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, finalUV);
                half3 albedo = albedoAlpha.rgb * _BaseColor.rgb;
                half alpha = albedoAlpha.a * _BaseColor.a;
                
                // 法线贴图
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, finalUV));
                half3 normalWS_final = normalize(mul(normalTS, TBN));
                
                // 光滑度 & 金属度
                half smoothness = SAMPLE_TEXTURE2D(_SmoothnessMap, sampler_SmoothnessMap, finalUV).r * _Glossiness;
                half metallic = _Metallic;
                
                // 噪声纹理（冰裂纹）
                half3 noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, finalUV * 2.0).rgb;
                albedo += noise * _NoiseIntensity;
                
                // 主光源计算
                Light mainLight = GetMainLight();
                half3 lightDir = normalize(mainLight.direction);
                half3 lightColor = mainLight.color;
                half3 viewDirWS_n = normalize(input.viewDirWS);
                half3 halfDir = normalize(lightDir + viewDirWS_n);
                
                half NdotL = saturate(dot(normalWS_final, lightDir));
                half NdotH = saturate(dot(normalWS_final, halfDir));
                half NdotV = saturate(dot(normalWS_final, viewDirWS_n));
                
                half3 diffuse = albedo * lightColor * NdotL;
                half specularIntensity = pow(NdotH, smoothness * 128.0);
                half3 specular = _SpecularColor.rgb * lightColor * specularIntensity * (1.0 - metallic);
                
                // 环境光
                half3 ambient = SampleSH(normalWS_final) * albedo;
                
                half3 finalColor = diffuse + specular + ambient;
                
                // 菲涅尔边缘光 & 自发光
                half fresnel = pow(1.0 - NdotV, _FresnelPower);
                half3 fresnelGlow = _FresnelColor.rgb * fresnel * _FresnelColor.a;
                finalColor += fresnelGlow + _EmissionIntensity * albedo;
                
                // 透明度菲涅尔（边缘更透）
                half alphaFresnel = pow(1.0 - NdotV, _AlphaFresnelPower);
                alpha = alpha * (1.0 - alphaFresnel * 0.8);
                
                // 额外光源（点光源、聚光灯等）
                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS);
                    half3 addLightDir = normalize(additionalLight.direction);
                    half addNdotL = saturate(dot(normalWS_final, addLightDir));
                    half3 addDiffuse = albedo * additionalLight.color * addNdotL;
                    finalColor += addDiffuse * 0.5;
                }
                #endif
                
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
        
        // ShadowCaster 阴影投射 Pass（修正 LerpWhiteTo 未定义错误）
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            // 修复缺失的 LerpWhiteTo 函数
            #ifndef LerpWhiteTo
            #define LerpWhiteTo(alpha, color) lerp(color, 1.0, alpha)
            #endif
            
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
                return output;
            }
            half4 ShadowFrag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
        
        // DepthOnly 深度写入 Pass（同样添加宏定义以防万一）
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            Cull Back
            
            HLSLPROGRAM
            #ifndef LerpWhiteTo
            #define LerpWhiteTo(alpha, color) lerp(color, 1.0, alpha)
            #endif
            
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS.xyz));
                return output;
            }
            half4 DepthFrag(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}