Shader "Custom/ToonGlassOutlineURP"
{
    Properties
    {
        [Header(Glass Color)]
        _BaseColor("Glass Color (Front)", Color) = (0.3, 0.8, 1, 0.2)
        _BackColor("Glass Color (Back)", Color) = (0.2, 0.6, 0.8, 0.15)
        _ShadowColor("Shadow Color", Color) = (0.1, 0.4, 0.6, 0.2) 

        [Header(Fresnel)]
        _FresnelColor("Fresnel Color", Color) = (1, 1, 1, 1)
        _FresnelPower("Fresnel Power", Range(1, 10)) = 5
        _FresnelStrength("Fresnel Strength", Range(0, 5)) = 1

        [Header(Specular)]
        _SpecColor("Specular Color", Color) = (1, 1, 1, 1)
        _SpecPower("Specular Power", Range(1, 256)) = 64
        _SpecStrength("Specular Strength", Range(0, 5)) = 1

        [Header(Top Highlight)]
        _TopHighlightStrength("Top Highlight Strength", Range(0, 3)) = 0.5
        _TopHighlightPower("Top Highlight Power", Range(1, 20)) = 8

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Range(0, 0.05)) = 0.008
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        //================================================
        // PASS 1: GLASS BACK FACES (先渲染玻璃背面，防止自穿透)
        //================================================
        Pass
        {
            Name "GlassBack"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front  // 只剔除正面 = 只渲染背面
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            float4 _BackColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target 
            { 
                return _BackColor; 
            }
            ENDHLSL
        }

        //================================================
        // PASS 2: OUTLINE PASS (渲染外扩描边)
        //================================================
        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineWidth;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // 沿着法线方向挤出
                float3 pos = IN.positionOS.xyz + normalize(IN.normalOS) * _OutlineWidth;
                OUT.positionCS = TransformObjectToHClip(pos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

       //================================================
        // PASS 3: GLASS FRONT FACES (最后渲染正面 + URP 完整光照)
        //================================================
        Pass
        {
            Name "GlassFront"
            // 【关键修复 1】必须告诉 URP 这个 Pass 需要接收场景光照！
            Tags { "LightMode" = "UniversalForward" } 
            
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back   // 正常渲染正面
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // 启用 URP 主光源阴影变体
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                float3 worldPos   : TEXCOORD2;
            };

            float4 _BaseColor;
            float4 _ShadowColor;
            float4 _FresnelColor;
            float _FresnelPower;
            float _FresnelStrength;

            float4 _SpecColor;
            float _SpecPower;
            float _SpecStrength;

            float _TopHighlightStrength;
            float _TopHighlightPower;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = posInputs.positionCS;
                OUT.worldPos   = posInputs.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = GetWorldSpaceViewDir(OUT.worldPos);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // 1. 获取 URP 主光源
                Light mainLight = GetMainLight();
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.worldPos);
                    mainLight = GetMainLight(shadowCoord);
                #endif

                float3 L = normalize(mainLight.direction);
                float3 H = normalize(L + V);

                // 2. 卡通明暗漫反射
                float NoL = dot(N, L);
                float toonBase = smoothstep(-0.2, 0.2, NoL); 
                
                float shadowAttenuation = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                float lightIntensity = toonBase * shadowAttenuation;

                // 结合光源固有颜色（加上一点环境底色防止死黑）
                float3 baseColor = lerp(_ShadowColor.rgb, _BaseColor.rgb, lightIntensity) * (mainLight.color + 0.2);

                // 3. 菲涅尔边缘光 (Fresnel)
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower) * _FresnelStrength;
                float3 fresnelResult = fresnel * _FresnelColor.rgb;

                // 4. 卡通硬边高光 (Toon Specular)
                float spec = pow(saturate(dot(N, H)), _SpecPower) * _SpecStrength;
                spec = smoothstep(0.5, 0.55, spec) * mainLight.shadowAttenuation; 
                float3 specularResult = spec * _SpecColor.rgb * mainLight.color;

                // 5. 顶部波光提亮 (Top Highlight)
                float topHighlight = pow(saturate(N.y), _TopHighlightPower) * _TopHighlightStrength;

                float3 finalColor = baseColor + fresnelResult + specularResult + topHighlight;

                // 【关键修复 2】动态 Alpha：有反光（高光、菲涅尔）的地方，玻璃应该显得更不透明，否则反光会被看穿！
                float outAlpha = _BaseColor.a + saturate(fresnel) + saturate(spec) + saturate(topHighlight);
                outAlpha = saturate(outAlpha); // 将透明度截断在最高 1.0

                return half4(finalColor, outAlpha);
            }
            ENDHLSL
        }
    }
}