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
       _ShadowColor("阴影颜色", Color) = (0.3, 0.3, 0.3, 1.0)   // 阴影颜色
       
       // 二次元卡通阴影控制参数
       _ToonThreshold("卡通阴影阈值 (明暗分界线)", Range(0, 1)) = 0.5
       _ToonSmoothness("卡通阴影过渡羽化值", Range(0.001, 0.5)) = 0.05
       
       [Header(Dissolve)]
       [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1, 1, 1, 1)
       _DissolveEdgeWidth ("Dissolve Edge Width", Range(0, 1)) = 0.1

       // Dissolve中心点和距离 (由 DissolutionCenter.cs 运行时设置)
       [HideInInspector] _Center ("Dissolve Center (World)", Vector) = (0, 0, 0, 0)
       [HideInInspector] _Distance ("Dissolve Distance", Float) = 1000.0
    }
    SubShader
    {
       Tags{"RenderPipeline" = "UniversalPipeline" "Queue"="Geometry" "RenderType"="Opaque"}
       LOD 100

       // --------------------------- MAIN PASS ---------------------------
       Pass
       {
          Name "ForwardLit"
          HLSLPROGRAM
          #pragma vertex vert
          #pragma fragment frag
          #pragma prefer_hlslcc gles
          #pragma multi_compile_fog
          #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
          #pragma multi_compile_instancing
          #pragma shader_feature USE_WC

          // ----- 新增：启用 URP 主光源阴影与级联阴影的变体编译 -----
          #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

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
             // ----- 新增：用于存储阴影采样的坐标 -----
             float4 shadowCoord : TEXCOORD4; 
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
          float4 _ShadowColor;
          
          float _ToonThreshold;
          float _ToonSmoothness;
          
          // 溶解常量映射
          float4 _DissolveEdgeColor;
          float _DissolveEdgeWidth;
          float4 _Center;
          float _Distance;
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

             // ----- 新增：计算阴影屏幕/空间坐标 -----
             #if SHADOWS_SCREEN
                 o.shadowCoord = ComputeScreenPos(vertexInput.positionCS);
             #else
                 o.shadowCoord = TransformWorldToShadowCoord(vertexInput.positionWS);
             #endif

             return o;
          }

          half4 frag(v2f i) : SV_Target
          {
             // 0. 溶解裁剪
             float dissolveDist = distance(i.worldPos, _Center.xyz);
             clip(_Distance - dissolveDist);

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

             // ========== 二次元/原神风分阶段卡通光照计算（已完美融合接收投影） ==========
             float3 worldNormal = normalize(i.worldNormal);
             
             // ----- 修改：传入正确的 shadowCoord 以获取场景物体的投影数据 -----
             Light mainLight = GetMainLight(i.shadowCoord);
             float3 lightDir = normalize(mainLight.direction);
             
             // 自身的受光面分析（半兰伯特映射映射到 0~1）
             float ndotl = dot(worldNormal, lightDir) * 0.5 + 0.5; 
             
             // mainLight.shadowAttenuation 现在包含了场景中其他物体投射过来的阴影数据
             float shadowAttenuation = mainLight.shadowAttenuation;
             
             // 将自发光遮罩与场景物体的投影相乘，混合出最终的“明暗依据”
             float shadowMask = ndotl * shadowAttenuation;

             // 使用 smoothstep 做硬边/微羽化的卡通分阶处理（原神风）
             float toonHalf = smoothstep(_ToonThreshold - _ToonSmoothness, _ToonThreshold + _ToonSmoothness, shadowMask);
             
             // 强度控制：_LightIntensity 为 0 时全亮，为 1 时展示硬核卡通阴影
             float finalToonFactor = lerp(1.0, toonHalf, _LightIntensity);
             
             // 颜色混合：让场景物体投射过来的阴影也渲染为我们指定的 _ShadowColor 调色
             half3 shadowCol = col * _ShadowColor.rgb;
             col = lerp(shadowCol, col, finalToonFactor);
             // =========================================================================

    #ifdef _DBUFFER
             ApplyDecalToBaseColor(i.pos, col);
    #endif

             col = MixFog(col, i.fogCoord);

             // 4. 溶解边缘发光融合
             float dissolveEdge = 1.0 - smoothstep(0.0, _DissolveEdgeWidth, _Distance - dissolveDist);
             col = lerp(col, _DissolveEdgeColor.rgb, dissolveEdge * _DissolveEdgeColor.a);

             return half4(col, 1);
          }
          ENDHLSL
       }

       // --------------------------- DepthNormalsOnly PASS ---------------------------
       Pass
       {
          Name "DepthNormalsOnly"
          Tags { "LightMode" = "DepthNormalsOnly" }
          ZWrite On
          
          HLSLPROGRAM
          #pragma vertex vert
          #pragma fragment frag
          #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
          #pragma multi_compile_instancing
          #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

          struct appdata 
          {
             float4 vertex : POSITION;
             float3 normal : NORMAL;
             UNITY_VERTEX_INPUT_INSTANCE_ID
          };
          
          struct v2f 
          {
             float4 pos : SV_POSITION;
             float3 normalWS : TEXCOORD0;
             float3 worldPos : TEXCOORD1;
             UNITY_VERTEX_INPUT_INSTANCE_ID
          };

          CBUFFER_START(UnityPerMaterial)
          float4 _Center;
          float _Distance;
          CBUFFER_END

          v2f vert(appdata v) 
          {
             v2f o;
             UNITY_SETUP_INSTANCE_ID(v);
             o.worldPos = TransformObjectToWorld(v.vertex.xyz);
             o.pos = TransformWorldToHClip(o.worldPos);
             o.normalWS = TransformObjectToWorldNormal(v.normal);
             return o;
          }

          half4 frag(v2f i) : SV_Target 
          {
             float dissolveDist = distance(i.worldPos, _Center.xyz);
             clip(_Distance - dissolveDist);

             #if defined(_GBUFFER_NORMALS_OCT)
             float2 packNormal = PackNormalOctQuadEncode(normalize(i.normalWS));
             return half4(packNormal, 0.0, 0.0);
             #else
             return half4(normalize(i.normalWS) * 0.5 + 0.5, 0.0);
             #endif
          }
          ENDHLSL
       }

       // --------------------------- ShadowCaster PASS ---------------------------
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
          
          struct appdata 
          { 
             float4 vertex : POSITION;
             float3 normal : NORMAL; 
             UNITY_VERTEX_INPUT_INSTANCE_ID 
          };
          
          struct v2f 
          { 
             float4 pos : SV_POSITION; 
             float3 worldPos : TEXCOORD0;
             UNITY_VERTEX_INPUT_INSTANCE_ID 
          };

          CBUFFER_START(UnityPerMaterial)
          float4 _Center;
          float _Distance;
          CBUFFER_END

          v2f vert(appdata v) 
          {
             v2f o; 
             UNITY_SETUP_INSTANCE_ID(v);
             float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
             o.worldPos = positionWS;
             
             float3 normalWS = TransformObjectToWorldNormal(v.normal);
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
             float dissolveDist = distance(i.worldPos, _Center.xyz);
             clip(_Distance - dissolveDist);
             return 0;
          }
          ENDHLSL
       }

       // --------------------------- DepthOnly PASS ---------------------------
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
             float3 worldPos : TEXCOORD0;
             UNITY_VERTEX_INPUT_INSTANCE_ID 
          };

          CBUFFER_START(UnityPerMaterial)
          float4 _Center;
          float _Distance;
          CBUFFER_END

          v2f vert(appdata v) 
          { 
             v2f o; 
             UNITY_SETUP_INSTANCE_ID(v);
             o.worldPos = TransformObjectToWorld(v.vertex.xyz);
             o.pos = TransformWorldToHClip(o.worldPos); 
             return o; 
          }

          half4 frag(v2f i) : SV_Target 
          { 
             float dissolveDist = distance(i.worldPos, _Center.xyz);
             clip(_Distance - dissolveDist);
             return 0;
          }
          ENDHLSL
       }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}