Shader "Unlit/grass"
{
    Properties
    {
       [Header(Tint Colors)]
       [Space]
       _Color("基础颜色",Color) = (0.5 ,0.5 ,0.5,1.0)
       _GroundColor("地面颜色",Color) = (0.7 ,0.68 ,0.68,1.0)

       [Header(Mask Blend)]
       _MaskTex("黑白遮罩图 (黑=A 白=B)", 2D) = "white" {}
       _MaskColorBlack("遮罩黑色区域颜色", Color) = (0.2, 0.4, 0.1, 1)
       _MaskColorWhite("遮罩白色区域颜色", Color) = (0.6, 0.8, 0.3, 1)

       [Header(Textures)]
       [Space]
       [MainTexture]_MainTex("主贴图", 2D) = "white" {}
       [NoScaleOffset]_GroundTex("地面贴图", 2D) = "white" {}

       [Space]
       [Toggle(USE_WC)] _UseWC("使用世界坐标贴图", Float) = 0
       _WorldScale("世界坐标缩放", Float) = 10
       _WorldRotation("世界坐标旋转", Range(0, 360)) = 0
    }
    SubShader
    {
       Tags{"RenderPipeline" = "UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"}
       LOD 100

       // --------------------------- MAIN PASS ---------------------------
       Pass
       {
          HLSLPROGRAM
          #pragma vertex vert
          #pragma fragment frag
          #pragma prefer_hlslcc gles
          #pragma multi_compile_fog
          #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
          #pragma multi_compile_instancing
          #pragma shader_feature USE_WC

          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

          struct appdata
          {
             float4 vertex : POSITION;
             float2 uv : TEXCOORD0;
             UNITY_VERTEX_INPUT_INSTANCE_ID
          };

          struct v2f
          {
             float4 pos : SV_POSITION;
             float2 uv : TEXCOORD0;
             float3 worldPos : TEXCOORD1;
             float fogCoord : TEXCOORD2;
             UNITY_VERTEX_INPUT_INSTANCE_ID
          };

          CBUFFER_START(UnityPerMaterial)
          // 注意：在URP中，sampler2D 最好放在 CBUFFER 外面，这里为了修正报错保持原样，
          // 修正了 CBUFFER_START/END 的配对使用
          sampler2D _MainTex;
          float4 _MainTex_ST;
          sampler2D _GroundTex;
          sampler2D _MaskTex;
          float4 _Color;
          float4 _GroundColor;
          float4 _MaskColorBlack;
          float4 _MaskColorWhite;
          float _WorldScale;
          float _WorldRotation;
          CBUFFER_END

          v2f vert(appdata v)
          {
             v2f o;
             UNITY_SETUP_INSTANCE_ID(v);
             UNITY_TRANSFER_INSTANCE_ID(v, o);

             VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
             o.pos = vertexInput.positionCS;
             o.uv = TRANSFORM_TEX(v.uv, _MainTex);
             o.worldPos = vertexInput.positionWS;
             o.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
             return o;
          }

          half4 frag(v2f i) : SV_Target
          {
             float2 uv;

    #ifdef USE_WC
             uv = i.worldPos.xz / max(_WorldScale, 0.001);
             float rot = _WorldRotation * 3.14159 / 180;
             float s, c;
             sincos(rot, s, c);
             uv = mul(float2x2(c, -s, s, c), uv);
    #else
             uv = i.uv;
    #endif

             half mask = tex2D(_MaskTex, uv).r;
             half3 colMask = lerp(_MaskColorBlack.rgb, _MaskColorWhite.rgb, mask);

             half4 ground = tex2D(_GroundTex, uv);
             half3 col = ground.rgb * _GroundColor.rgb * _Color.rgb;

             col *= colMask;

    #ifdef _DBUFFER
             ApplyDecalToBaseColor(i.pos, col);
    #endif

             col = MixFog(col, i.fogCoord);
             return half4(col, 1);
          }
          ENDHLSL
       }

       // --------------------------- DEPTH NORMALS ---------------------------
       Pass
       {
          Name "DepthNormalsOnly"
          Tags { "LightMode" = "DepthNormalsOnly" }
          ZWrite On

          HLSLPROGRAM
          #pragma vertex DepthNormalsVertex
          #pragma fragment DepthNormalsFragment
          #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
          #pragma multi_compile_instancing
          #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
          #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitDepthNormalsPass.hlsl"
          ENDHLSL
       }

       // --------------------------- SHADOW CASTER ---------------------------
       Pass
       {
          Name "ShadowCaster"
          Tags { "LightMode" = "ShadowCaster" }
          ZWrite On ZTest LEqual ColorMask 0 Cull Off

          HLSLPROGRAM
          #pragma vertex vert
          #pragma fragment frag
          #pragma multi_compile_instancing
          
          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
          
          // --- 核心修复部分 START ---
          // 必须在 Shadows.hlsl 之前包含 Lighting.hlsl，因为 Shadows依赖 Lighting里面的数学函数
          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
          // --- 核心修复部分 END ---
          
          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

          struct appdata
          {
             float4 vertex : POSITION;
             float3 normal : NORMAL;
             UNITY_VERTEX_INPUT_INSTANCE_ID
          };

          struct v2f
          {
             float4 pos : SV_POSITION;
             UNITY_VERTEX_INPUT_INSTANCE_ID
          };

          // URP 阴影 Pass 不需要手动声明 _LightDirection，ApplyShadowBias 内部会处理

          v2f vert(appdata v)
          {
             v2f o;
             UNITY_SETUP_INSTANCE_ID(v);

             // 使用标准 URP 宏进行坐标转换，ApplyShadowBias 需要世界坐标和世界法线
             float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
             float3 normalWS = TransformObjectToWorldNormal(v.normal);

             // _LightDirection 替换为 _MainLightPosition.xyz (来自 Shadows.hlsl)
             float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
             
             #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
             #else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
             #endif

             o.pos = positionCS;
             return o;
          }

          half4 frag(v2f i) : SV_Target
          {
             return 0;
          }
          ENDHLSL
       }

       // --------------------------- DEPTH ONLY ---------------------------
       Pass
       {
          Name "DepthOnly"
          Tags { "LightMode" = "DepthOnly" }
          ZWrite On ColorMask 0

          HLSLPROGRAM
          #pragma vertex vert
          #pragma fragment frag
          #pragma multi_compile_instancing
          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

          struct appdata
          {
             float4 vertex : POSITION;
             UNITY_VERTEX_INPUT_INSTANCE_ID
          };

          struct v2f
          {
             float4 pos : SV_POSITION;
             UNITY_VERTEX_INPUT_INSTANCE_ID
          };

          v2f vert(appdata v)
          {
             v2f o;
             UNITY_SETUP_INSTANCE_ID(v);
             // 修正：使用标准 URP 转换宏，原 TransformObjectToHClip(v.vertex.xyz) 在某些非标准Mesh下可能有名词冲突
             o.pos = GetVertexPositionInputs(v.vertex.xyz).positionCS;
             return o;
          }

          half4 frag(v2f i) : SV_Target
          {
             return 0;
          }
          ENDHLSL
       }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
