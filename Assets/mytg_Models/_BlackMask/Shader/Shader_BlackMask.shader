Shader "Custom/Shader_BlackMask"
{
    Properties
    {
        [Header(Base Settings)]
        _MainTex ("Main Texture", 2D) = "white" {}
        [HDR]_MainColor ("Main Color", Color) = (1,1,1,1)
        _AlphaClip ("Alpha Clip", Range(0,1)) = 0
        [Header(Rendering Options)]
        // [Toggle(_SHADER_FEATURE_SOFT_PARTICLES)] _UseSoftParticles ("Use Soft Particles", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2 // 默认Back
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 0
        [Toggle] _UseAlphaAsColor ("UseAlphaAsColor", Int) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)]_ZTest ("ZTest", Float) = 4
        
         [Header(Blend Mode)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlendA ("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlendA ("Dst Blend", Float) = 10 // OneMinusSrcAlpha

    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        // Blend SrcAlpha OneMinusSrcAlpha
        Blend [_SrcBlend] [_DstBlend],[_SrcBlendA] [_DstBlendA]
        ZWrite [_ZWrite]  // 通过属性控制
        ZTest [_ZTest]
        Cull [_Cull]      // 通过属性控制

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // #pragma shader_feature_local _SHADER_FEATURE_SOFT_PARTICLES

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                #if defined(_SHADER_FEATURE_SOFT_PARTICLES)
                float4 screenPos   : TEXCOORD1;
                #endif
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _MainColor;
            half _SoftParticlesScale;
            half _FadeDistance;
            half _AlphaClip;
            int _UseAlphaAsColor;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;

                // #if defined(_SHADER_FEATURE_SOFT_PARTICLES)
                // o.screenPos = ComputeScreenPos(o.positionCS);
                // #endif

                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // 采样主纹理
                half4 texColor = tex2D(_MainTex, i.uv);
                if (_AlphaClip > texColor.r){
                        discard;
                    }
                // 颜色混合
                texColor.rgb = lerp(texColor.rgb,texColor.a,_UseAlphaAsColor);
                half4 finalColor = texColor * i.color * _MainColor;

                // #if defined(_SHADER_FEATURE_SOFT_PARTICLES)
                // 软粒子计算
                // float2 screenUV = i.screenPos.xy / i.screenPos.w;
                // float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                // float particleDepth = i.screenPos.w;
                // float depthDiff = sceneDepth - particleDepth;
                
                // 渐隐计算
                // half softFactor = saturate(depthDiff / _FadeDistance);
                // finalColor.a *= smoothstep(0, 1, softFactor * _SoftParticlesScale);
                // #endif


                return finalColor;
            }
            ENDHLSL
        }
    }

    CustomEditor "UnityEditor.ShaderGUI.ParticleShaderGUI"
}