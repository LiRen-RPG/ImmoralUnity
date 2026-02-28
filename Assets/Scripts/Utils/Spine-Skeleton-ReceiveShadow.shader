Shader "Immortal/Spine-Skeleton-ReceiveShadow"
{
    // 基于 Spine/Skeleton，增加阴影接收能力。
    // 使用此 shader 替换需要接收阴影的 Spine 角色材质。

    Properties
    {
        _Cutoff ("Shadow alpha cutoff", Range(0,1)) = 0.1
        [NoScaleOffset] _MainTex ("Main Texture", 2D) = "black" {}
        [Toggle(_STRAIGHT_ALPHA_INPUT)] _StraightAlphaInput("Straight Alpha Texture", Int) = 0

        [HideInInspector] _StencilRef("Stencil Reference", Float) = 1.0
        [HideInInspector][Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp("Stencil Comparison", Float) = 8

        [HideInInspector] _OutlineWidth("Outline Width", Range(0,8)) = 3.0
        [HideInInspector][MaterialToggle(_USE_SCREENSPACE_OUTLINE_WIDTH)] _UseScreenSpaceOutlineWidth("Width in Screen Space", Float) = 0
        [HideInInspector] _OutlineColor("Outline Color", Color) = (1,1,0,1)
        [HideInInspector][MaterialToggle(_OUTLINE_FILL_INSIDE)] _Fill("Fill", Float) = 0
        [HideInInspector] _OutlineReferenceTexWidth("Reference Texture Width", Int) = 1024
        [HideInInspector] _ThresholdEnd("Outline Threshold", Range(0,1)) = 0.25
        [HideInInspector] _OutlineSmoothness("Outline Smoothness", Range(0,1)) = 1.0
        [HideInInspector][MaterialToggle(_USE8NEIGHBOURHOOD_ON)] _Use8Neighbourhood("Sample 8 Neighbours", Float) = 1
        [HideInInspector] _OutlineOpaqueAlpha("Opaque Alpha", Range(0,1)) = 1.0
        [HideInInspector] _OutlineMipLevel("Outline Mip Level", Range(0,3)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "AlphaTest+10" // 确保在普通不透明物体之后、透明物体之前渲染
            "IgnoreProjector" = "False"
            "RenderType"      = "Opaque"
            "PreviewType"     = "Plane"
            
        }

        Fog     { Mode Off }
        Cull    Off
        ZWrite  Off
        Blend   One OneMinusSrcAlpha
        Lighting On

        Stencil
        {
            Ref  [_StencilRef]
            Comp [_StencilComp]
            Pass Keep
        }

        // ── 共享常量 ────────────────────────────────────────────────
        CGINCLUDE
        // 模型空间平面法线（朝斜前上方 45°，y/z 分量归一化）
        static const float3 kPlaneNormalOS = normalize(float3(0, 5, -1));
        ENDCG

        // ── Pass 1：正向基础渲染 + 阴影接收 ──────────────────────────
        Pass
        {
            Name "ForwardBase"
            Tags { "LightMode" = "ForwardBase" }

            
            CGPROGRAM

            #pragma shader_feature _ _STRAIGHT_ALPHA_INPUT
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            // 编译带/不带阴影的变体（nolightmap 等跳过不需要的 lightmap 变体）
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight

            #include "AutoLight.cginc"
            #include "../../Spine/Runtime/spine-unity/Shaders/CGIncludes/Spine-Common.cginc"
            
            sampler2D _MainTex;


            struct VertexInput
            {
                float4 vertex      : POSITION;
                float2 uv          : TEXCOORD0;
                float4 vertexColor : COLOR;
            };

            struct VertexOutput
            {
                float4 pos         : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 vertexColor : COLOR;
                float3 worldNormal : TEXCOORD2; // 世界空间法线，用于漫反射+环境光计算
                SHADOW_COORDS(3)               // 阴影数据写入 TEXCOORD3
            };

            VertexOutput vert(VertexInput v)
            {
                VertexOutput o;
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.uv          = v.uv;
                o.vertexColor = v.vertexColor;
                // 固定模型空间法线 (0,0,1)，变换到世界空间
                o.worldNormal = UnityObjectToWorldNormal(kPlaneNormalOS);
                // 计算阴影接收坐标（有阴影时写入数据，无阴影时编译为空操作）
                TRANSFER_SHADOW(o)
                return o;
            }

            float4 frag(VertexOutput i) : SV_Target
            {
                float4 texColor = tex2D(_MainTex, i.uv);

                #if defined(_STRAIGHT_ALPHA_INPUT)
				texColor.rgb *= texColor.a;
                #endif
                float4 col = texColor * i.vertexColor;

                // 平面法线（已插值，重新归一化）
                half3 worldNormal = normalize(i.worldNormal);

                // Lambert 漫反射：nl = dot(N, L)，clamp 到 [0,1]
                half  nl   = max(0, dot(worldNormal, _WorldSpaceLightPos0.xyz));
                fixed3 diff = nl * _LightColor0.rgb;

                // 球谐环境光（SH9）
                fixed3 ambient = ShadeSH9(half4(worldNormal, 1));

                // 阴影衰减：1.0 = 完全受光，0.0 = 完全在阴影中
                // 无阴影变体时宏自动返回 1.0
                fixed shadow = SHADOW_ATTENUATION(i);

                // 最终光照 = 漫反射 × 阴影 + 环境光
                fixed3 lighting = diff * shadow + ambient;
                col.rgb *= lighting;

                return col;
            }
            ENDCG
        }

        // ── Pass 2：追加光源（点光 / 聚光灯 / 额外方向光）────────────────
        // ForwardAdd 每个额外光源执行一次，叠加到 ForwardBase 结果上
        Pass
        {
            Name "ForwardAdd"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One   // 加法混合：叠加额外光源贡献
            ZWrite Off
            Fog { Mode Off }
            Cull Off

            CGPROGRAM
            #pragma shader_feature _ _STRAIGHT_ALPHA_INPUT
            #pragma vertex   vert
            #pragma fragment frag
            // 编译所有追加光源变体（方向光/点光/聚光灯 + 各自的阴影）
            #pragma multi_compile_fwdadd_fullshadows
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            #include "../../Spine/Runtime/spine-unity/Shaders/CGIncludes/Spine-Common.cginc"

            sampler2D _MainTex;

            struct VertexInput
            {
                float4 vertex      : POSITION;
                float2 uv          : TEXCOORD0;
                float4 vertexColor : COLOR;
            };

            struct VertexOutput
            {
                float4 pos         : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 vertexColor : COLOR;
                float3 worldPos    : TEXCOORD2; // 世界坐标，用于点光/聚光灯方向计算
                float3 worldNormal : TEXCOORD3;
                LIGHTING_COORDS(4, 5)           // 距离衰减 + 阴影（TEXCOORD4/5）
            };

            VertexOutput vert(VertexInput v)
            {
                VertexOutput o;
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.uv          = v.uv;
                o.vertexColor = v.vertexColor;
                o.worldPos    = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(kPlaneNormalOS);
                TRANSFER_VERTEX_TO_FRAGMENT(o)
                return o;
            }

            float4 frag(VertexOutput i) : SV_Target
            {
                float4 texColor = tex2D(_MainTex, i.uv);
                #if defined(_STRAIGHT_ALPHA_INPUT)
                texColor.rgb *= texColor.a;
                #endif
                float4 col = texColor * i.vertexColor;

                half3 worldNormal = normalize(i.worldNormal);

                // 方向光：_WorldSpaceLightPos0 是方向向量（w=0）
                // 点光/聚光灯：_WorldSpaceLightPos0 是位置（w=1），需要减去世界坐标
                #if defined(DIRECTIONAL) || defined(DIRECTIONAL_COOKIE)
                    half3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                #else
                    half3 lightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                #endif

                half nl = max(0, dot(worldNormal, lightDir));

                // 包含距离衰减 + 阴影遮蔽（AutoLight.cginc 宏）
                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos)

                fixed3 addLight = nl * _LightColor0.rgb * atten;
                col.rgb *= addLight;

                return col;
            }
            ENDCG
        }

        // ── Pass 3：阴影投射（原版保留）────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            Offset  1, 1
            ZWrite  On
            ZTest   LEqual
            Fog     { Mode Off }
            Cull    Off
            Lighting Off

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #pragma fragmentoption ARB_precision_hint_fastest
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed     _Cutoff;
            float     _ShadowZOffset1;

            struct VertexOutput
            {
                V2F_SHADOW_CASTER;
                float4 uvAndAlpha : TEXCOORD1;
            };

            VertexOutput vert(appdata_base v, float4 vertexColor : COLOR)
            {
                VertexOutput o;
                o.uvAndAlpha   = v.texcoord;
                o.uvAndAlpha.a = vertexColor.a;
                TRANSFER_SHADOW_CASTER(o)
                return o;
            }

            float4 frag(VertexOutput i) : SV_Target
            {
                fixed4 texcol = tex2D(_MainTex, i.uvAndAlpha.xy);
                clip(texcol.a * i.uvAndAlpha.a - _Cutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

        FallBack Off
}
