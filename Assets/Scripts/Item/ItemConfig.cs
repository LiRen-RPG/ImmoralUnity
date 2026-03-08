using System;
using System.Collections.Generic;
using Immortal.Core;

namespace Immortal.Item
{
    // 物品类型
    public enum ItemType
    {
        Ammo,         // 弹药
        Weapon,       // 法器/武器
        Talisman,     // 符箓
        Pill,         // 丹药
        Material,     // 材料
        Book,         // 功法/秘籍
        Treasure,     // 法宝
        Tool,         // 工具
        Quest,        // 任务
        Formation,    // 阵盘
        Other         // 其他
    }

    // 品质/稀有度
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary,
        Immortal
    }

    // ═══════════════════════════════════════════════════════════════
    //  配置数据层（从 JSON 加载，存于 ItemDatabase，只读）
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// 物品配置基类。id 来自配置文件，代表物品种类的静态属性。
    /// 子类：PillConfig / WeaponConfig / AmmoConfig / TalismanConfig /
    ///       MaterialConfig / BookConfig / TreasureConfig / ToolConfig /
    ///       QuestConfig / FormationConfig
    /// </summary>
    [System.Serializable]
    public abstract class BaseItemConfig
    {
        public string id;
        public string name;
        public ItemType type;
        public ItemRarity rarity;
        public string description;
        public string icon;
        public bool stackable = true;
        public int maxStack = 99;
        public FivePhases? phase;
        public CultivationRealm? requiredRealm;

        protected BaseItemConfig() { }

        protected BaseItemConfig(string id, string name, ItemType type, ItemRarity rarity,
            string description = "", string icon = "", bool stackable = true,
            int maxStack = 99, FivePhases? phase = null)
        {
            this.id          = id;
            this.name        = name;
            this.type        = type;
            this.rarity      = rarity;
            this.description = description;
            this.icon        = icon;
            this.stackable   = stackable;
            this.maxStack    = maxStack;
            this.phase       = phase;
        }

        public virtual string GetDisplayName()          => name;
        public virtual string GetDetailedDescription()  => description;
    }

    [System.Serializable]
    public class AmmoConfig : BaseItemConfig
    {
        public float damage;
        public List<string> compatibleWeapons;

        public AmmoConfig() : base() { type = ItemType.Ammo; compatibleWeapons = new List<string>(); }

        public AmmoConfig(string id, string name, ItemRarity rarity, float damage,
            List<string> compatibleWeapons, string description = "", string icon = "",
            bool stackable = true, int maxStack = 99, FivePhases? phase = null)
            : base(id, name, ItemType.Ammo, rarity, description, icon, stackable, maxStack, phase)
        {
            this.damage             = damage;
            this.compatibleWeapons  = compatibleWeapons ?? new List<string>();
        }
    }

    [System.Serializable]
    public class WeaponConfig : BaseItemConfig
    {
        public float attack;
        public float range;
        public string ammoType;
        public string specialEffect;

        public WeaponConfig() : base() { type = ItemType.Weapon; }

        public WeaponConfig(string id, string name, ItemRarity rarity, float attack, float range,
            string ammoType = "", string specialEffect = "", string description = "",
            string icon = "", FivePhases? phase = null)
            : base(id, name, ItemType.Weapon, rarity, description, icon, false, 1, phase)
        {
            this.attack        = attack;
            this.range         = range;
            this.ammoType      = ammoType;
            this.specialEffect = specialEffect;
        }
    }

    [System.Serializable]
    public class TalismanConfig : BaseItemConfig
    {
        public string effect;
        public float duration;

        public TalismanConfig() : base() { type = ItemType.Talisman; }

        public TalismanConfig(string id, string name, ItemRarity rarity, string effect,
            float duration = 0, string description = "", string icon = "",
            bool stackable = true, int maxStack = 99, FivePhases? phase = null)
            : base(id, name, ItemType.Talisman, rarity, description, icon, stackable, maxStack, phase)
        {
            this.effect   = effect;
            this.duration = duration;
        }
    }

    [System.Serializable]
    public class PillConfig : BaseItemConfig
    {
        public float restoreHp;
        public float restoreMp;
        public float restoreCultivation;
        public float breakthroughBonus;
        public string buff;
        public float duration;

        public PillConfig() : base() { type = ItemType.Pill; }

        public PillConfig(string id, string name, ItemRarity rarity,
            float restoreHp = 0, float restoreMp = 0, float restoreCultivation = 0,
            float breakthroughBonus = 0, string buff = "", float duration = 0,
            string description = "", string icon = "",
            bool stackable = true, int maxStack = 99, FivePhases? phase = null)
            : base(id, name, ItemType.Pill, rarity, description, icon, stackable, maxStack, phase)
        {
            this.restoreHp          = restoreHp;
            this.restoreMp          = restoreMp;
            this.restoreCultivation = restoreCultivation;
            this.breakthroughBonus  = breakthroughBonus;
            this.buff               = buff;
            this.duration           = duration;
        }
    }

    [System.Serializable]
    public class MaterialConfig : BaseItemConfig
    {
        public int grade;

        public MaterialConfig() : base() { type = ItemType.Material; }

        public MaterialConfig(string id, string name, ItemRarity rarity, int grade = 1,
            string description = "", string icon = "", bool stackable = true,
            int maxStack = 99, FivePhases? phase = null)
            : base(id, name, ItemType.Material, rarity, description, icon, stackable, maxStack, phase)
        {
            this.grade = grade;
        }
    }

    [System.Serializable]
    public class BookConfig : BaseItemConfig
    {
        public string skillName;
        public SkillType skillType;
        public int requiredLevel;

        public enum SkillType { Active, Passive }

        public BookConfig() : base() { type = ItemType.Book; }

        public BookConfig(string id, string name, ItemRarity rarity, string skillName,
            SkillType skillType, int requiredLevel = 1, string description = "",
            string icon = "", FivePhases? phase = null)
            : base(id, name, ItemType.Book, rarity, description, icon, false, 1, phase)
        {
            this.skillName     = skillName;
            this.skillType     = skillType;
            this.requiredLevel = requiredLevel;
        }
    }

    [System.Serializable]
    public class TreasureConfig : BaseItemConfig
    {
        public float power;
        public string uniqueEffect;

        public TreasureConfig() : base() { type = ItemType.Treasure; }

        public TreasureConfig(string id, string name, ItemRarity rarity, float power,
            string uniqueEffect = "", string description = "", string icon = "",
            FivePhases? phase = null)
            : base(id, name, ItemType.Treasure, rarity, description, icon, false, 1, phase)
        {
            this.power        = power;
            this.uniqueEffect = uniqueEffect;
        }
    }

    [System.Serializable]
    public class ToolConfig : BaseItemConfig
    {
        public float efficiency;
        public float durability;

        public ToolConfig() : base() { type = ItemType.Tool; }

        public ToolConfig(string id, string name, ItemRarity rarity, float efficiency,
            float durability, string description = "", string icon = "", FivePhases? phase = null)
            : base(id, name, ItemType.Tool, rarity, description, icon, false, 1, phase)
        {
            this.efficiency = efficiency;
            this.durability = durability;
        }
    }

    [System.Serializable]
    public class QuestConfig : BaseItemConfig
    {
        public string questId;

        public QuestConfig() : base() { type = ItemType.Quest; }

        public QuestConfig(string id, string name, string questId,
            string description = "", string icon = "")
            : base(id, name, ItemType.Quest, ItemRarity.Common, description, icon, false, 1)
        {
            this.questId = questId;
        }
    }

    [System.Serializable]
    public class FormationConfig : BaseItemConfig
    {
        public FormationConfig() : base() { type = ItemType.Formation; }

        public FormationConfig(string id, string name, ItemRarity rarity,
            string description = "", string icon = "")
            : base(id, name, ItemType.Formation, rarity, description, icon, false, 1, null)
        { }
    }
}
