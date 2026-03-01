using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Immortal.Item;

namespace Immortal.UI
{
    /// <summary>
    /// 快捷栏 UI：竖向排列 N 个 InventorySlotUI，绑定到 Inventory 的快捷栏。
    /// </summary>
    public class QuickBarUI : MonoBehaviour
    {
        [Header("子节点引用")]
        [SerializeField] private Transform      quickBarContainer;  // VerticalLayoutGroup 节点
        [SerializeField] private GameObject     slotPrefab;         // InventorySlotUI 预制体（同背包共用）

        private Inventory               inventory;
        private List<InventorySlotUI>   slotUIs = new List<InventorySlotUI>();

        // ======================== 绑定 ========================

        public void BindToInventory(Inventory inv)
        {
            UnbindFromInventory();
            inventory = inv;
            CreateSlotUIs();
            inventory.AddEventListener(InventoryEventType.QuickBarChanged,     OnInventoryChanged);
            inventory.AddEventListener(InventoryEventType.QuickBarSlotChanged, OnInventoryChanged);
        }

        public void UnbindFromInventory()
        {
            if (inventory != null)
            {
                inventory.RemoveEventListener(InventoryEventType.QuickBarChanged,     OnInventoryChanged);
                inventory.RemoveEventListener(InventoryEventType.QuickBarSlotChanged, OnInventoryChanged);
                inventory = null;
            }
            ClearSlotUIs();
        }

        // ======================== 创建/销毁 ========================

        private void CreateSlotUIs()
        {
            if (inventory == null || quickBarContainer == null || slotPrefab == null) return;
            ClearSlotUIs();

            int capacity = inventory.GetQuickBarCapacity();
            for (int i = 0; i < capacity; i++)
            {
                var go  = Instantiate(slotPrefab, quickBarContainer);
                var sui = go.GetComponent<InventorySlotUI>();
                if (sui == null) continue;

                sui.InitializeQuickBar(inventory.GetQuickBarSlot(i));
                sui.SetSingleClickCallback((idx, _) => OnSlotClicked(idx));
                slotUIs.Add(sui);
            }
        }

        private void ClearSlotUIs()
        {
            foreach (var sui in slotUIs)
                if (sui != null) Destroy(sui.gameObject);
            slotUIs.Clear();
        }

        // ======================== 事件处理 ========================

        private void OnInventoryChanged(InventoryEvent evt)
        {
            int idx = evt.quickBarSlotIndex;
            if (idx >= 0 && idx < slotUIs.Count)
                slotUIs[idx]?.UpdateDisplay();
        }

        // ======================== 快捷栏点击 ========================

        private void OnSlotClicked(int quickBarIndex)
        {
            if (inventory == null) return;
            bool ok = inventory.UseQuickBarItem(quickBarIndex);
            Debug.Log(ok
                ? $"使用快捷栏槽位 {quickBarIndex} 的物品"
                : $"快捷栏槽位 {quickBarIndex} 没有可用物品");
        }

        // ======================== 热键支持 ========================

        private void Update()
        {
            // 数字键 1-9 触发快捷栏
            for (int i = 0; i < 9 && i < slotUIs.Count; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    OnSlotClicked(i);
                }
            }
            // 0 键对应第 10 个槽
            if (slotUIs.Count >= 10 && Input.GetKeyDown(KeyCode.Alpha0))
                OnSlotClicked(9);
        }

        // ======================== 可见性 ========================

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
