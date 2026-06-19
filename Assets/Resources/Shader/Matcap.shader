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

        [Header(Dissolve)]
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1, 1, 1, 1)
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 1)) = 0.1

        // Dissolve中心点和距离 (由 DissolutionCenter.cs 运行时设置)
        [HideInInspector] _Center ("Dissolve Center (World)", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Distance ("Dissolve Distance", Float) = 1000.0
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

            float4 _DissolveEdgeColor;
            float _DissolveEdgeWidth;
            float4 _Center;
            float _Distance;
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
                float3 positionWS : TEXCOORD0;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // 获取顶点位置和法线
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // 保存原始世界位置用于溶解计算
                output.positionWS = positionWS;
                
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
                // 溶解：超出距离则裁剪
                float dissolveDist = distance(input.positionWS, _Center.xyz);
                clip(_Distance - dissolveDist);
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
                
                // 计算反射向量
                float3 reflectionVS = reflect(-viewDirVS, normalVS);
                
                // 将反射向量映射到[0,1]范围用于采样Matcap纹理
                float2 matcapUV = reflectionVS.xy * 0.5 + 0.5;
                return matcapUV;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // 计算世界空间位置
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                
                // UV
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                
                // 世界空间法线
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                // 阴影坐标
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                output.shadowCoord = shadowCoord;
                
                // 观察方向
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 溶解：超出距离则裁剪（最先处理，确保溶解区域完全剔除）
                float dissolveDist = distance(input.positionWS, _Center.xyz);
                clip(_Distance - dissolveDist);

                // 采样主纹理
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                
                // 获取主光源
                Light mainLight = GetMainLight(input.shadowCoord);
                
                // 兰伯特光照
                float NdotL = dot(input.normalWS, mainLight.direction);
                
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
                half3 ambient = SampleSH(input.normalWS) * baseColor.rgb;
                
                // 计算Matcap效果
                float2 matcapUV = CalculateMatcapUV(input.normalWS, input.viewDirWS);
                half3 matcap = SAMPLE_TEXTURE2D(_MatcapTex, sampler_MatcapTex, matcapUV).rgb;
                
                // 应用Matcap颜色和强度
                half3 matcapEffect = matcap * _MatcapColor.rgb * _MatcapIntensity;
                
                // 结合所有光照
                // 基础光照 + 环境光 + Matcap效果
                half3 finalColor = diffuse + ambient + matcapEffect;

                // 溶解边缘发光
                float dissolveEdge = 1.0 - smoothstep(0.0, _DissolveEdgeWidth, _Distance - dissolveDist);
                finalColor = lerp(finalColor, _DissolveEdgeColor.rgb, dissolveEdge * _DissolveEdgeColor.a);

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
            // _Center and _Distance are in HLSLINCLUDE CBUFFER
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
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
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }
            
            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                // 溶解：超出距离则裁剪阴影
                float dissolveDist = distance(input.positionWS, _Center.xyz);
                clip(_Distance - dissolveDist);
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
            
            // _Center and _Distance are in HLSLINCLUDE CBUFFER
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };
            
            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            
            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                // 溶解：超出距离则裁剪深度
                float dissolveDist = distance(input.positionWS, _Center.xyz);
                clip(_Distance - dissolveDist);
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Lit"
}

