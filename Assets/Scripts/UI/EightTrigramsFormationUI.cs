using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Immortal.Controllers;
using Immortal.Item;

namespace Immortal.UI
{
    /// <summary>
    /// 八卦阵盘 UI（View）：
    ///   - 中心：阴阳鱼旋转图 + 内框
    ///   - 外圈：8 个 TrigramUI，均匀分布
    ///   - 每个 TrigramUI 内含 3 个 InventorySlotUI（可拖入物品）
    ///   - 以 FormationInstance 作为数据模型（MVC Model）
    /// </summary>
    public class EightTrigramsFormationUI : BaseEightTrigramsUI
    {
        [Header("中心图像")]
        [SerializeField] private Image     yinYangFishImage;   // 阴阳鱼
        [SerializeField] private Image     innerFrameImage;    // 内框
        [SerializeField] private float     spinSpeed = 35f;    // 旋转速度（度/秒）

        [Header("外圈卦象")]
        [SerializeField] private Transform          trigramsRoot;   // 8 个 TrigramUI 的父节点
        [SerializeField] private GameObject         trigramPrefab;  // TrigramUI 预制体

        [Header("槽位预制体（供运行时创建用）")]
        [SerializeField] private GameObject slotPrefab;

        [Header("关闭按钮")]
        [SerializeField] private Button closeButton;

        // 阵盘专属背包（24 格：卦位 i → 槽位 i*3 ~ i*3+2）
        private Inventory formationInventory;
        // slotIndex → InventorySlotUI，用于背包事件后快速刷新 UI
        private readonly Dictionary<int, InventorySlotUI> slotUIMap = new Dictionary<int, InventorySlotUI>();
        // 内部状态
        private FormationInstance currentInstance;
        private Action            onCleared;
        private List<TrigramUI>   trigramUIs = new List<TrigramUI>();

        // ======================== 生命周期 ========================

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);

            gameObject.SetActive(false);
        }

        private void Update()
        {
            // 阴阳鱼旋转
            if (yinYangFishImage != null)
                yinYangFishImage.transform.Rotate(0, 0, -spinSpeed * Time.deltaTime);
        }

        // ======================== 切换阵盘 ========================

        /// <summary>
        /// 切换到新的阵盘实例并刷新整个 UI。
        /// </summary>
        /// <param name="instance">目标阵盘数据模型</param>
        /// <param name="clearCallback">阵盘被关闭时调用的回调</param>
        public void SwitchToFormation(FormationInstance instance, Action clearCallback)
        {
            currentInstance = instance;
            onCleared       = clearCallback;

            RebuildFormationInventory();
            BuildOrRefreshTrigrams();
        }

        // ======================== 阵盘背包 ========================

        private void RebuildFormationInventory()
        {
            if (formationInventory == null)
            {
                formationInventory = new Inventory(24, 0);
            }
            else
            {
                formationInventory.RemoveEventListener(InventoryEventType.SlotChanged,  OnFormationSlotChanged);
                formationInventory.RemoveEventListener(InventoryEventType.ItemAdded,    OnFormationSlotChanged);
                formationInventory.RemoveEventListener(InventoryEventType.ItemRemoved,  OnFormationSlotChanged);
            }

            // 从 FormationInstance 数据对应填充背包槽位
            for (int t = 0; t < 8; t++)
            {
                var type = (EightTrigramsType)t;
                for (int s = 0; s < 3; s++)
                {
                    var config = currentInstance?.GetItem(type, s);
                    var fslot  = formationInventory.GetSlot(t * 3 + s);
                    if (fslot != null)
                        fslot.item = config != null ? new StackableItem(config, 1) : null;
                }
            }

            formationInventory.AddEventListener(InventoryEventType.SlotChanged,  OnFormationSlotChanged);
            formationInventory.AddEventListener(InventoryEventType.ItemAdded,    OnFormationSlotChanged);
            formationInventory.AddEventListener(InventoryEventType.ItemRemoved,  OnFormationSlotChanged);
            formationInventory.AddEventListener(InventoryEventType.ItemMoved,    OnFormationItemMoved);
        }

        private void OnFormationSlotChanged(InventoryEvent evt)
        {
            if (currentInstance == null || formationInventory == null) return;
            SyncSlotToInstance(evt.slotIndex);
            RefreshSlotUI(evt.slotIndex);
        }

        private void OnFormationItemMoved(InventoryEvent evt)
        {
            if (currentInstance == null || formationInventory == null) return;
            SyncSlotToInstance(evt.fromSlot);
            SyncSlotToInstance(evt.toSlot);
            RefreshSlotUI(evt.fromSlot);
            RefreshSlotUI(evt.toSlot);
        }

        private void SyncSlotToInstance(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= 24) return;
            int t = slotIndex / 3;
            int s = slotIndex % 3;
            var fslot = formationInventory.GetSlot(slotIndex);
            currentInstance.SetItem((EightTrigramsType)t, s, fslot?.item?.config);
            if (t < trigramUIs.Count)
                trigramUIs[t].RefreshYaoOutlines();
        }

        private void RefreshSlotUI(int slotIndex)
        {
            if (slotUIMap.TryGetValue(slotIndex, out var slotUI))
                slotUI.UpdateDisplay();
        }

        // ======================== 构建卦象 ========================

        private void BuildOrRefreshTrigrams()
        {
            // 首次创建时生成 8 个 TrigramUI
            if (trigramUIs.Count == 0)
                CreateTrigramUIs();

            if (currentInstance == null) return;

            // 刷新每个卦象的数据和槽位显示
            for (int i = 0; i < trigramUIs.Count; i++)
            {
                var type = (EightTrigramsType)i;
                trigramUIs[i].SetTrigram(type, currentInstance);
                BindTrigramSlots(trigramUIs[i], type);
            }
        }

        [ContextMenu("生成卦象UI")]
        private void CreateTrigramUIs()
        {
            if (trigramsRoot == null || trigramPrefab == null) return;

            // 清理旧节点（含编辑器模式预生成的子物体）
            for (int c = trigramsRoot.childCount - 1; c >= 0; c--)
            {
                var child = trigramsRoot.GetChild(c).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child);
                else
#endif
                    Destroy(child);
            }
            trigramUIs.Clear();

            float radius = yinYangFishImage != null ? yinYangFishImage.rectTransform.rect.width / 2f : 280f;

            for (int i = 0; i < 8; i++)
            {
                float angle = (i - 2) * 45f * Mathf.Deg2Rad;
                var   pos   = new Vector2(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);

                var go  = Instantiate(trigramPrefab, trigramsRoot);
                var rt  = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = pos;
                    rt.localEulerAngles = new Vector3(0f, 0f, -(i - 2) * 45f);
                }

                var tui = go.GetComponent<TrigramUI>();
                if (tui != null)
                {
                    tui.UpdateLabelOrientation();
                    trigramUIs.Add(tui);
                }
            }
        }

        private void BindTrigramSlots(TrigramUI trigramUI, EightTrigramsType type)
        {
            if (currentInstance == null || formationInventory == null)
            {
                Debug.LogWarning($"[Formation] BindTrigramSlots skipped: instance={currentInstance != null} inv={formationInventory != null}");
                return;
            }

            var slots = trigramUI.GetItemSlots();
            Debug.Log($"[Formation] BindTrigramSlots type={type} slotsLen={slots?.Length}");
            for (int i = 0; i < slots.Length && i < 3; i++)
            {
                if (slots[i] == null) continue;

                int invSlotIdx = (int)type * 3 + i;
                Debug.Log($"[Formation] Binding slot type={type} i={i} invIdx={invSlotIdx} go={slots[i].gameObject.name}");

                // 绑定到阵盘专属背包的对应槽位
                slots[i].Initialize(formationInventory.GetSlot(invSlotIdx));
                slots[i].SetFormationSlot(true);   // 标记为阵盘槽，禁止拖出阵盘外

                // 记录映射，供事件驱动刷新
                slotUIMap[invSlotIdx] = slots[i];

                // 跨背包拖入：从玩家背包拖入时复制配置到阵盘槽位（不消耗原物品）
                // 绑定完成后注册悬停→爻高亮 & 描边（每次 BindTrigramSlots 都重新注册）
                int capturedIdx     = invSlotIdx;
                var capturedSlotUI  = slots[i];

                // 右键 / 双击：将物品放回玩家背包
                slots[i].SetRightClickCallback((_, __) => ReturnItemToInventory(capturedIdx));
                slots[i].SetDoubleClickCallback((_, __) => ReturnItemToInventory(capturedIdx));

                slots[i].SetDropCallback(source =>
                {
                    var sourceItem = source.GetCurrentItem();
                    Debug.Log($"[Formation] DropCallback fired: source={source.gameObject.name} item={sourceItem?.config?.name ?? "null"} targetIdx={capturedIdx}");
                    if (sourceItem?.config == null) return;
                    var fslot = formationInventory.GetSlot(capturedIdx);
                    if (fslot == null) return;
                    fslot.item = new StackableItem(sourceItem.config, 1);
                    formationInventory.NotifySlotChanged(capturedIdx);
                    capturedSlotUI.UpdateDisplay();   // 强制刷新槽位图标

                    // 可堆叠物品：原背包数量 -1（不足1时整格清除）
                    if (sourceItem is StackableItem)
                    {
                        var srcSlot = source.GetSlot();
                        srcSlot?.inventory?.RemoveItem(srcSlot.slotIndex, 1);
                        source.UpdateDisplay();
                    }
                });
            }
            // 注册悬停→爻高亮 & 描边初始状态
            trigramUI.BindSlotCallbacks();
        }

        // ======================== 物品退回 ========================

        /// <summary>
        /// 将阵盘槽位中的物品放回玩家背包（右键 / 双击触发）。
        /// 可叠加物品：优先叠入已有堆，无处叠时放入空格；不可叠加物品直接放入空格。
        /// </summary>
        private void ReturnItemToInventory(int slotIndex)
        {
            if (formationInventory == null) return;
            var fslot = formationInventory.GetSlot(slotIndex);
            if (fslot?.item == null) return;

            var playerInventory = UIManager.Instance?.GetCurrentInventory();
            if (playerInventory == null)
            {
                Debug.LogWarning("[Formation] ReturnItemToInventory: 无法获取玩家背包");
                return;
            }

            var config = fslot.item.config;
            if (config == null) return;

            // AddItem 内部已按「先找可叠加槽，再找空槽」顺序处理
            if (playerInventory.AddItem(config, 1))
                formationInventory.RemoveItem(slotIndex, 1);
            else
                Debug.LogWarning("[Formation] 玩家背包已满，无法放回物品");
        }

        // ======================== 关闭 ========================

        private void OnCloseClicked()
        {
            // 停止监听阵盘背包事件
            if (formationInventory != null)
            {
                formationInventory.RemoveEventListener(InventoryEventType.SlotChanged,  OnFormationSlotChanged);
                formationInventory.RemoveEventListener(InventoryEventType.ItemAdded,    OnFormationSlotChanged);
                formationInventory.RemoveEventListener(InventoryEventType.ItemRemoved,  OnFormationSlotChanged);
                formationInventory.RemoveEventListener(InventoryEventType.ItemMoved,    OnFormationItemMoved);
            }

            currentInstance = null;
            gameObject.SetActive(false);

            onCleared?.Invoke();
            onCleared = null;
        }

        // ======================== 外部访问 ========================

        public FormationInstance GetCurrentInstance() => currentInstance;
        public List<TrigramUI>   GetTrigramUIs()       => trigramUIs;
        public bool              IsOpen                => gameObject.activeSelf;

        /// <summary>返回指定卦位对应 TrigramUI 的 Transform，供 Formation3D 定位特效节点。</summary>
        public override Transform GetTrigramNode(EightTrigramsType trigram)
        {
            int idx = (int)trigram;
            if (idx < 0 || idx >= trigramUIs.Count) return null;
            return trigramUIs[idx]?.transform;
        }
    }
}
