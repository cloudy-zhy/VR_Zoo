Shader "Universal Render Pipeline/Custom/ContainerWaterToon_Final"
{
    Properties
    {
        [Header(Base Texture)]
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Inside Liquid Color", Color) = (1,1,1,1)
        
        [Header(Exterior Control)]
        _FrontColor ("Front Shell Color (RGBA)", Color) = (0.8, 0.9, 1.0, 0.3)
        
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
        
        [Header(Water Level Control)]
        _WaterMinY ("Water Empty Level (Local Y, bottom of container)", Float) = -1.0
        _WaterMaxY ("Water Full Level (Local Y, top of container)", Float) = 1.0
        _WaterLevel ("Water Level (Normalized 0-1)", Range(0, 1)) = 0.5
        [Toggle(_INVERT_CLIP)] _InvertClip ("Invert Clip Direction", Float) = 1 

        [Header(Water Surface Control)]
        _WaterSurfaceColor ("Water Surface Color", Color) = (0.3, 0.7, 0.9, 1.0)
        _WaveSpeed ("Wave Speed", Range(0, 10)) = 2.0
        _WaveFrequency ("Wave Frequency", Range(0, 30)) = 10.0
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.5)) = 0.02
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "RenderPipeline"="UniversalPipeline"
        }
        
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float4 _FrontColor;
            float4 _ShadowColor;
            float _ShadowStep;
            float _ShadowFeather;
            float4 _MatcapColor;
            float _MatcapIntensity;
            float4 _OutlineColor;
            float _OutlineWidth;
            float _WaterMinY;
            float _WaterMaxY;
            float _WaterLevel;
            float4 _WaterSurfaceColor;
            float _WaveSpeed;
            float _WaveFrequency;
            float _WaveAmplitude;
        CBUFFER_END

        // 用归一化的 _WaterLevel (0~1) 换算出实际的局部Y高度
        // 这样所有原有代码里用到 _WaterHeight 的地方都不需要改动
        #define _WaterHeight (lerp(_WaterMinY, _WaterMaxY, _WaterLevel))
        
        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_MatcapTex); SAMPLER(sampler_MatcapTex);
        ENDHLSL

        // ================================================
        // PASS 1: 内壁渲染
        // ================================================
        Pass
        {
            Name "InsideWall"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front  
            ZWrite Off  

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _INVERT_CLIP
            
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 texcoord : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; float3 viewDirWS : TEXCOORD3; float localY : TEXCOORD4; };
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);
                output.localY = input.positionOS.y; 
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                #if defined(_INVERT_CLIP)
                    clip(input.localY - _WaterHeight);
                #else
                    clip(_WaterHeight - input.localY);
                #endif
                
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                float3 normalWS = -normalize(input.normalWS); 
                
                Light mainLight = GetMainLight();
                float NdotL = dot(normalWS, mainLight.direction);
                float shadowMask = smoothstep(_ShadowStep - _ShadowFeather, _ShadowStep + _ShadowFeather, NdotL);
                
                half3 litColor = baseColor.rgb * mainLight.color;
                half3 shadowColor = baseColor.rgb * mainLight.color * _ShadowColor.rgb;
                half3 diffuse = lerp(shadowColor, litColor, shadowMask);
                
                return half4(diffuse + SampleSH(normalWS) * baseColor.rgb, _Color.a);
            }
            ENDHLSL
        }

        // ================================================
        // PASS 2: 水面封口 (彻底断绝世界空间干扰)
        // ================================================
        Pass
        {
            Name "WaterCap"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _INVERT_CLIP

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float isCap : TEXCOORD0; float waveIntensity : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o = (Varyings)0;
                float3 posOS = i.positionOS.xyz;
                
                // 1. 完全基于模型局部坐标(Object Space)判断哪个部分是水面顶盖
                #if defined(_INVERT_CLIP)
                    bool isCap = posOS.y > _WaterHeight;
                #else
                    bool isCap = posOS.y < _WaterHeight;
                #endif

                float wave = 0;
                if (isCap) 
                {
                    // 2. 强制在局部空间把顶点压平到指定的局部高度 _WaterHeight
                    posOS.y = _WaterHeight; 
                    
                    // 3. 波浪完全基于局部 X 和 Z 轴计算，确保波浪纹理死死贴在灯笼上
                    wave = sin(posOS.x * _WaveFrequency + _Time.y * _WaveSpeed) * cos(posOS.z * _WaveFrequency * 0.85 + _Time.y * _WaveSpeed * 1.15);
                    posOS.y += wave * _WaveAmplitude;
                }

                // 4. 所有局部空间的形变完成后，再统一转换到裁剪空间，彻底跟随物体旋转
                o.positionCS = TransformObjectToHClip(posOS);
                o.isCap = isCap ? 1.0 : -1.0; 
                o.waveIntensity = wave;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                clip(i.isCap);
                half3 capColor = _WaterSurfaceColor.rgb + (i.waveIntensity * _WaveAmplitude * 2.0);
                return half4(capColor, _WaterSurfaceColor.a);
            }
            ENDHLSL
        }

        // ================================================
        // PASS 3: 描边
        // ================================================
        Pass
        {
            Name "Outline"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _INVERT_CLIP
            
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float localY : TEXCOORD0; };
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.localY = input.positionOS.y; 
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                #if defined(_INVERT_CLIP)
                    clip(input.localY - _WaterHeight);
                #else
                    clip(_WaterHeight - input.localY);
                #endif
                return _OutlineColor;
            }
            ENDHLSL
        }

        // ================================================
        // PASS 4: 外壳渲染
        // ================================================
        Pass
        {
            Name "ForwardLitFront"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back  
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _RECEIVE_SHADOW
            #pragma shader_feature_local _INVERT_CLIP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 texcoord : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; float4 shadowCoord : TEXCOORD3; float3 viewDirWS : TEXCOORD4; float localY : TEXCOORD5; };
            
            float2 CalculateMatcapUV(float3 normalWS, float3 viewDirWS)
            {
                float3 normalVS = normalize(mul((float3x3)GetWorldToViewMatrix(), normalWS));
                float3 viewDirVS = normalize(mul((float3x3)GetWorldToViewMatrix(), viewDirWS));
                float3 reflectVec = reflect(-viewDirVS, normalVS);
                return reflectVec.xy * 0.5 + 0.5;
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
                output.viewDirWS = GetWorldSpaceViewDir(output.positionWS);
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                output.localY = input.positionOS.y; 
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                #if defined(_INVERT_CLIP)
                    clip(input.localY - _WaterHeight);
                #else
                    clip(_WaterHeight - input.localY);
                #endif
                
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _FrontColor;
                float3 normalWS = normalize(input.normalWS); 
                float3 viewDirWS = normalize(input.viewDirWS);
                
                Light mainLight = GetMainLight(input.shadowCoord);
                float NdotL = dot(normalWS, mainLight.direction);
                float shadowMask = smoothstep(_ShadowStep - _ShadowFeather, _ShadowStep + _ShadowFeather, NdotL);
                
                #if defined(_RECEIVE_SHADOW) && defined(_MAIN_LIGHT_SHADOWS)
                shadowMask *= mainLight.shadowAttenuation;
                #endif
                
                half3 litColor = baseColor.rgb * mainLight.color;
                half3 shadowColor = baseColor.rgb * mainLight.color * _ShadowColor.rgb;
                half3 diffuse = lerp(shadowColor, litColor, shadowMask);
                half3 ambient = SampleSH(normalWS) * baseColor.rgb;
                
                float2 matcapUV = CalculateMatcapUV(normalWS, viewDirWS);
                half3 matcap = SAMPLE_TEXTURE2D(_MatcapTex, sampler_MatcapTex, matcapUV).rgb;
                half3 matcapEffect = matcap * _MatcapColor.rgb * _MatcapIntensity;
                
                half3 finalColor = diffuse + ambient + matcapEffect;
                return half4(finalColor, _FrontColor.a);
            }
            ENDHLSL
        }
        
        Pass { Name "ShadowCaster" Tags{"LightMode"="ShadowCaster"} Cull Front ZWrite On ZTest LEqual ColorMask 0 HLSLPROGRAM /* ... */ ENDHLSL }
        Pass { Name "DepthOnly" Tags{"LightMode"="DepthOnly"} Cull Front ZWrite On ColorMask 0 HLSLPROGRAM /* ... */ ENDHLSL }
    }
}