using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Immortal.Item;
using Immortal.Utils;

namespace Immortal.UI
{
    /// <summary>
    /// 背包/快捷栏单个槽位的 UI 组件。
    /// 挂在 InventorySlot Prefab 根节点上。
    /// </summary>
    public class InventorySlotUI : MonoBehaviour,
        IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("子节点引用")]
        [SerializeField] private Image          itemIcon;       // 物品图标
        [SerializeField] private Text           quantityText;   // 数量文字
        [SerializeField] private Image          slotBackground; // 插槽背景
        [SerializeField] private Image          highlightImage; // 悬停/高亮遮罩

        [Header("拖拽")]
        [SerializeField] private Canvas         rootCanvas;     // 用于拖拽时设置层级

        // ---------- 数据 ----------
        private InventorySlot   slot;           // 普通背包槽
        private QuickBarSlot    quickBarSlot;   // 快捷栏槽（两者互斥）
        private bool            isQuickBar;
        private int             slotIndex;

        // ---------- 回调 ----------
        private Action<int, InventorySlotUI> onImmediateClick;
        private Action<int, InventorySlotUI> onSingleClick;
        private Action<int, InventorySlotUI> onDoubleClick;
        private Action<int, InventorySlotUI> onRightClick;           // 右键单击
        private Action<InventorySlotUI>      onDropCallback;      // 跨背包拖入时触发
        private Action<InventorySlotUI>      onPointerEnterCallback;  // 悬停进入
        private Action<InventorySlotUI>      onPointerExitCallback;   // 悬停离开

        // ---------- 拖拽内部状态 ----------
        private float           lastClickTime;
        private const float     DoubleClickThreshold = 0.3f;
        private bool            pendingSingleClick;
        private bool            isFormationSlot;     // 是否阵盘槽位（不允许拖出阵盘外）
        private bool            isDragging;          // 当前是否由本槽位发起了拖拽
        private bool            isLocked;            // 锁定状态（阵盘开启时背包槽位禁用）

        // ======================== 初始化 ========================

        /// <summary>初始化为背包槽</summary>
        public void Initialize(InventorySlot inventorySlot)
        {
            slot            = inventorySlot;
            isQuickBar      = false;
            slotIndex       = inventorySlot.slotIndex;
            isFormationSlot = false;
            UpdateDisplay();
        }

        /// <summary>初始化为快捷栏槽</summary>
        public void InitializeQuickBar(QuickBarSlot qbSlot)
        {
            quickBarSlot    = qbSlot;
            isQuickBar      = true;
            slotIndex       = qbSlot.slotIndex;
            isFormationSlot = false;
            UpdateDisplay();
        }

        // ======================== 显示刷新 ========================

        public void UpdateDisplay()
        {
            BaseItem item     = GetCurrentItem();
            int      quantity = GetCurrentQuantity();

            bool hasItem = item != null;

            if (itemIcon != null)
            {
                itemIcon.enabled = hasItem;
                if (hasItem)
                {
                    var sprite = IconManager.GetInstance().LoadItemIconSync(item.config);
                    itemIcon.sprite = sprite;
                }
            }

            if (quantityText != null)
            {
                quantityText.gameObject.SetActive(hasItem && quantity > 1);
                if (hasItem && quantity > 1)
                    quantityText.text = quantity.ToString();
            }
        }

        private Sprite LoadIconSprite(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath)) return null;
            return Resources.Load<Sprite>(iconPath);
        }

        // ======================== 数据访问 ========================

        public BaseItem GetCurrentItem()
        {
            if (isQuickBar)
                return quickBarSlot?.linkedSlot?.item;
            return slot?.item;
        }

        public int GetCurrentQuantity()
        {
            if (isQuickBar)
            {
                var linked = quickBarSlot?.linkedSlot?.item;
                return linked is StackableItem si ? si.quantity : (linked != null ? 1 : 0);
            }
            return slot?.Quantity ?? 0;
        }

        public bool HasItem() => GetCurrentItem() != null;

        public InventorySlot  GetSlot()        => isQuickBar ? quickBarSlot?.linkedSlot : slot;
        public QuickBarSlot   GetQuickBarSlot() => quickBarSlot;
        public int            GetSlotIndex()   => slotIndex;

        // ======================== 交互状态 ========================

        public void SetInteractionEnabled(bool enabled)
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.interactable = enabled;
        }

        public bool IsInteractionEnabled()
        {
            var btn = GetComponent<Button>();
            return btn == null || btn.interactable;
        }

        // ======================== 回调注册 ========================

        public void SetImmediateClickCallback(Action<int, InventorySlotUI> cb)  => onImmediateClick = cb;
        public void SetSingleClickCallback(Action<int, InventorySlotUI> cb)     => onSingleClick    = cb;
        public void SetDoubleClickCallback(Action<int, InventorySlotUI> cb)     => onDoubleClick    = cb;
        public void SetRightClickCallback(Action<int, InventorySlotUI> cb)      => onRightClick     = cb;

        /// <summary>注册跨背包拖入回调（从另一个 Inventory 拖来时触发）。</summary>
        public void SetDropCallback(Action<InventorySlotUI> callback) => onDropCallback = callback;

        /// <summary>标记该槽位为阵盘槽位，拖出阵盘外时显示禁止图标。</summary>
        public void SetFormationSlot(bool value) => isFormationSlot = value;

        /// <summary>
        /// 锁定/解锁槽位交互。锁定时槽位略微变暗，且不响应悬停、点击、拖拽。
        /// </summary>
        public void SetLocked(bool locked)
        {
            isLocked = locked;
            // 锁定时强制隐藏高亮（可能在悬停期间被锁定）
            if (locked && highlightImage != null)
                highlightImage.enabled = false;
            // 锁定时将背景和图标着色为灰色，解锁时恢复白色（不改变 alpha）
            Color tint = locked ? Color.gray : Color.white;
            if (slotBackground != null)
            {
                var c = slotBackground.color;
                slotBackground.color = new Color(tint.r, tint.g, tint.b, c.a);
            }
            if (itemIcon != null)
            {
                var c = itemIcon.color;
                itemIcon.color = new Color(tint.r, tint.g, tint.b, c.a);
            }
        }

        public void SetPointerEnterCallback(Action<InventorySlotUI> cb) => onPointerEnterCallback = cb;
        public void SetPointerExitCallback(Action<InventorySlotUI> cb)  => onPointerExitCallback  = cb;

        /// <summary>单独设置槽位背景图的 alpha（不影响图标）。</summary>
        public void SetBackgroundAlpha(float alpha)
        {
            if (slotBackground != null)
            {
                var c = slotBackground.color;
                slotBackground.color = new Color(c.r, c.g, c.b, alpha);
            }
        }

        /// <summary>单独设置物品图标的 alpha（不影响背景）。</summary>
        public void SetIconAlpha(float alpha)
        {
            if (itemIcon != null)
            {
                var c = itemIcon.color;
                itemIcon.color = new Color(c.r, c.g, c.b, alpha);
            }
        }

        // ======================== 指针事件 ========================

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractionEnabled() || isLocked) return;

            // 右键：直接触发右键回调，不走单击/双击逻辑
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                onRightClick?.Invoke(slotIndex, this);
                return;
            }

            // 立即回调
            onImmediateClick?.Invoke(slotIndex, this);

            float now = Time.unscaledTime;
            if (now - lastClickTime < DoubleClickThreshold)
            {
                // 双击
                CancelPendingSingleClick();
                onDoubleClick?.Invoke(slotIndex, this);
                lastClickTime = 0f; // 重置，防止三连击
            }
            else
            {
                // 可能是单击——延迟确认
                lastClickTime = now;
                pendingSingleClick = true;
                CancelInvoke(nameof(FireSingleClick));
                Invoke(nameof(FireSingleClick), DoubleClickThreshold);
            }
        }

        private void FireSingleClick()
        {
            if (!pendingSingleClick) return;
            pendingSingleClick = false;
            onSingleClick?.Invoke(slotIndex, this);
        }

        private void CancelPendingSingleClick()
        {
            pendingSingleClick = false;
            CancelInvoke(nameof(FireSingleClick));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isLocked) return;
            if (highlightImage != null)
                highlightImage.enabled = true;
            onPointerEnterCallback?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isLocked) return;
            if (highlightImage != null)
                highlightImage.enabled = false;
            onPointerExitCallback?.Invoke(this);
        }

        // ======================== 拖拽事件 ========================

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsInteractionEnabled() || !HasItem() || isLocked)
            {
                Debug.Log($"[Drag] BLOCKED on {gameObject.name}: enabled={IsInteractionEnabled()} hasItem={HasItem()} locked={isLocked}");
                return;
            }
            Debug.Log($"[Drag] BEGIN from {gameObject.name} slot={slotIndex} item={GetCurrentItem()?.config?.name}");
            CancelPendingSingleClick();

            // 若 Inspector 未赋值则自动查找根 Canvas
            if (rootCanvas == null)
            {
                var c = GetComponentInParent<Canvas>();
                if (c != null) rootCanvas = c.rootCanvas;
            }

            // 通过全局 DragProxy 创建跟随鼠标的幻影
            if (itemIcon != null && rootCanvas != null)
            {
                var proxy = UIManager.Instance?.DragProxy;
                if (proxy != null)
                {
                    // 用 rect.size 取实际渲染尺寸，避免 stretch 锚点下 sizeDelta 为负
                    var iconRect = ((RectTransform)itemIcon.transform).rect;
                    proxy.Begin(rootCanvas, itemIcon.sprite, iconRect.size,
                                isFormationSlot, UIManager.Instance.GetStopSprite());
                    proxy.Move(eventData); // 立即定位到鼠标位置，避免闪现在 (0,0)
                    isDragging = true;
                }
            }

            if (itemIcon != null) itemIcon.color = new Color(1, 1, 1, 0.4f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            UIManager.Instance?.DragProxy.Move(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            UIManager.Instance?.DragProxy.End();
            isDragging = false;
            if (itemIcon != null) itemIcon.color = Color.white;
        }

        public void OnDrop(PointerEventData eventData)
        {
            // 拿到被拖来的源槽位
            var source = eventData.pointerDrag?.GetComponent<InventorySlotUI>();
            if (source == null || source == this) return;

            Debug.Log($"[Drop] {gameObject.name}(slot={slotIndex}) ← {source.gameObject.name}(slot={source.slotIndex}) | sameInv={slot?.inventory != null && slot.inventory == source.slot?.inventory} | callback={(onDropCallback != null ? "SET" : "NULL")}");

            // 背包 → 背包（同一背包）：交换物品
            if (!isQuickBar && !source.isQuickBar && slot != null && source.slot != null
                && slot.inventory != null && slot.inventory == source.slot.inventory)
            {
                slot.inventory.MoveItem(source.slotIndex, slotIndex);
                return;
            }

            // 背包 → 快捷栏：建立快捷方式（不移动物品）
            if (isQuickBar && !source.isQuickBar && quickBarSlot != null && source.slot != null)
            {
                var inv = source.slot.inventory;
                if (inv != null)
                    inv.AddToQuickBar(source.slotIndex, slotIndex);
                return;
            }

            // 跨背包 / 非背包槽：自定义处理
            onDropCallback?.Invoke(source);
        }

        private void OnDestroy()
        {
            if (isDragging)
                UIManager.Instance?.DragProxy.End();
        }
    }
}
