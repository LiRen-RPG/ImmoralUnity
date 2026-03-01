using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Immortal.UI;

/// <summary>
/// 一键在当前场景中搭建完整 UI 层级（Canvas + 所有面板）。
/// </summary>
public class UIBuilder : EditorWindow
{
    private const string PrefabFolder = "Assets/UI/Prefabs";

    [MenuItem("Tools/Build Immortal UI")]
    public static void BuildUI()
    {
        CleanUI();   // 先清理旧节点

        // ── 0. 独立 Prefab 预先生成（面板构建时会引用）─────────────────
        EnsurePrefabFolder();
        SavePrefab(BuildClosePrefabGO(), "Close");

        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var cgo    = new GameObject("Canvas");
            canvas     = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            cgo.AddComponent<GraphicRaycaster>();
        }

        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ── 1. UIRoot ────────────────────────────────────────────────────
        var uiRoot = GetOrCreate(canvas.transform, "UIRoot");
        SetStretch(uiRoot);

        // ── 2. 背包面板 (InventoryPanel) ─────────────────────────────────
        var inventoryPanel = BuildInventoryPanel(uiRoot.transform, font);

        // ── 3. 阵盘面板 (FormationPanel) ─────────────────────────────────
        var formationPanel = BuildFormationPanel(uiRoot.transform, font);

        // ── 4. 快捷栏 (QuickBar) ──────────────────────────────────────────
        var quickBarPanel = BuildQuickBar(uiRoot.transform, font);

        // ── 5. UIManager ─────────────────────────────────────────────────
        var uiManagerGo = GetOrCreate(uiRoot, "UIManager");
        var uiManager   = uiManagerGo.GetComponent<UIManager>() ?? uiManagerGo.AddComponent<UIManager>();
        EnsurePrefabFolder();
        // 反射赋值（避免修改已有 UIManager 字段）
        SetField(uiManager, "inventoryUI",  inventoryPanel.GetComponent<InventoryUI>());
        SetField(uiManager, "quickBarUI",   quickBarPanel .GetComponent<QuickBarUI>());
        SetField(uiManager, "formationUI",  formationPanel.GetComponent<EightTrigramsFormationUI>());
        SetField(uiManager, "sceneCanvas",  canvas);

        Debug.Log("✅ Immortal UI 构建完成！请检查 Canvas → UIRoot 下的层级。");
        Selection.activeGameObject = uiRoot;
    }

    // ══════════════════════════════════════════════════════════════
    //  背包面板
    // ══════════════════════════════════════════════════════════════
    private static GameObject BuildInventoryPanel(Transform parent, Font font)
    {
        var panel = GetOrCreate(parent, "InventoryPanel");
        SetAnchored(panel, new Vector2(-450, 0), new Vector2(820, 970));
        AddBackgroundImage(panel, "UIFrame");

        // CanvasGroup（供 BaseInventoryPanel 控制开关）
        if (panel.GetComponent<CanvasGroup>() == null)
            panel.AddComponent<CanvasGroup>();

        // BaseInventoryPanel
        var bp = panel.GetComponent<BaseInventoryPanel>() ?? panel.AddComponent<BaseInventoryPanel>();

        // 标题
        BuildBanner(panel, "物品", font);

        // 容量文字
        var capTxt = CreateText(panel, "CapacityText", "0/30", font, 22, TextAnchor.MiddleRight);
        SetAnchored(capTxt, new Vector2(360, -920), new Vector2(120, 30));

        // 格子容器
        var gridGo = GetOrCreate(panel.transform, "SlotsContainer");
        SetAnchored(gridGo, new Vector2(0, -100), new Vector2(760, 780));
        var grid = gridGo.GetComponent<GridLayoutGroup>() ?? gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize         = new Vector2(110, 110);
        grid.spacing          = new Vector2(6,  6);
        grid.constraint       = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount  = 6;
        grid.childAlignment   = TextAnchor.UpperCenter;

        // 把 slotsContainer 和 slotPrefab 赋给 BaseInventoryPanel
        SetField(bp, "slotsContainer", gridGo.transform);
        SetField(bp, "slotPrefab",     SavePrefab(BuildInventorySlotPrefabGO(font), "InventorySlot"));

        // 关闭按钮
        BuildCloseButton(panel);

        // InventoryUI 包装层
        var invUI = panel.GetComponent<InventoryUI>() ?? panel.AddComponent<InventoryUI>();
        SetField(invUI, "inventoryPanel", bp);
        SetField(invUI, "capacityText",   capTxt.GetComponent<Text>());

        return panel;
    }

    // ══════════════════════════════════════════════════════════════
    //  阵盘面板
    // ══════════════════════════════════════════════════════════════
    private static GameObject BuildFormationPanel(Transform parent, Font font)
    {
        var panel = GetOrCreate(parent, "FormationPanel");
        SetAnchored(panel, new Vector2(450, 0), new Vector2(820, 970));
        AddBackgroundImage(panel, "UIFrame");

        // 标题
        BuildBanner(panel, "阵盘", font);

        // 关闭按钮
        BuildCloseButton(panel);

        // 中心阴阳鱼
        var centerRoot = GetOrCreate(panel.transform, "CenterRoot");
        SetAnchored(centerRoot, new Vector2(0, -60), new Vector2(700, 700));

        var innerFrame = GetOrCreate(centerRoot.transform, "InnerFrame");
        SetAnchored(innerFrame, Vector2.zero, new Vector2(700, 700));
        AddSprite(innerFrame, "InnerFrame");

        var yinyang = GetOrCreate(centerRoot.transform, "YinYangFish");
        SetAnchored(yinyang, Vector2.zero, new Vector2(300, 300));
        AddSprite(yinyang, "YinYangFish");

        // 卦象容器
        var trigramsRoot = GetOrCreate(centerRoot.transform, "TrigramsRoot");
        SetAnchored(trigramsRoot, Vector2.zero, new Vector2(0, 0));

        // EightTrigramsFormationUI 组件
        var fui = panel.GetComponent<EightTrigramsFormationUI>() ?? panel.AddComponent<EightTrigramsFormationUI>();
        SetField(fui, "yinYangFishImage", yinyang.GetComponent<Image>());
        SetField(fui, "innerFrameImage",  innerFrame.GetComponent<Image>());
        SetField(fui, "trigramsRoot",     trigramsRoot.transform);
        SetField(fui, "trigramPrefab",    SavePrefab(BuildTrigramPrefabGO(font), "Trigram"));

        var closeBtn = panel.transform.Find("TitleBanner/CloseButton");
        if (closeBtn != null)
            SetField(fui, "closeButton", closeBtn.GetComponent<Button>());

        // 不在编辑器构建时隐藏，方便预览；运行时由 EightTrigramsFormationUI.Awake 控制

        return panel;
    }

    // ══════════════════════════════════════════════════════════════
    //  快捷栏
    // ══════════════════════════════════════════════════════════════
    private static GameObject BuildQuickBar(Transform parent, Font font)
    {
        var bar = GetOrCreate(parent, "QuickBarPanel");
        var rt  = bar.GetComponent<RectTransform>() ?? bar.AddComponent<RectTransform>();
        rt.anchorMin       = new Vector2(1, 0.5f);
        rt.anchorMax       = new Vector2(1, 0.5f);
        rt.pivot           = new Vector2(1, 0.5f);
        rt.anchoredPosition = new Vector2(-10, 0);
        rt.sizeDelta        = new Vector2(130, 630);
        AddBackgroundImage(bar, "UIFrame");

        var containerGo = GetOrCreate(bar.transform, "QuickBarContainer");
        var crt         = containerGo.GetComponent<RectTransform>() ?? containerGo.AddComponent<RectTransform>();
        crt.anchorMin   = Vector2.zero;
        crt.anchorMax   = Vector2.one;
        crt.sizeDelta   = Vector2.zero;

        var vlg         = containerGo.GetComponent<VerticalLayoutGroup>() ?? containerGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment       = TextAnchor.UpperCenter;
        vlg.padding              = new RectOffset(10, 10, 20, 10);
        vlg.spacing              = 6;
        vlg.childForceExpandWidth = false;
        vlg.childForceExpandHeight= false;

        var qbUI = bar.GetComponent<QuickBarUI>() ?? bar.AddComponent<QuickBarUI>();
        SetField(qbUI, "quickBarContainer", containerGo.transform);
        // 复用同一个 prefab asset；若已存在则直接加载
        var slotPrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/InventorySlot.prefab")
                              ?? SavePrefab(BuildInventorySlotPrefabGO(font), "InventorySlot");
        SetField(qbUI, "slotPrefab", slotPrefabAsset);

        return bar;
    }

    // ══════════════════════════════════════════════════════════════
    //  小工具：构建标题 Banner
    // ══════════════════════════════════════════════════════════════
    private static void BuildBanner(GameObject panel, string title, Font font)
    {
        var banner = GetOrCreate(panel.transform, "TitleBanner");
        var rt     = banner.GetComponent<RectTransform>() ?? banner.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, 0);
        rt.sizeDelta        = new Vector2(0, 100);
        AddSprite(banner, "ItemShortcut");

        // 标题文字
        var txtGo = CreateText(banner, "TitleText", title, font, 60, TextAnchor.MiddleCenter);
        txtGo.GetComponent<Text>().fontStyle = FontStyle.Bold;
        txtGo.GetComponent<Text>().color     = new Color(0.2f, 0.15f, 0.05f);
        var trt = txtGo.GetComponent<RectTransform>();
        trt.sizeDelta = new Vector2(400, 80);

        // 关闭按钮（从 Close.prefab 实例化，fallback 到 inline 构建）
        var closePrefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/Close.prefab");
        GameObject closeGo;
        if (closePrefabAsset != null)
        {
            closeGo      = (GameObject)PrefabUtility.InstantiatePrefab(closePrefabAsset, banner.transform);
            closeGo.name = "CloseButton";
        }
        else
        {
            closeGo = GetOrCreate(banner.transform, "CloseButton");
            AddSprite(closeGo, "Close");
            if (closeGo.GetComponent<Button>() == null) closeGo.AddComponent<Button>();
        }
        var closeRt  = closeGo.GetComponent<RectTransform>() ?? closeGo.AddComponent<RectTransform>();
        closeRt.anchorMin        = new Vector2(1, 0.5f);
        closeRt.anchorMax        = new Vector2(1, 0.5f);
        closeRt.pivot            = new Vector2(0.5f, 0.5f);
        closeRt.anchoredPosition = new Vector2(-60, 0);
        closeRt.sizeDelta        = new Vector2(60, 60);
    }

    private static void BuildCloseButton(GameObject panel)
    {
        // Banner 里已包含，此处留空供扩展
    }

    // ══════════════════════════════════════════════════════════════
    //  预制体构建（运行时 Instantiate 用）
    // ══════════════════════════════════════════════════════════════

    // 构建 Close 按钮 Prefab GO（对应 Cocos Close.prefab：64×64，Sprite+Button）
    private static GameObject BuildClosePrefabGO()
    {
        var root = new GameObject("Close", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(64, 64);

        var img    = root.AddComponent<Image>();
        img.sprite = LoadSprite("Close");
        img.color  = new Color(221f/255, 221f/255, 221f/255);   // Cocos _color rgb(221,221,221)

        root.AddComponent<Button>();
        return root;
    }

    // 构建 GO，由 SavePrefab 负责持久化
    private static GameObject BuildInventorySlotPrefabGO(Font font)
    {
        var root = new GameObject("InventorySlot", typeof(RectTransform));
        ((RectTransform)root.transform).sizeDelta = new Vector2(110, 110);

        // 背景
        var bg  = new GameObject("Background", typeof(RectTransform));
        bg.transform.SetParent(root.transform, false);
        SetStretch(bg);
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = LoadSprite("InventorySlot");

        // 图标（Item，全拉伸+5px边距，对应 Cocos Widget l=r=t=b=5 flags=45）
        var iconGo = new GameObject("Item", typeof(RectTransform));
        iconGo.transform.SetParent(root.transform, false);
        var iconRt = (RectTransform)iconGo.transform;
        iconRt.anchorMin        = Vector2.zero;
        iconRt.anchorMax        = Vector2.one;
        iconRt.offsetMin        = new Vector2(5, 5);    // left, bottom
        iconRt.offsetMax        = new Vector2(-5, -5);  // -right, -top
        var ico = iconGo.AddComponent<Image>();
        ico.enabled = false;

        // QuantityLabel 数量（右下角，fontSize=16，白色）
        var qtyGo = new GameObject("QuantityLabel", typeof(RectTransform));
        qtyGo.transform.SetParent(root.transform, false);
        var qtyRt = (RectTransform)qtyGo.transform;
        qtyRt.anchorMin        = new Vector2(1, 0);
        qtyRt.anchorMax        = new Vector2(1, 0);
        qtyRt.pivot            = new Vector2(1, 0);
        qtyRt.anchoredPosition = new Vector2(-4, 4);
        qtyRt.sizeDelta        = new Vector2(60, 28);
        var qtyTxt = qtyGo.AddComponent<Text>();
        qtyTxt.font      = font;
        qtyTxt.fontSize  = 16;   // Cocos Label fontSize=16
        qtyTxt.alignment = TextAnchor.LowerRight;
        qtyTxt.color     = Color.white;
        qtyGo.SetActive(false);

        // 高亮遮罩
        var hlGo = new GameObject("Highlight", typeof(RectTransform));
        hlGo.transform.SetParent(root.transform, false);
        SetStretch(hlGo);
        var hlImg = hlGo.AddComponent<Image>();
        hlImg.color          = new Color(1, 1, 1, 0.25f);
        hlImg.enabled        = false;
        hlImg.raycastTarget  = false;

        // InventorySlotUI 组件
        var sui = root.AddComponent<InventorySlotUI>();
        SetField(sui, "slotBackground", bgImg);
        SetField(sui, "itemIcon",       ico);
        SetField(sui, "quantityText",   qtyTxt);
        SetField(sui, "highlightImage", hlImg);

        // Button（用于 SetInteractionEnabled）
        root.AddComponent<Button>();

        return root;
    }

    private static void AddLayoutElem(GameObject go, float w, float h)
    {
        var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        le.preferredWidth  = w;
        le.preferredHeight = h;
    }

    // 构建 GO，由 SavePrefab 负责持久化
    private static GameObject BuildTrigramPrefabGO(Font font)
    {
        // Cocos Trigram.prefab 结构：根节点带 cc.Layout（垂直布局）
        // 子节点（从下到上）: Position(y=20) > First(y=66) > Second(y=103) > Third(y=140) > Name(y=186)
        // 没有 InventorySlot 子节点；物品槽由阵盘面板管理

        var darkBrown = new Color(37f/255, 34f/255, 10f/255);

        var root = new GameObject("Trigram", typeof(RectTransform));
        var rootRt = (RectTransform)root.transform;
        rootRt.sizeDelta = new Vector2(160, 220);

        // 垂直布局（对应 cc.Layout）
        var vlg = root.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment        = TextAnchor.MiddleCenter;
        vlg.spacing               = 8;
        vlg.padding               = new RectOffset(4, 4, 4, 4);
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight= false;
        vlg.reverseArrangement    = false;
        root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Name 卦名（最顶部）
        var nameGo = CreateText(root, "Name", "", font, 30, TextAnchor.MiddleCenter);
        nameGo.GetComponent<Text>().color = darkBrown;
        AddLayoutElem(nameGo, 160, 36);

        // Third 爻3（上）
        var thirdGo = new GameObject("Third", typeof(RectTransform));
        thirdGo.transform.SetParent(root.transform, false);
        var thirdImg = thirdGo.AddComponent<Image>();
        AddLayoutElem(thirdGo, 128, 22);

        // Second 爻2（中）
        var secondGo = new GameObject("Second", typeof(RectTransform));
        secondGo.transform.SetParent(root.transform, false);
        var secondImg = secondGo.AddComponent<Image>();
        AddLayoutElem(secondGo, 128, 22);

        // First 爻1（下）
        var firstGo = new GameObject("First", typeof(RectTransform));
        firstGo.transform.SetParent(root.transform, false);
        var firstImg = firstGo.AddComponent<Image>();
        AddLayoutElem(firstGo, 128, 22);

        // Position 方位（最底部）
        var posGo = CreateText(root, "Position", "", font, 30, TextAnchor.MiddleCenter);
        posGo.GetComponent<Text>().color = darkBrown;
        AddLayoutElem(posGo, 160, 36);

        // TrigramUI 组件
        var tui = root.AddComponent<TrigramUI>();
        SetField(tui, "yao1Sprite",       firstImg);   // First = 爻1
        SetField(tui, "yao2Sprite",       secondImg);  // Second = 爻2
        SetField(tui, "yao3Sprite",       thirdImg);   // Third = 爻3
        SetField(tui, "yangYaoFrame",     LoadSprite("Yang"));
        SetField(tui, "yinYaoFrame",      LoadSprite("Yin"));
        SetField(tui, "trigramNameLabel", nameGo.GetComponent<Text>());
        SetField(tui, "positionLabel",    posGo.GetComponent<Text>());
        // itemSlots 不在 Trigram prefab 里，由 EightTrigramsFormationUI 单独管理

        return root;
    }

    // ══════════════════════════════════════════════════════════════
    //  内部辅助方法
    // ══════════════════════════════════════════════════════════════

    private static GameObject GetOrCreate(Transform parent, string name)
    {
        var existing = parent.Find(name);
        if (existing != null) return existing.gameObject;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static GameObject GetOrCreate(GameObject parent, string name)
        => GetOrCreate(parent.transform, name);

    private static void SetStretch(GameObject go)
    {
        var rt         = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin   = Vector2.zero;
        rt.anchorMax   = Vector2.one;
        rt.sizeDelta   = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void SetAnchored(GameObject go, Vector2 pos, Vector2 size)
    {
        var rt              = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
    }

    private static void AddBackgroundImage(GameObject go, string spriteName)
    {
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.sprite = LoadSprite(spriteName);
        img.type   = Image.Type.Sliced;
    }

    private static void AddSprite(GameObject go, string spriteName)
    {
        var img    = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.sprite = LoadSprite(spriteName);
    }

    private static GameObject CreateText(GameObject parent, string name, string content, Font font, int size, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        var t       = go.AddComponent<Text>();
        t.text      = content;
        t.font      = font;
        t.fontSize  = size;
        t.alignment = anchor;
        t.color     = Color.white;
        return go;
    }

    private static Sprite LoadSprite(string name)
    {
        var guids = AssetDatabase.FindAssets(name + " t:Sprite", new[] { "Assets/UI/Sprites" });
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guids[0]));

        guids = AssetDatabase.FindAssets(name + " t:Texture2D", new[] { "Assets/UI/Sprites" });
        if (guids.Length > 0)
        {
            var path     = AssetDatabase.GUIDToAssetPath(guids[0]);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        return null;
    }

    // ── 清理旧 UI ────────────────────────────────────────────────────
    private static void CleanUI()
    {
        // 删除 UIRoot 及其所有子节点
        var canvas = FindObjectOfType<Canvas>();
        if (canvas != null)
        {
            var uiRoot = canvas.transform.Find("UIRoot");
            if (uiRoot != null)
                Object.DestroyImmediate(uiRoot.gameObject);
        }

        // 删除旧 Prefab assets（下次 Build 重新生成）
        foreach (var name in new[] { "Close", "InventorySlot", "Trigram" })
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.DeleteAsset(path + ".meta");
            }
        }
        AssetDatabase.Refresh();
    }

    // ── Prefab 持久化 ───────────────────────────────────────────────
    private static void EnsurePrefabFolder()    {
        if (!AssetDatabase.IsValidFolder("Assets/UI"))
            AssetDatabase.CreateFolder("Assets", "UI");
        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/UI", "Prefabs");
    }

    /// <summary>将临时 GO 保存为 prefab asset，销毁 GO 并返回 asset 引用。</summary>
    private static GameObject SavePrefab(GameObject go, string assetName)
    {
        EnsurePrefabFolder();
        string path   = $"{PrefabFolder}/{assetName}.prefab";
        var    saved  = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        AssetDatabase.Refresh();
        return saved;
    }

    // 通过反射给私有 SerializeField 赋值
    private static void SetField(object target, string fieldName, object value)
    {
        var f = target.GetType().GetField(fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f == null)
            f = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        f?.SetValue(target, value);
    }
}
