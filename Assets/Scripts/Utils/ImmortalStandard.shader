Shader "Immortal/StandardReflection"
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Immortal/StandardReflection  —  Built-in Forward 管线 PBR 着色器
    //  功能对标 Unity Standard（Metallic 工作流）：
    //    • Albedo + Tint + Alpha
    //    • Metallic / Smoothness（支持贴图）
    //    • Normal Map
    //    • Ambient Occlusion
    //    • Emission
    //    • 4 种渲染模式：Opaque / Cutout / Fade / Transparent
    //    • ForwardBase + ForwardAdd + ShadowCaster + Meta（GI烘焙）
    //    • 反射探头 + 全局光照（Lightmap / SH）
    // ─────────────────────────────────────────────────────────────────────────

    Properties
    {
        // ── 渲染模式（由编辑器脚本写入，也可手动改）─────────────────────
        [Enum(Opaque,0, Cutout,1, Fade,2, Transparent,3)]
        _Mode           ("Rendering Mode",      Float)      = 0

        // ── 主贴图 ───────────────────────────────────────────────────────
        _Color          ("Albedo Color",        Color)      = (1,1,1,1)
        _MainTex        ("Albedo (RGBA)",        2D)         = "white" {}
        _Cutoff         ("Alpha Cutoff",         Range(0,1)) = 0.333

        // ── Metallic 工作流 ──────────────────────────────────────────────
        [Gamma]
        _Metallic       ("Metallic",             Range(0,1)) = 0.0
        _Glossiness     ("Smoothness",           Range(0,1)) = 0.5
        // R = Metallic  A = Smoothness
        _MetallicGlossMap ("Metallic (R) Smooth (A)", 2D)   = "white" {}

        // ── 法线贴图 ──────────────────────────────────────────────────────
        [Normal]
        _BumpMap        ("Normal Map",           2D)         = "bump" {}
        _BumpScale      ("Normal Scale",         Float)      = 1.0

        // ── 遮蔽 ──────────────────────────────────────────────────────────
        _OcclusionMap   ("Occlusion (G)",        2D)         = "white" {}
        _OcclusionStrength ("Occlusion Strength",Range(0,1)) = 1.0

        // ── 自发光 ───────────────────────────────────────────────────────
        [HDR]
        _EmissionColor  ("Emission Color",       Color)      = (0,0,0,1)
        _EmissionMap    ("Emission",             2D)         = "black" {}

        // ── 屏幕空间反射贴图 ─────────────────────────────────────────────
        // 使用片元的裁剪空间坐标转换为屏幕 UV 进行采样
        _ReflectionTex      ("Screen Reflection Tex", 2D)     = "black" {}
        _ReflectionStrength ("Reflection Strength",   Range(0,1)) = 0.5
        // 混合模式：0=叠加(Additive)  1=线性插值(Lerp)  2=乘积(Multiply)
        [Enum(Additive,0, Lerp,1, Multiply,2)]
        _ReflectionBlendMode("Reflection Blend",      Float)  = 1

        // ── 内部（由 CustomEditor 写入，不要手动修改）────────────────────
        [HideInInspector] _SrcBlend  ("",        Float)      = 1.0
        [HideInInspector] _DstBlend  ("",        Float)      = 0.0
        [HideInInspector] _ZWrite    ("",        Float)      = 1.0
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  SubShader
    // ─────────────────────────────────────────────────────────────────────────
    SubShader
    {
        Tags
        {
            "RenderType"    = "Opaque"
            "Queue"         = "Geometry"
            "IgnoreProjector" = "True"
            "PerformanceChecks" = "False"
        }
        LOD 300

        // ═════════════════════════════════════════════════════════════════
        //  Pass 1 — ForwardBase
        //  主方向光 + 环境光（Lightmap / SH）+ 反射探头
        // ═════════════════════════════════════════════════════════════════
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            Blend  [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest  LEqual
            Cull   Back

            CGPROGRAM
            #pragma target 3.0

            #pragma vertex   vert
            #pragma fragment frag

            // 渲染模式关键字
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _ALPHABLEND_ON
            #pragma shader_feature _ALPHAPREMULTIPLY_ON

            // 功能关键字
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _METALLICGLOSSMAP
            #pragma shader_feature _EMISSION
            #pragma shader_feature _OCCLUSION

            // Unity 内置
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "UnityStandardBRDF.cginc"
            #include "UnityStandardUtils.cginc"
            #include "AutoLight.cginc"
            #include "UnityGlobalIllumination.cginc"

            // ── 属性绑定 ─────────────────────────────────────────────────
            sampler2D _MainTex;         float4 _MainTex_ST;
            sampler2D _MetallicGlossMap;
            sampler2D _BumpMap;
            sampler2D _OcclusionMap;
            sampler2D _EmissionMap;
            sampler2D _ReflectionTex;

            fixed4  _Color;
            half    _Metallic;
            half    _Glossiness;
            half    _BumpScale;
            half    _OcclusionStrength;
            half4   _EmissionColor;
            fixed   _Cutoff;
            half    _ReflectionStrength;
            half    _ReflectionBlendMode;

            // ── 顶点数据 ─────────────────────────────────────────────────
            struct appdata
            {
                float4 vertex   : POSITION;
                float3 normal   : NORMAL;
                float4 tangent  : TANGENT;
                float2 uv0      : TEXCOORD0;
                float2 uv1      : TEXCOORD1;   // Lightmap UV
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos          : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 worldPos_fog : TEXCOORD1;   // w = fog factor
                float3 worldNormal  : TEXCOORD2;

                #ifdef _NORMALMAP
                float4 worldTangent  : TEXCOORD3;  // w = sign
                float3 worldBitang   : TEXCOORD4;
                #endif

                float4 lightmapUV   : TEXCOORD5;   // xy=lightmap  zw=动态GI
                float4 screenPos    : TEXCOORD7;   // 屏幕空间坐标（透视除法前）

                SHADOW_COORDS(6)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── 顶点着色器 ───────────────────────────────────────────────
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos          = UnityObjectToClipPos(v.vertex);
                o.uv           = TRANSFORM_TEX(v.uv0, _MainTex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos_fog  = float4(worldPos, 0);
                o.worldNormal   = UnityObjectToWorldNormal(v.normal);

                #ifdef _NORMALMAP
                o.worldTangent  = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
                o.worldBitang   = cross(o.worldNormal, o.worldTangent.xyz) * v.tangent.w;
                #endif

                // Lightmap / Dynamic GI UV
                o.lightmapUV = float4(0,0,0,0);
                #ifdef LIGHTMAP_ON
                o.lightmapUV.xy = v.uv1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif
                #ifdef DYNAMICLIGHTMAP_ON
                o.lightmapUV.zw = v.uv1.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
                #endif

                TRANSFER_SHADOW(o);

                // 屏幕空间坐标（供 ReflectionTex 采样用）
                o.screenPos = o.pos.xyzw;

                // Fog
                UNITY_CALC_FOG_FACTOR(o.pos.z);
                o.worldPos_fog.w = unityFogFactor;

                return o;
            }

            // ── 片元着色器 ───────────────────────────────────────────────
            fixed4 frag(v2f i) : SV_Target
            {
                // ── 采样 Albedo ──────────────────────────────────────────
                fixed4 albedoAlpha = tex2D(_MainTex, i.uv) * _Color;

                // Alpha 模式处理
                #ifdef _ALPHATEST_ON
                    clip(albedoAlpha.a - _Cutoff);
                #endif
                #ifdef _ALPHAPREMULTIPLY_ON
                    albedoAlpha.rgb *= albedoAlpha.a;
                #endif

                half3 albedo = albedoAlpha.rgb;
                half  alpha  = albedoAlpha.a;

                // ── Metallic / Smoothness ─────────────────────────────────
                #ifdef _METALLICGLOSSMAP
                    half4 mg     = tex2D(_MetallicGlossMap, i.uv);
                    half metallic    = mg.r;
                    half smoothness  = mg.a;
                #else
                    half metallic    = _Metallic;
                    half smoothness  = _Glossiness;
                #endif

                // DiffuseColor + SpecularColor（能量守恒分解）
                half3 diffColor, specColor;
                half  oneMinusRefl;
                diffColor = DiffuseAndSpecularFromMetallic(
                    albedo, metallic, /*out*/ specColor, /*out*/ oneMinusRefl);

                half perceptualRoughness = 1.0 - smoothness;
                half roughness           = perceptualRoughness * perceptualRoughness;
                half roughness2          = roughness * roughness;

                // ── 法线 ──────────────────────────────────────────────────
                #ifdef _NORMALMAP
                    half3 normalTS = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _BumpScale);
                    float3 T = normalize(i.worldTangent.xyz);
                    float3 B = normalize(i.worldBitang);
                    float3 N = normalize(T * normalTS.x + B * normalTS.y +
                                         normalize(i.worldNormal) * normalTS.z);
                #else
                    float3 N = normalize(i.worldNormal);
                #endif

                float3 worldPos = i.worldPos_fog.xyz;
                float3 V        = normalize(UnityWorldSpaceViewDir(worldPos));
                float  NdotV    = saturate(dot(N, V));

                // ── 阴影 & 衰减 ───────────────────────────────────────────
                UNITY_LIGHT_ATTENUATION(atten, i, worldPos);

                // ── 直接光照（方向光）─────────────────────────────────────
                float3 L = normalize(UnityWorldSpaceLightDir(worldPos));
                float3 H = normalize(L + V);
                float  NdotL = saturate(dot(N, L));
                float  NdotH = saturate(dot(N, H));
                float  LdotH = saturate(dot(L, H));

                // ---- Diffuse（Burley/Disney）
                float fd90 = 0.5 + 2.0 * LdotH * LdotH * roughness;
                float FdL  = 1.0 + (fd90 - 1.0) * pow(1.0 - NdotL, 5.0);
                float FdV  = 1.0 + (fd90 - 1.0) * pow(1.0 - NdotV, 5.0);
                half3 directDiff = diffColor * (UNITY_INV_PI * FdL * FdV) * NdotL;

                // ---- Specular（GGX Cook-Torrance）
                float D = roughness2 /
                          (UNITY_PI * pow((NdotH * NdotH) * (roughness2 - 1.0) + 1.0, 2.0) + 1e-7);

                float k   = roughness * 0.5;
                float GNL = NdotL / (NdotL * (1.0 - k) + k + 1e-7);
                float GNV = NdotV / (NdotV * (1.0 - k) + k + 1e-7);
                float G   = GNL * GNV;

                half3 F0  = specColor;
                half3 F   = F0 + (1.0 - F0) * pow(1.0 - LdotH, 5.0);

                half3 directSpec = (D * G * F) / (4.0 * NdotL * NdotV + 1e-7) * NdotL;

                half3 directLight = (directDiff + directSpec) * _LightColor0.rgb * atten;

                // ── 间接光照 ──────────────────────────────────────────────
                UnityGIInput giInput;
                UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
                giInput.light.color     = _LightColor0.rgb;
                giInput.light.dir       = L;
                giInput.worldPos        = worldPos;
                giInput.worldViewDir    = V;
                giInput.atten           = atten;
                giInput.lightmapUV      = i.lightmapUV;
                giInput.ambient         = float3(0,0,0);
                giInput.probeHDR[0]     = unity_SpecCube0_HDR;
                giInput.probeHDR[1]     = unity_SpecCube1_HDR;
                #if UNITY_SPECCUBE_BLENDING || UNITY_SPECCUBE_BOX_PROJECTION
                giInput.boxMin[0]       = unity_SpecCube0_BoxMin;
                giInput.boxMax[0]       = unity_SpecCube0_BoxMax;
                giInput.probePosition[0]= unity_SpecCube0_ProbePosition;
                giInput.boxMin[1]       = unity_SpecCube1_BoxMin;
                giInput.boxMax[1]       = unity_SpecCube1_BoxMax;
                giInput.probePosition[1]= unity_SpecCube1_ProbePosition;
                #endif

                Unity_GlossyEnvironmentData glossEnv;
                glossEnv.roughness    = perceptualRoughness;
                glossEnv.reflUVW      = reflect(-V, N);

                UnityGI gi = UnityGlobalIllumination(giInput,
                    /*occlusion*/ 1.0, N, glossEnv);

                // 间接漫反射（AO）
                #ifdef _OCCLUSION
                    half ao = LerpOneTo(tex2D(_OcclusionMap, i.uv).g, _OcclusionStrength);
                #else
                    half ao = 1.0;
                #endif
                half3 indirectDiff = gi.indirect.diffuse * diffColor * ao;

                // 间接高光（菲涅尔混合）
                half  surfReduction = 1.0 / (roughness2 + 1.0);
                half3 grazingTerm   = saturate(smoothness + (1.0 - oneMinusRefl));
                half3 indirectSpec  =
                    surfReduction * gi.indirect.specular *
                    FresnelLerp(specColor, grazingTerm, NdotV) * ao;

                // ── 自发光 ───────────────────────────────────────────────
                #ifdef _EMISSION
                    half3 emission = tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                #else
                    half3 emission = half3(0,0,0);
                #endif

                // ── 合并 ──────────────────────────────────────────────────
                half3 col = directLight + indirectDiff + indirectSpec + emission;

                // ── 屏幕空间反射贴图 ──────────────────────────────────────
                // 将裁剪空间坐标做透视除法得到 [0,1] 屏幕 UV
                {
                    float2 screenUV = i.screenPos.xy/i.screenPos.w * 0.5 + 0.5;
                    half4  reflSample = tex2D(_ReflectionTex, screenUV);
                    half   reflAlpha  = reflSample.a * _ReflectionStrength;

                    // 菲涅尔遮蔽：掠射角反射更强
                    half fresnelMask  = pow(1.0 - NdotV, 1);
                    reflAlpha *= fresnelMask * 2.0;
                    reflAlpha  = saturate(reflAlpha);

                    // 0=Additive  1=Lerp  2=Multiply
                    if (_ReflectionBlendMode < 0.5)
                        col += reflSample.rgb * reflAlpha;               // 叠加
                    else if (_ReflectionBlendMode < 1.5)
                        col = lerp(col, reflSample.rgb, reflAlpha);      // 线性插值
                    else
                        col = lerp(col, col * reflSample.rgb, reflAlpha);// 乘积
                }

                fixed4 finalColor = fixed4(col, alpha);

                // Fog
                UNITY_APPLY_FOG(i.worldPos_fog.w, finalColor);

                return finalColor;
            }
            ENDCG
        }

        // ═════════════════════════════════════════════════════════════════
        //  Pass 2 — ForwardAdd
        //  附加点光 / 聚光灯（逐个叠加）
        // ═════════════════════════════════════════════════════════════════
        Pass
        {
            Name "FORWARD_DELTA"
            Tags { "LightMode" = "ForwardAdd" }

            Blend  [_SrcBlend] One        // 叠加
            ZWrite Off
            ZTest  LEqual
            Cull   Back

            CGPROGRAM
            #pragma target 3.0

            #pragma vertex   vert
            #pragma fragment frag

            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _ALPHABLEND_ON
            #pragma shader_feature _ALPHAPREMULTIPLY_ON
            #pragma shader_feature _NORMALMAP
            #pragma shader_feature _METALLICGLOSSMAP

            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "UnityStandardBRDF.cginc"
            #include "UnityStandardUtils.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;         float4 _MainTex_ST;
            sampler2D _MetallicGlossMap;
            sampler2D _BumpMap;

            fixed4  _Color;
            half    _Metallic;
            half    _Glossiness;
            half    _BumpScale;
            fixed   _Cutoff;

            struct appdata
            {
                float4 vertex   : POSITION;
                float3 normal   : NORMAL;
                float4 tangent  : TANGENT;
                float2 uv0      : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos          : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 worldPos     : TEXCOORD1;
                float3 worldNormal  : TEXCOORD2;
                #ifdef _NORMALMAP
                float4 worldTangent : TEXCOORD3;
                float3 worldBitang  : TEXCOORD4;
                #endif
                SHADOW_COORDS(5)
                UNITY_FOG_COORDS(6)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos         = UnityObjectToClipPos(v.vertex);
                o.uv          = TRANSFORM_TEX(v.uv0, _MainTex);
                o.worldPos    = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                #ifdef _NORMALMAP
                o.worldTangent = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
                o.worldBitang  = cross(o.worldNormal, o.worldTangent.xyz) * v.tangent.w;
                #endif
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedoAlpha = tex2D(_MainTex, i.uv) * _Color;
                #ifdef _ALPHATEST_ON
                    clip(albedoAlpha.a - _Cutoff);
                #endif

                half3 albedo = albedoAlpha.rgb;
                half  alpha  = albedoAlpha.a;

                #ifdef _METALLICGLOSSMAP
                    half4 mg    = tex2D(_MetallicGlossMap, i.uv);
                    half metallic   = mg.r;
                    half smoothness = mg.a;
                #else
                    half metallic   = _Metallic;
                    half smoothness = _Glossiness;
                #endif

                half3 diffColor, specColor;
                half  oneMinusRefl;
                diffColor = DiffuseAndSpecularFromMetallic(albedo, metallic, specColor, oneMinusRefl);

                half perceptualRoughness = 1.0 - smoothness;
                half roughness  = perceptualRoughness * perceptualRoughness;
                half roughness2 = roughness * roughness;

                #ifdef _NORMALMAP
                    half3 normalTS = UnpackScaleNormal(tex2D(_BumpMap, i.uv), _BumpScale);
                    float3 T = normalize(i.worldTangent.xyz);
                    float3 B = normalize(i.worldBitang);
                    float3 N = normalize(T * normalTS.x + B * normalTS.y +
                                         normalize(i.worldNormal) * normalTS.z);
                #else
                    float3 N = normalize(i.worldNormal);
                #endif

                float3 worldPos = i.worldPos;
                float3 V  = normalize(UnityWorldSpaceViewDir(worldPos));
                float3 L  = normalize(UnityWorldSpaceLightDir(worldPos));
                float3 H  = normalize(L + V);
                float  NdotL = saturate(dot(N, L));
                float  NdotV = saturate(dot(N, V));
                float  NdotH = saturate(dot(N, H));
                float  LdotH = saturate(dot(L, H));

                UNITY_LIGHT_ATTENUATION(atten, i, worldPos);

                // Diffuse
                float fd90 = 0.5 + 2.0 * LdotH * LdotH * roughness;
                half3 directDiff = diffColor * UNITY_INV_PI *
                    (1.0 + (fd90 - 1.0) * pow(1.0 - NdotL, 5.0)) *
                    (1.0 + (fd90 - 1.0) * pow(1.0 - NdotV, 5.0)) * NdotL;

                // Specular GGX
                float D = roughness2 / (UNITY_PI * pow((NdotH*NdotH)*(roughness2-1.0)+1.0, 2.0) + 1e-7);
                float k = roughness * 0.5;
                float G = (NdotL/(NdotL*(1-k)+k+1e-7)) * (NdotV/(NdotV*(1-k)+k+1e-7));
                half3 F = specColor + (1.0 - specColor) * pow(1.0 - LdotH, 5.0);
                half3 directSpec = (D * G * F) / (4.0 * NdotL * NdotV + 1e-7) * NdotL;

                half3 col = (directDiff + directSpec) * _LightColor0.rgb * atten;

                fixed4 finalColor = fixed4(col, alpha);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, finalColor, fixed4(0,0,0,0));
                return finalColor;
            }
            ENDCG
        }

        // ═════════════════════════════════════════════════════════════════
        //  Pass 3 — ShadowCaster
        // ═════════════════════════════════════════════════════════════════
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest  LEqual
            Cull   Back

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex   vertShadow
            #pragma fragment fragShadow
            #pragma shader_feature _ALPHATEST_ON
            #pragma shader_feature _ALPHABLEND_ON
            #pragma multi_compile_shadowcaster
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            sampler2D _MainTex; float4 _MainTex_ST;
            fixed4    _Color;
            fixed     _Cutoff;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                V2F_SHADOW_CASTER;
                float2 uv : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vertShadow(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 fragShadow(v2f i) : SV_Target
            {
                #if defined(_ALPHATEST_ON) || defined(_ALPHABLEND_ON)
                    fixed a = tex2D(_MainTex, i.uv).a * _Color.a;
                    clip(a - _Cutoff);
                #endif
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }

        // ═════════════════════════════════════════════════════════════════
        //  Pass 4 — Meta（用于 GI 烘焙：Lightmap / Light Probe）
        // ═════════════════════════════════════════════════════════════════
        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }
            Cull Off

            CGPROGRAM
            #pragma vertex   vertMeta
            #pragma fragment fragMeta
            #pragma shader_feature _EMISSION
            #pragma shader_feature _METALLICGLOSSMAP

            #include "UnityCG.cginc"
            #include "UnityStandardBRDF.cginc"
            #include "UnityStandardUtils.cginc"
            #include "UnityMetaPass.cginc"

            sampler2D _MainTex;     float4 _MainTex_ST;
            sampler2D _EmissionMap;
            sampler2D _MetallicGlossMap;

            fixed4  _Color;
            half    _Metallic;
            half    _Glossiness;
            half4   _EmissionColor;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv0    : TEXCOORD0;
                float2 uv1    : TEXCOORD1;
                float2 uv2    : TEXCOORD2;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vertMeta(appdata v)
            {
                v2f o;
                o.pos = UnityMetaVertexPosition(v.vertex, v.uv1.xy, v.uv2.xy,
                                                unity_LightmapST, unity_DynamicLightmapST);
                o.uv  = TRANSFORM_TEX(v.uv0, _MainTex);
                return o;
            }

            fixed4 fragMeta(v2f i) : SV_Target
            {
                UnityMetaInput meta;
                UNITY_INITIALIZE_OUTPUT(UnityMetaInput, meta);

                fixed4 albedo   = tex2D(_MainTex, i.uv) * _Color;
                meta.Albedo     = albedo.rgb;

                #ifdef _METALLICGLOSSMAP
                    half4 mg = tex2D(_MetallicGlossMap, i.uv);
                    half metallic = mg.r;
                #else
                    half metallic = _Metallic;
                #endif
                half3 specForMeta; half omr;
                DiffuseAndSpecularFromMetallic(albedo.rgb, metallic, specForMeta, omr);
                meta.SpecularColor = specForMeta;

                #ifdef _EMISSION
                    meta.Emission = tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                #else
                    meta.Emission = half3(0,0,0);
                #endif

                return UnityMetaFragment(meta);
            }
            ENDCG
        }
    }

    // ── 不支持的平台降级 → Unity 内置 Standard ────────────────────────────
    FallBack "Standard"

    // ── 自定义 Inspector（渲染模式切换）──────────────────────────────────
    CustomEditor "ImmortalStandardShaderGUI"
}
