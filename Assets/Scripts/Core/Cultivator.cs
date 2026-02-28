using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Immortal.Core
{
    // 定义攻击效果
    [System.Serializable]
    public class AttackEffect
    {
        public float extraDamage; // 额外伤害
        public Debuff debuff; // Debuff 实例

        public AttackEffect(float extraDamage, Debuff debuff = null)
        {
            this.extraDamage = extraDamage;
            this.debuff = debuff;
        }
    }

    // 定义技能接口
    [System.Serializable]
    public class Skill
    {
        public string id;
        public string name;
        public FivePhases element; // 技能五行属性
        public float baseDamage; // 基础伤害
        public float damageMultiplier; // 伤害倍率
        public float critRateBonus; // 暴击率加成
        public float critDamageMultiplier; // 暴击伤害倍率
        public float accuracyModifier; // 命中修正
        public List<AttackEffect> effects; // 附加效果
        public string description;

        public Skill()
        {
            effects = new List<AttackEffect>();
        }

        public Skill(string id, string name, FivePhases element, float baseDamage, float damageMultiplier,
                    float critRateBonus, float critDamageMultiplier, float accuracyModifier, string description)
        {
            this.id = id;
            this.name = name;
            this.element = element;
            this.baseDamage = baseDamage;
            this.damageMultiplier = damageMultiplier;
            this.critRateBonus = critRateBonus;
            this.critDamageMultiplier = critDamageMultiplier;
            this.accuracyModifier = accuracyModifier;
            this.description = description;
            this.effects = new List<AttackEffect>();
        }
    }

    // 定义技能实例
    public class SkillInstance
    {
        public Skill skill;
        public Cultivator caster;
        public float finalDamage;
        public bool isCritical;
        public float hitChance;

        public SkillInstance(Skill skill, Cultivator caster)
        {
            this.skill = skill;
            this.caster = caster;

            float critRate = this.caster.currentCritRate + this.skill.critRateBonus;
            this.isCritical = UnityEngine.Random.Range(0f, 1f) < critRate;

            // 计算命中率
            this.hitChance = this.caster.currentAccuracy + this.skill.accuracyModifier;
        }

        // 计算对目标的命中率
        public float GetHitChance(Cultivator target)
        {
            float dodgeRate = target.currentSpeed / 10000f; // 简单的闪避计算
            return Mathf.Max(0.1f, this.hitChance - dodgeRate); // 最低10%命中率
        }

        // 判断是否命中目标
        public bool WillHit(Cultivator target)
        {
            return UnityEngine.Random.Range(0f, 1f) < GetHitChance(target);
        }

        // 计算对目标的伤害
        public float GetDamage(Cultivator target)
        {
            float baseDamage = this.skill.baseDamage + this.caster.currentAttack * this.skill.damageMultiplier;

            // 暴击伤害
            if (this.isCritical)
            {
                baseDamage *= this.skill.critDamageMultiplier;
            }

            // 五行相克伤害加成
            float elementWeakness = GetElementalBonus(target);
            baseDamage *= elementWeakness;

            this.finalDamage = Mathf.Max(1f, Mathf.Floor(baseDamage));
            return this.finalDamage;
        }

        // 获取五行相克加成
        private float GetElementalBonus(Cultivator target)
        {
            // 克制目标时增加伤害
            if (FivePhasesUtils.IsRestraint(this.skill.element, target.element))
            {
                return 1.3f; // 30%额外伤害
            }

            // 被克制时减少伤害
            if (FivePhasesUtils.IsRestraint(target.element, this.skill.element))
            {
                return 0.7f; // 30%减免
            }

            return 1.0f; // 无克制关系
        }
    }

    // 定义 Debuff 基类
    public abstract class Debuff
    {
        public string stat;
        public float value;

        protected Debuff(string stat, float value)
        {
            this.stat = stat;
            this.value = value;
        }

        // 抽象方法：应用 debuff 效果
        public abstract void Apply(Cultivator target);

        // 抽象方法：描述 debuff 效果
        public abstract string Describe();
    }

    // 定义具体的 Debuff 子类
    public class AttackDebuff : Debuff
    {
        public AttackDebuff() : base("attack", 0.1f) // 降低攻击力 10%
        {
        }

        public override void Apply(Cultivator target)
        {
            target.currentAttack = target.baseAttack * (1 - this.value);
        }

        public override string Describe()
        {
            return $"攻击力降低 {this.value * 100}%";
        }
    }

    public class DefenseDebuff : Debuff
    {
        public DefenseDebuff() : base("defense", 0.1f) // 降低防御力 10%
        {
        }

        public override void Apply(Cultivator target)
        {
            target.currentDefense = target.baseDefense * (1 - this.value);
        }

        public override string Describe()
        {
            return $"防御力降低 {this.value * 100}%";
        }
    }

    public class SpeedDebuff : Debuff
    {
        public SpeedDebuff() : base("speed", 0.1f) // 降低速度 10%
        {
        }

        public override void Apply(Cultivator target)
        {
            target.currentSpeed = target.baseSpeed * (1 - this.value);
        }

        public override string Describe()
        {
            return $"速度降低 {this.value * 100}%";
        }
    }

    public class CritRateDebuff : Debuff
    {
        public CritRateDebuff() : base("critRate", 0.1f) // 降低暴击率 10%
        {
        }

        public override void Apply(Cultivator target)
        {
            target.currentCritRate = target.baseCritRate * (1 - this.value);
        }

        public override string Describe()
        {
            return $"暴击率降低 {this.value * 100}%";
        }
    }

    public class AccuracyDebuff : Debuff
    {
        public AccuracyDebuff() : base("accuracy", 0.1f) // 降低命中率 10%
        {
        }

        public override void Apply(Cultivator target)
        {
            target.currentAccuracy = target.baseAccuracy * (1 - this.value);
        }

        public override string Describe()
        {
            return $"命中率降低 {this.value * 100}%";
        }
    }

    public enum Gender
    {
        Male = 0,
        Female = 1
    }

    // 定义修仙者类
    [System.Serializable]
    public class Cultivator
    {
        public string name;
        public FivePhases element; // 使用五行枚举表示五行属性
        public Gender gender; // 性别，0=男，1=女
        public float personality; // 性格，0.0=弱势，1.0=强势

        // 基础属性（原始值）
        public float baseAttack;
        public float baseDefense;
        public float baseSpeed;
        public float baseCritRate;
        public float baseAccuracy;

        // 当前属性（受 debuff 影响后的值）
        [System.NonSerialized]
        public float currentAttack;
        [System.NonSerialized]
        public float currentDefense;
        [System.NonSerialized]
        public float currentSpeed;
        [System.NonSerialized]
        public float currentCritRate;
        [System.NonSerialized]
        public float currentAccuracy;

        // 生命值和状态系统
        public float maxHealth;
        [System.NonSerialized]
        public float currentHealth;
        [System.NonSerialized]
        public bool isAlive;

        // 状态效果
        [System.NonSerialized]
        public List<Debuff> activeDebuffs;

        // 技能系统
        public List<Skill> skills;
        public Skill defaultSkill;

        public object actorBase = null; // 可选：类型可指定为ActorBase，避免循环依赖可用object或接口
        public float appearanceAge;

        public Cultivator()
        {
            // 用于反序列化
            activeDebuffs = new List<Debuff>();
            skills = new List<Skill>();
        }

        public Cultivator(
            string name,
            FivePhases element,
            float attack,
            float defense,
            float speed,
            float critRate,
            float accuracy,
            Gender gender = Gender.Male,
            float personality = 0.5f,
            float appearanceAge = 20f) // 外观年龄，默认为20
        {
            this.name = name;
            this.element = element;
            this.gender = gender;
            this.personality = Mathf.Clamp01(personality); // 确保在0-1范围内

            // 初始化基础属性
            this.baseAttack = attack;
            this.baseDefense = defense;
            this.baseSpeed = speed;
            this.baseCritRate = critRate;
            this.baseAccuracy = accuracy;

            // 初始化当前属性
            this.currentAttack = attack;
            this.currentDefense = defense;
            this.currentSpeed = speed;
            this.currentCritRate = critRate;
            this.currentAccuracy = accuracy;
            this.appearanceAge = appearanceAge;

            // 初始化生命值系统
            this.maxHealth = defense * 10; // 最大生命值为防御力的10倍
            this.currentHealth = this.maxHealth;
            this.isAlive = true;

            // 初始化状态效果
            this.activeDebuffs = new List<Debuff>();

            // 初始化技能系统
            this.skills = new List<Skill>();
            InitializeDefaultSkill();
        }

        // 初始化默认技能
        private void InitializeDefaultSkill()
        {
            this.defaultSkill = new Skill(
                $"default_{this.element}",
                $"{FivePhasesUtils.GetName(this.element)}诀",
                this.element,
                50f,
                0.8f,
                0f,
                2.0f,
                0f,
                $"{FivePhasesUtils.GetName(this.element)}属性的基础攻击"
            );
            this.skills.Add(this.defaultSkill);
        }

        // 重置当前属性为原始值
        public void ResetStats()
        {
            this.currentAttack = this.baseAttack;
            this.currentDefense = this.baseDefense;
            this.currentSpeed = this.baseSpeed;
            this.currentCritRate = this.baseCritRate;
            this.currentAccuracy = this.baseAccuracy;
        }

        public SkillInstance CreateSkillInstance(int skillIndex = 0)
        {
            Skill skill = skillIndex < skills.Count ? skills[skillIndex] : defaultSkill;
            return new SkillInstance(skill, this);
        }

        // 受到伤害 - 传入SkillInstance进行伤害计算
        public float TakeDamage(SkillInstance skillInstance)
        {
            if (!this.isAlive)
            {
                return 0;
            }

            // 从SkillInstance获取伤害值
            float damage = skillInstance.GetDamage(this);
            bool isCrit = skillInstance.isCritical;
            FivePhases attackElement = skillInstance.skill.element;

            // 应用防御力减免
            float defense = this.currentDefense;
            float damageReduction = Mathf.Min(0.8f, defense / (defense + 100f)); // 最高80%减免

            // 计算最终伤害
            float finalDamage = damage * (1 - damageReduction);

            // 暴击额外效果（在SkillInstance中已计算基础暴击，这里可以添加额外效果）
            if (isCrit)
            {
                // 可以在这里添加暴击的额外效果，比如特殊状态
            }

            // 应用伤害
            finalDamage = Mathf.Max(1f, Mathf.Floor(finalDamage)); // 至少造成1点伤害
            this.currentHealth = Mathf.Max(0f, this.currentHealth - finalDamage);

            // 检查死亡
            if (this.currentHealth <= 0)
            {
                this.isAlive = false;
                Debug.Log($"{this.name} 已经死亡");

                // 调用ActorBase的死亡回调
                if (this.actorBase != null && this.actorBase.GetType().GetMethod("OnCultivatorDeath") != null)
                {
                    this.actorBase.GetType().GetMethod("OnCultivatorDeath").Invoke(this.actorBase, null);
                }
            }

            // 应用技能效果（debuff等）
            ApplySkillEffects(skillInstance.skill, this);

            return finalDamage;
        }

        // 应用技能效果
        private void ApplySkillEffects(Skill skill, Cultivator target)
        {
            // 这里可以添加状态效果应用逻辑
            // 比如debuff、buff等
            foreach (var effect in skill.effects)
            {
                // 应用效果到目标
                Debug.Log($"对 {target.name} 应用了效果");
            }
        }

        // 检查是否还活着
        public bool IsAliveCheck()
        {
            return this.isAlive && this.currentHealth > 0;
        }

        // 治疗
        public void Heal(float amount)
        {
            if (this.isAlive)
            {
                this.currentHealth = Mathf.Min(this.maxHealth, this.currentHealth + amount);
                Debug.Log($"{this.name} 恢复了 {amount} 点生命值，当前生命值: {this.currentHealth}/{this.maxHealth}");
            }
        }

        // 获取生命值百分比
        public float GetHealthPercentage()
        {
            return this.maxHealth > 0 ? this.currentHealth / this.maxHealth : 0;
        }

        // 添加技能
        public void AddSkill(Skill skill)
        {
            this.skills.Add(skill);
            Debug.Log($"{this.name} 学会了技能: {skill.name}");
        }

        // 获取状态信息
        public string GetStatusInfo()
        {
            int healthPercent = Mathf.FloorToInt(GetHealthPercentage() * 100);
            string status = this.isAlive ? "存活" : "死亡";
            return $"{this.name} ({FivePhasesUtils.GetName(this.element)}): {status} - 生命值: {this.currentHealth}/{this.maxHealth} ({healthPercent}%)";
        }

        /// <summary>
        /// 通过JSON对象创建Cultivator实例
        /// </summary>
        /// <param name="jsonString">符合schema的JSON字符串</param>
        public static Cultivator FromJSON(string jsonString)
        {
            var data = JsonConvert.DeserializeObject<CultivatorData>(jsonString);
            return new Cultivator(
                data.name,
                (FivePhases)data.element,
                data.baseAttack,
                data.baseDefense,
                data.baseSpeed,
                data.baseCritRate,
                data.baseAccuracy,
                (Gender)data.gender,
                data.personality
            );
        }

        /// <summary>
        /// 导出为JSON字符串
        /// </summary>
        public string ToJSON()
        {
            var data = new CultivatorData
            {
                name = this.name,
                element = (int)this.element,
                gender = (int)this.gender,
                personality = this.personality,
                baseAttack = this.baseAttack,
                baseDefense = this.baseDefense,
                baseSpeed = this.baseSpeed,
                baseCritRate = this.baseCritRate,
                baseAccuracy = this.baseAccuracy
            };
            return JsonConvert.SerializeObject(data);
        }
    }

    // Cultivator JSON Data类
    [System.Serializable]
    public class CultivatorData
    {
        public int id;
        public string name;
        public int element;
        public int gender;
        public float personality;
        public float baseAttack;
        public float baseDefense;
        public float baseSpeed;
        public float baseCritRate;
        public float baseAccuracy;
        public float appearanceAge;
    }
}