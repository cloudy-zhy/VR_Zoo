Shader "Universal Render Pipeline/Custom/ToonNormal"
{
    Properties
    {
        [Header(Base Texture)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        
        [Header(Normal Map)]
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0
        
        [Header(Toon Shading)]
        _ShadowColor ("Shadow Color", Color) = (0.5,0.5,0.5,1)
        _ShadowStep ("Shadow Step", Range(0, 1)) = 0.5
        _ShadowFeather ("Shadow Feather", Range(0, 0.1)) = 0.01
        
        [Header(Ambient Lighting)]
        _EnvUpColor ("Environment Up Color", Color) = (0.7,0.7,1.0,1.0)
        _EnvSideColor ("Environment Side Color", Color) = (0.4,0.4,0.5,1.0)
        _EnvDownColor ("Environment Down Color", Color) = (0.1,0.1,0.2,1.0)
        _EnvIntensity ("Environment Intensity", Range(0, 2)) = 0.5
        _EnvFalloff ("Environment Falloff", Range(0.1, 5)) = 2.0
        
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 0.05)) = 0.025
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        
        // --- 轮廓线 Pass ---
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            
            float _OutlineWidth;
            float4 _OutlineColor;
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += normalWS * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }
            half4 frag(Varyings input) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
        
        // --- 主渲染 Pass ---
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 tangentWS : TEXCOORD3; float3 bitangentWS : TEXCOORD4; float3 positionWS : TEXCOORD5; };
            
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            float4 _BaseMap_ST; float4 _BaseColor; float _BumpScale;
            float4 _ShadowColor; float _ShadowStep; float _ShadowFeather;
            float4 _EnvUpColor; float4 _EnvSideColor; float4 _EnvDownColor; float _EnvIntensity; float _EnvFalloff;
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs posInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = posInput.positionCS;
                output.positionWS = posInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normInput.normalWS;
                output.tangentWS = normInput.tangentWS;
                output.bitangentWS = normInput.bitangentWS;
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3x3 tbn = float3x3(input.tangentWS, input.bitangentWS, input.normalWS);
                float3 normalWS = normalize(mul(normalTS, tbn));
                
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float NdotL = dot(normalWS, mainLight.direction);
                float shadowMask = smoothstep(_ShadowStep - _ShadowFeather, _ShadowStep + _ShadowFeather, NdotL);
                shadowMask *= mainLight.shadowAttenuation;
                
                half3 lit = albedo.rgb * mainLight.color;
                half3 shadow = albedo.rgb * mainLight.color * _ShadowColor.rgb;
                half3 diffuse = lerp(shadow, lit, shadowMask);
                
                // 模拟环境光
                float upWeight = pow(saturate(normalWS.y), _EnvFalloff);
                float downWeight = pow(saturate(-normalWS.y), _EnvFalloff);
                float sideWeight = pow(1.0 - abs(normalWS.y), _EnvFalloff);
                float total = upWeight + sideWeight + downWeight;
                half3 env = (_EnvUpColor.rgb * (upWeight/total) + _EnvSideColor.rgb * (sideWeight/total) + _EnvDownColor.rgb * (downWeight/total)) * _EnvIntensity;
                
                return half4(diffuse + (env * albedo.rgb) + (SampleSH(normalWS) * albedo.rgb), albedo.a);
            }
            ENDHLSL
        }
    }
}