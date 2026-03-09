using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Immortal.Item;
using Immortal.Controllers;

namespace Immortal.Item
{
    // 常量定义
    public static class InventoryConstants
    {
        public const int INVALID_SLOT_INDEX = -1; // 表示无效的槽位索引
    }

    // 背包槽位
    [System.Serializable]
    public class InventorySlot
    {
        public BaseItem item;          // 物品实例（StackableItem 或 FormationInstance）
        public int slotIndex;
        [System.NonSerialized]
        public Inventory inventory;

        public InventorySlot(int slotIndex, Inventory inventory)
        {
            this.item = null;
            this.slotIndex = slotIndex;
            this.inventory = inventory;
        }

        /// <summary>便捷属性：可叠加物品的当前数量，非叠加物品返回 1。</summary>
        public int Quantity => item == null ? 0 :
            item is StackableItem s ? s.quantity : (item != null ? 1 : 0);

        /// <summary>阵盘实例（若 item 是 FormationInstance 则有值）。</summary>
        public FormationInstance FormationInstance => item as FormationInstance;
    }

    // 快捷栏槽位
    [System.Serializable]
    public class QuickBarSlot
    {
        [System.NonSerialized]
        public InventorySlot linkedSlot; // 引用背包中的槽位对象
        public int slotIndex; // 快捷栏自身的槽位索引
        public int linkedSlotIndex = InventoryConstants.INVALID_SLOT_INDEX; // 用于序列化的槽位索引

        public QuickBarSlot(int slotIndex)
        {
            this.linkedSlot = null;
            this.slotIndex = slotIndex;
        }
    }

    // 背包事件类型
    public enum InventoryEventType
    {
        ItemAdded,
        ItemRemoved,
        ItemMoved,
        SlotChanged,
        QuickBarChanged,
        QuickBarSlotChanged
    }

    // 背包事件
    public class InventoryEvent
    {
        public InventoryEventType type;
        public int slotIndex;
        public BaseItem item;       // 实例引用
        public int quantity;        // 便于日志/UI：变动的数量
        public int fromSlot;
        public int toSlot;
        public int quickBarSlotIndex;

        public InventoryEvent(InventoryEventType type, int slotIndex = InventoryConstants.INVALID_SLOT_INDEX)
        {
            this.type = type;
            this.slotIndex = slotIndex;
            this.fromSlot = InventoryConstants.INVALID_SLOT_INDEX;
            this.toSlot = InventoryConstants.INVALID_SLOT_INDEX;
            this.quickBarSlotIndex = InventoryConstants.INVALID_SLOT_INDEX;
        }
    }

    // 背包事件监听器
    public delegate void InventoryEventListener(InventoryEvent inventoryEvent);

    // 背包系统类
    [System.Serializable]
    public class Inventory
    {
        [SerializeField]
        private InventorySlot[] slots;
        [SerializeField]
        private int capacity;
        [System.NonSerialized]
        private Dictionary<InventoryEventType, List<InventoryEventListener>> eventListeners;
        [SerializeField]
        private QuickBarSlot[] quickBarSlots;
        [SerializeField]
        private int quickBarCapacity;

        public Inventory(int capacity = 30, int quickBarCapacity = 5)
        {
            this.capacity = capacity;
            this.quickBarCapacity = quickBarCapacity;
            this.slots = new InventorySlot[capacity];
            this.quickBarSlots = new QuickBarSlot[quickBarCapacity];
            this.eventListeners = new Dictionary<InventoryEventType, List<InventoryEventListener>>();

            // 初始化背包槽位和快捷栏
            InitializeSlots();
            InitializeQuickBar();
        }

        // 初始化背包槽位
        private void InitializeSlots()
        {
            for (int i = 0; i < capacity; i++)
            {
                slots[i] = new InventorySlot(i, this);
            }
        }

        // 初始化快捷栏
        private void InitializeQuickBar()
        {
            for (int i = 0; i < quickBarCapacity; i++)
            {
                quickBarSlots[i] = new QuickBarSlot(i);
            }
        }

        // 添加事件监听器
        public void AddEventListener(InventoryEventType eventType, InventoryEventListener listener)
        {
            if (!eventListeners.ContainsKey(eventType))
            {
                eventListeners[eventType] = new List<InventoryEventListener>();
            }
            eventListeners[eventType].Add(listener);
        }

        // 移除事件监听器
        public void RemoveEventListener(InventoryEventType eventType, InventoryEventListener listener)
        {
            if (eventListeners.ContainsKey(eventType))
            {
                eventListeners[eventType].Remove(listener);
            }
        }

        // 触发事件
        private void EmitEvent(InventoryEvent inventoryEvent)
        {
            if (eventListeners == null) return;

            if (eventListeners.ContainsKey(inventoryEvent.type))
            {
                foreach (var listener in eventListeners[inventoryEvent.type])
                {
                    listener(inventoryEvent);
                }
            }
        }

        // 检查物品是否可加栈（基于配置）
        private bool IsStackable(BaseItemConfig config)
        {
            return config != null && config.stackable && config.maxStack > 1;
        }

        // 获取物品的最大堆叠数量
        private int GetMaxStack(BaseItemConfig config)
        {
            return config?.maxStack ?? 1;
        }

        // 尝试向指定槽位堆叠物品（槽位必须已有相同物品）
        private int TryStackToSlot(BaseItemConfig config, int quantity, InventorySlot slot)
        {
            if (slot.item == null || !(slot.item is StackableItem si) ||
                si.ConfigId != config.id || !IsStackable(config))
                return 0;

            int maxStack = GetMaxStack(config);
            int canAdd = Mathf.Min(quantity, maxStack - si.quantity);
            if (canAdd > 0) si.quantity += canAdd;
            return canAdd;
        }

        // 向空槽位添加物品，返回实际添加数量
        private int AddToEmptySlot(BaseItemConfig config, int quantity, InventorySlot slot)
        {
            if (slot.item != null) return 0;

            if (IsStackable(config))
            {
                int addQty = Mathf.Min(quantity, GetMaxStack(config));
                slot.item = new StackableItem(config, addQty);
                return addQty;
            }
            else if (config is Immortal.Item.FormationConfig || config?.type == ItemType.Formation)
            {
                if (quantity >= 1)
                {
                    slot.item = new Immortal.Controllers.FormationInstance(config.id);
                    return 1;
                }
                return 0;
            }
            else
            {
                if (quantity >= 1)
                {
                    slot.item = new StackableItem(config, 1);
                    return 1;
                }
                return 0;
            }
        }

        // 查找可以堆叠的槽位
        private int FindStackableSlot(BaseItemConfig config)
        {
            if (!IsStackable(config)) return InventoryConstants.INVALID_SLOT_INDEX;

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.item is StackableItem si && si.ConfigId == config.id)
                {
                    if (si.quantity < GetMaxStack(config))
                        return i;
                }
            }
            return InventoryConstants.INVALID_SLOT_INDEX;
        }

        // 查找空槽位
        private int FindEmptySlot()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].item == null)
                {
                    return i;
                }
            }
            return InventoryConstants.INVALID_SLOT_INDEX;
        }

        // 添加物品到背包（从配置创建实例）
        public bool AddItem(BaseItemConfig config, int quantity = 1)
        {
            if (config == null || quantity <= 0) return false;

            int remainingQuantity = quantity;

            // 如果物品可堆叠，先尝试堆叠到现有槽位
            if (IsStackable(config))
            {
                for (int i = 0; i < slots.Length && remainingQuantity > 0; i++)
                {
                    var slot = slots[i];
                    if (slot.item is StackableItem si && si.ConfigId == config.id)
                    {
                        int stackedQuantity = TryStackToSlot(config, remainingQuantity, slot);
                        if (stackedQuantity > 0)
                        {
                            remainingQuantity -= stackedQuantity;
                            EmitEvent(new InventoryEvent(InventoryEventType.SlotChanged, i)
                                { item = slot.item, quantity = si.quantity });
                        }
                    }
                }
            }

            // 将剩余的物品放入空槽位
            while (remainingQuantity > 0)
            {
                int emptySlotIndex = FindEmptySlot();
                if (emptySlotIndex == InventoryConstants.INVALID_SLOT_INDEX)
                    return remainingQuantity == 0;

                var emptySlot = slots[emptySlotIndex];
                int addedQuantity = AddToEmptySlot(config, remainingQuantity, emptySlot);

                if (addedQuantity > 0)
                {
                    remainingQuantity -= addedQuantity;
                    EmitEvent(new InventoryEvent(InventoryEventType.ItemAdded, emptySlotIndex)
                        { item = emptySlot.item, quantity = addedQuantity });
                }
                else break;
            }

            return remainingQuantity == 0;
        }

        // 向指定槽位添加物品（从配置创建实例）
        public bool AddItemToSlot(BaseItemConfig config, int quantity, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length || config == null || quantity <= 0)
                return false;

            var slot = slots[slotIndex];

            if (slot.item == null)
            {
                int addedQuantity = AddToEmptySlot(config, quantity, slot);
                if (addedQuantity > 0)
                {
                    EmitEvent(new InventoryEvent(InventoryEventType.ItemAdded, slotIndex)
                        { item = slot.item, quantity = addedQuantity });
                    return addedQuantity == quantity;
                }
                return false;
            }

            int stackedQuantity = TryStackToSlot(config, quantity, slot);
            if (stackedQuantity > 0)
            {
                EmitEvent(new InventoryEvent(InventoryEventType.SlotChanged, slotIndex)
                    { item = slot.item, quantity = slot.Quantity });
                return stackedQuantity == quantity;
            }
            return false;
        }

        // 移除物品
        public bool RemoveItem(int slotIndex, int quantity = 1)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return false;

            var slot = slots[slotIndex];
            if (slot.item == null || slot.Quantity < quantity) return false;

            if (slot.item is StackableItem si)
            {
                si.quantity -= quantity;
                if (si.quantity <= 0)
                {
                    var removed = slot.item;
                    slot.item = null;
                    EmitEvent(new InventoryEvent(InventoryEventType.ItemRemoved, slotIndex)
                        { item = removed, quantity = quantity });
                }
                else
                {
                    EmitEvent(new InventoryEvent(InventoryEventType.SlotChanged, slotIndex)
                        { item = slot.item, quantity = si.quantity });
                }
            }
            else
            {
                // Non-stackable (FormationInstance etc.) — remove entire item
                var removed = slot.item;
                slot.item = null;
                EmitEvent(new InventoryEvent(InventoryEventType.ItemRemoved, slotIndex)
                    { item = removed, quantity = 1 });
            }

            return true;
        }

        // 移动物品
        public bool MoveItem(int fromSlot, int toSlot)
        {
            if (fromSlot < 0 || fromSlot >= slots.Length ||
                toSlot < 0 || toSlot >= slots.Length ||
                fromSlot == toSlot)
                return false;

            var sourceSlot = slots[fromSlot];
            var targetSlot = slots[toSlot];

            if (sourceSlot.item == null) return false;

            // 目标槽位为空，直接移动
            if (targetSlot.item == null)
            {
                targetSlot.item = sourceSlot.item;
                sourceSlot.item = null;

                EmitEvent(new InventoryEvent(InventoryEventType.ItemMoved, toSlot)
                    { fromSlot = fromSlot, toSlot = toSlot, item = targetSlot.item, quantity = targetSlot.Quantity });
                return true;
            }

            // 尝试堆叠
            if (sourceSlot.item is StackableItem src && targetSlot.item is StackableItem dst &&
                src.ConfigId == dst.ConfigId)
            {
                int maxStack = src.config?.maxStack ?? 1;
                int canStack = Mathf.Min(src.quantity, maxStack - dst.quantity);
                if (canStack > 0)
                {
                    dst.quantity += canStack;
                    src.quantity -= canStack;
                    if (src.quantity <= 0) sourceSlot.item = null;

                    EmitEvent(new InventoryEvent(InventoryEventType.ItemMoved, toSlot)
                        { fromSlot = fromSlot, toSlot = toSlot, item = targetSlot.item, quantity = targetSlot.Quantity });
                    return true;
                }
            }

            // 交换物品位置
            var tempItem = targetSlot.item;
            targetSlot.item = sourceSlot.item;
            sourceSlot.item = tempItem;

            EmitEvent(new InventoryEvent(InventoryEventType.ItemMoved, toSlot)
                { fromSlot = fromSlot, toSlot = toSlot, item = targetSlot.item, quantity = targetSlot.Quantity });
            return true;
        }

        // 获取槽位信息
        public InventorySlot GetSlot(int index)
        {
            if (index < 0 || index >= slots.Length)
            {
                return null;
            }
            return slots[index];
        }

        // 获取所有槽位
        public InventorySlot[] GetAllSlots()
        {
            return slots;
        }

        // 获取背包容量
        public int GetCapacity()
        {
            return capacity;
        }

        /// <summary>手动触发指定槽位的 SlotChanged 事件，用于跨背包拖拽后刷新 UI。</summary>
        public void NotifySlotChanged(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            EmitEvent(new InventoryEvent(InventoryEventType.SlotChanged, slotIndex)
                { item = slots[slotIndex].item, quantity = slots[slotIndex].Quantity });
        }

        // 获取已使用的槽位数量
        public int GetUsedSlots()
        {
            return slots.Count(slot => slot.item != null);
        }

        // 获取空闲槽位数量
        public int GetFreeSlots()
        {
            return capacity - GetUsedSlots();
        }

        // 查找物品
        public InventorySlot[] FindItem(string itemId)
        {
            return slots.Where(slot => slot.item != null && slot.item.ConfigId == itemId).ToArray();
        }

        // 获取物品总数量
        public int GetItemCount(string itemId)
        {
            return slots
                .Where(slot => slot.item != null && slot.item.ConfigId == itemId)
                .Sum(slot => slot.Quantity);
        }

        // 检查是否有足够的物品
        public bool HasItem(string itemId, int quantity = 1)
        {
            return GetItemCount(itemId) >= quantity;
        }

        // 使用物品
        public bool UseItem(int slotIndex, int quantity = 1)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return false;
            var slot = slots[slotIndex];
            if (slot.item == null || slot.Quantity < quantity) return false;

            var usedItem = slot.item;
            bool success = RemoveItem(slotIndex, quantity);
            if (success) Debug.Log($"使用物品: {usedItem.Name} x{quantity}");
            return success;
        }

        // 清空背包
        public void Clear()
        {
            ClearQuickBar();
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].item != null) RemoveItem(i, slots[i].Quantity);
        }

        // 整理背包（将相同物品堆叠）
        public void Organize()
        {
            var itemGroups = new Dictionary<string, (BaseItemConfig config, int totalQuantity)>();
            var nonStackables = new List<BaseItem>();

            foreach (var slot in slots)
            {
                if (slot.item == null) continue;
                if (slot.item is StackableItem si && si.config != null)
                {
                    string id = si.ConfigId;
                    if (itemGroups.ContainsKey(id))
                    {
                        var existing = itemGroups[id];
                        itemGroups[id] = (existing.config, existing.totalQuantity + si.quantity);
                    }
                    else
                    {
                        itemGroups[id] = (si.config, si.quantity);
                    }
                }
                else
                {
                    nonStackables.Add(slot.item);
                }
            }

            ClearQuickBar();
            for (int i = 0; i < slots.Length; i++) slots[i].item = null;

            foreach (var kvp in itemGroups)
                AddItem(kvp.Value.config, kvp.Value.totalQuantity);

            foreach (var inst in nonStackables)
            {
                int empty = FindEmptySlot();
                if (empty >= 0) slots[empty].item = inst;
            }

            UpdateQuickBarReferences();
        }

        // ===== 快捷栏相关方法 =====

        // 获取快捷栏容量
        public int GetQuickBarCapacity()
        {
            return quickBarCapacity;
        }

        // 获取快捷栏槽位
        public QuickBarSlot GetQuickBarSlot(int index)
        {
            if (index < 0 || index >= quickBarSlots.Length)
            {
                return null;
            }
            return quickBarSlots[index];
        }

        // 获取所有快捷栏槽位
        public QuickBarSlot[] GetAllQuickBarSlots()
        {
            return quickBarSlots;
        }

        // 将背包中的物品添加到快捷栏
        public bool AddToQuickBar(int inventorySlotIndex, int quickBarSlotIndex)
        {
            if (inventorySlotIndex < 0 || inventorySlotIndex >= slots.Length ||
                quickBarSlotIndex < 0 || quickBarSlotIndex >= quickBarSlots.Length)
            {
                return false;
            }

            var inventorySlot = slots[inventorySlotIndex];
            if (inventorySlot.item == null)
            {
                return false;
            }

            var quickBarSlot = quickBarSlots[quickBarSlotIndex];

            // 设置快捷栏槽位 - 直接引用背包槽位
            quickBarSlot.linkedSlot = inventorySlot;
            quickBarSlot.linkedSlotIndex = inventorySlotIndex;

            EmitEvent(new InventoryEvent(InventoryEventType.QuickBarChanged)
            {
                quickBarSlotIndex = quickBarSlotIndex
            });

            return true;
        }

        // 从快捷栏移除物品
        public bool RemoveFromQuickBar(int quickBarSlotIndex)
        {
            if (quickBarSlotIndex < 0 || quickBarSlotIndex >= quickBarSlots.Length)
            {
                return false;
            }

            var quickBarSlot = quickBarSlots[quickBarSlotIndex];

            // 清空快捷栏槽位的引用
            quickBarSlot.linkedSlot = null;
            quickBarSlot.linkedSlotIndex = InventoryConstants.INVALID_SLOT_INDEX;

            EmitEvent(new InventoryEvent(InventoryEventType.QuickBarChanged)
            {
                quickBarSlotIndex = quickBarSlotIndex
            });

            return true;
        }

        // 使用快捷栏中的物品
        public bool UseQuickBarItem(int quickBarSlotIndex, int quantity = 1)
        {
            if (quickBarSlotIndex < 0 || quickBarSlotIndex >= quickBarSlots.Length) return false;

            var quickBarSlot = quickBarSlots[quickBarSlotIndex];
            if (quickBarSlot.linkedSlot?.item == null) return false;

            string itemName = quickBarSlot.linkedSlot.item.Name;
            bool success = UseItem(quickBarSlot.linkedSlot.slotIndex, quantity);
            if (success)
            {
                UpdateQuickBarSlot(quickBarSlotIndex);
                Debug.Log($"从快捷栏使用物品: {itemName} x{quantity}");
            }
            return success;
        }

        // 更新快捷栏槽位（同步背包数据）
        private void UpdateQuickBarSlot(int quickBarSlotIndex)
        {
            var quickBarSlot = quickBarSlots[quickBarSlotIndex];
            if (quickBarSlot.linkedSlot == null)
            {
                return;
            }

            // 如果背包中的物品已经用完，清空快捷栏槽位
            if (quickBarSlot.linkedSlot.item == null)
            {
                RemoveFromQuickBar(quickBarSlotIndex);
            }
            else
            {
                // 快捷栏槽位自动同步背包数据，无需手动更新
                EmitEvent(new InventoryEvent(InventoryEventType.QuickBarSlotChanged)
                {
                    quickBarSlotIndex = quickBarSlotIndex
                });
            }
        }

        // 更新所有快捷栏引用（在整理背包后调用）
        private void UpdateQuickBarReferences()
        {
            for (int i = 0; i < quickBarSlots.Length; i++)
            {
                var quickBarSlot = quickBarSlots[i];
                if (quickBarSlot.linkedSlot?.item != null)
                {
                    int newSlotIndex = FindItemSlot(quickBarSlot.linkedSlot.item.ConfigId);
                    if (newSlotIndex != InventoryConstants.INVALID_SLOT_INDEX)
                    {
                        quickBarSlot.linkedSlot = slots[newSlotIndex];
                        quickBarSlot.linkedSlotIndex = newSlotIndex;
                        UpdateQuickBarSlot(i);
                    }
                    else
                    {
                        RemoveFromQuickBar(i);
                    }
                }
            }
        }

        // 查找物品在背包中的第一个槽位
        private int FindItemSlot(string itemId)
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].item != null && slots[i].item.ConfigId == itemId) return i;
            return InventoryConstants.INVALID_SLOT_INDEX;
        }

        // 清空快捷栏
        public void ClearQuickBar()
        {
            for (int i = 0; i < quickBarSlots.Length; i++)
            {
                RemoveFromQuickBar(i);
            }
        }

        // 获取快捷栏中指定物品的数量
        public int GetQuickBarItemCount(string itemId)
        {
            return quickBarSlots
                .Where(slot => slot.linkedSlot?.item != null && slot.linkedSlot.item.ConfigId == itemId)
                .Sum(slot => slot.linkedSlot?.Quantity ?? 0);
        }

        // 检查快捷栏中是否有指定物品
        public bool HasQuickBarItem(string itemId, int quantity = 1)
        {
            return GetQuickBarItemCount(itemId) >= quantity;
        }

        // 序列化后重建快捷栏引用
        public void RebuildQuickBarReferences()
        {
            if (eventListeners == null)
            {
                eventListeners = new Dictionary<InventoryEventType, List<InventoryEventListener>>();
            }

            // 重建背包引用
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].inventory = this;
            }

            // 重建快捷栏引用
            for (int i = 0; i < quickBarSlots.Length; i++)
            {
                if (quickBarSlots[i].linkedSlotIndex >= 0 && quickBarSlots[i].linkedSlotIndex < slots.Length)
                {
                    quickBarSlots[i].linkedSlot = slots[quickBarSlots[i].linkedSlotIndex];
                }
            }
        }
    }
}