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

       // ---------- 光照属性 ----------
       [Header(Lighting)]
       _LightIntensity("光照影响强度", Range(0, 2)) = 1.0
       _ShadowColor("阴影颜色", Color) = (0.3, 0.3, 0.3, 1.0)   // 新增阴影颜色
    }
    SubShader
    {
       Tags{"RenderPipeline" = "UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"}
       LOD 100

       // --------------------------- MAIN PASS (增加光照和阴影颜色) ---------------------------
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
          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

          struct appdata
          {
             float4 vertex : POSITION;
             float2 uv : TEXCOORD0;
             float3 normal : NORMAL;
             UNITY_VERTEX_INPUT_INSTANCE_ID
          };

          struct v2f
          {
             float4 pos : SV_POSITION;
             float2 uv : TEXCOORD0;
             float3 worldPos : TEXCOORD1;
             float3 worldNormal : TEXCOORD2;
             float fogCoord : TEXCOORD3;
             UNITY_VERTEX_INPUT_INSTANCE_ID
          };

          CBUFFER_START(UnityPerMaterial)
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
          float _LightIntensity;
          float4 _ShadowColor;          // 阴影颜色
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
             o.worldNormal = TransformObjectToWorldNormal(v.normal);
             o.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
             return o;
          }

          half4 frag(v2f i) : SV_Target
          {
             // 1. 获取UV
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

             // 2. 遮罩颜色混合
             half mask = tex2D(_MaskTex, uv).r;
             half3 colMask = lerp(_MaskColorBlack.rgb, _MaskColorWhite.rgb, mask);

             // 3. 基础贴图和颜色
             half4 ground = tex2D(_GroundTex, uv);
             half3 baseColor = ground.rgb * _GroundColor.rgb * _Color.rgb;
             half3 col = baseColor * colMask;

             // ========== 光照计算（支持阴影颜色） ==========
             Light mainLight = GetMainLight();
             float3 lightDir = normalize(mainLight.direction);
             float3 worldNormal = normalize(i.worldNormal);
             float ndotl = max(0, dot(worldNormal, lightDir));

             // 计算混合系数：强度为0时完全不受光照影响，强度为1时标准漫反射
             float blend = clamp(lerp(1.0, ndotl, _LightIntensity), 0, 1);
             // 最终颜色 = 插值(阴影颜色×原始颜色 , 原始颜色 , 混合系数)
             half3 shadowCol = col * _ShadowColor.rgb;
             col = lerp(shadowCol, col, blend);
             // =========================================

    #ifdef _DBUFFER
             ApplyDecalToBaseColor(i.pos, col);
    #endif

             col = MixFog(col, i.fogCoord);
             return half4(col, 1);
          }
          ENDHLSL
       }

       // 以下 Pass 与原代码完全相同（DepthNormalsOnly，ShadowCaster，DepthOnly），为节省篇幅略写，
       // 实际使用时请保留原文件中的这些 Pass。
       // 此处仅为示意，您可以直接复制之前版本中的对应 Pass 内容。
       // 为保持完整性，下面给出 ShadowCaster 的完整版本（其余 Pass 不变，请参照上文或原文件）。
       
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
          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
          struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
          struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
          v2f vert(appdata v) {
             v2f o; UNITY_SETUP_INSTANCE_ID(v);
             float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
             float3 normalWS = TransformObjectToWorldNormal(v.normal);
             float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
             #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
             #else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
             #endif
             o.pos = positionCS; return o;
          }
          half4 frag(v2f i) : SV_Target { return 0; }
          ENDHLSL
       }

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
          struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
          struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
          v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); o.pos = GetVertexPositionInputs(v.vertex.xyz).positionCS; return o; }
          half4 frag(v2f i) : SV_Target { return 0; }
          ENDHLSL
       }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}