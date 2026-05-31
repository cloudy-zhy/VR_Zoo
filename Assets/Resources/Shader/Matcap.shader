Shader "Universal Render Pipeline/Custom/Matcap"
{
    Properties
    {
        [Header(Base Texture)]
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color Tint", Color) = (1,1,1,1)
        
        [Header(Toon Shading)]
        _ShadowColor ("Shadow Color", Color) = (0.5,0.5,0.5,1)
        _ShadowStep ("Shadow Step", Range(0, 1)) = 0.5
        _ShadowFeather ("Shadow Feather", Range(0, 0.1)) = 0.01
        
        [Header(Matcap)]
        _MatcapTex ("Matcap Texture", 2D) = "white" {}
        _MatcapColor ("Matcap Color", Color) = (1,1,1,1)
        _MatcapIntensity ("Matcap Intensity", Range(0, 2)) = 1.0
        
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 0.05)) = 0.025
        
        [Header(Receive Shadow)]
        [Toggle(_RECEIVE_SHADOW)] _ReceiveShadow ("Receive Shadow", Float) = 1
    }
    
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
            "Queue"="Geometry" 
            "RenderPipeline"="UniversalPipeline"
        }
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float4 _ShadowColor;
            float _ShadowStep;
            float _ShadowFeather;
            
            float4 _MatcapColor;
            float _MatcapIntensity;
            
            float4 _OutlineColor;
            float _OutlineWidth;
        CBUFFER_END
        
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        
        TEXTURE2D(_MatcapTex);
        SAMPLER(sampler_MatcapTex);
        ENDHLSL
        
        // 第一个Pass：轮廓线
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // 获取顶点位置和法线
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                // 计算轮廓线扩展方向
                float3 outlineOffset = normalWS * _OutlineWidth;
                
                // 将偏移应用到世界位置
                positionWS += outlineOffset;
                
                // 转换到裁剪空间
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
        
        // 第二个Pass：主渲染
        Pass
        {
            Name "ForwardLit"
            Tags { 
                "LightMode" = "UniversalForward"
            }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // 添加接收阴影的关键字
            #pragma shader_feature _RECEIVE_SHADOW
            
            // 让URP识别阴影宏
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
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
                float3 viewDirWS : TEXCOORD4;
            };
            
            // 计算Matcap UV
            float2 CalculateMatcapUV(float3 normalWS, float3 viewDirWS)
            {
                // 基于法线和观察方向计算Matcap UV
                float3 normalVS = normalize(mul((float3x3)GetWorldToViewMatrix(), normalWS));
                float3 viewDirVS = normalize(mul((float3x3)GetWorldToViewMatrix(), viewDirWS));
                
                // 反射向量
                float3 reflectVec = reflect(-viewDirVS, normalVS);
                
                // 转换为Matcap UV (范围从[-1,1]转换到[0,1])
                float2 matcapUV = reflectVec.xy * 0.5 + 0.5;
                
                return matcapUV;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                
                // 计算观察方向
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);
                
                // 计算阴影坐标
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 基础颜色
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                
                // 获取法线和观察方向
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);
                
                // 获取主光源
                Light mainLight = GetMainLight(input.shadowCoord);
                
                // 计算兰伯特光照
                float NdotL = dot(normalWS, mainLight.direction);
                
                // 硬阴影过渡
                float shadowMask = smoothstep(_ShadowStep - _ShadowFeather, 
                                             _ShadowStep + _ShadowFeather, 
                                             NdotL);
                
                // 接收阴影
                #if defined(_RECEIVE_SHADOW) && defined(_MAIN_LIGHT_SHADOWS)
                float shadow = mainLight.shadowAttenuation;
                shadowMask *= shadow;
                #endif
                
                // 三渲二颜色混合
                half3 litColor = baseColor.rgb * mainLight.color;
                half3 shadowColor = baseColor.rgb * mainLight.color * _ShadowColor.rgb;
                half3 diffuse = lerp(shadowColor, litColor, shadowMask);
                
                // 获取URP环境光
                half3 ambient = SampleSH(normalWS) * baseColor.rgb;
                
                // 计算Matcap效果
                float2 matcapUV = CalculateMatcapUV(normalWS, viewDirWS);
                half3 matcap = SAMPLE_TEXTURE2D(_MatcapTex, sampler_MatcapTex, matcapUV).rgb;
                
                // 应用Matcap颜色和强度
                half3 matcapEffect = matcap * _MatcapColor.rgb * _MatcapIntensity;
                
                // 结合所有光照
                // 基础光照 + 环境光 + Matcap效果
                half3 finalColor = diffuse + ambient + matcapEffect;
                return half4(finalColor, baseColor.a);
            }
            ENDHLSL
        }
        
        // ShadowCaster Pass (让物体能投射阴影)
        Pass
        {
            Name "ShadowCaster"
            Tags { 
                "LightMode" = "ShadowCaster"
            }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            float3 _LightDirection;
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                
                return positionCS;
            }
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }
            
            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
        
        // DepthOnly Pass (可选，用于深度写入)
        Pass
        {
            Name "DepthOnly"
            Tags { 
                "LightMode" = "DepthOnly"
            }
            
            ZWrite On
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
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
    
    FallBack "Universal Render Pipeline/Lit"
}