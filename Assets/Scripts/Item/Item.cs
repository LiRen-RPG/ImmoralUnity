using System;
using System.Collections.Generic;
using UnityEngine;
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

    // 通用物品基类
    [System.Serializable]
    public abstract class BaseItem
    {
        public string id;
        public string name;
        public ItemType type;
        public ItemRarity rarity;
        public string description;
        public string icon;
        public bool stackable = true;
        public int maxStack = 99;
        public FivePhases? phase; // 五行属性
        public CultivationRealm? requiredRealm; // 需要的修仙境界

        protected BaseItem()
        {
        }

        protected BaseItem(string id, string name, ItemType type, ItemRarity rarity, string description = "",
                          string icon = "", bool stackable = true, int maxStack = 99, FivePhases? phase = null)
        {
            this.id = id;
            this.name = name;
            this.type = type;
            this.rarity = rarity;
            this.description = description;
            this.icon = icon;
            this.stackable = stackable;
            this.maxStack = maxStack;
            this.phase = phase;
        }

        public virtual string GetDisplayName()
        {
            return name;
        }

        public virtual string GetDetailedDescription()
        {
            return description;
        }
    }

    // 弹药
    [System.Serializable]
    public class AmmoItem : BaseItem
    {
        public float damage;
        public List<string> compatibleWeapons; // 可用法器id

        public AmmoItem() : base()
        {
            type = ItemType.Ammo;
            compatibleWeapons = new List<string>();
        }

        public AmmoItem(string id, string name, ItemRarity rarity, float damage, List<string> compatibleWeapons,
                       string description = "", string icon = "", bool stackable = true, int maxStack = 99,
                       FivePhases? phase = null)
            : base(id, name, ItemType.Ammo, rarity, description, icon, stackable, maxStack, phase)
        {
            this.damage = damage;
            this.compatibleWeapons = compatibleWeapons ?? new List<string>();
        }
    }

    // 法器/武器
    [System.Serializable]
    public class WeaponItem : BaseItem
    {
        public float attack;
        public float range;
        public string ammoType; // 需要的弹药类型id
        public string specialEffect;

        public WeaponItem() : base()
        {
            type = ItemType.Weapon;
        }

        public WeaponItem(string id, string name, ItemRarity rarity, float attack, float range,
                         string ammoType = "", string specialEffect = "", string description = "",
                         string icon = "", FivePhases? phase = null)
            : base(id, name, ItemType.Weapon, rarity, description, icon, false, 1, phase)
        {
            this.attack = attack;
            this.range = range;
            this.ammoType = ammoType;
            this.specialEffect = specialEffect;
        }
    }

    // 符箓
    [System.Serializable]
    public class TalismanItem : BaseItem
    {
        public string effect; // 例如：火球术、护身符等
        public float duration; // 持续时间（秒）

        public TalismanItem() : base()
        {
            type = ItemType.Talisman;
        }

        public TalismanItem(string id, string name, ItemRarity rarity, string effect, float duration = 0,
                           string description = "", string icon = "", bool stackable = true, int maxStack = 99,
                           FivePhases? phase = null)
            : base(id, name, ItemType.Talisman, rarity, description, icon, stackable, maxStack, phase)
        {
            this.effect = effect;
            this.duration = duration;
        }
    }

    // 丹药
    [System.Serializable]
    public class PillItem : BaseItem
    {
        public float restoreHp;
        public float restoreMp;
        public float restoreCultivation; // 恢复/增加修为值
        public float breakthroughBonus;  // 突破境界成功率加成（0~1）
        public string buff; // 增益效果
        public float duration;

        public PillItem() : base()
        {
            type = ItemType.Pill;
        }

        public PillItem(string id, string name, ItemRarity rarity, float restoreHp = 0, float restoreMp = 0,
                       float restoreCultivation = 0, float breakthroughBonus = 0,
                       string buff = "", float duration = 0, string description = "", string icon = "",
                       bool stackable = true, int maxStack = 99, FivePhases? phase = null)
            : base(id, name, ItemType.Pill, rarity, description, icon, stackable, maxStack, phase)
        {
            this.restoreHp = restoreHp;
            this.restoreMp = restoreMp;
            this.restoreCultivation = restoreCultivation;
            this.breakthroughBonus = breakthroughBonus;
            this.buff = buff;
            this.duration = duration;
        }
    }

    // 材料
    [System.Serializable]
    public class MaterialItem : BaseItem
    {
        public int grade; // 品阶

        public MaterialItem() : base()
        {
            type = ItemType.Material;
        }

        public MaterialItem(string id, string name, ItemRarity rarity, int grade = 1, string description = "",
                           string icon = "", bool stackable = true, int maxStack = 99, FivePhases? phase = null)
            : base(id, name, ItemType.Material, rarity, description, icon, stackable, maxStack, phase)
        {
            this.grade = grade;
        }
    }

    // 功法/秘籍
    [System.Serializable]
    public class BookItem : BaseItem
    {
        public string skillName;
        public SkillType skillType;
        public int requiredLevel;

        public enum SkillType
        {
            Active,
            Passive
        }

        public BookItem() : base()
        {
            type = ItemType.Book;
        }

        public BookItem(string id, string name, ItemRarity rarity, string skillName, SkillType skillType,
                       int requiredLevel = 1, string description = "", string icon = "", FivePhases? phase = null)
            : base(id, name, ItemType.Book, rarity, description, icon, false, 1, phase)
        {
            this.skillName = skillName;
            this.skillType = skillType;
            this.requiredLevel = requiredLevel;
        }
    }

    // 法宝/宝物
    [System.Serializable]
    public class TreasureItem : BaseItem
    {
        public float power;
        public string uniqueEffect;

        public TreasureItem() : base()
        {
            type = ItemType.Treasure;
        }

        public TreasureItem(string id, string name, ItemRarity rarity, float power, string uniqueEffect = "",
                           string description = "", string icon = "", FivePhases? phase = null)
            : base(id, name, ItemType.Treasure, rarity, description, icon, false, 1, phase)
        {
            this.power = power;
            this.uniqueEffect = uniqueEffect;
        }
    }

    // 工具
    [System.Serializable]
    public class ToolItem : BaseItem
    {
        public float efficiency; // 工具效率
        public float durability; // 耐久度

        public ToolItem() : base()
        {
            type = ItemType.Tool;
        }

        public ToolItem(string id, string name, ItemRarity rarity, float efficiency, float durability,
                       string description = "", string icon = "", FivePhases? phase = null)
            : base(id, name, ItemType.Tool, rarity, description, icon, false, 1, phase)
        {
            this.efficiency = efficiency;
            this.durability = durability;
        }
    }

    // 任务物品
    [System.Serializable]
    public class QuestItem : BaseItem
    {
        public string questId; // 关联的任务ID

        public QuestItem() : base()
        {
            type = ItemType.Quest;
        }

        public QuestItem(string id, string name, string questId, string description = "", string icon = "")
            : base(id, name, ItemType.Quest, ItemRarity.Common, description, icon, false, 1)
        {
            this.questId = questId;
        }
    }

    // 阵盘
    [System.Serializable]
    public class FormationItem : BaseItem
    {
        public int formationLevel; // 阵法等级
        public List<FivePhases> requiredElements; // 所需五行元素

        public FormationItem() : base()
        {
            type = ItemType.Formation;
            requiredElements = new List<FivePhases>();
        }

        public FormationItem(string id, string name, ItemRarity rarity, int formationLevel,
                            List<FivePhases> requiredElements, string description = "", string icon = "",
                            FivePhases? phase = null)
            : base(id, name, ItemType.Formation, rarity, description, icon, false, 1, phase)
        {
            this.formationLevel = formationLevel;
            this.requiredElements = requiredElements ?? new List<FivePhases>();
        }
    }
}