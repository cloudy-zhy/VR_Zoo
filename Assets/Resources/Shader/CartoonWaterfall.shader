Shader "Custom/CartoonWaterfall"
{
    Properties
    {
        _BaseColor ("水主色", Color) = (0.2,0.5,1,0.8)
        _FoamColor ("泡沫色", Color) = (0.9,1,1,1)
        _EdgeColor ("边缘高光", Color) = (0.6,0.9,1,1)

        _MainTex ("流动噪声", 2D) = "white" {}
        _FlowSpeed ("流动速度", Float) = 1.0
        _NoiseScale ("噪声缩放", Float) = 2.0

        _FoamThreshold ("泡沫阈值", Float) = 0.3
        _EdgePower ("边缘强度", Float) = 2.0
        _Opacity ("透明度", Float) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv            : TEXCOORD0;
                float3 normalWS       : TEXCOORD1;
                float3 viewWS        : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FoamColor;
                half4 _EdgeColor;
                sampler2D _MainTex;
                float4 _MainTex_ST;
                float _FlowSpeed;
                float _NoiseScale;
                float _FoamThreshold;
                float _EdgePower;
                float _Opacity;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewWS = GetWorldSpaceViewDir(TransformObjectToWorld(input.positionOS.xyz));
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // 1. 流动 UV（向下流）
                float2 flowUV = input.uv;
                flowUV.y += _Time.y * _FlowSpeed * 0.2;
                flowUV *= _NoiseScale;

                // 2. 噪声纹理
                half noise = tex2D(_MainTex, flowUV).r;

                // 3. 光照（卡通化）
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half toon = smoothstep(0.3, 0.6, NdotL);

                // 4. 泡沫（亮部+噪声）
                half foam = smoothstep(_FoamThreshold, 1.0, noise * toon);

                // 5. 边缘高光（菲涅尔）
                half3 normalWS = normalize(input.normalWS);
                half3 viewWS = normalize(input.viewWS);
                half fresnel = pow(1 - saturate(dot(normalWS, viewWS)), _EdgePower);

                // 6. 颜色混合
                half3 col = lerp(_BaseColor.rgb, _FoamColor.rgb, foam);
                col += _EdgeColor.rgb * fresnel * 0.5;

                // 7. 透明度
                half alpha = _Opacity * (0.7 + 0.3 * noise);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/Transparent"
}