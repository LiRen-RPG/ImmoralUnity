using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Immortal.UI;

/// <summary>
/// 一键为背包 SlotsContainer 添加 ScrollRect + RectMask2D 裁剪 + 竖直滚动条。
/// 执行路径：Tools > Setup Inventory ScrollView
/// </summary>
public class SetupInventoryScrollView : MonoBehaviour
{
    [MenuItem("Tools/Setup Inventory ScrollView")]
public static void Setup()
    {
        // ── 1. 找到 SlotsContainer ──────────────────────────────────────────
        // 用 Resources.FindObjectsOfTypeAll 也能找到不活跃对象
        GameObject slotsContainer = null;
        foreach (var rt in Resources.FindObjectsOfTypeAll<RectTransform>())
        {
            if (rt.name == "SlotsContainer" && rt.GetComponent<GridLayoutGroup>() != null)
            {
                slotsContainer = rt.gameObject;
                break;
            }
        }

        if (slotsContainer == null)
        {
            Debug.LogError("[SetupInventoryScrollView] 找不到 SlotsContainer，请确认场景层级。");
            return;
        }

        var inventoryPanel = slotsContainer.transform.parent;

        // 如果 ScrollView 已存在，跳过
        if (inventoryPanel.Find("ScrollView") != null)
        {
            Debug.LogWarning("[SetupInventoryScrollView] ScrollView 已存在，跳过。");
            return;
        }

        Undo.SetCurrentGroupName("Setup Inventory ScrollView");
        int undoGroup = Undo.GetCurrentGroup();

        // ── 2. 记录 SlotsContainer 原始 RectTransform ──────────────────────
        var slotRT = slotsContainer.GetComponent<RectTransform>();
        Vector2 origAnchorMin    = slotRT.anchorMin;
        Vector2 origAnchorMax    = slotRT.anchorMax;
        Vector2 origOffset       = slotRT.anchoredPosition;
        Vector2 origSizeDelta    = slotRT.sizeDelta;

        // ── 3. 创建 ScrollView GameObject ──────────────────────────────────
        var scrollViewGO = new GameObject("ScrollView", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(scrollViewGO, "Create ScrollView");
        scrollViewGO.transform.SetParent(inventoryPanel, false);

        var scrollViewRT = scrollViewGO.GetComponent<RectTransform>();
        scrollViewRT.anchorMin        = origAnchorMin;
        scrollViewRT.anchorMax        = origAnchorMax;
        scrollViewRT.anchoredPosition = origOffset;
        scrollViewRT.sizeDelta        = new Vector2(origSizeDelta.x - 20f, origSizeDelta.y);

        // ── 4. 创建 Viewport（负责裁剪） ────────────────────────────────────
        var viewportGO = new GameObject("Viewport", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(viewportGO, "Create Viewport");
        viewportGO.transform.SetParent(scrollViewGO.transform, false);
        Undo.AddComponent<RectMask2D>(viewportGO);

        var viewportRT = viewportGO.GetComponent<RectTransform>();
        viewportRT.anchorMin  = Vector2.zero;
        viewportRT.anchorMax  = Vector2.one;
        viewportRT.offsetMin  = Vector2.zero;
        viewportRT.offsetMax  = Vector2.zero;

        // ── 5. 将 SlotsContainer 移入 Viewport ─────────────────────────────
        Undo.SetTransformParent(slotsContainer.transform, viewportGO.transform, "Move SlotsContainer into Viewport");
        slotRT.anchorMin        = new Vector2(0f, 1f);
        slotRT.anchorMax        = new Vector2(1f, 1f);
        slotRT.pivot            = new Vector2(0.5f, 1f);
        slotRT.anchoredPosition = Vector2.zero;
        slotRT.sizeDelta        = new Vector2(0f, 0f);

        // ── 6. 给 SlotsContainer 加 ContentSizeFitter ───────────────────────
        var csf = slotsContainer.GetComponent<ContentSizeFitter>();
        if (csf == null)
            csf = Undo.AddComponent<ContentSizeFitter>(slotsContainer);
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // ── 7. 创建竖直滚动条 ────────────────────────────────────────────────
        var scrollbarGO = new GameObject("Scrollbar Vertical", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(scrollbarGO, "Create Scrollbar Vertical");
        scrollbarGO.transform.SetParent(scrollViewGO.transform, false);

        var sbBg = Undo.AddComponent<Image>(scrollbarGO);
        sbBg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);

        var scrollbarRT = scrollbarGO.GetComponent<RectTransform>();
        scrollbarRT.anchorMin        = new Vector2(1f, 0f);
        scrollbarRT.anchorMax        = new Vector2(1f, 1f);
        scrollbarRT.pivot            = new Vector2(1f, 0.5f);
        scrollbarRT.anchoredPosition = new Vector2(20f, 0f);
        scrollbarRT.sizeDelta        = new Vector2(20f, 0f);

        // Sliding Area
        var slidingAreaGO = new GameObject("Sliding Area", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(slidingAreaGO, "Create Sliding Area");
        slidingAreaGO.transform.SetParent(scrollbarGO.transform, false);
        var slidingRT = slidingAreaGO.GetComponent<RectTransform>();
        slidingRT.anchorMin  = Vector2.zero;
        slidingRT.anchorMax  = Vector2.one;
        slidingRT.offsetMin  = new Vector2(10f, 10f);
        slidingRT.offsetMax  = new Vector2(-10f, -10f);

        // Handle
        var handleGO = new GameObject("Handle", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(handleGO, "Create Handle");
        handleGO.transform.SetParent(slidingAreaGO.transform, false);
        var handleImg = Undo.AddComponent<Image>(handleGO);
        handleImg.color = new Color(0.7f, 0.6f, 0.4f, 0.9f);
        var handleRT = handleGO.GetComponent<RectTransform>();
        handleRT.anchorMin  = Vector2.zero;
        handleRT.anchorMax  = Vector2.one;
        handleRT.offsetMin  = new Vector2(-10f, -10f);
        handleRT.offsetMax  = new Vector2(10f, 10f);

        // Scrollbar component
        var scrollbar = Undo.AddComponent<Scrollbar>(scrollbarGO);
        scrollbar.handleRect  = handleRT;
        scrollbar.direction   = Scrollbar.Direction.BottomToTop;
        scrollbar.value       = 1f;

        // ── 8. 添加 ScrollRect 并完成连线 ──────────────────────────────────
        var scrollRect = Undo.AddComponent<ScrollRect>(scrollViewGO);
        scrollRect.content              = slotRT;
        scrollRect.viewport             = viewportRT;
        scrollRect.horizontal           = false;
        scrollRect.vertical             = true;
        scrollRect.verticalScrollbar    = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.scrollSensitivity    = 30f;
        scrollRect.movementType         = ScrollRect.MovementType.Clamped;

        // ── 9. 更新 BaseInventoryPanel 的 slotsContainer 引用 ──────────────
        var panel = inventoryPanel.GetComponent<BaseInventoryPanel>();
        if (panel != null)
        {
            Undo.RecordObject(panel, "Update slotsContainer reference");
            var field = typeof(BaseInventoryPanel).GetField(
                "slotsContainer",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(panel, slotsContainer.transform);
            EditorUtility.SetDirty(panel);
        }

        Undo.CollapseUndoOperations(undoGroup);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[SetupInventoryScrollView] 完成！\n" +
                  "InventoryPanel/ScrollView/Viewport/SlotsContainer\n" +
                  "InventoryPanel/ScrollView/Scrollbar Vertical");
    }
}
