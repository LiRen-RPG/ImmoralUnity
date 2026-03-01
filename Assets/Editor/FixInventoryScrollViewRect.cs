using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

/// <summary>
/// 修正 ScrollView 的 RectTransform，使其以 Stretch 模式填充背包面板内容区域。
/// 执行路径：Tools / Fix Inventory ScrollView Rect
/// </summary>
public class FixInventoryScrollViewRect : MonoBehaviour
{
    [MenuItem("Tools/Fix Inventory ScrollView Rect")]
public static void Fix()
    {
        // ── 找节点 ────────────────────────────────────────────────────────
        GameObject scrollViewGO = null;
        RectTransform titleBannerRT = null;

        foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rt.name == "ScrollView" && rt.GetComponent<ScrollRect>() != null)
                scrollViewGO = rt.gameObject;
            if (rt.name == "TitleBanner" && rt.parent?.name == "InventoryPanel")
                titleBannerRT = rt;
        }

        if (scrollViewGO == null) { Debug.LogError("找不到 ScrollView"); return; }

        var svRT = scrollViewGO.GetComponent<RectTransform>();
        Undo.RecordObject(svRT, "Fix ScrollView Rect");

        // 计算顶部内缩量：TitleBanner 本地 anchoredPosition.y - sizeDelta.y/2 得到底边，
        // 从面板顶部（+panel.rect.height/2）到 TitleBanner 底边的距离。
        // 兜底值 80px（编辑器中 rect 可能为 zero）。
        float topInset = 80f;
        if (titleBannerRT != null)
        {
            // 尝试从 sizeDelta 和 anchoredPosition 推断（不依赖 rect 计算结果）
            float tbBottom = titleBannerRT.anchoredPosition.y - titleBannerRT.sizeDelta.y * 0.5f;
            // tbBottom 是距面板中心的 Y 偏移；转换为距面板顶部的距离需要加半高
            // 面板顶部偏移 = +sizeDelta.y/2（anchor=stretch 时，sizeDelta 通常为 0，rect.height 生效）
            // 直接用观测到的世界 Y 坐标差：TitleBanner 世界 Y ≈ 485，下边界 ≈ 485 - sizeDelta/2
            // 作为保守估算：topInset = -tbBottom（距中心的距离，向上为正）
            // 当 anchorMin/Max = (0.5,1) 时：anchoredPosition.y 已是距顶部的负偏移
            topInset = Mathf.Max(Mathf.Abs(tbBottom) + 10f, 60f);
        }

        // Stretch 模式填满父级
        svRT.anchorMin = Vector2.zero;
        svRT.anchorMax = Vector2.one;
        svRT.pivot     = new Vector2(0.5f, 0.5f);

        // offsetMin=(left, bottom)  offsetMax=(-right, -top)
        // 顶部留给 TitleBanner，底部留给 CapacityText(~80px)
        svRT.offsetMin = new Vector2(20f,  80f);
        svRT.offsetMax = new Vector2( 0f, -(topInset));

        EditorUtility.SetDirty(scrollViewGO);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log($"[FixInventoryScrollViewRect] 完成。" +
                  $"topInset={topInset:F1}  offsetMin={svRT.offsetMin}  offsetMax={svRT.offsetMax}  " +
                  $"TitleBanner anchoredPos={titleBannerRT?.anchoredPosition}  sizeDelta={titleBannerRT?.sizeDelta}");
    }
}
