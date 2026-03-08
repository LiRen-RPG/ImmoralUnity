using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Immortal.Core;
using Immortal.Item;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Immortal.Editor
{
    /// <summary>
    /// 物品管理器 Editor Window
    /// 菜单: Tools / 物品管理器
    /// 支持查看、新增、修改、删除所有类型物品，并保存回 JSON 文件。
    /// </summary>
    public class ItemEditorWindow : EditorWindow
    {
        private const string DataFolder = "Assets/Resources/Data/Items";

        // ── 物品类型元数据 ───────────────────────────────────────────────────
        private static readonly (ItemType type, string label, string file)[] ItemTypeInfos =
        {
            (ItemType.Pill,      "丹药", "Pill"),
            (ItemType.Weapon,    "法器", "Weapon"),
            (ItemType.Ammo,      "弹药", "Ammo"),
            (ItemType.Talisman,  "符箓", "Talisman"),
            (ItemType.Material,  "材料", "Material"),
            (ItemType.Book,      "功法", "Book"),
            (ItemType.Treasure,  "法宝", "Treasure"),
            (ItemType.Tool,      "工具", "Tool"),
            (ItemType.Formation, "阵盘", "Formation"),
            (ItemType.Quest,     "任务", "Quest"),
        };

        // ── 枚举中文标签 ─────────────────────────────────────────────────────
        private static readonly List<string> RarityLabels =
            new List<string> { "普通", "精良", "稀有", "史诗", "传说", "仙品" };

        private static readonly List<string> PhaseLabelsNullable =
            new List<string> { "无", "金", "木", "水", "火", "土" };

        private static readonly List<string> RealmLabelsNullable =
            new List<string> { "无", "练气", "筑基", "金丹", "元婴", "化神", "返虚", "渡劫", "大乘" };

        private static readonly List<string> PhaseLabels =
            new List<string> { "金", "木", "水", "火", "土" };

        private static readonly Color[] RarityColors =
        {
            new Color(0.70f, 0.70f, 0.70f), // 普通
            new Color(0.30f, 0.80f, 0.30f), // 精良
            new Color(0.35f, 0.55f, 1.00f), // 稀有
            new Color(0.75f, 0.30f, 1.00f), // 史诗
            new Color(1.00f, 0.60f, 0.10f), // 传说
            new Color(1.00f, 0.90f, 0.10f), // 仙品
        };

        // ── 丹药表格列定义 ───────────────────────────────────────────────────
        private static readonly (string header, float width, bool flex)[] PillColumns =
        {
            ("图标"    ,  32, false),  // 0  图标
            ("ID"      , 175, false),  // 1
            ("名称"    , 100, false),  // 2
            ("稀有度"  ,  80, false),  // 3
            ("五行"    ,  65, false),  // 4
            ("境界"    ,  75, false),  // 5
            ("恢复气血",  78, false),  // 6
            ("恢复灵力",  78, false),  // 7
            ("增加修为",  78, false),  // 8
            ("突破加成",  78, false),  // 9
            ("增益效果", 110, false),  // 10
            ("持续(秒)",  68, false),  // 11
            ("可堆叠"  ,  55, false),  // 12
            ("最大堆叠",  65, false),  // 13
            ("描述"    , 220,  true),  // 14
            (""        ,  30, false),  // 15 删除按钮
        };

        // ── 阵盘表格列定义 ───────────────────────────────────────────────────
        private static readonly (string header, float width, bool flex)[] FormationColumns =
        {
            ("ID"    , 220, false),  // 0
            ("名称"  , 160, false),  // 1
            ("稀有度",  80, false),  // 2
            ("描述"  , 300,  true),  // 3
            (""      ,  30, false),  // 4 删除按钮
        };

        // ── 法器表格列定义 ───────────────────────────────────────────────────
        private static readonly (string header, float width, bool flex)[] WeaponColumns =
        {
            ("ID"      , 175, false),  // 0
            ("名称"    , 100, false),  // 1
            ("稀有度"  ,  80, false),  // 2
            ("五行"    ,  65, false),  // 3
            ("境界"    ,  75, false),  // 4
            ("攻击力"  ,  72, false),  // 5
            ("射程"    ,  72, false),  // 6
            ("弹药ID"  , 120, false),  // 7
            ("特殊效果", 140, false),  // 8
            ("描述"    , 220,  true),  // 9
            (""        ,  30, false),  // 10 删除
        };

        // ── 弹药表格列定义 ───────────────────────────────────────────────────
        private static readonly (string header, float width, bool flex)[] AmmoColumns =
        {
            ("ID"              , 175, false),  // 0
            ("名称"            , 100, false),  // 1
            ("稀有度"          ,  80, false),  // 2
            ("五行"            ,  65, false),  // 3
            ("境界"            ,  75, false),  // 4
            ("伤害"            ,  72, false),  // 5
            ("可堆叠"          ,  55, false),  // 6
            ("最大堆叠"        ,  65, false),  // 7
            ("兼容法器(逗号分隔)", 160, false),  // 8
            ("描述"            , 220,  true),  // 9
            (""                ,  30, false),  // 10 删除
        };

        // ── 符箓表格列定义 ───────────────────────────────────────────────────
        private static readonly (string header, float width, bool flex)[] TalismanColumns =
        {
            ("ID"      , 175, false),  // 0
            ("名称"    , 100, false),  // 1
            ("稀有度"  ,  80, false),  // 2
            ("五行"    ,  65, false),  // 3
            ("境界"    ,  75, false),  // 4
            ("效果"    , 120, false),  // 5
            ("持续(秒)",  68, false),  // 6
            ("可堆叠"  ,  55, false),  // 7
            ("最大堆叠",  65, false),  // 8
            ("描述"    , 220,  true),  // 9
            (""        ,  30, false),  // 10 删除
        };

        // ── 材料表格列定义 ───────────────────────────────────────────────────
        private static readonly (string header, float width, bool flex)[] MaterialColumns =
        {
            ("ID"      , 175, false),  // 0
            ("名称"    , 100, false),  // 1
            ("稀有度"  ,  80, false),  // 2
            ("五行"    ,  65, false),  // 3
            ("境界"    ,  75, false),  // 4
            ("品阶"    ,  60, false),  // 5
            ("可堆叠"  ,  55, false),  // 6
            ("最大堆叠",  65, false),  // 7
            ("描述"    , 220,  true),  // 8
            (""        ,  30, false),  // 9 删除
        };

        // ── 功法表格列定义 ───────────────────────────────────────────────────
        private static readonly (string header, float width, bool flex)[] BookColumns =
        {
            ("ID"      , 175, false),  // 0
            ("名称"    , 100, false),  // 1
            ("稀有度"  ,  80, false),  // 2
            ("五行"    ,  65, false),  // 3
            ("境界"    ,  75, false),  // 4
            ("技能名称", 110, false),  // 5
            ("技能类型",  80, false),  // 6
            ("需求等级",  65, false),  // 7
            ("描述"    , 220,  true),  // 8
            (""        ,  30, false),  // 9 删除
        };

        // ── 法宝表格列定义 ───────────────────────────────────────────────────
        private static readonly (string header, float width, bool flex)[] TreasureColumns =
        {
            ("ID"      , 175, false),  // 0
            ("名称"    , 100, false),  // 1
            ("稀有度"  ,  80, false),  // 2
            ("五行"    ,  65, false),  // 3
            ("境界"    ,  75, false),  // 4
            ("威力"    ,  72, false),  // 5
            ("特殊效果", 160, false),  // 6
            ("描述"    , 220,  true),  // 7
            (""        ,  30, false),  // 8 删除
        };

        // ── 工具表格列定义 ───────────────────────────────────────────────────
        private static readonly (string header, float width, bool flex)[] ToolColumns =
        {
            ("ID"      , 175, false),  // 0
            ("名称"    , 100, false),  // 1
            ("稀有度"  ,  80, false),  // 2
            ("五行"    ,  65, false),  // 3
            ("境界"    ,  75, false),  // 4
            ("效率"    ,  72, false),  // 5
            ("耐久度"  ,  72, false),  // 6
            ("描述"    , 220,  true),  // 7
            (""        ,  30, false),  // 8 删除
        };

        // ── 任务物品表格列定义 ────────────────────────────────────────────────
        private static readonly (string header, float width, bool flex)[] QuestColumns =
        {
            ("ID"    , 175, false),  // 0
            ("名称"  , 100, false),  // 1
            ("任务ID", 160, false),  // 2
            ("描述"  , 300,  true),  // 3
            (""      ,  30, false),  // 4 删除
        };

        // ── 状态 ────────────────────────────────────────────────────────────
        private ItemType         _currentType    = ItemType.Pill;
        private List<BaseItemConfig>   _allItems       = new List<BaseItemConfig>();
        private List<BaseItemConfig>   _filteredItems  = new List<BaseItemConfig>();
        private BaseItemConfig         _selectedItem;
        private bool             _isDirty;
        private bool             _suppressCb;   // suppress field callbacks while rebuilding form
        private string           _searchText    = "";

        // ── UI 引用 ──────────────────────────────────────────────────────────
        private VisualElement    _tabBar;
        private Label            _statusLabel;
        private VisualElement    _mainArea;
        private VisualElement    _tableBody;
        private VisualElement    _iconTooltip;
        private Image            _iconTooltipImage;

        // ════════════════════════════════════════════════════════════════════
        //  Entry point
        // ════════════════════════════════════════════════════════════════════

        [MenuItem("Tools/物品管理器")]
        public static void ShowWindow()
        {
            var w = GetWindow<ItemEditorWindow>();
            w.titleContent = new GUIContent("物品管理器");
            w.minSize = new Vector2(920, 620);
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow      = 1;

            root.Add(BuildTabBar());

            _mainArea = new VisualElement();
            _mainArea.style.flexDirection = FlexDirection.Row;
            _mainArea.style.flexGrow      = 1;
            root.Add(_mainArea);

            root.Add(BuildBottomBar());

            LoadItemsForType(_currentType);
            RebuildMainArea();
            RefreshTabHighlight();
            BuildIconTooltip();
        }

        // ════════════════════════════════════════════════════════════════════
        //  Panel builders
        // ════════════════════════════════════════════════════════════════════

        private VisualElement BuildTabBar()
        {
            _tabBar = new VisualElement();
            _tabBar.style.flexDirection     = FlexDirection.Row;
            _tabBar.style.flexShrink        = 0;
            _tabBar.style.flexWrap          = Wrap.Wrap;
            _tabBar.style.backgroundColor   = new Color(0.17f, 0.17f, 0.17f);
            _tabBar.style.paddingLeft        = 6;
            _tabBar.style.paddingTop         = 4;
            _tabBar.style.paddingBottom      = 4;

            foreach (var (type, label, _) in ItemTypeInfos)
            {
                var t   = type;
                var btn = new Button(() => SwitchType(t)) { text = label };
                btn.style.height       = 24;
                btn.style.marginRight  = 3;
                btn.style.paddingLeft  = 10;
                btn.style.paddingRight = 10;
                _tabBar.Add(btn);
            }
            return _tabBar;
        }

        private VisualElement BuildBottomBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection   = FlexDirection.Row;
            bar.style.flexShrink      = 0;
            bar.style.height          = 38;
            bar.style.backgroundColor = new Color(0.17f, 0.17f, 0.17f);
            bar.style.paddingLeft     = 10;
            bar.style.paddingRight    = 10;
            bar.style.alignItems      = Align.Center;

            var addBtn = new Button(OnAddItem) { text = "+ 新增" };
            addBtn.style.width = 72;
            bar.Add(addBtn);

            var delBtn = new Button(OnDeleteItem) { text = "- 删除" };
            delBtn.style.width      = 72;
            delBtn.style.marginLeft = 4;
            bar.Add(delBtn);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            bar.Add(spacer);

            _statusLabel = new Label("");
            _statusLabel.style.color       = new Color(1f, 0.6f, 0f);
            _statusLabel.style.marginRight = 10;
            bar.Add(_statusLabel);

            var saveBtn = new Button(OnSave) { text = "保存 JSON" };
            saveBtn.style.width = 90;
            bar.Add(saveBtn);

            return bar;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Data operations
        // ════════════════════════════════════════════════════════════════════

        private void SwitchType(ItemType type)
        {
            if (_isDirty)
            {
                int r = EditorUtility.DisplayDialogComplex(
                    "有未保存的修改",
                    $"当前 {GetTypeLabel(_currentType)} 有未保存的修改，是否先保存？",
                    "保存后切换", "直接切换", "取消");
                if (r == 2) return;
                if (r == 0) OnSave();
            }

            _currentType  = type;
            _selectedItem = null;
            _isDirty      = false;
            UpdateStatusLabel();
            LoadItemsForType(type);
            RebuildMainArea();
            RefreshTabHighlight();
        }

        private void LoadItemsForType(ItemType type)
        {
            _allItems.Clear();
            _selectedItem = null;

            string path = Path.Combine(DataFolder, $"{GetFileName(type)}.json");
            if (!File.Exists(path)) { ApplySearch(); return; }

            try
            {
                string json = File.ReadAllText(path);
                switch (type)
                {
                    case ItemType.Pill:      Deserialize<PillConfig>(json);      break;
                    case ItemType.Weapon:    Deserialize<WeaponConfig>(json);    break;
                    case ItemType.Ammo:      Deserialize<AmmoConfig>(json);      break;
                    case ItemType.Talisman:  Deserialize<TalismanConfig>(json);  break;
                    case ItemType.Material:  Deserialize<MaterialConfig>(json);  break;
                    case ItemType.Book:      Deserialize<BookConfig>(json);      break;
                    case ItemType.Treasure:  Deserialize<TreasureConfig>(json);  break;
                    case ItemType.Tool:      Deserialize<ToolConfig>(json);      break;
                    case ItemType.Formation: Deserialize<FormationConfig>(json); break;
                    case ItemType.Quest:     Deserialize<QuestConfig>(json);     break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[物品管理器] 加载 {GetFileName(type)}.json 失败: {ex.Message}");
            }

            ApplySearch();
        }

        private void Deserialize<T>(string json) where T : BaseItemConfig
        {
            var w = JsonConvert.DeserializeObject<JsonWrapper<T>>(json);
            if (w?.items != null)
                foreach (var item in w.items)
                    if (item != null) _allItems.Add(item);
        }

        private void ApplySearch()
        {
            _filteredItems = string.IsNullOrEmpty(_searchText)
                ? new List<BaseItemConfig>(_allItems)
                : _allItems.Where(i =>
                      (i.name?.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                      (i.id?.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                      (i.description?.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0))
                  .ToList();

            RefreshTableRows();

            var countLabel = rootVisualElement?.Q<Label>("countLabel");
            if (countLabel != null)
                countLabel.text = $"共 {_allItems.Count} 个  |  显示 {_filteredItems.Count} 个";
        }

        private void OnAddItem()
        {
            var item = CreateItem(_currentType);
            item.id        = MakeNewId(_currentType);
            item.name      = $"新{GetTypeLabel(_currentType)}";
            item.rarity    = ItemRarity.Common;
            item.stackable = DefaultStackable(_currentType);
            item.maxStack  = DefaultStackable(_currentType) ? 99 : 1;

            _allItems.Add(item);
            ApplySearch();
            MarkDirty();
        }

        private void OnDeleteItem()
        {
            if (_selectedItem == null) return;
            if (!EditorUtility.DisplayDialog("确认删除",
                    $"删除「{_selectedItem.name}」(ID: {_selectedItem.id})？\n保存前可通过重新加载恢复。",
                    "删除", "取消")) return;

            _allItems.Remove(_selectedItem);
            _selectedItem = null;
            ApplySearch();
            MarkDirty();
        }

        private void OnSave()
        {
            string path = Path.Combine(DataFolder, $"{GetFileName(_currentType)}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            try
            {
                string json = SerializeCurrentItems();
                File.WriteAllText(path, json);
                _isDirty = false;
                UpdateStatusLabel();
                AssetDatabase.Refresh();
                Debug.Log($"[物品管理器] 已保存 {_allItems.Count} 条 {GetTypeLabel(_currentType)} 到 {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[物品管理器] 保存失败: {ex.Message}");
                EditorUtility.DisplayDialog("保存失败", ex.Message, "确定");
            }
        }

        private string SerializeCurrentItems()
        {
            var settings = new JsonSerializerSettings
            {
                Formatting           = Formatting.Indented,
                NullValueHandling    = NullValueHandling.Include,
            };
            switch (_currentType)
            {
                case ItemType.Pill:      return Serialize<PillConfig>(settings);
                case ItemType.Weapon:    return Serialize<WeaponConfig>(settings);
                case ItemType.Ammo:      return Serialize<AmmoConfig>(settings);
                case ItemType.Talisman:  return Serialize<TalismanConfig>(settings);
                case ItemType.Material:  return Serialize<MaterialConfig>(settings);
                case ItemType.Book:      return Serialize<BookConfig>(settings);
                case ItemType.Treasure:  return Serialize<TreasureConfig>(settings);
                case ItemType.Tool:      return Serialize<ToolConfig>(settings);
                case ItemType.Formation: return Serialize<FormationConfig>(settings);
                case ItemType.Quest:     return Serialize<QuestConfig>(settings);
                default:                 return "{}";
            }
        }

        private string Serialize<T>(JsonSerializerSettings settings) where T : BaseItemConfig
        {
            var wrapper = new JsonWrapper<T> { items = _allItems.OfType<T>().ToList() };
            return JsonConvert.SerializeObject(wrapper, settings);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Misc helpers
        // ════════════════════════════════════════════════════════════════════

        private void MarkDirty()
        {
            _isDirty = true;
            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel != null)
                _statusLabel.text = _isDirty ? "● 有未保存的修改" : "";
        }

        private void RefreshTabHighlight()
        {
            if (_tabBar == null) return;
            int i = 0;
            foreach (var btn in _tabBar.Children().OfType<Button>())
            {
                bool active = i < ItemTypeInfos.Length && ItemTypeInfos[i].type == _currentType;
                btn.style.backgroundColor = active
                    ? new Color(0.25f, 0.45f, 0.75f)
                    : (StyleColor)StyleKeyword.Null;
                i++;
            }
        }

        private string GetFileName(ItemType type)
        {
            foreach (var (t, _, file) in ItemTypeInfos)
                if (t == type) return file;
            return type.ToString();
        }

        private string GetTypeLabel(ItemType type)
        {
            foreach (var (t, label, _) in ItemTypeInfos)
                if (t == type) return label;
            return type.ToString();
        }

        private string MakeNewId(ItemType type)
        {
            string prefix = GetFileName(type).ToLower();
            int n = 1;
            while (_allItems.Any(i => i.id == $"{prefix}_new_{n:000}")) n++;
            return $"{prefix}_new_{n:000}";
        }

        private static BaseItemConfig CreateItem(ItemType type) => type switch
        {
            ItemType.Pill      => new PillConfig(),
            ItemType.Weapon    => new WeaponConfig(),
            ItemType.Ammo      => new AmmoConfig(),
            ItemType.Talisman  => new TalismanConfig(),
            ItemType.Material  => new MaterialConfig(),
            ItemType.Book      => new BookConfig(),
            ItemType.Treasure  => new TreasureConfig(),
            ItemType.Tool      => new ToolConfig(),
            ItemType.Formation => new FormationConfig(),
            ItemType.Quest     => new QuestConfig(),
            _                  => new PillConfig(),
        };

        private static bool DefaultStackable(ItemType type) =>
            type == ItemType.Pill     ||
            type == ItemType.Ammo     ||
            type == ItemType.Talisman ||
            type == ItemType.Material ||
            type == ItemType.Quest;

        // ════════════════════════════════════════════════════════════════════
        //  Pill table view
        // ════════════════════════════════════════════════════════════════════

        private void RebuildMainArea()
        {
            if (_mainArea == null) return;
            _mainArea.Clear();
            _tableBody = null;

            _mainArea.Add(BuildTableView(GetColumnsForType(_currentType)));

            var countLabel = rootVisualElement?.Q<Label>("countLabel");
            if (countLabel != null)
                countLabel.text = $"共 {_allItems.Count} 个  |  显示 {_filteredItems.Count} 个";
        }

        private VisualElement BuildTableView((string header, float width, bool flex)[] columns)
        {
            var outer = new VisualElement();
            outer.style.flexGrow      = 1;
            outer.style.flexDirection = FlexDirection.Column;

            // ── 搜索栏 ─────────────────────────────────────────────────────
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.flexShrink    = 0;
            searchRow.style.alignItems    = Align.Center;
            searchRow.style.paddingLeft   = 8;
            searchRow.style.paddingRight  = 8;
            searchRow.style.paddingTop    = 6;
            searchRow.style.paddingBottom = 4;

            var searchField = new TextField { value = _searchText };
            searchField.style.flexGrow = 1;
            searchField.style.maxWidth = 320;
            searchField.RegisterValueChangedCallback(e =>
            {
                _searchText = e.newValue;
                ApplySearch();
            });
            searchRow.Add(searchField);

            var countLbl = new Label();
            countLbl.name             = "countLabel";
            countLbl.style.fontSize   = 11;
            countLbl.style.color      = new Color(0.5f, 0.5f, 0.5f);
            countLbl.style.marginLeft = 12;
            countLbl.style.minWidth   = 150;
            searchRow.Add(countLbl);
            outer.Add(searchRow);

            // ── 表格 ───────────────────────────────────────────────────────
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;

            var tableWrap = new VisualElement();
            tableWrap.style.flexDirection = FlexDirection.Column;
            tableWrap.Add(BuildTableHeader(columns));

            _tableBody = new VisualElement();
            _tableBody.style.flexDirection = FlexDirection.Column;
            tableWrap.Add(_tableBody);

            scroll.Add(tableWrap);
            outer.Add(scroll);

            RefreshTableRows();
            return outer;
        }

        private VisualElement BuildTableHeader((string header, float width, bool flex)[] columns)
        {
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = new Color(0.20f, 0.20f, 0.20f);
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.12f, 0.12f, 0.12f);
            row.style.height            = 24;

            foreach (var (header, width, flex) in columns)
            {
                var cell = new Label(header);
                cell.style.width                   = width;
                cell.style.minWidth                = width;
                cell.style.height                  = 24;
                cell.style.marginTop               = 0;
                cell.style.marginBottom            = 0;
                cell.style.marginLeft              = 0;
                cell.style.marginRight             = 0;
                cell.style.paddingLeft             = 4;
                cell.style.unityFontStyleAndWeight = FontStyle.Bold;
                cell.style.color                   = new Color(0.85f, 0.75f, 0.35f);
                cell.style.unityTextAlign          = TextAnchor.MiddleLeft;
                cell.style.overflow                = Overflow.Hidden;
                if (flex) cell.style.flexGrow = 1;
                row.Add(cell);
            }
            return row;
        }

        private void RefreshTableRows()
        {
            if (_tableBody == null) return;
            _suppressCb = true;
            _tableBody.Clear();

            int rowIndex = 0;
            foreach (var item in _filteredItems)
            {
                var row = BuildItemRow(item, rowIndex++);
                if (row != null) _tableBody.Add(row);
            }
            _suppressCb = false;
        }

        private VisualElement BuildItemRow(BaseItemConfig item, int rowIndex) => item switch
        {
            PillConfig pill           => BuildPillTableRow(pill, rowIndex),
            FormationConfig formation => BuildFormationTableRow(formation, rowIndex),
            WeaponConfig weapon       => BuildWeaponTableRow(weapon, rowIndex),
            AmmoConfig ammo           => BuildAmmoTableRow(ammo, rowIndex),
            TalismanConfig talisman   => BuildTalismanTableRow(talisman, rowIndex),
            MaterialConfig material   => BuildMaterialTableRow(material, rowIndex),
            BookConfig book           => BuildBookTableRow(book, rowIndex),
            TreasureConfig treasure   => BuildTreasureTableRow(treasure, rowIndex),
            ToolConfig tool           => BuildToolTableRow(tool, rowIndex),
            QuestConfig quest         => BuildQuestTableRow(quest, rowIndex),
            _                         => null,
        };

        private (string header, float width, bool flex)[] GetColumnsForType(ItemType type) => type switch
        {
            ItemType.Pill       => PillColumns,
            ItemType.Formation  => FormationColumns,
            ItemType.Weapon     => WeaponColumns,
            ItemType.Ammo       => AmmoColumns,
            ItemType.Talisman   => TalismanColumns,
            ItemType.Material   => MaterialColumns,
            ItemType.Book       => BookColumns,
            ItemType.Treasure   => TreasureColumns,
            ItemType.Tool       => ToolColumns,
            ItemType.Quest      => QuestColumns,
            _                   => PillColumns,
        };

        private VisualElement BuildPillTableRow(PillConfig pill, int rowIndex)
        {
            bool odd = rowIndex % 2 == 1;
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = odd
                ? new Color(0.19f, 0.19f, 0.19f)
                : new Color(0.215f, 0.215f, 0.215f);
            row.style.height            = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);

            row.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedItem = pill;
                HighlightTableRow(row);
            });

            // Col 0: 图标
            {
                var iconEl = new Image { scaleMode = ScaleMode.ScaleToFit };
                StyleTableCell(iconEl, PillColumns[0].width, false);
                iconEl.style.paddingLeft   = 5;
                iconEl.style.paddingRight  = 5;
                iconEl.style.paddingTop    = 2;
                iconEl.style.paddingBottom = 2;
                var tex = LoadIconTexture(pill.id);
                if (tex != null)
                    iconEl.image = tex;
                else
                    iconEl.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f);

                iconEl.RegisterCallback<MouseEnterEvent>(_ => ShowIconTooltip(tex, iconEl));
                iconEl.RegisterCallback<MouseLeaveEvent>(_ => HideIconTooltip());
                row.Add(iconEl);
            }

            // Col 1: ID
            row.Add(MakeTableText(pill.id, PillColumns[1].width, false,
                v => { pill.id = v; MarkDirty(); }));

            // Col 2: 名称
            row.Add(MakeTableText(pill.name, PillColumns[2].width, false,
                v => { pill.name = v; MarkDirty(); }));

            // Col 3: 稀有度
            {
                var drop = new DropdownField(RarityLabels,
                    Mathf.Clamp((int)pill.rarity, 0, RarityLabels.Count - 1));
                drop.labelElement.style.display = DisplayStyle.None;
                StyleTableCell(drop, PillColumns[3].width, false);
                drop.RegisterValueChangedCallback(e =>
                {
                    if (_suppressCb) return;
                    pill.rarity = (ItemRarity)RarityLabels.IndexOf(e.newValue);
                    MarkDirty();
                });
                row.Add(drop);
            }

            // Col 4: 五行
            {
                int pi = pill.phase.HasValue ? (int)pill.phase.Value + 1 : 0;
                var drop = new DropdownField(PhaseLabelsNullable,
                    Mathf.Clamp(pi, 0, PhaseLabelsNullable.Count - 1));
                drop.labelElement.style.display = DisplayStyle.None;
                StyleTableCell(drop, PillColumns[4].width, false);
                drop.RegisterValueChangedCallback(e =>
                {
                    if (_suppressCb) return;
                    int idx = PhaseLabelsNullable.IndexOf(e.newValue);
                    pill.phase = idx == 0 ? (FivePhases?)null : (FivePhases)(idx - 1);
                    MarkDirty();
                });
                row.Add(drop);
            }

            // Col 5: 境界
            {
                int ri = pill.requiredRealm.HasValue ? (int)pill.requiredRealm.Value + 1 : 0;
                var drop = new DropdownField(RealmLabelsNullable,
                    Mathf.Clamp(ri, 0, RealmLabelsNullable.Count - 1));
                drop.labelElement.style.display = DisplayStyle.None;
                StyleTableCell(drop, PillColumns[5].width, false);
                drop.RegisterValueChangedCallback(e =>
                {
                    if (_suppressCb) return;
                    int idx = RealmLabelsNullable.IndexOf(e.newValue);
                    pill.requiredRealm = idx == 0
                        ? (CultivationRealm?)null
                        : (CultivationRealm)(idx - 1);
                    MarkDirty();
                });
                row.Add(drop);
            }

            // Col 6-9: 数值字段
            row.Add(MakeTableFloat(pill.restoreHp,          PillColumns[6].width,
                v => { pill.restoreHp          = v; MarkDirty(); }));
            row.Add(MakeTableFloat(pill.restoreMp,          PillColumns[7].width,
                v => { pill.restoreMp          = v; MarkDirty(); }));
            row.Add(MakeTableFloat(pill.restoreCultivation, PillColumns[8].width,
                v => { pill.restoreCultivation = v; MarkDirty(); }));
            row.Add(MakeTableFloat(pill.breakthroughBonus,  PillColumns[9].width,
                v => { pill.breakthroughBonus  = v; MarkDirty(); }));

            // Col 10: 增益效果
            row.Add(MakeTableText(pill.buff, PillColumns[10].width, false,
                v => { pill.buff = v; MarkDirty(); }));

            // Col 11: 持续时间
            row.Add(MakeTableFloat(pill.duration, PillColumns[11].width,
                v => { pill.duration = v; MarkDirty(); }));

            // Col 12: 可堆叠
            {
                var toggle = new Toggle { value = pill.stackable };
                toggle.labelElement.style.display = DisplayStyle.None;
                StyleTableCell(toggle, PillColumns[12].width, false);
                toggle.RegisterValueChangedCallback(e =>
                {
                    if (_suppressCb) return;
                    pill.stackable = e.newValue;
                    MarkDirty();
                });
                row.Add(toggle);
            }

            // Col 13: 最大堆叠
            {
                var f = new IntegerField { value = pill.maxStack };
                f.labelElement.style.display = DisplayStyle.None;
                StyleTableCell(f, PillColumns[13].width, false);
                f.RegisterValueChangedCallback(e =>
                {
                    if (_suppressCb) return;
                    pill.maxStack = e.newValue;
                    MarkDirty();
                });
                row.Add(f);
            }

            // Col 14: 描述（弹性）
            row.Add(MakeTableText(pill.description, PillColumns[14].width,
                PillColumns[14].flex, v => { pill.description = v; MarkDirty(); }));

            // Col 15: 删除（最右）
            var delBtn = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("确认删除",
                        $"删除「{pill.name}」？", "删除", "取消")) return;
                _allItems.Remove(pill);
                if (_selectedItem == pill) _selectedItem = null;
                ApplySearch();
                MarkDirty();
            }) { text = "×" };
            StyleTableCell(delBtn, PillColumns[15].width, false);
            row.Add(delBtn);

            return row;
        }

        private VisualElement MakeTableText(string val, float width, bool flex, Action<string> onChange)
        {
            var f = new TextField { value = val ?? "" };
            f.labelElement.style.display = DisplayStyle.None;
            StyleTableCell(f, width, flex);
            f.RegisterValueChangedCallback(e =>
            {
                if (!_suppressCb) onChange(e.newValue);
            });
            return f;
        }

        private VisualElement MakeTableFloat(float val, float width, Action<float> onChange)
        {
            var f = new FloatField { value = val };
            f.labelElement.style.display = DisplayStyle.None;
            StyleTableCell(f, width, false);
            f.RegisterValueChangedCallback(e =>
            {
                if (!_suppressCb) onChange(e.newValue);
            });
            return f;
        }

        private void StyleTableCell(VisualElement el, float width, bool flex)
        {
            el.style.width        = width;
            el.style.minWidth     = width;
            el.style.height       = 22;
            el.style.marginTop    = 1;
            el.style.marginBottom = 0;
            el.style.marginLeft   = 0;
            el.style.marginRight  = 0;
            if (flex) el.style.flexGrow = 1;
        }

        private void BuildIconTooltip()
        {
            _iconTooltip = new VisualElement();
            _iconTooltip.style.position        = Position.Absolute;
            _iconTooltip.style.width           = 96;
            _iconTooltip.style.height          = 96;
            _iconTooltip.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f, 0.97f);
            _iconTooltip.style.borderTopWidth    = 1;
            _iconTooltip.style.borderBottomWidth = 1;
            _iconTooltip.style.borderLeftWidth   = 1;
            _iconTooltip.style.borderRightWidth  = 1;
            _iconTooltip.style.borderTopColor    = new Color(0.45f, 0.45f, 0.45f);
            _iconTooltip.style.borderBottomColor = new Color(0.45f, 0.45f, 0.45f);
            _iconTooltip.style.borderLeftColor   = new Color(0.45f, 0.45f, 0.45f);
            _iconTooltip.style.borderRightColor  = new Color(0.45f, 0.45f, 0.45f);
            _iconTooltip.style.borderTopLeftRadius     = 4;
            _iconTooltip.style.borderTopRightRadius    = 4;
            _iconTooltip.style.borderBottomLeftRadius  = 4;
            _iconTooltip.style.borderBottomRightRadius = 4;
            _iconTooltip.style.paddingTop    = 4;
            _iconTooltip.style.paddingBottom = 4;
            _iconTooltip.style.paddingLeft   = 4;
            _iconTooltip.style.paddingRight  = 4;
            _iconTooltip.style.display       = DisplayStyle.None;
            _iconTooltip.pickingMode         = PickingMode.Ignore;

            _iconTooltipImage = new Image { scaleMode = ScaleMode.ScaleToFit };
            _iconTooltipImage.style.flexGrow = 1;
            _iconTooltipImage.pickingMode    = PickingMode.Ignore;
            _iconTooltip.Add(_iconTooltipImage);

            rootVisualElement.Add(_iconTooltip);
        }

        private void ShowIconTooltip(Texture2D tex, VisualElement anchor)
        {
            if (_iconTooltip == null || tex == null) return;
            _iconTooltipImage.image = tex;

            // Convert anchor's world bounds to root-local coordinates
            var wb       = anchor.worldBound;
            var rootPos  = rootVisualElement.WorldToLocal(new Vector2(wb.xMax + 6, wb.y));
            float tipW   = 96f;
            float tipH   = 96f;
            float rootW  = rootVisualElement.resolvedStyle.width;
            float rootH  = rootVisualElement.resolvedStyle.height;

            float left = rootPos.x;
            float top  = rootPos.y;
            // Keep inside window bounds
            if (left + tipW > rootW) left = rootVisualElement.WorldToLocal(new Vector2(wb.x - tipW - 6, 0)).x;
            if (top  + tipH > rootH) top  = rootH - tipH - 4;
            if (top  < 0)            top  = 0;

            _iconTooltip.style.left    = left;
            _iconTooltip.style.top     = top;
            _iconTooltip.style.display = DisplayStyle.Flex;
        }

        private void HideIconTooltip()
        {
            if (_iconTooltip != null)
                _iconTooltip.style.display = DisplayStyle.None;
        }

        // 按 id 搜索图标时依次尝试的 Resources 子目录
        private static readonly string[] IconSearchPaths =
        {
            "Icons/Pills",
            "Icons",
        };

        private static Texture2D LoadIconTexture(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var folder in IconSearchPaths)
            {
                var tex = Resources.Load<Texture2D>($"{folder}/{id}");
                if (tex != null) return tex;
            }
            return null;
        }

        private void HighlightTableRow(VisualElement selectedRow)
        {
            if (_tableBody == null) return;
            int idx = 0;
            foreach (var child in _tableBody.Children())
            {
                bool isSelected = child == selectedRow;
                bool odd        = idx % 2 == 1;
                child.style.backgroundColor = isSelected
                    ? new Color(0.25f, 0.40f, 0.65f)
                    : odd
                        ? new Color(0.19f, 0.19f, 0.19f)
                        : new Color(0.215f, 0.215f, 0.215f);
                idx++;
            }
        }

        private VisualElement BuildFormationTableRow(FormationConfig formation, int rowIndex)
        {
            bool odd = rowIndex % 2 == 1;
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = odd
                ? new Color(0.19f, 0.19f, 0.19f)
                : new Color(0.215f, 0.215f, 0.215f);
            row.style.height            = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);

            row.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedItem = formation;
                HighlightTableRow(row);
            });

            // Col 0: ID
            row.Add(MakeTableText(formation.id, FormationColumns[0].width, false,
                v => { formation.id = v; MarkDirty(); }));

            // Col 1: 名称
            row.Add(MakeTableText(formation.name, FormationColumns[1].width, false,
                v => { formation.name = v; MarkDirty(); }));

            // Col 2: 稀有度
            {
                var drop = new DropdownField(RarityLabels,
                    Mathf.Clamp((int)formation.rarity, 0, RarityLabels.Count - 1));
                drop.labelElement.style.display = DisplayStyle.None;
                StyleTableCell(drop, FormationColumns[2].width, false);
                drop.RegisterValueChangedCallback(e =>
                {
                    if (_suppressCb) return;
                    formation.rarity = (ItemRarity)RarityLabels.IndexOf(e.newValue);
                    MarkDirty();
                });
                row.Add(drop);
            }

            // Col 3: 描述（弹性）
            row.Add(MakeTableText(formation.description, FormationColumns[3].width,
                FormationColumns[3].flex, v => { formation.description = v; MarkDirty(); }));

            // Col 4: 删除
            var delBtn = new Button(() =>
            {
                if (!EditorUtility.DisplayDialog("确认删除",
                        $"删除「{formation.name}」？", "删除", "取消")) return;
                _allItems.Remove(formation);
                if (_selectedItem == formation) _selectedItem = null;
                ApplySearch();
                MarkDirty();
            }) { text = "×" };
            StyleTableCell(delBtn, FormationColumns[4].width, false);
            row.Add(delBtn);

            return row;
        }

        private VisualElement BuildWeaponTableRow(WeaponConfig weapon, int rowIndex)
        {
            bool odd = rowIndex % 2 == 1;
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = odd ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.215f, 0.215f, 0.215f);
            row.style.height            = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            row.RegisterCallback<ClickEvent>(_ => { _selectedItem = weapon; HighlightTableRow(row); });

            row.Add(MakeTableText(weapon.id,            WeaponColumns[0].width, false, v => { weapon.id            = v; MarkDirty(); }));
            row.Add(MakeTableText(weapon.name,          WeaponColumns[1].width, false, v => { weapon.name          = v; MarkDirty(); }));

            { var drop = new DropdownField(RarityLabels, Mathf.Clamp((int)weapon.rarity, 0, RarityLabels.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, WeaponColumns[2].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; weapon.rarity = (ItemRarity)RarityLabels.IndexOf(e.newValue); MarkDirty(); }); row.Add(drop); }
            { int pi = weapon.phase.HasValue ? (int)weapon.phase.Value + 1 : 0; var drop = new DropdownField(PhaseLabelsNullable, Mathf.Clamp(pi, 0, PhaseLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, WeaponColumns[3].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = PhaseLabelsNullable.IndexOf(e.newValue); weapon.phase = idx == 0 ? (FivePhases?)null : (FivePhases)(idx - 1); MarkDirty(); }); row.Add(drop); }
            { int ri = weapon.requiredRealm.HasValue ? (int)weapon.requiredRealm.Value + 1 : 0; var drop = new DropdownField(RealmLabelsNullable, Mathf.Clamp(ri, 0, RealmLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, WeaponColumns[4].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = RealmLabelsNullable.IndexOf(e.newValue); weapon.requiredRealm = idx == 0 ? (CultivationRealm?)null : (CultivationRealm)(idx - 1); MarkDirty(); }); row.Add(drop); }

            row.Add(MakeTableFloat(weapon.attack,        WeaponColumns[5].width, v => { weapon.attack        = v; MarkDirty(); }));
            row.Add(MakeTableFloat(weapon.range,         WeaponColumns[6].width, v => { weapon.range         = v; MarkDirty(); }));
            row.Add(MakeTableText(weapon.ammoType,       WeaponColumns[7].width, false, v => { weapon.ammoType      = v; MarkDirty(); }));
            row.Add(MakeTableText(weapon.specialEffect,  WeaponColumns[8].width, false, v => { weapon.specialEffect = v; MarkDirty(); }));
            row.Add(MakeTableText(weapon.description,    WeaponColumns[9].width, WeaponColumns[9].flex, v => { weapon.description = v; MarkDirty(); }));

            var delBtn = new Button(() => { if (!EditorUtility.DisplayDialog("确认删除", $"删除「{weapon.name}」？", "删除", "取消")) return; _allItems.Remove(weapon); if (_selectedItem == weapon) _selectedItem = null; ApplySearch(); MarkDirty(); }) { text = "×" };
            StyleTableCell(delBtn, WeaponColumns[10].width, false);
            row.Add(delBtn);
            return row;
        }

        private VisualElement BuildAmmoTableRow(AmmoConfig ammo, int rowIndex)
        {
            bool odd = rowIndex % 2 == 1;
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = odd ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.215f, 0.215f, 0.215f);
            row.style.height            = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            row.RegisterCallback<ClickEvent>(_ => { _selectedItem = ammo; HighlightTableRow(row); });

            row.Add(MakeTableText(ammo.id,   AmmoColumns[0].width, false, v => { ammo.id   = v; MarkDirty(); }));
            row.Add(MakeTableText(ammo.name, AmmoColumns[1].width, false, v => { ammo.name = v; MarkDirty(); }));

            { var drop = new DropdownField(RarityLabels, Mathf.Clamp((int)ammo.rarity, 0, RarityLabels.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, AmmoColumns[2].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; ammo.rarity = (ItemRarity)RarityLabels.IndexOf(e.newValue); MarkDirty(); }); row.Add(drop); }
            { int pi = ammo.phase.HasValue ? (int)ammo.phase.Value + 1 : 0; var drop = new DropdownField(PhaseLabelsNullable, Mathf.Clamp(pi, 0, PhaseLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, AmmoColumns[3].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = PhaseLabelsNullable.IndexOf(e.newValue); ammo.phase = idx == 0 ? (FivePhases?)null : (FivePhases)(idx - 1); MarkDirty(); }); row.Add(drop); }
            { int ri = ammo.requiredRealm.HasValue ? (int)ammo.requiredRealm.Value + 1 : 0; var drop = new DropdownField(RealmLabelsNullable, Mathf.Clamp(ri, 0, RealmLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, AmmoColumns[4].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = RealmLabelsNullable.IndexOf(e.newValue); ammo.requiredRealm = idx == 0 ? (CultivationRealm?)null : (CultivationRealm)(idx - 1); MarkDirty(); }); row.Add(drop); }

            row.Add(MakeTableFloat(ammo.damage, AmmoColumns[5].width, v => { ammo.damage = v; MarkDirty(); }));

            { var toggle = new Toggle { value = ammo.stackable }; toggle.labelElement.style.display = DisplayStyle.None; StyleTableCell(toggle, AmmoColumns[6].width, false); toggle.RegisterValueChangedCallback(e => { if (_suppressCb) return; ammo.stackable = e.newValue; MarkDirty(); }); row.Add(toggle); }
            { var f = new IntegerField { value = ammo.maxStack }; f.labelElement.style.display = DisplayStyle.None; StyleTableCell(f, AmmoColumns[7].width, false); f.RegisterValueChangedCallback(e => { if (_suppressCb) return; ammo.maxStack = e.newValue; MarkDirty(); }); row.Add(f); }

            // compatibleWeapons as comma-separated text
            {
                string joined = ammo.compatibleWeapons != null ? string.Join(",", ammo.compatibleWeapons) : "";
                var f = new TextField { value = joined };
                f.labelElement.style.display = DisplayStyle.None;
                StyleTableCell(f, AmmoColumns[8].width, false);
                f.RegisterValueChangedCallback(e =>
                {
                    if (_suppressCb) return;
                    ammo.compatibleWeapons = new System.Collections.Generic.List<string>(
                        e.newValue.Split(new[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries));
                    MarkDirty();
                });
                row.Add(f);
            }

            row.Add(MakeTableText(ammo.description, AmmoColumns[9].width, AmmoColumns[9].flex, v => { ammo.description = v; MarkDirty(); }));

            var delBtn = new Button(() => { if (!EditorUtility.DisplayDialog("确认删除", $"删除「{ammo.name}」？", "删除", "取消")) return; _allItems.Remove(ammo); if (_selectedItem == ammo) _selectedItem = null; ApplySearch(); MarkDirty(); }) { text = "×" };
            StyleTableCell(delBtn, AmmoColumns[10].width, false);
            row.Add(delBtn);
            return row;
        }

        private VisualElement BuildTalismanTableRow(TalismanConfig talisman, int rowIndex)
        {
            bool odd = rowIndex % 2 == 1;
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = odd ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.215f, 0.215f, 0.215f);
            row.style.height            = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            row.RegisterCallback<ClickEvent>(_ => { _selectedItem = talisman; HighlightTableRow(row); });

            row.Add(MakeTableText(talisman.id,   TalismanColumns[0].width, false, v => { talisman.id   = v; MarkDirty(); }));
            row.Add(MakeTableText(talisman.name, TalismanColumns[1].width, false, v => { talisman.name = v; MarkDirty(); }));

            { var drop = new DropdownField(RarityLabels, Mathf.Clamp((int)talisman.rarity, 0, RarityLabels.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, TalismanColumns[2].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; talisman.rarity = (ItemRarity)RarityLabels.IndexOf(e.newValue); MarkDirty(); }); row.Add(drop); }
            { int pi = talisman.phase.HasValue ? (int)talisman.phase.Value + 1 : 0; var drop = new DropdownField(PhaseLabelsNullable, Mathf.Clamp(pi, 0, PhaseLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, TalismanColumns[3].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = PhaseLabelsNullable.IndexOf(e.newValue); talisman.phase = idx == 0 ? (FivePhases?)null : (FivePhases)(idx - 1); MarkDirty(); }); row.Add(drop); }
            { int ri = talisman.requiredRealm.HasValue ? (int)talisman.requiredRealm.Value + 1 : 0; var drop = new DropdownField(RealmLabelsNullable, Mathf.Clamp(ri, 0, RealmLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, TalismanColumns[4].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = RealmLabelsNullable.IndexOf(e.newValue); talisman.requiredRealm = idx == 0 ? (CultivationRealm?)null : (CultivationRealm)(idx - 1); MarkDirty(); }); row.Add(drop); }

            row.Add(MakeTableText(talisman.effect,  TalismanColumns[5].width, false, v => { talisman.effect   = v; MarkDirty(); }));
            row.Add(MakeTableFloat(talisman.duration, TalismanColumns[6].width, v => { talisman.duration = v; MarkDirty(); }));

            { var toggle = new Toggle { value = talisman.stackable }; toggle.labelElement.style.display = DisplayStyle.None; StyleTableCell(toggle, TalismanColumns[7].width, false); toggle.RegisterValueChangedCallback(e => { if (_suppressCb) return; talisman.stackable = e.newValue; MarkDirty(); }); row.Add(toggle); }
            { var f = new IntegerField { value = talisman.maxStack }; f.labelElement.style.display = DisplayStyle.None; StyleTableCell(f, TalismanColumns[8].width, false); f.RegisterValueChangedCallback(e => { if (_suppressCb) return; talisman.maxStack = e.newValue; MarkDirty(); }); row.Add(f); }

            row.Add(MakeTableText(talisman.description, TalismanColumns[9].width, TalismanColumns[9].flex, v => { talisman.description = v; MarkDirty(); }));

            var delBtn = new Button(() => { if (!EditorUtility.DisplayDialog("确认删除", $"删除「{talisman.name}」？", "删除", "取消")) return; _allItems.Remove(talisman); if (_selectedItem == talisman) _selectedItem = null; ApplySearch(); MarkDirty(); }) { text = "×" };
            StyleTableCell(delBtn, TalismanColumns[10].width, false);
            row.Add(delBtn);
            return row;
        }

        private VisualElement BuildMaterialTableRow(MaterialConfig material, int rowIndex)
        {
            bool odd = rowIndex % 2 == 1;
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = odd ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.215f, 0.215f, 0.215f);
            row.style.height            = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            row.RegisterCallback<ClickEvent>(_ => { _selectedItem = material; HighlightTableRow(row); });

            row.Add(MakeTableText(material.id,   MaterialColumns[0].width, false, v => { material.id   = v; MarkDirty(); }));
            row.Add(MakeTableText(material.name, MaterialColumns[1].width, false, v => { material.name = v; MarkDirty(); }));

            { var drop = new DropdownField(RarityLabels, Mathf.Clamp((int)material.rarity, 0, RarityLabels.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, MaterialColumns[2].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; material.rarity = (ItemRarity)RarityLabels.IndexOf(e.newValue); MarkDirty(); }); row.Add(drop); }
            { int pi = material.phase.HasValue ? (int)material.phase.Value + 1 : 0; var drop = new DropdownField(PhaseLabelsNullable, Mathf.Clamp(pi, 0, PhaseLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, MaterialColumns[3].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = PhaseLabelsNullable.IndexOf(e.newValue); material.phase = idx == 0 ? (FivePhases?)null : (FivePhases)(idx - 1); MarkDirty(); }); row.Add(drop); }
            { int ri = material.requiredRealm.HasValue ? (int)material.requiredRealm.Value + 1 : 0; var drop = new DropdownField(RealmLabelsNullable, Mathf.Clamp(ri, 0, RealmLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, MaterialColumns[4].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = RealmLabelsNullable.IndexOf(e.newValue); material.requiredRealm = idx == 0 ? (CultivationRealm?)null : (CultivationRealm)(idx - 1); MarkDirty(); }); row.Add(drop); }

            { var f = new IntegerField { value = material.grade }; f.labelElement.style.display = DisplayStyle.None; StyleTableCell(f, MaterialColumns[5].width, false); f.RegisterValueChangedCallback(e => { if (_suppressCb) return; material.grade = e.newValue; MarkDirty(); }); row.Add(f); }

            { var toggle = new Toggle { value = material.stackable }; toggle.labelElement.style.display = DisplayStyle.None; StyleTableCell(toggle, MaterialColumns[6].width, false); toggle.RegisterValueChangedCallback(e => { if (_suppressCb) return; material.stackable = e.newValue; MarkDirty(); }); row.Add(toggle); }
            { var f = new IntegerField { value = material.maxStack }; f.labelElement.style.display = DisplayStyle.None; StyleTableCell(f, MaterialColumns[7].width, false); f.RegisterValueChangedCallback(e => { if (_suppressCb) return; material.maxStack = e.newValue; MarkDirty(); }); row.Add(f); }

            row.Add(MakeTableText(material.description, MaterialColumns[8].width, MaterialColumns[8].flex, v => { material.description = v; MarkDirty(); }));

            var delBtn = new Button(() => { if (!EditorUtility.DisplayDialog("确认删除", $"删除「{material.name}」？", "删除", "取消")) return; _allItems.Remove(material); if (_selectedItem == material) _selectedItem = null; ApplySearch(); MarkDirty(); }) { text = "×" };
            StyleTableCell(delBtn, MaterialColumns[9].width, false);
            row.Add(delBtn);
            return row;
        }

        private static readonly List<string> SkillTypeLabels = new List<string> { "主动", "被动" };

        private VisualElement BuildBookTableRow(BookConfig book, int rowIndex)
        {
            bool odd = rowIndex % 2 == 1;
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = odd ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.215f, 0.215f, 0.215f);
            row.style.height            = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            row.RegisterCallback<ClickEvent>(_ => { _selectedItem = book; HighlightTableRow(row); });

            row.Add(MakeTableText(book.id,   BookColumns[0].width, false, v => { book.id   = v; MarkDirty(); }));
            row.Add(MakeTableText(book.name, BookColumns[1].width, false, v => { book.name = v; MarkDirty(); }));

            { var drop = new DropdownField(RarityLabels, Mathf.Clamp((int)book.rarity, 0, RarityLabels.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, BookColumns[2].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; book.rarity = (ItemRarity)RarityLabels.IndexOf(e.newValue); MarkDirty(); }); row.Add(drop); }
            { int pi = book.phase.HasValue ? (int)book.phase.Value + 1 : 0; var drop = new DropdownField(PhaseLabelsNullable, Mathf.Clamp(pi, 0, PhaseLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, BookColumns[3].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = PhaseLabelsNullable.IndexOf(e.newValue); book.phase = idx == 0 ? (FivePhases?)null : (FivePhases)(idx - 1); MarkDirty(); }); row.Add(drop); }
            { int ri = book.requiredRealm.HasValue ? (int)book.requiredRealm.Value + 1 : 0; var drop = new DropdownField(RealmLabelsNullable, Mathf.Clamp(ri, 0, RealmLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, BookColumns[4].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = RealmLabelsNullable.IndexOf(e.newValue); book.requiredRealm = idx == 0 ? (CultivationRealm?)null : (CultivationRealm)(idx - 1); MarkDirty(); }); row.Add(drop); }

            row.Add(MakeTableText(book.skillName, BookColumns[5].width, false, v => { book.skillName = v; MarkDirty(); }));

            { var drop = new DropdownField(SkillTypeLabels, Mathf.Clamp((int)book.skillType, 0, SkillTypeLabels.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, BookColumns[6].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; book.skillType = (BookConfig.SkillType)SkillTypeLabels.IndexOf(e.newValue); MarkDirty(); }); row.Add(drop); }

            { var f = new IntegerField { value = book.requiredLevel }; f.labelElement.style.display = DisplayStyle.None; StyleTableCell(f, BookColumns[7].width, false); f.RegisterValueChangedCallback(e => { if (_suppressCb) return; book.requiredLevel = e.newValue; MarkDirty(); }); row.Add(f); }

            row.Add(MakeTableText(book.description, BookColumns[8].width, BookColumns[8].flex, v => { book.description = v; MarkDirty(); }));

            var delBtn = new Button(() => { if (!EditorUtility.DisplayDialog("确认删除", $"删除「{book.name}」？", "删除", "取消")) return; _allItems.Remove(book); if (_selectedItem == book) _selectedItem = null; ApplySearch(); MarkDirty(); }) { text = "×" };
            StyleTableCell(delBtn, BookColumns[9].width, false);
            row.Add(delBtn);
            return row;
        }

        private VisualElement BuildTreasureTableRow(TreasureConfig treasure, int rowIndex)
        {
            bool odd = rowIndex % 2 == 1;
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = odd ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.215f, 0.215f, 0.215f);
            row.style.height            = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            row.RegisterCallback<ClickEvent>(_ => { _selectedItem = treasure; HighlightTableRow(row); });

            row.Add(MakeTableText(treasure.id,   TreasureColumns[0].width, false, v => { treasure.id   = v; MarkDirty(); }));
            row.Add(MakeTableText(treasure.name, TreasureColumns[1].width, false, v => { treasure.name = v; MarkDirty(); }));

            { var drop = new DropdownField(RarityLabels, Mathf.Clamp((int)treasure.rarity, 0, RarityLabels.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, TreasureColumns[2].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; treasure.rarity = (ItemRarity)RarityLabels.IndexOf(e.newValue); MarkDirty(); }); row.Add(drop); }
            { int pi = treasure.phase.HasValue ? (int)treasure.phase.Value + 1 : 0; var drop = new DropdownField(PhaseLabelsNullable, Mathf.Clamp(pi, 0, PhaseLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, TreasureColumns[3].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = PhaseLabelsNullable.IndexOf(e.newValue); treasure.phase = idx == 0 ? (FivePhases?)null : (FivePhases)(idx - 1); MarkDirty(); }); row.Add(drop); }
            { int ri = treasure.requiredRealm.HasValue ? (int)treasure.requiredRealm.Value + 1 : 0; var drop = new DropdownField(RealmLabelsNullable, Mathf.Clamp(ri, 0, RealmLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, TreasureColumns[4].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = RealmLabelsNullable.IndexOf(e.newValue); treasure.requiredRealm = idx == 0 ? (CultivationRealm?)null : (CultivationRealm)(idx - 1); MarkDirty(); }); row.Add(drop); }

            row.Add(MakeTableFloat(treasure.power, TreasureColumns[5].width, v => { treasure.power = v; MarkDirty(); }));
            row.Add(MakeTableText(treasure.uniqueEffect, TreasureColumns[6].width, false, v => { treasure.uniqueEffect = v; MarkDirty(); }));
            row.Add(MakeTableText(treasure.description,  TreasureColumns[7].width, TreasureColumns[7].flex, v => { treasure.description = v; MarkDirty(); }));

            var delBtn = new Button(() => { if (!EditorUtility.DisplayDialog("确认删除", $"删除「{treasure.name}」？", "删除", "取消")) return; _allItems.Remove(treasure); if (_selectedItem == treasure) _selectedItem = null; ApplySearch(); MarkDirty(); }) { text = "×" };
            StyleTableCell(delBtn, TreasureColumns[8].width, false);
            row.Add(delBtn);
            return row;
        }

        private VisualElement BuildToolTableRow(ToolConfig tool, int rowIndex)
        {
            bool odd = rowIndex % 2 == 1;
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = odd ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.215f, 0.215f, 0.215f);
            row.style.height            = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            row.RegisterCallback<ClickEvent>(_ => { _selectedItem = tool; HighlightTableRow(row); });

            row.Add(MakeTableText(tool.id,   ToolColumns[0].width, false, v => { tool.id   = v; MarkDirty(); }));
            row.Add(MakeTableText(tool.name, ToolColumns[1].width, false, v => { tool.name = v; MarkDirty(); }));

            { var drop = new DropdownField(RarityLabels, Mathf.Clamp((int)tool.rarity, 0, RarityLabels.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, ToolColumns[2].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; tool.rarity = (ItemRarity)RarityLabels.IndexOf(e.newValue); MarkDirty(); }); row.Add(drop); }
            { int pi = tool.phase.HasValue ? (int)tool.phase.Value + 1 : 0; var drop = new DropdownField(PhaseLabelsNullable, Mathf.Clamp(pi, 0, PhaseLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, ToolColumns[3].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = PhaseLabelsNullable.IndexOf(e.newValue); tool.phase = idx == 0 ? (FivePhases?)null : (FivePhases)(idx - 1); MarkDirty(); }); row.Add(drop); }
            { int ri = tool.requiredRealm.HasValue ? (int)tool.requiredRealm.Value + 1 : 0; var drop = new DropdownField(RealmLabelsNullable, Mathf.Clamp(ri, 0, RealmLabelsNullable.Count - 1)); drop.labelElement.style.display = DisplayStyle.None; StyleTableCell(drop, ToolColumns[4].width, false); drop.RegisterValueChangedCallback(e => { if (_suppressCb) return; int idx = RealmLabelsNullable.IndexOf(e.newValue); tool.requiredRealm = idx == 0 ? (CultivationRealm?)null : (CultivationRealm)(idx - 1); MarkDirty(); }); row.Add(drop); }

            row.Add(MakeTableFloat(tool.efficiency, ToolColumns[5].width, v => { tool.efficiency = v; MarkDirty(); }));
            row.Add(MakeTableFloat(tool.durability, ToolColumns[6].width, v => { tool.durability = v; MarkDirty(); }));
            row.Add(MakeTableText(tool.description,  ToolColumns[7].width, ToolColumns[7].flex, v => { tool.description = v; MarkDirty(); }));

            var delBtn = new Button(() => { if (!EditorUtility.DisplayDialog("确认删除", $"删除「{tool.name}」？", "删除", "取消")) return; _allItems.Remove(tool); if (_selectedItem == tool) _selectedItem = null; ApplySearch(); MarkDirty(); }) { text = "×" };
            StyleTableCell(delBtn, ToolColumns[8].width, false);
            row.Add(delBtn);
            return row;
        }

        private VisualElement BuildQuestTableRow(QuestConfig quest, int rowIndex)
        {
            bool odd = rowIndex % 2 == 1;
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = odd ? new Color(0.19f, 0.19f, 0.19f) : new Color(0.215f, 0.215f, 0.215f);
            row.style.height            = 24;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.15f, 0.15f, 0.15f);
            row.RegisterCallback<ClickEvent>(_ => { _selectedItem = quest; HighlightTableRow(row); });

            row.Add(MakeTableText(quest.id,      QuestColumns[0].width, false, v => { quest.id      = v; MarkDirty(); }));
            row.Add(MakeTableText(quest.name,    QuestColumns[1].width, false, v => { quest.name    = v; MarkDirty(); }));
            row.Add(MakeTableText(quest.questId, QuestColumns[2].width, false, v => { quest.questId = v; MarkDirty(); }));
            row.Add(MakeTableText(quest.description, QuestColumns[3].width, QuestColumns[3].flex, v => { quest.description = v; MarkDirty(); }));

            var delBtn = new Button(() => { if (!EditorUtility.DisplayDialog("确认删除", $"删除「{quest.name}」？", "删除", "取消")) return; _allItems.Remove(quest); if (_selectedItem == quest) _selectedItem = null; ApplySearch(); MarkDirty(); }) { text = "×" };
            StyleTableCell(delBtn, QuestColumns[4].width, false);
            row.Add(delBtn);
            return row;
        }

        [Serializable]
        private class JsonWrapper<T> { public List<T> items; }
    }
}
