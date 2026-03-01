using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Immortal.Item;

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

        // ---------- 拖拽内部状态 ----------
        private float           lastClickTime;
        private const float     DoubleClickThreshold = 0.3f;
        private bool            pendingSingleClick;
        private GameObject      dragProxy;          // 拖拽时跟随鼠标的幻影图片

        // ======================== 初始化 ========================

        /// <summary>初始化为背包槽</summary>
        public void Initialize(InventorySlot inventorySlot)
        {
            slot        = inventorySlot;
            isQuickBar  = false;
            slotIndex   = inventorySlot.slotIndex;
            UpdateDisplay();
        }

        /// <summary>初始化为快捷栏槽</summary>
        public void InitializeQuickBar(QuickBarSlot qbSlot)
        {
            quickBarSlot = qbSlot;
            isQuickBar   = true;
            slotIndex    = qbSlot.slotIndex;
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
                    // icon 字段存放的是 Resources 路径；实际项目可改为 Sprite 引用
                    var sprite = LoadIconSprite(item.icon);
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
                return quickBarSlot?.linkedSlot?.quantity ?? 0;
            return slot?.quantity ?? 0;
        }

        public bool HasItem() => GetCurrentItem() != null;

        public InventorySlot  GetSlot()        => slot;
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

        // ======================== 指针事件 ========================

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractionEnabled()) return;

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
            if (highlightImage != null)
                highlightImage.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (highlightImage != null)
                highlightImage.enabled = false;
        }

        // ======================== 拖拽事件 ========================

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!IsInteractionEnabled() || !HasItem()) return;
            CancelPendingSingleClick();

            // 创建跟随鼠标的幻影
            if (itemIcon != null && rootCanvas != null)
            {
                dragProxy = new GameObject("DragProxy");
                dragProxy.transform.SetParent(rootCanvas.transform, false);
                dragProxy.transform.SetAsLastSibling();

                var rt = dragProxy.AddComponent<RectTransform>();
                rt.sizeDelta = ((RectTransform)itemIcon.transform).sizeDelta;

                var img = dragProxy.AddComponent<Image>();
                img.sprite = itemIcon.sprite;
                img.raycastTarget = false;

                var cg = dragProxy.AddComponent<CanvasGroup>();
                cg.alpha = 0.8f;
                cg.blocksRaycasts = false;
            }

            if (itemIcon != null) itemIcon.color = new Color(1, 1, 1, 0.4f);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragProxy == null) return;
            dragProxy.transform.position = Input.mousePosition;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragProxy != null)
            {
                Destroy(dragProxy);
                dragProxy = null;
            }
            if (itemIcon != null) itemIcon.color = Color.white;
        }

        public void OnDrop(PointerEventData eventData)
        {
            // 拿到被拖来的源槽位
            var source = eventData.pointerDrag?.GetComponent<InventorySlotUI>();
            if (source == null || source == this) return;

            // 交换两个槽位的物品（通过 Inventory API）
            if (!isQuickBar && !source.isQuickBar && slot != null && source.slot != null)
            {
                var inv = slot.inventory;
                if (inv != null)
                    inv.MoveItem(source.slotIndex, slotIndex);
            }
        }

        private void OnDestroy()
        {
            if (dragProxy != null) Destroy(dragProxy);
        }
    }
}
