using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 为 Built-in Forward 管线配置场景反射环境。
/// 菜单：Tools/Setup Forward Reflection
/// </summary>
public static class ReflectionSetup
{
    [MenuItem("Tools/Setup Forward Reflection")]
    public static void Setup()
    {
        ConfigureLightingSettings();
        ConfigureGraphicsSettings();
        ConfigureDirectionalLight();

        // 若场景没有 Reflection Probe，自动创建
        var probe = Object.FindObjectOfType<ReflectionProbe>();
        if (probe == null)
        {
            var go  = new GameObject("Reflection Probe");
            probe = go.AddComponent<ReflectionProbe>();
            go.transform.position = new Vector3(10, 4, 22);
        }

        ConfigureProbe(probe);

        EditorUtility.SetDirty(probe.gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

        Debug.Log("✅ Forward Reflection 配置完成！");
    }

    // ── Lighting Settings（环境反射）────────────────────────────────
    private static void ConfigureLightingSettings()
    {
        // 创建或加载 LightingSettings 资产
        const string path = "Assets/Scenes/MainLighting.lighting";
        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
        if (settings == null)
        {
            settings = new LightingSettings();
            AssetDatabase.CreateAsset(settings, path);
        }

        // 环境全局光照 & 反射
        settings.realtimeGI   = false;   // 关闭 Realtime GI，减少开销
        settings.bakedGI      = true;    // 允许 Baked GI（可选）
        AssetDatabase.SaveAssets();

        // 把 LightingSettings 绑定到当前场景
        Lightmapping.lightingSettings = settings;

        // 环境反射来源 = Skybox，分辨率 128
        RenderSettings.defaultReflectionMode       = DefaultReflectionMode.Skybox;
        RenderSettings.defaultReflectionResolution = 128;
        RenderSettings.reflectionIntensity         = 1.0f;
        RenderSettings.reflectionBounces           = 1;    // Forward 路径一次反弹足够
    }

    // ── 图形质量层配置────────────────────────────────────────────────
    private static void ConfigureGraphicsSettings()
    {
        // Built-in 管线下 Tier 1/2/3 都确保反射功能开启
        // （GraphicsSettings.currentRenderPipeline == null 即 Built-in）
        for (int tier = 0; tier < 3; tier++)
        {
            var t = (GraphicsTier)tier;
            var ts = EditorGraphicsSettings.GetTierSettings(BuildTargetGroup.Standalone, t);
            ts.reflectionProbeBlending  = true;   // 探头间平滑过渡
            ts.reflectionProbeBoxProjection = true; // 盒体投影，更精准
            EditorGraphicsSettings.SetTierSettings(BuildTargetGroup.Standalone, t, ts);
        }
    }

    // ── 方向光 Shadow / Reflection ───────────────────────────────────
    private static void ConfigureDirectionalLight()
    {
        var lights = Object.FindObjectsOfType<Light>();
        foreach (var l in lights)
        {
            if (l.type != LightType.Directional) continue;
            l.shadows        = LightShadows.Soft;
            l.shadowStrength = 0.8f;
            EditorUtility.SetDirty(l);
        }
    }

    // ── Reflection Probe 参数 ────────────────────────────────────────
    private static void ConfigureProbe(ReflectionProbe probe)
    {
        probe.mode          = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
        probe.refreshMode   = UnityEngine.Rendering.ReflectionProbeRefreshMode.EveryFrame;
        probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
        probe.resolution    = 128;
        probe.size          = new Vector3(30, 15, 55);   // 覆盖主场景区域
        probe.center        = Vector3.zero;
        probe.intensity     = 1.0f;
        probe.hdr           = true;
        probe.blendDistance = 1.0f;
        probe.shadowDistance = 100f;
        probe.clearFlags    = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;
        probe.cullingMask   = ~0;   // 捕捉所有层
    }
}
