using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Immortal.Item;

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
        public BaseItem item;
        public int quantity;
        public int slotIndex;
        [System.NonSerialized]
        public Inventory inventory; // 添加背包引用

        public InventorySlot(int slotIndex, Inventory inventory)
        {
            this.item = null;
            this.quantity = 0;
            this.slotIndex = slotIndex;
            this.inventory = inventory;
        }
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
        public BaseItem item;
        public int quantity;
        public int fromSlot;
        public int toSlot;
        public int quickBarSlotIndex; // 快捷栏槽位索引（用于快捷栏相关事件）

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

        // 检查物品是否可堆叠
        private bool IsStackable(BaseItem item)
        {
            return item.stackable && item.maxStack > 1;
        }

        // 获取物品的最大堆叠数量
        private int GetMaxStack(BaseItem item)
        {
            return item.maxStack;
        }

        // 尝试向指定槽位堆叠物品（槽位必须已有相同物品）
        private int TryStackToSlot(BaseItem item, int quantity, InventorySlot slot)
        {
            if (slot.item == null || slot.item.id != item.id || !IsStackable(item))
            {
                return 0;
            }

            int maxStack = GetMaxStack(item);
            int canAdd = Mathf.Min(quantity, maxStack - slot.quantity);

            if (canAdd > 0)
            {
                slot.quantity += canAdd;
            }

            return canAdd;
        }

        // 向空槽位添加物品
        private int AddToEmptySlot(BaseItem item, int quantity, InventorySlot slot)
        {
            if (slot.item != null)
            {
                return 0; // 槽位不为空
            }

            if (IsStackable(item))
            {
                int maxStack = GetMaxStack(item);
                int addQuantity = Mathf.Min(quantity, maxStack);
                slot.item = item;
                slot.quantity = addQuantity;
                return addQuantity;
            }
            else
            {
                // 不可堆叠物品，只能放一个
                if (quantity >= 1)
                {
                    slot.item = item;
                    slot.quantity = 1;
                    return 1;
                }
                return 0;
            }
        }

        // 查找可以堆叠的槽位
        private int FindStackableSlot(BaseItem item)
        {
            if (!IsStackable(item))
            {
                return InventoryConstants.INVALID_SLOT_INDEX;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.item != null && slot.item.id == item.id)
                {
                    int maxStack = GetMaxStack(item);
                    if (slot.quantity < maxStack)
                    {
                        return i;
                    }
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

        // 添加物品到背包
        public bool AddItem(BaseItem item, int quantity = 1)
        {
            if (quantity <= 0)
            {
                return false;
            }

            int remainingQuantity = quantity;

            // 如果物品可堆叠，先尝试堆叠到现有槽位
            if (IsStackable(item))
            {
                for (int i = 0; i < slots.Length && remainingQuantity > 0; i++)
                {
                    var slot = slots[i];
                    if (slot.item != null && slot.item.id == item.id)
                    {
                        int stackedQuantity = TryStackToSlot(item, remainingQuantity, slot);

                        if (stackedQuantity > 0)
                        {
                            remainingQuantity -= stackedQuantity;

                            EmitEvent(new InventoryEvent(InventoryEventType.SlotChanged, i)
                            {
                                item = item,
                                quantity = slot.quantity
                            });
                        }
                    }
                }
            }

            // 将剩余的物品放入空槽位
            while (remainingQuantity > 0)
            {
                int emptySlotIndex = FindEmptySlot();
                if (emptySlotIndex == InventoryConstants.INVALID_SLOT_INDEX)
                {
                    // 背包已满，返回是否完全添加成功
                    return remainingQuantity == 0;
                }

                var emptySlot = slots[emptySlotIndex];
                int addedQuantity = AddToEmptySlot(item, remainingQuantity, emptySlot);

                if (addedQuantity > 0)
                {
                    remainingQuantity -= addedQuantity;

                    EmitEvent(new InventoryEvent(InventoryEventType.ItemAdded, emptySlotIndex)
                    {
                        item = item,
                        quantity = addedQuantity
                    });
                }
                else
                {
                    // 如果添加失败，跳出循环
                    break;
                }
            }

            return remainingQuantity == 0; // 如果所有物品都成功添加，返回true
        }

        // 向指定槽位添加物品
        public bool AddItemToSlot(BaseItem item, int quantity, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length || quantity <= 0)
            {
                return false;
            }

            var slot = slots[slotIndex];

            // 如果槽位为空，直接添加
            if (slot.item == null)
            {
                int addedQuantity = AddToEmptySlot(item, quantity, slot);

                if (addedQuantity > 0)
                {
                    EmitEvent(new InventoryEvent(InventoryEventType.ItemAdded, slotIndex)
                    {
                        item = item,
                        quantity = addedQuantity
                    });

                    return addedQuantity == quantity; // 如果没有完全添加，返回false
                }

                return false;
            }

            // 如果槽位有物品，尝试堆叠
            int stackedQuantity = TryStackToSlot(item, quantity, slot);

            if (stackedQuantity > 0)
            {
                EmitEvent(new InventoryEvent(InventoryEventType.SlotChanged, slotIndex)
                {
                    item = item,
                    quantity = slot.quantity
                });

                return stackedQuantity == quantity; // 如果没有完全添加，返回false
            }

            return false; // 无法添加到此槽位
        }

        // 移除物品
        public bool RemoveItem(int slotIndex, int quantity = 1)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length)
            {
                return false;
            }

            var slot = slots[slotIndex];
            if (slot.item == null || slot.quantity < quantity)
            {
                return false;
            }

            slot.quantity -= quantity;

            if (slot.quantity <= 0)
            {
                var removedItem = slot.item;
                slot.item = null;
                slot.quantity = 0;

                EmitEvent(new InventoryEvent(InventoryEventType.ItemRemoved, slotIndex)
                {
                    item = removedItem,
                    quantity = quantity
                });
            }
            else
            {
                EmitEvent(new InventoryEvent(InventoryEventType.SlotChanged, slotIndex)
                {
                    item = slot.item,
                    quantity = slot.quantity
                });
            }

            return true;
        }

        // 移动物品
        public bool MoveItem(int fromSlot, int toSlot)
        {
            if (fromSlot < 0 || fromSlot >= slots.Length ||
                toSlot < 0 || toSlot >= slots.Length ||
                fromSlot == toSlot)
            {
                return false;
            }

            var sourceSlot = slots[fromSlot];
            var targetSlot = slots[toSlot];

            if (sourceSlot.item == null)
            {
                return false;
            }

            // 如果目标槽位为空，直接移动
            if (targetSlot.item == null)
            {
                targetSlot.item = sourceSlot.item;
                targetSlot.quantity = sourceSlot.quantity;
                sourceSlot.item = null;
                sourceSlot.quantity = 0;

                EmitEvent(new InventoryEvent(InventoryEventType.ItemMoved, toSlot)
                {
                    fromSlot = fromSlot,
                    toSlot = toSlot,
                    item = targetSlot.item,
                    quantity = targetSlot.quantity
                });

                return true;
            }

            // 如果目标槽位有物品，检查是否可以堆叠
            if (sourceSlot.item.id == targetSlot.item.id && IsStackable(sourceSlot.item))
            {
                int maxStack = GetMaxStack(sourceSlot.item);
                int canStack = Mathf.Min(sourceSlot.quantity, maxStack - targetSlot.quantity);

                if (canStack > 0)
                {
                    targetSlot.quantity += canStack;
                    sourceSlot.quantity -= canStack;

                    if (sourceSlot.quantity <= 0)
                    {
                        sourceSlot.item = null;
                        sourceSlot.quantity = 0;
                    }

                    EmitEvent(new InventoryEvent(InventoryEventType.ItemMoved, toSlot)
                    {
                        fromSlot = fromSlot,
                        toSlot = toSlot,
                        item = targetSlot.item,
                        quantity = targetSlot.quantity
                    });

                    return true;
                }
            }

            // 交换物品位置
            var tempItem = targetSlot.item;
            int tempQuantity = targetSlot.quantity;

            targetSlot.item = sourceSlot.item;
            targetSlot.quantity = sourceSlot.quantity;
            sourceSlot.item = tempItem;
            sourceSlot.quantity = tempQuantity;

            EmitEvent(new InventoryEvent(InventoryEventType.ItemMoved, toSlot)
            {
                fromSlot = fromSlot,
                toSlot = toSlot,
                item = targetSlot.item,
                quantity = targetSlot.quantity
            });

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
            return slots.Where(slot => slot.item != null && slot.item.id == itemId).ToArray();
        }

        // 获取物品总数量
        public int GetItemCount(string itemId)
        {
            return slots
                .Where(slot => slot.item != null && slot.item.id == itemId)
                .Sum(slot => slot.quantity);
        }

        // 检查是否有足够的物品
        public bool HasItem(string itemId, int quantity = 1)
        {
            return GetItemCount(itemId) >= quantity;
        }

        // 使用物品（从背包中移除并触发使用逻辑）
        public bool UseItem(int slotIndex, int quantity = 1)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length)
            {
                return false;
            }

            var slot = slots[slotIndex];
            if (slot.item == null || slot.quantity < quantity)
            {
                return false;
            }

            // 保存物品引用用于日志
            var usedItem = slot.item;

            // 从背包中移除物品
            bool success = RemoveItem(slotIndex, quantity);
            if (!success)
            {
                return false;
            }

            Debug.Log($"使用物品: {usedItem.name} x{quantity}");
            return true;
        }

        // 清空背包
        public void Clear()
        {
            // 先清空快捷栏
            ClearQuickBar();

            // 然后清空背包
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].item != null)
                {
                    RemoveItem(i, slots[i].quantity);
                }
            }
        }

        // 整理背包（将相同物品堆叠）
        public void Organize()
        {
            // 获取所有物品
            var allSlots = GetAllSlots();
            var itemGroups = new Dictionary<string, (BaseItem item, int totalQuantity)>();

            // 按物品ID分组
            foreach (var slot in allSlots)
            {
                if (slot.item != null)
                {
                    string itemId = slot.item.id;
                    if (itemGroups.ContainsKey(itemId))
                    {
                        var existing = itemGroups[itemId];
                        itemGroups[itemId] = (existing.item, existing.totalQuantity + slot.quantity);
                    }
                    else
                    {
                        itemGroups[itemId] = (slot.item, slot.quantity);
                    }
                }
            }

            // 清空背包
            Clear();

            // 重新添加物品
            foreach (var kvp in itemGroups)
            {
                AddItem(kvp.Value.item, kvp.Value.totalQuantity);
            }

            // 更新快捷栏引用
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
            if (quickBarSlotIndex < 0 || quickBarSlotIndex >= quickBarSlots.Length)
            {
                return false;
            }

            var quickBarSlot = quickBarSlots[quickBarSlotIndex];
            if (quickBarSlot.linkedSlot == null || quickBarSlot.linkedSlot.item == null)
            {
                return false;
            }

            // 保存物品信息用于日志
            string itemName = quickBarSlot.linkedSlot.item.name;

            // 使用背包中的物品
            bool success = UseItem(quickBarSlot.linkedSlot.slotIndex, quantity);

            if (success)
            {
                // 更新快捷栏显示
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
                if (quickBarSlot.linkedSlot != null && quickBarSlot.linkedSlot.item != null)
                {
                    // 重新查找物品在背包中的位置
                    int newSlotIndex = FindItemSlot(quickBarSlot.linkedSlot.item.id);
                    if (newSlotIndex != InventoryConstants.INVALID_SLOT_INDEX)
                    {
                        // 更新引用到新的背包槽位
                        quickBarSlot.linkedSlot = slots[newSlotIndex];
                        quickBarSlot.linkedSlotIndex = newSlotIndex;
                        UpdateQuickBarSlot(i);
                    }
                    else
                    {
                        // 物品不在背包中了，清空快捷栏槽位
                        RemoveFromQuickBar(i);
                    }
                }
            }
        }

        // 查找物品在背包中的第一个槽位
        private int FindItemSlot(string itemId)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].item != null && slots[i].item.id == itemId)
                {
                    return i;
                }
            }
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
                .Where(slot => slot.linkedSlot != null && slot.linkedSlot.item != null && slot.linkedSlot.item.id == itemId)
                .Sum(slot => slot.linkedSlot?.quantity ?? 0);
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