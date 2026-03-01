using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Immortal/Standard shader 的自定义 Inspector。
/// 提供 Rendering Mode 下拉，自动同步 Blend/ZWrite/关键字/Queue/RenderType。
/// </summary>
public class ImmortalStandardShaderGUI : ShaderGUI
{
    private enum RenderingMode { Opaque, Cutout, Fade, Transparent }

    // ── 折叠状态 ─────────────────────────────────────────────────────────
    private bool _showMain      = true;
    private bool _showPBR       = true;
    private bool _showMaps      = true;
    private bool _showEmission  = true;
    private bool _showReflection= true;
    private bool _showAdvanced  = false;

    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        Material mat = (Material)editor.target;

        // ── Rendering Mode ───────────────────────────────────────────────
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("渲染模式", EditorStyles.boldLabel);

        var modeProp = FindProperty("_Mode", props);
        RenderingMode currentMode = (RenderingMode)(int)modeProp.floatValue;
        EditorGUI.BeginChangeCheck();
        RenderingMode newMode = (RenderingMode)EditorGUILayout.EnumPopup("Mode", currentMode);
        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo("Rendering Mode");
            modeProp.floatValue = (float)newMode;
            foreach (Object t in editor.targets)
                ApplyRenderingMode((Material)t, newMode);
        }

        EditorGUILayout.Space(8);

        // ── 主贴图 ───────────────────────────────────────────────────────
        _showMain = EditorGUILayout.BeginFoldoutHeaderGroup(_showMain, "主贴图");
        if (_showMain)
        {
            editor.ShaderProperty(FindProperty("_Color",   props), "颜色");
            editor.TexturePropertySingleLine(
                new GUIContent("Albedo", "RGB=颜色  A=透明度"),
                FindProperty("_MainTex", props));

            if (currentMode == RenderingMode.Cutout)
                editor.ShaderProperty(FindProperty("_Cutoff", props), "Alpha Cutoff");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── PBR ──────────────────────────────────────────────────────────
        _showPBR = EditorGUILayout.BeginFoldoutHeaderGroup(_showPBR, "PBR (Metallic 工作流)");
        if (_showPBR)
        {
            var mgMap = FindProperty("_MetallicGlossMap", props);
            editor.TexturePropertySingleLine(
                new GUIContent("Metallic(R) Smooth(A)", "R=金属度  A=光滑度"),
                mgMap,
                mgMap.textureValue == null ? FindProperty("_Metallic", props) : null);

            if (mgMap.textureValue == null)
                editor.ShaderProperty(FindProperty("_Glossiness", props), "Smoothness");

            SetKeyword(mat, "_METALLICGLOSSMAP", mgMap.textureValue != null);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 法线 / 遮蔽 ──────────────────────────────────────────────────
        _showMaps = EditorGUILayout.BeginFoldoutHeaderGroup(_showMaps, "法线 / 遮蔽");
        if (_showMaps)
        {
            var bumpMap = FindProperty("_BumpMap", props);
            editor.TexturePropertySingleLine(
                new GUIContent("Normal Map"),
                bumpMap,
                bumpMap.textureValue != null ? FindProperty("_BumpScale", props) : null);
            SetKeyword(mat, "_NORMALMAP", bumpMap.textureValue != null);

            var aoMap = FindProperty("_OcclusionMap", props);
            editor.TexturePropertySingleLine(
                new GUIContent("Occlusion (G)"),
                aoMap,
                aoMap.textureValue != null ? FindProperty("_OcclusionStrength", props) : null);
            SetKeyword(mat, "_OCCLUSION", aoMap.textureValue != null);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 自发光 ───────────────────────────────────────────────────────
        _showEmission = EditorGUILayout.BeginFoldoutHeaderGroup(_showEmission, "自发光");
        if (_showEmission)
        {
            bool hadEmission = mat.IsKeywordEnabled("_EMISSION");
            editor.TexturePropertyWithHDRColor(
                new GUIContent("Emission"),
                FindProperty("_EmissionMap", props),
                FindProperty("_EmissionColor", props),
                false);

            // 若 Emission Color != black 则自动开启关键字
            bool hasEmission = ((Color)FindProperty("_EmissionColor", props).colorValue).maxColorComponent > 0.01f
                               || FindProperty("_EmissionMap", props).textureValue != null;
            SetKeyword(mat, "_EMISSION", hasEmission);

            if (hasEmission)
                editor.LightmapEmissionProperty();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 高级 ────────────────────────────────────────────────
        _showReflection = EditorGUILayout.BeginFoldoutHeaderGroup(_showReflection, "屏幕空间反射");
        if (_showReflection)
        {
            var refTex = FindProperty("_ReflectionTex", props);
            bool wasEnabled = true;

            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle("开启屏幕空间反射", wasEnabled);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object t in editor.targets)
                    SetKeyword((Material)t, "_SCREENSPACE_REFLECTION", enabled);
            }

            EditorGUI.BeginDisabledGroup(!enabled);
            editor.TexturePropertySingleLine(
                new GUIContent("Reflection Tex", "使用屏幕空间 UV 采样的反射贴图"),
                refTex);
            editor.ShaderProperty(FindProperty("_ReflectionStrength",   props), "强度");
            editor.ShaderProperty(FindProperty("_ReflectionBlendMode",  props), "混合模式");
            EditorGUILayout.HelpBox(
                "UV 来自片元屏幕坐标 (pos.xy/pos.w)。\n" +
                "菲涅尔遇观角 NdotV 会加强边缘处的反射。",
                MessageType.None);
            EditorGUI.EndDisabledGroup();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();

        // ── 高级 ─────────────────────────────────────────────────────────
        _showAdvanced = EditorGUILayout.BeginFoldoutHeaderGroup(_showAdvanced, "高级");
        if (_showAdvanced)
        {
            editor.RenderQueueField();
            editor.EnableInstancingField();
            editor.DoubleSidedGIField();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    // ── 渲染模式应用 ─────────────────────────────────────────────────────
    private static void ApplyRenderingMode(Material mat, RenderingMode mode)
    {
        switch (mode)
        {
            case RenderingMode.Opaque:
                mat.SetOverrideTag("RenderType", "Opaque");
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                mat.SetInt("_ZWrite",   1);
                SetKeyword(mat, "_ALPHATEST_ON",      false);
                SetKeyword(mat, "_ALPHABLEND_ON",     false);
                SetKeyword(mat, "_ALPHAPREMULTIPLY_ON",false);
                mat.renderQueue = (int)RenderQueue.Geometry;
                break;

            case RenderingMode.Cutout:
                mat.SetOverrideTag("RenderType", "TransparentCutout");
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.Zero);
                mat.SetInt("_ZWrite",   1);
                SetKeyword(mat, "_ALPHATEST_ON",      true);
                SetKeyword(mat, "_ALPHABLEND_ON",     false);
                SetKeyword(mat, "_ALPHAPREMULTIPLY_ON",false);
                mat.renderQueue = (int)RenderQueue.AlphaTest;
                break;

            case RenderingMode.Fade:
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite",   0);
                SetKeyword(mat, "_ALPHATEST_ON",      false);
                SetKeyword(mat, "_ALPHABLEND_ON",     true);
                SetKeyword(mat, "_ALPHAPREMULTIPLY_ON",false);
                mat.renderQueue = (int)RenderQueue.Transparent;
                break;

            case RenderingMode.Transparent:
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)BlendMode.One);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite",   0);
                SetKeyword(mat, "_ALPHATEST_ON",      false);
                SetKeyword(mat, "_ALPHABLEND_ON",     false);
                SetKeyword(mat, "_ALPHAPREMULTIPLY_ON",true);
                mat.renderQueue = (int)RenderQueue.Transparent;
                break;
        }
        EditorUtility.SetDirty(mat);
    }

    private static void SetKeyword(Material mat, string keyword, bool state)
    {
        if (state) mat.EnableKeyword(keyword);
        else       mat.DisableKeyword(keyword);
    }
}
