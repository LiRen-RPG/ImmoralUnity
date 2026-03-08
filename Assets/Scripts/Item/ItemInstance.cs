using System.Collections.Generic;
using UnityEngine;
using Immortal.Core;

namespace Immortal.Item
{
    // ═══════════════════════════════════════════════════════════════
    //  实例层（运行时持有，背包槽位中存储的是实例，不是配置）
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 物品实例基类。背包槽位中持有的是实例，不是配置。
    /// instanceId 唯一标识这一个实体；config 引用其静态配置。
    /// </summary>
    [System.Serializable]
    public abstract class BaseItem
    {
        public string instanceId;
        public BaseItemConfig config;

        public string ConfigId   => config?.id;
        public ItemType Type     => config?.type ?? default;
        public string Name       => config?.name;
        public ItemRarity Rarity => config?.rarity ?? default;

        protected BaseItem() { }

        protected BaseItem(BaseItemConfig config)
        {
            this.instanceId = System.Guid.NewGuid().ToString("N");
            this.config     = config;
        }
    }

    /// <summary>
    /// 可叠加物品实例（丹药、材料、弹药、符箓等）。
    /// 一个实例代表背包里的"一格"，可能含多个数量。
    /// </summary>
    [System.Serializable]
    public class StackableItem : BaseItem
    {
        public int quantity;

        public StackableItem() { }

        public StackableItem(BaseItemConfig config, int quantity = 1) : base(config)
        {
            this.quantity = Mathf.Max(1, quantity);
        }
    }
}

namespace Immortal.Controllers
{
    // ═══════════════════════════════════════════════════════════════
    //  阵盘实例（物品实例 + 八卦运行时数据，两者合一）
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 阵盘实例（MVC Model），同时也是背包中的物品实例。
    /// 融合了原有的 FormationInstance（存档数据）与 EightTrigramsFormationPlate（运行时数据），
    /// 改为按卦位结构存储与序列化物品，直接作为 EightTrigramsFormationUI 的数据模型。
    /// </summary>
    [System.Serializable]
    public class FormationInstance : Immortal.Item.BaseItem
    {
        // instanceId 和 config 由基类 BaseItem 提供
        // configId 保留作序列化用的字符串 key
        public string configId;     // → FormationConfig.id，关联静态配置

        // 按卦位存储，下标与 EightTrigramsType 枚举一致（共 8 卦）
        public TrigramSlotData[] trigramSlots = new TrigramSlotData[8];

        // 运行时技能绑定（不参与序列化）
        [System.NonSerialized]
        private Dictionary<EightTrigramsType, SkillConfig> _trigramSkills;
        private Dictionary<EightTrigramsType, SkillConfig> Skills =>
            _trigramSkills ?? (_trigramSkills = new Dictionary<EightTrigramsType, SkillConfig>());

        public FormationInstance() { InitSlots(); }

        public FormationInstance(string configId)
            : base(Immortal.Item.ItemDatabase.Get(configId))
        {
            this.configId = configId;
            InitSlots();
        }

        private void InitSlots()
        {
            for (int i = 0; i < 8; i++)
                trigramSlots[i] = new TrigramSlotData();
        }

        // ──── 卦位物品访问（存储配置引用） ────────────────────────────────────

        /// <summary>获取指定卦位、槽序（0–2）的物品配置。</summary>
        public Immortal.Item.BaseItemConfig GetItem(EightTrigramsType trigram, int slotInTrigram)
            => trigramSlots[(int)trigram].Get(slotInTrigram);

        /// <summary>设置指定卦位、槽序（0–2）的物品配置，并同步序列化 ID。</summary>
        public void SetItem(EightTrigramsType trigram, int slotInTrigram, Immortal.Item.BaseItemConfig item)
            => trigramSlots[(int)trigram].Set(slotInTrigram, item);

        // ──── 卦位等级 ────────────────────────────────────────────────────────

        /// <summary>根据该卦位放入物品的数量返回等级（0–2），无物品时返回 null。</summary>
        public int? GetTrigramLevel(EightTrigramsType trigram)
        {
            int count = trigramSlots[(int)trigram].ItemCount();
            return count == 0 ? (int?)null : Mathf.Clamp(count - 1, 0, 2);
        }

        // ──── 技能绑定 ────────────────────────────────────────────────────────

        public SkillConfig GetTrigramSkill(EightTrigramsType trigram)
        {
            Skills.TryGetValue(trigram, out SkillConfig skill);
            return skill;
        }

        public void SetTrigramSkill(EightTrigramsType trigram, SkillConfig skill)
            => Skills[trigram] = skill;

        // ──── 水化 ────────────────────────────────────────────────────────────

        /// <summary>从 ItemDatabase 将各卦位的 itemIds 还原为 BaseItem 运行时引用。</summary>
        public void Hydrate()
        {
            for (int i = 0; i < 8; i++)
            {
                if (trigramSlots[i] == null) trigramSlots[i] = new TrigramSlotData();
                trigramSlots[i].Hydrate();
            }
        }
    }
}
