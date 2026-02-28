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
    /// 八卦阵盘数据，管理各卦位的物品槽与技能信息
    /// </summary>
    [System.Serializable]
    public class EightTrigramsFormationPlate
    {
        // 每个卦位对应 3 个槽位，共 8 卦 × 3 槽 = 24 槽
        private const int SLOTS_PER_TRIGRAM = 3;
        private const int TRIGRAM_COUNT = 8;

        // 各槽位存储的物品（使用 BaseItem 基类）
        private Immortal.Item.BaseItem[] slots = new Immortal.Item.BaseItem[TRIGRAM_COUNT * SLOTS_PER_TRIGRAM];

        // 各卦位绑定的技能
        private Dictionary<EightTrigramsType, SkillConfig> trigramSkills =
            new Dictionary<EightTrigramsType, SkillConfig>();

        /// <summary>
        /// 获取指定卦位的所有槽位索引
        /// </summary>
        public int[] GetSlotIndicesForTrigram(EightTrigramsType trigram)
        {
            int baseIndex = (int)trigram * SLOTS_PER_TRIGRAM;
            int[] indices = new int[SLOTS_PER_TRIGRAM];
            for (int i = 0; i < SLOTS_PER_TRIGRAM; i++)
            {
                indices[i] = baseIndex + i;
            }
            return indices;
        }

        /// <summary>
        /// 获取指定槽位的物品
        /// </summary>
        public Immortal.Item.BaseItem GetItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return null;
            return slots[slotIndex];
        }

        /// <summary>
        /// 在指定槽位放入物品
        /// </summary>
        public void SetItem(int slotIndex, Immortal.Item.BaseItem item)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            slots[slotIndex] = item;
        }

        /// <summary>
        /// 获取指定卦位的等级（由该卦位拥有物品的数量决定，0 ~ MAX_EFFECT_LEVELS-1）
        /// </summary>
        public int? GetTrigramLevel(EightTrigramsType trigram)
        {
            int[] slotIndices = GetSlotIndicesForTrigram(trigram);
            int itemCount = 0;
            foreach (int idx in slotIndices)
            {
                if (slots[idx] != null) itemCount++;
            }
            if (itemCount == 0) return null;
            // 等级 0、1、2 对应物品数 1、2、3
            return Mathf.Clamp(itemCount - 1, 0, 2);
        }

        /// <summary>
        /// 获取指定卦位绑定的技能（如未绑定则返回 null）
        /// </summary>
        public SkillConfig GetTrigramSkill(EightTrigramsType trigram)
        {
            trigramSkills.TryGetValue(trigram, out SkillConfig skill);
            return skill;
        }

        /// <summary>
        /// 为指定卦位绑定技能
        /// </summary>
        public void SetTrigramSkill(EightTrigramsType trigram, SkillConfig skill)
        {
            trigramSkills[trigram] = skill;
        }
    }
}
