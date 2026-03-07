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

        // ── 状态 ────────────────────────────────────────────────────────────
        private ItemType         _currentType    = ItemType.Pill;
        private List<BaseItem>   _allItems       = new List<BaseItem>();
        private List<BaseItem>   _filteredItems  = new List<BaseItem>();
        private BaseItem         _selectedItem;
        private bool             _isDirty;
        private bool             _suppressCb;   // suppress field callbacks while rebuilding form
        private string           _searchText    = "";

        // ── UI 引用 ──────────────────────────────────────────────────────────
        private VisualElement    _tabBar;
        private ListView         _listView;
        private VisualElement    _detailContainer;
        private Label            _statusLabel;
        private VisualElement    _mainArea;
        private VisualElement    _pillTableBody;
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

        private VisualElement BuildLeftPanel()
        {
            var panel = new VisualElement();
            panel.style.width            = 280;
            panel.style.flexShrink       = 0;
            panel.style.flexDirection    = FlexDirection.Column;
            panel.style.borderRightWidth = 1;
            panel.style.borderRightColor = new Color(0.12f, 0.12f, 0.12f);

            // Search
            var search = new TextField { value = _searchText };
            search.style.marginLeft   = 6;
            search.style.marginRight  = 6;
            search.style.marginTop    = 6;
            search.style.marginBottom = 4;
            search.RegisterValueChangedCallback(e =>
            {
                _searchText = e.newValue;
                ApplySearch();
            });
            panel.Add(search);

            // Count
            var countLabel = new Label();
            countLabel.name                    = "countLabel";
            countLabel.style.fontSize          = 11;
            countLabel.style.color             = new Color(0.5f, 0.5f, 0.5f);
            countLabel.style.marginLeft        = 8;
            countLabel.style.marginBottom      = 2;
            panel.Add(countLabel);

            // ListView
            _listView = new ListView
            {
                makeItem        = MakeListItem,
                bindItem        = BindListItem,
                selectionType   = SelectionType.Single,
                fixedItemHeight = 28,
            };
            _listView.style.flexGrow   = 1;
            _listView.itemsSource      = _filteredItems;
            _listView.selectionChanged += OnListSelectionChanged;
            panel.Add(_listView);

            return panel;
        }

        private VisualElement BuildRightPanel()
        {
            var panel = new VisualElement();
            panel.style.flexGrow      = 1;
            panel.style.flexDirection = FlexDirection.Column;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.contentContainer.style.paddingLeft   = 16;
            scroll.contentContainer.style.paddingRight  = 16;
            scroll.contentContainer.style.paddingTop    = 12;
            scroll.contentContainer.style.paddingBottom = 12;

            _detailContainer = scroll.contentContainer;
            panel.Add(scroll);
            ShowEmptyState();
            return panel;
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
        //  ListView
        // ════════════════════════════════════════════════════════════════════

        private VisualElement MakeListItem()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;
            row.style.paddingLeft   = 8;
            row.style.paddingRight  = 8;
            row.style.height        = 28;

            var bar = new VisualElement();
            bar.name                              = "rarityBar";
            bar.style.width                       = 4;
            bar.style.height                      = 18;
            bar.style.borderTopLeftRadius         = 2;
            bar.style.borderBottomLeftRadius      = 2;
            bar.style.borderTopRightRadius        = 2;
            bar.style.borderBottomRightRadius     = 2;
            bar.style.marginRight                 = 8;
            row.Add(bar);

            var name = new Label();
            name.name              = "nameLabel";
            name.style.flexGrow    = 1;
            name.style.fontSize    = 12;
            row.Add(name);

            var id = new Label();
            id.name            = "idLabel";
            id.style.color     = new Color(0.45f, 0.45f, 0.45f);
            id.style.fontSize  = 10;
            row.Add(id);

            return row;
        }

        private void BindListItem(VisualElement el, int index)
        {
            if (index < 0 || index >= _filteredItems.Count) return;
            var item = _filteredItems[index];

            el.Q<Label>("nameLabel").text = string.IsNullOrEmpty(item.name) ? "(未命名)" : item.name;
            el.Q<Label>("idLabel").text   = item.id ?? "";

            int ri = (int)item.rarity;
            var color = ri >= 0 && ri < RarityColors.Length ? RarityColors[ri] : Color.gray;
            el.Q<VisualElement>("rarityBar").style.backgroundColor = color;
        }

        private void OnListSelectionChanged(IEnumerable<object> objs)
        {
            _selectedItem = objs.FirstOrDefault() as BaseItem;
            BuildDetailPanel();
        }

        // ════════════════════════════════════════════════════════════════════
        //  Detail panel
        // ════════════════════════════════════════════════════════════════════

        private void ShowEmptyState()
        {
            _detailContainer.Clear();
            var lbl = new Label("← 从左侧列表中选择物品进行编辑");
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            lbl.style.color          = new Color(0.42f, 0.42f, 0.42f);
            lbl.style.marginTop      = 80;
            lbl.style.fontSize       = 13;
            _detailContainer.Add(lbl);
        }

        private void BuildDetailPanel()
        {
            _detailContainer.Clear();
            if (_selectedItem == null) { ShowEmptyState(); return; }

            _suppressCb = true;

            // ── 标题 ──────────────────────────────────────────────────────
            var header = new Label(string.IsNullOrEmpty(_selectedItem.name) ? "新物品" : _selectedItem.name);
            header.name                                 = "detailHeader";
            header.style.fontSize                       = 17;
            header.style.unityFontStyleAndWeight        = FontStyle.Bold;
            header.style.marginBottom                   = 12;
            _detailContainer.Add(header);

            // ── 基础属性 ──────────────────────────────────────────────────
            AddSectionHeader("基础属性");

            AddStringRow("ID", _selectedItem.id, v =>
            {
                _selectedItem.id = v;
                MarkDirty();
            });

            AddStringRow("名称", _selectedItem.name, v =>
            {
                _selectedItem.name  = v;
                header.text         = string.IsNullOrEmpty(v) ? "新物品" : v;
                RefreshListItem(_selectedItem);
                MarkDirty();
            });

            // 稀有度
            var rarityDrop = MakeDropdown("稀有度", RarityLabels, (int)_selectedItem.rarity, idx =>
            {
                _selectedItem.rarity = (ItemRarity)idx;
                RefreshListItem(_selectedItem);
                MarkDirty();
            });
            _detailContainer.Add(rarityDrop);

            // 五行属性 (nullable)
            int phaseIdx = _selectedItem.phase.HasValue ? (int)_selectedItem.phase.Value + 1 : 0;
            var phaseDrop = MakeDropdown("五行属性", PhaseLabelsNullable, phaseIdx, idx =>
            {
                _selectedItem.phase = idx == 0 ? (FivePhases?)null : (FivePhases)(idx - 1);
                MarkDirty();
            });
            _detailContainer.Add(phaseDrop);

            // 需要境界 (nullable)
            int realmIdx = _selectedItem.requiredRealm.HasValue ? (int)_selectedItem.requiredRealm.Value + 1 : 0;
            var realmDrop = MakeDropdown("需要境界", RealmLabelsNullable, realmIdx, idx =>
            {
                _selectedItem.requiredRealm = idx == 0 ? (CultivationRealm?)null : (CultivationRealm)(idx - 1);
                MarkDirty();
            });
            _detailContainer.Add(realmDrop);

            // 描述
            var descField = new TextField("描述") { value = _selectedItem.description ?? "", multiline = true };
            descField.style.marginBottom = 4;
            descField.style.minHeight    = 60;
            var input = descField.Q<VisualElement>("unity-text-input");
            if (input != null) input.style.minHeight = 54;
            descField.RegisterValueChangedCallback(e =>
            {
                if (_suppressCb) return;
                _selectedItem.description = e.newValue;
                MarkDirty();
            });
            _detailContainer.Add(descField);

            // 图标路径
            AddStringRow("图标路径", _selectedItem.icon, v => { _selectedItem.icon = v; MarkDirty(); });

            // 堆叠
            var stackRow = new VisualElement();
            stackRow.style.flexDirection = FlexDirection.Row;
            stackRow.style.marginBottom  = 4;
            stackRow.style.alignItems    = Align.Center;

            var stackToggle = new Toggle("可堆叠") { value = _selectedItem.stackable };
            stackToggle.style.flexGrow = 1;
            stackToggle.RegisterValueChangedCallback(e =>
            {
                if (_suppressCb) return;
                _selectedItem.stackable = e.newValue;
                MarkDirty();
            });
            stackRow.Add(stackToggle);

            var maxField = new IntegerField("最大堆叠") { value = _selectedItem.maxStack };
            maxField.style.flexGrow = 1;
            maxField.RegisterValueChangedCallback(e =>
            {
                if (_suppressCb) return;
                _selectedItem.maxStack = e.newValue;
                MarkDirty();
            });
            stackRow.Add(maxField);
            _detailContainer.Add(stackRow);

            // ── 类型专属字段 ─────────────────────────────────────────────
            BuildTypeSection(_selectedItem);

            _suppressCb = false;
        }

        private void BuildTypeSection(BaseItem item)
        {
            switch (item)
            {
                case PillItem pill:
                    AddSectionHeader("丹药属性");
                    AddFloatRow("恢复气血",      pill.restoreHp,           v => { pill.restoreHp          = v; MarkDirty(); });
                    AddFloatRow("恢复灵力",      pill.restoreMp,           v => { pill.restoreMp          = v; MarkDirty(); });
                    AddFloatRow("增加修为",      pill.restoreCultivation,  v => { pill.restoreCultivation = v; MarkDirty(); });
                    AddFloatRow("突破加成",      pill.breakthroughBonus,   v => { pill.breakthroughBonus  = v; MarkDirty(); });
                    AddStringRow("增益效果",     pill.buff,                v => { pill.buff               = v; MarkDirty(); });
                    AddFloatRow("持续时间 (秒)", pill.duration,            v => { pill.duration           = v; MarkDirty(); });
                    break;

                case WeaponItem weapon:
                    AddSectionHeader("法器属性");
                    AddFloatRow("攻击力",   weapon.attack,        v => { weapon.attack        = v; MarkDirty(); });
                    AddFloatRow("射程",     weapon.range,         v => { weapon.range         = v; MarkDirty(); });
                    AddStringRow("弹药类型 ID", weapon.ammoType,  v => { weapon.ammoType      = v; MarkDirty(); });
                    AddStringRow("特殊效果", weapon.specialEffect, v => { weapon.specialEffect = v; MarkDirty(); });
                    break;

                case AmmoItem ammo:
                    AddSectionHeader("弹药属性");
                    AddFloatRow("伤害", ammo.damage, v => { ammo.damage = v; MarkDirty(); });
                    AddStringListSection("兼容法器 ID 列表", ammo.compatibleWeapons);
                    break;

                case TalismanItem talisman:
                    AddSectionHeader("符箓属性");
                    AddStringRow("效果", talisman.effect,             v => { talisman.effect   = v; MarkDirty(); });
                    AddFloatRow("持续时间 (秒)", talisman.duration,   v => { talisman.duration = v; MarkDirty(); });
                    break;

                case MaterialItem material:
                    AddSectionHeader("材料属性");
                    AddIntRow("品阶", material.grade, v => { material.grade = v; MarkDirty(); });
                    break;

                case BookItem book:
                    AddSectionHeader("功法属性");
                    AddStringRow("技能名称", book.skillName, v => { book.skillName = v; MarkDirty(); });
                    var skillDrop = MakeDropdown("技能类型",
                        new List<string> { "主动", "被动" }, (int)book.skillType, idx =>
                        {
                            book.skillType = (BookItem.SkillType)idx;
                            MarkDirty();
                        });
                    _detailContainer.Add(skillDrop);
                    AddIntRow("需求等级", book.requiredLevel, v => { book.requiredLevel = v; MarkDirty(); });
                    break;

                case TreasureItem treasure:
                    AddSectionHeader("法宝属性");
                    AddFloatRow("威力",   treasure.power,         v => { treasure.power         = v; MarkDirty(); });
                    AddStringRow("特殊效果", treasure.uniqueEffect, v => { treasure.uniqueEffect = v; MarkDirty(); });
                    break;

                case ToolItem tool:
                    AddSectionHeader("工具属性");
                    AddFloatRow("效率",   tool.efficiency, v => { tool.efficiency = v; MarkDirty(); });
                    AddFloatRow("耐久度", tool.durability, v => { tool.durability = v; MarkDirty(); });
                    break;

                case FormationItem formation:
                    AddSectionHeader("阵盘属性");
                    AddIntRow("阵法等级", formation.formationLevel, v => { formation.formationLevel = v; MarkDirty(); });
                    AddPhaseListSection("所需五行元素", formation.requiredElements);
                    break;

                case QuestItem quest:
                    AddSectionHeader("任务属性");
                    AddStringRow("任务 ID", quest.questId, v => { quest.questId = v; MarkDirty(); });
                    break;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Form helpers
        // ════════════════════════════════════════════════════════════════════

        private void AddSectionHeader(string title)
        {
            var hr = new VisualElement();
            hr.style.height          = 1;
            hr.style.backgroundColor = new Color(0.28f, 0.28f, 0.28f);
            hr.style.marginTop       = 12;
            hr.style.marginBottom    = 6;
            _detailContainer.Add(hr);

            var lbl = new Label(title);
            lbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            lbl.style.color                   = new Color(0.85f, 0.75f, 0.35f);
            lbl.style.marginBottom            = 6;
            _detailContainer.Add(lbl);
        }

        private DropdownField MakeDropdown(string label, List<string> choices, int selectedIndex, Action<int> onIndexChange)
        {
            var drop = new DropdownField(label, choices, Mathf.Clamp(selectedIndex, 0, choices.Count - 1));
            drop.style.marginBottom = 4;
            drop.RegisterValueChangedCallback(e =>
            {
                if (_suppressCb) return;
                onIndexChange(choices.IndexOf(e.newValue));
            });
            return drop;
        }

        private void AddStringRow(string label, string val, Action<string> onChange)
        {
            var f = new TextField(label) { value = val ?? "" };
            f.style.marginBottom = 4;
            f.RegisterValueChangedCallback(e => { if (!_suppressCb) onChange(e.newValue); });
            _detailContainer.Add(f);
        }

        private void AddFloatRow(string label, float val, Action<float> onChange)
        {
            var f = new FloatField(label) { value = val };
            f.style.marginBottom = 4;
            f.RegisterValueChangedCallback(e => { if (!_suppressCb) onChange(e.newValue); });
            _detailContainer.Add(f);
        }

        private void AddIntRow(string label, int val, Action<int> onChange)
        {
            var f = new IntegerField(label) { value = val };
            f.style.marginBottom = 4;
            f.RegisterValueChangedCallback(e => { if (!_suppressCb) onChange(e.newValue); });
            _detailContainer.Add(f);
        }

        // ── String list (e.g. 兼容法器IDs) ──────────────────────────────────

        private void AddStringListSection(string title, List<string> list)
        {
            var container = new VisualElement();
            container.style.marginBottom = 4;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems    = Align.Center;
            headerRow.style.marginBottom  = 2;

            var lbl = new Label(title);
            lbl.style.flexGrow = 1;
            headerRow.Add(lbl);

            var addBtn = new Button(() =>
            {
                list.Add("");
                RebuildStringList(container, list);
                MarkDirty();
            }) { text = "＋" };
            addBtn.style.width  = 24;
            addBtn.style.height = 20;
            headerRow.Add(addBtn);

            container.Add(headerRow);
            RebuildStringList(container, list);
            _detailContainer.Add(container);
        }

        private void RebuildStringList(VisualElement container, List<string> list)
        {
            // Keep the header (index 0), remove the rest
            while (container.childCount > 1) container.RemoveAt(1);

            for (int i = 0; i < list.Count; i++)
            {
                int idx = i;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom  = 2;

                var f = new TextField { value = list[idx] };
                f.style.flexGrow = 1;
                f.RegisterValueChangedCallback(e =>
                {
                    if (!_suppressCb) { list[idx] = e.newValue; MarkDirty(); }
                });
                row.Add(f);

                var delBtn = new Button(() =>
                {
                    if (idx < list.Count)
                    {
                        list.RemoveAt(idx);
                        RebuildStringList(container, list);
                        MarkDirty();
                    }
                }) { text = "×" };
                delBtn.style.width = 24;
                row.Add(delBtn);

                container.Add(row);
            }
        }

        // ── FivePhases list (e.g. 所需五行元素) ─────────────────────────────

        private void AddPhaseListSection(string title, List<FivePhases> list)
        {
            var container = new VisualElement();
            container.style.marginBottom = 4;

            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems    = Align.Center;
            headerRow.style.marginBottom  = 2;

            var lbl = new Label(title);
            lbl.style.flexGrow = 1;
            headerRow.Add(lbl);

            var addBtn = new Button(() =>
            {
                list.Add(FivePhases.Metal);
                RebuildPhaseList(container, list);
                MarkDirty();
            }) { text = "＋" };
            addBtn.style.width  = 24;
            addBtn.style.height = 20;
            headerRow.Add(addBtn);

            container.Add(headerRow);
            RebuildPhaseList(container, list);
            _detailContainer.Add(container);
        }

        private void RebuildPhaseList(VisualElement container, List<FivePhases> list)
        {
            while (container.childCount > 1) container.RemoveAt(1);

            for (int i = 0; i < list.Count; i++)
            {
                int idx = i;

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginBottom  = 2;

                var drop = new DropdownField(PhaseLabels, (int)list[idx]);
                drop.style.flexGrow = 1;
                drop.RegisterValueChangedCallback(e =>
                {
                    if (!_suppressCb)
                    {
                        list[idx] = (FivePhases)PhaseLabels.IndexOf(e.newValue);
                        MarkDirty();
                    }
                });
                row.Add(drop);

                var delBtn = new Button(() =>
                {
                    if (idx < list.Count)
                    {
                        list.RemoveAt(idx);
                        RebuildPhaseList(container, list);
                        MarkDirty();
                    }
                }) { text = "×" };
                delBtn.style.width = 24;
                row.Add(delBtn);

                container.Add(row);
            }
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
                    case ItemType.Pill:      Deserialize<PillItem>(json);      break;
                    case ItemType.Weapon:    Deserialize<WeaponItem>(json);    break;
                    case ItemType.Ammo:      Deserialize<AmmoItem>(json);      break;
                    case ItemType.Talisman:  Deserialize<TalismanItem>(json);  break;
                    case ItemType.Material:  Deserialize<MaterialItem>(json);  break;
                    case ItemType.Book:      Deserialize<BookItem>(json);      break;
                    case ItemType.Treasure:  Deserialize<TreasureItem>(json);  break;
                    case ItemType.Tool:      Deserialize<ToolItem>(json);      break;
                    case ItemType.Formation: Deserialize<FormationItem>(json); break;
                    case ItemType.Quest:     Deserialize<QuestItem>(json);     break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[物品管理器] 加载 {GetFileName(type)}.json 失败: {ex.Message}");
            }

            ApplySearch();
        }

        private void Deserialize<T>(string json) where T : BaseItem
        {
            var w = JsonConvert.DeserializeObject<JsonWrapper<T>>(json);
            if (w?.items != null)
                foreach (var item in w.items)
                    if (item != null) _allItems.Add(item);
        }

        private void ApplySearch()
        {
            _filteredItems = string.IsNullOrEmpty(_searchText)
                ? new List<BaseItem>(_allItems)
                : _allItems.Where(i =>
                      (i.name?.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                      (i.id?.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0) ||
                      (i.description?.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0))
                  .ToList();

            if (_currentType == ItemType.Pill)
            {
                RefreshPillTableRows();
            }
            else if (_listView != null)
            {
                _listView.itemsSource = _filteredItems;
                _listView.Rebuild();
            }

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

            if (_currentType != ItemType.Pill && _listView != null)
            {
                int idx = _filteredItems.IndexOf(item);
                if (idx >= 0) _listView.SetSelection(idx);
            }
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
            if (_currentType != ItemType.Pill)
                BuildDetailPanel();
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
                case ItemType.Pill:      return Serialize<PillItem>(settings);
                case ItemType.Weapon:    return Serialize<WeaponItem>(settings);
                case ItemType.Ammo:      return Serialize<AmmoItem>(settings);
                case ItemType.Talisman:  return Serialize<TalismanItem>(settings);
                case ItemType.Material:  return Serialize<MaterialItem>(settings);
                case ItemType.Book:      return Serialize<BookItem>(settings);
                case ItemType.Treasure:  return Serialize<TreasureItem>(settings);
                case ItemType.Tool:      return Serialize<ToolItem>(settings);
                case ItemType.Formation: return Serialize<FormationItem>(settings);
                case ItemType.Quest:     return Serialize<QuestItem>(settings);
                default:                 return "{}";
            }
        }

        private string Serialize<T>(JsonSerializerSettings settings) where T : BaseItem
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

        private void RefreshListItem(BaseItem item)
        {
            int idx = _filteredItems.IndexOf(item);
            if (idx >= 0) _listView.RefreshItem(idx);
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

        private static BaseItem CreateItem(ItemType type) => type switch
        {
            ItemType.Pill      => new PillItem(),
            ItemType.Weapon    => new WeaponItem(),
            ItemType.Ammo      => new AmmoItem(),
            ItemType.Talisman  => new TalismanItem(),
            ItemType.Material  => new MaterialItem(),
            ItemType.Book      => new BookItem(),
            ItemType.Treasure  => new TreasureItem(),
            ItemType.Tool      => new ToolItem(),
            ItemType.Formation => new FormationItem(),
            ItemType.Quest     => new QuestItem(),
            _                  => new PillItem(),
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
            _listView        = null;
            _detailContainer = null;
            _pillTableBody   = null;

            if (_currentType == ItemType.Pill)
            {
                _mainArea.Add(BuildPillTableView());
            }
            else
            {
                _mainArea.Add(BuildLeftPanel());
                _mainArea.Add(BuildRightPanel());
                ShowEmptyState();
            }

            var countLabel = rootVisualElement?.Q<Label>("countLabel");
            if (countLabel != null)
                countLabel.text = $"共 {_allItems.Count} 个  |  显示 {_filteredItems.Count} 个";
        }

        private VisualElement BuildPillTableView()
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
            tableWrap.Add(BuildPillTableHeader());

            _pillTableBody = new VisualElement();
            _pillTableBody.style.flexDirection = FlexDirection.Column;
            tableWrap.Add(_pillTableBody);

            scroll.Add(tableWrap);
            outer.Add(scroll);

            RefreshPillTableRows();
            return outer;
        }

        private VisualElement BuildPillTableHeader()
        {
            var row = new VisualElement();
            row.style.flexDirection     = FlexDirection.Row;
            row.style.flexShrink        = 0;
            row.style.backgroundColor   = new Color(0.20f, 0.20f, 0.20f);
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = new Color(0.12f, 0.12f, 0.12f);
            row.style.height            = 24;

            foreach (var (header, width, flex) in PillColumns)
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

        private void RefreshPillTableRows()
        {
            if (_pillTableBody == null) return;
            _suppressCb = true;
            _pillTableBody.Clear();

            int rowIndex = 0;
            foreach (var baseItem in _filteredItems)
            {
                if (baseItem is PillItem pill)
                    _pillTableBody.Add(BuildPillTableRow(pill, rowIndex++));
            }
            _suppressCb = false;
        }

        private VisualElement BuildPillTableRow(PillItem pill, int rowIndex)
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
                HighlightPillRow(row);
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

        private void HighlightPillRow(VisualElement selectedRow)
        {
            if (_pillTableBody == null) return;
            int idx = 0;
            foreach (var child in _pillTableBody.Children())
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

        [Serializable]
        private class JsonWrapper<T> { public List<T> items; }
    }
}
