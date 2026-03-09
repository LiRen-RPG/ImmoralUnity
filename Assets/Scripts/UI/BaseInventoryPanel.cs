using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Immortal.Item;

namespace Immortal.UI
{
    /// <summary>
    /// 背包面板基类：负责创建/销毁 InventorySlotUI 并监听 Inventory 事件。
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class BaseInventoryPanel : MonoBehaviour
    {
        [Header("子节点引用")]
        [SerializeField] protected Transform    slotsContainer;     // GridLayoutGroup 所在节点
        [SerializeField] protected GameObject   slotPrefab;         // InventorySlotUI 预制体

        [Header("面板控制")]
        [SerializeField] protected float        openAlpha  = 1f;
        [SerializeField] protected float        closeAlpha = 0f;

        protected Inventory                 inventory;
        protected List<InventorySlotUI>     slotUIs = new List<InventorySlotUI>();

        // 外部回调
        private Action<int, InventorySlotUI> immediateClickCallback;
        private Action<int, InventorySlotUI> singleClickCallback;
        private Action<int, InventorySlotUI> doubleClickCallback;

        private CanvasGroup canvasGroup;

        protected virtual void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        // ======================== 绑定 ========================

        public virtual void BindToInventory(Inventory inv)
        {
            UnbindFromInventory();
            inventory = inv;
            CreateSlotUIs();
            inventory.AddEventListener(InventoryEventType.SlotChanged,   OnInventoryChanged);
            inventory.AddEventListener(InventoryEventType.ItemAdded,     OnInventoryChanged);
            inventory.AddEventListener(InventoryEventType.ItemRemoved,   OnInventoryChanged);
            inventory.AddEventListener(InventoryEventType.ItemMoved,     OnInventoryChanged);
        }

        public virtual void UnbindFromInventory()
        {
            if (inventory != null)
            {
                inventory.RemoveEventListener(InventoryEventType.SlotChanged,   OnInventoryChanged);
                inventory.RemoveEventListener(InventoryEventType.ItemAdded,     OnInventoryChanged);
                inventory.RemoveEventListener(InventoryEventType.ItemRemoved,   OnInventoryChanged);
                inventory.RemoveEventListener(InventoryEventType.ItemMoved,     OnInventoryChanged);
                inventory = null;
            }
            ClearSlotUIs();
        }

        // ======================== 槽位创建 ========================

        protected virtual void CreateSlotUIs()
        {
            if (inventory == null || slotsContainer == null || slotPrefab == null) return;
            ClearSlotUIs();

            int capacity = inventory.GetCapacity();
            for (int i = 0; i < capacity; i++)
            {
                var go  = Instantiate(slotPrefab, slotsContainer);
                var sui = go.GetComponent<InventorySlotUI>();
                if (sui == null) continue;

                sui.Initialize(inventory.GetSlot(i));
                sui.SetImmediateClickCallback(immediateClickCallback);
                sui.SetSingleClickCallback(singleClickCallback);
                sui.SetDoubleClickCallback(doubleClickCallback);
                slotUIs.Add(sui);
            }
        }

        protected virtual void ClearSlotUIs()
        {
            foreach (var sui in slotUIs)
            {
                if (sui != null) Destroy(sui.gameObject);
            }
            slotUIs.Clear();
        }

        // ======================== 事件处理 ========================

        protected virtual void OnInventoryChanged(InventoryEvent evt)
        {
            RefreshSlot(evt.slotIndex);
            // 移动操作需同时刷新来源槽位，否则源槽位视觉上不清空
            if (evt.type == InventoryEventType.ItemMoved && evt.fromSlot >= 0)
                RefreshSlot(evt.fromSlot);
        }

        protected void RefreshSlot(int index)
        {
            if (index < 0 || index >= slotUIs.Count) return;
            slotUIs[index]?.UpdateDisplay();
        }

        public void RefreshAll()
        {
            foreach (var sui in slotUIs)
                sui?.UpdateDisplay();
        }

        // ======================== 面板开关 ========================

        public virtual void OpenPanel()
        {
            gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha          = openAlpha;
                canvasGroup.interactable   = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        public virtual void ClosePanel()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha          = closeAlpha;
                canvasGroup.interactable   = false;
                canvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);
        }

        public bool IsOpen => gameObject.activeSelf &&
                              (canvasGroup == null || canvasGroup.alpha > 0f);

        // ======================== 回调注册 ========================

        public void SetSlotImmediateClickCallback(Action<int, InventorySlotUI> cb)
        {
            immediateClickCallback = cb;
            foreach (var sui in slotUIs) sui?.SetImmediateClickCallback(cb);
        }

        public void SetSlotClickCallback(Action<int, InventorySlotUI> cb)
        {
            singleClickCallback = cb;
            foreach (var sui in slotUIs) sui?.SetSingleClickCallback(cb);
        }

        public void SetSlotDoubleClickCallback(Action<int, InventorySlotUI> cb)
        {
            doubleClickCallback = cb;
            foreach (var sui in slotUIs) sui?.SetDoubleClickCallback(cb);
        }

        public Inventory GetBoundInventory() => inventory;
        public IReadOnlyList<InventorySlotUI> GetSlotUIs() => slotUIs;
    }
}
