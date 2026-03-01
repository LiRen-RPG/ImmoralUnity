using UnityEngine;
using UnityEngine.UI;
using Immortal.Item;
using Immortal.Controllers;

namespace Immortal.UI
{
    /// <summary>
    /// 背包 UI：封装 BaseInventoryPanel，处理单击/双击物品的特化逻辑。
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private BaseInventoryPanel inventoryPanel;

        [Header("容量显示")]
        [SerializeField] private Text capacityText; // 例如 "10/33"

        private void OnEnable()
        {
            if (inventoryPanel == null)
            {
                Debug.LogError("InventoryUI: 需要引用 BaseInventoryPanel 组件");
                return;
            }

            inventoryPanel.SetSlotImmediateClickCallback(OnSlotImmediateClick);
            inventoryPanel.SetSlotClickCallback(OnSlotSingleClick);
            inventoryPanel.SetSlotDoubleClickCallback(OnSlotDoubleClick);
        }

        // ---------- 外部接口 ----------

        public BaseInventoryPanel GetInventoryPanel() => inventoryPanel;

        public void RefreshCapacityText(int used, int total)
        {
            if (capacityText != null)
                capacityText.text = $"{used}/{total}";
        }

        // ---------- 点击回调 ----------

        private void OnSlotImmediateClick(int slotIndex, InventorySlotUI slotUI)
        {
            var item = slotUI.GetCurrentItem();
            if (item == null) return;
            // 立即反馈（动画/音效等）
            Debug.Log($"立即点击槽位 {slotIndex}: {item.name}");
        }

        private void OnSlotSingleClick(int slotIndex, InventorySlotUI slotUI)
        {
            var item = slotUI.GetCurrentItem();
            if (item == null) return;
            Debug.Log($"单击槽位 {slotIndex}: {item.name}");

            switch (item.type)
            {
                case ItemType.Formation:
                    OpenFormationUI(item, slotUI);
                    break;
                default:
                    Debug.Log($"显示物品基础信息: {item.name}");
                    break;
            }
        }

        private void OnSlotDoubleClick(int slotIndex, InventorySlotUI slotUI)
        {
            var item = slotUI.GetCurrentItem();
            if (item == null) return;
            Debug.Log($"双击槽位 {slotIndex}: {item.name}");

            switch (item.type)
            {
                case ItemType.Formation:
                    HandleFormationDoubleClick(item, slotUI);
                    break;
                default:
                    PerformDefaultAction(item);
                    break;
            }
        }

        // ---------- 阵盘逻辑 ----------

        private void OpenFormationUI(BaseItem item, InventorySlotUI slotUI)
        {
            var mgr = UIManager.Instance;
            if (mgr == null) return;

            // Formation plate 由外部（Actor/Controller）注入；此处创建临时数据供预览
            var plate = new EightTrigramsFormationPlate();
            mgr.ShowFormationUI();
            mgr.GetFormationUI()?.SwitchToFormation(plate, () =>
            {
                slotUI.SetInteractionEnabled(true);
            });
            slotUI.SetInteractionEnabled(false);
        }

        private void HandleFormationDoubleClick(BaseItem item, InventorySlotUI slotUI)
        {
            var mgr = UIManager.Instance;
            if (mgr == null) return;

            var plate = new EightTrigramsFormationPlate();
            mgr.ShowFormationUI();
            mgr.GetFormationUI()?.SwitchToFormation(plate, null);
        }

        private void PerformDefaultAction(BaseItem item)
        {
            Debug.Log($"执行默认操作: {item.name}");
        }
    }
}
