Shader "Immortal/LShapeTransparent"
{
    Properties
    {
        _MainTex  ("Texture",        2D)    = "white" {}
        _Color    ("Tint Color",     Color) = (1,1,1,1)
        _Cutoff   ("Alpha Cutoff",   Range(0,1)) = 0.1
        // 控制深度偏移，避免与地面 Z-fighting
        _ZBias    ("Depth Bias",     Float) = 0
    }

    SubShader
    {
        // 透明队列，但比普通 Transparent 稍早渲染（先占深度）
        Tags
        {
            "Queue"             = "AlphaTest"
            "RenderType"        = "Opaque"
            "IgnoreProjector"   = "True"
        }

        // ── Pass 1：深度预写入（带 Alpha Test）────────────────────────
        // 对纹理 Alpha 采样，低于 _Cutoff 的像素不写深度，与透明边缘对齐
        Pass
        {
            Name "DEPTH_PREPASS"
            ZWrite On
            ZTest  LEqual
            ColorMask 0             // 不写颜色
            Cull   Off              // 双面写深度
            Offset [_ZBias], [_ZBias]

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed     _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed alpha = tex2D(_MainTex, i.uv).a;
                clip(alpha - _Cutoff);  // alpha < _Cutoff 时丢弃该像素，不写深度
                return 0;
            }
            ENDCG
        }

        // ── Pass 2：透明颜色渲染 ───────────────────────────────────────
        Pass
        {
            Name "TRANSPARENT_COLOR"
            ZWrite Off
            ZTest  LEqual
            Blend  SrcAlpha OneMinusSrcAlpha
            Cull   Off                // 双面渲染

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _Color;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }
        // ── Pass 3：阴影投射（带 Alpha Test）─────────────────────────
        // 没有此 Pass，透明对象不会投影；clip() 使阴影轮廓与透明边缘一致
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest  LEqual
            Cull   Off

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed     _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed alpha = tex2D(_MainTex, i.uv).a;
                clip(alpha - _Cutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}
