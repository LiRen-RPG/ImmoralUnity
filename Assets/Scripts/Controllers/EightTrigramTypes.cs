using System.Collections.Generic;
using UnityEngine;

namespace Immortal.Controllers
{
    /// <summary>
    /// 八卦类型枚举（震、巽、离、坤、兑、乾、坎、艮）
    /// </summary>
    public enum EightTrigramsType
    {
        Zhen = 0,   // 震
        Xun  = 1,   // 巽
        Li   = 2,   // 离
        Kun  = 3,   // 坤
        Dui  = 4,   // 兑
        Qian = 5,   // 乾
        Kan  = 6,   // 坎
        Gen  = 7    // 艮
    }

    /// <summary>
    /// 技能配置数据
    /// </summary>
    [System.Serializable]
    public class SkillConfig
    {
        public string id;
        public string name;
        public float power = 20f;
        public string description;
        public EightTrigramsType trigramType;
    }

    /// <summary>
    /// 单个卦位的槽位数据（3 个物品槽），序列化时保存物品 ID，运行时缓存 BaseItemConfig 引用。
    /// </summary>
    [System.Serializable]
    public class TrigramSlotData
    {
        public string[] itemIds = new string[3];    // 序列化：按槽序存储物品 ID

        [System.NonSerialized]
        public Immortal.Item.BaseItemConfig[] items;      // 运行时缓存，通过 Hydrate() 填充

        public TrigramSlotData() { items = new Immortal.Item.BaseItemConfig[3]; }

        public Immortal.Item.BaseItemConfig Get(int slot)
        {
            if (items == null) items = new Immortal.Item.BaseItemConfig[3];
            return (slot >= 0 && slot < 3) ? items[slot] : null;
        }

        public void Set(int slot, Immortal.Item.BaseItemConfig item)
        {
            if (slot < 0 || slot >= 3) return;
            if (items == null) items = new Immortal.Item.BaseItemConfig[3];
            items[slot]    = item;
            itemIds[slot]  = item?.id;
        }

        public int ItemCount()
        {
            if (items == null) return 0;
            int n = 0;
            foreach (var it in items) if (it != null) n++;
            return n;
        }

        /// <summary>将序列化的 itemIds 通过 ItemDatabase 还原为运行时引用。</summary>
        public void Hydrate()
        {
            if (items == null) items = new Immortal.Item.BaseItemConfig[3];
            for (int i = 0; i < 3; i++)
                items[i] = !string.IsNullOrEmpty(itemIds?[i])
                    ? Immortal.Item.ItemDatabase.Get(itemIds[i])
                    : null;
        }
    }

    // FormationInstance 已移至 Assets/Scripts/Item/ItemInstance.cs
    // （与 BaseItem / StackableItem 共同存放于同一文件中）
}
