using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Immortal.Core
{
    /// <summary>
    /// 五行系统 (Five Phases System)
    /// 包含五行属性定义、相克关系和相关工具方法
    /// </summary>

    /// <summary>
    /// 五行枚举
    /// </summary>
    public enum FivePhases
    {
        Metal = 0,  // 金
        Wood = 1,   // 木
        Water = 2,  // 水
        Fire = 3,   // 火
        Earth = 4   // 土
    }

    /// <summary>
    /// 五行工具类
    /// </summary>
    public static class FivePhasesUtils
    {
        /// <summary>
        /// 五行中文名称映射
        /// </summary>
        private static readonly Dictionary<FivePhases, string> FivePhasesNames = new Dictionary<FivePhases, string>
        {
            { FivePhases.Metal, "金" },
            { FivePhases.Wood, "木" },
            { FivePhases.Water, "水" },
            { FivePhases.Fire, "火" },
            { FivePhases.Earth, "土" }
        };

        /// <summary>
        /// 五行相克关系
        /// 金克木，木克土，土克水，水克火，火克金
        /// </summary>
        private static readonly Dictionary<FivePhases, FivePhases> FivePhasesRestraint = new Dictionary<FivePhases, FivePhases>
        {
            { FivePhases.Metal, FivePhases.Wood },   // 金克木
            { FivePhases.Wood, FivePhases.Earth },   // 木克土
            { FivePhases.Earth, FivePhases.Water },  // 土克水
            { FivePhases.Water, FivePhases.Fire },   // 水克火
            { FivePhases.Fire, FivePhases.Metal }    // 火克金
        };

        /// <summary>
        /// 五行相生关系
        /// 金生水，水生木，木生火，火生土，土生金
        /// </summary>
        private static readonly Dictionary<FivePhases, FivePhases> FivePhasesGeneration = new Dictionary<FivePhases, FivePhases>
        {
            { FivePhases.Metal, FivePhases.Water },  // 金生水
            { FivePhases.Water, FivePhases.Wood },   // 水生木
            { FivePhases.Wood, FivePhases.Fire },    // 木生火
            { FivePhases.Fire, FivePhases.Earth },   // 火生土
            { FivePhases.Earth, FivePhases.Metal }   // 土生金
        };

        /// <summary>
        /// 获取五行名称
        /// </summary>
        public static string GetName(FivePhases phase)
        {
            return FivePhasesNames[phase];
        }

        /// <summary>
        /// 获取所有五行
        /// </summary>
        public static FivePhases[] GetAllPhases()
        {
            return new FivePhases[] { FivePhases.Metal, FivePhases.Wood, FivePhases.Water, FivePhases.Fire, FivePhases.Earth };
        }

        /// <summary>
        /// 检查是否相克（attacker 克制 defender）
        /// </summary>
        public static bool IsRestraint(FivePhases attacker, FivePhases defender)
        {
            return FivePhasesRestraint.ContainsKey(attacker) && FivePhasesRestraint[attacker] == defender;
        }

        /// <summary>
        /// 检查是否相生（generator 生 generated）
        /// </summary>
        public static bool IsGeneration(FivePhases generator, FivePhases generated)
        {
            return FivePhasesGeneration.ContainsKey(generator) && FivePhasesGeneration[generator] == generated;
        }

        /// <summary>
        /// 获取克制的元素
        /// </summary>
        public static FivePhases GetRestrainedPhase(FivePhases phase)
        {
            return FivePhasesRestraint[phase];
        }

        /// <summary>
        /// 获取生成的元素
        /// </summary>
        public static FivePhases GetGeneratedPhase(FivePhases phase)
        {
            return FivePhasesGeneration[phase];
        }

        /// <summary>
        /// 获取被克制自己的元素
        /// </summary>
        public static FivePhases GetRestrainingPhase(FivePhases phase)
        {
            return FivePhasesRestraint.FirstOrDefault(kvp => kvp.Value == phase).Key;
        }

        /// <summary>
        /// 获取生成自己的元素
        /// </summary>
        public static FivePhases GetGeneratingPhase(FivePhases phase)
        {
            return FivePhasesGeneration.FirstOrDefault(kvp => kvp.Value == phase).Key;
        }

        /// <summary>
        /// 计算五行相克的伤害倍率
        /// </summary>
        public static float GetRestraintDamageMultiplier(FivePhases attacker, FivePhases defender)
        {
            if (IsRestraint(attacker, defender))
            {
                return 1.3f; // 相克时增加30%伤害
            }
            if (IsRestraint(defender, attacker))
            {
                return 0.7f; // 被克时减少30%伤害
            }
            return 1.0f; // 无相克关系
        }

        /// <summary>
        /// 计算五行相生的增益倍率
        /// </summary>
        public static float GetGenerationBonusMultiplier(FivePhases generator, FivePhases generated)
        {
            if (IsGeneration(generator, generated))
            {
                return 1.1f; // 相生时增加10%效果
            }
            return 1.0f; // 无相生关系
        }

        /// <summary>
        /// 获取五行关系描述
        /// </summary>
        public static string GetRelationshipDescription(FivePhases phase1, FivePhases phase2)
        {
            string name1 = GetName(phase1);
            string name2 = GetName(phase2);

            if (IsRestraint(phase1, phase2))
            {
                return $"{name1}克{name2}";
            }
            if (IsRestraint(phase2, phase1))
            {
                return $"{name2}克{name1}";
            }
            if (IsGeneration(phase1, phase2))
            {
                return $"{name1}生{name2}";
            }
            if (IsGeneration(phase2, phase1))
            {
                return $"{name2}生{name1}";
            }
            return $"{name1}与{name2}无特殊关系";
        }

        /// <summary>
        /// 从数字转换为五行枚举（兼容旧代码）
        /// </summary>
        public static FivePhases FromNumber(int num)
        {
            if (num >= 0 && num <= 4)
            {
                return (FivePhases)num;
            }
            throw new ArgumentException($"Invalid phase number: {num}. Must be 0-4.");
        }

        /// <summary>
        /// 从字符串转换为五行枚举
        /// </summary>
        public static FivePhases? FromString(string str)
        {
            string lowerStr = str.ToLower();
            switch (lowerStr)
            {
                case "金":
                case "metal":
                    return FivePhases.Metal;
                case "木":
                case "wood":
                    return FivePhases.Wood;
                case "水":
                case "water":
                    return FivePhases.Water;
                case "火":
                case "fire":
                    return FivePhases.Fire;
                case "土":
                case "earth":
                    return FivePhases.Earth;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// 五行属性接口
    /// 用于需要五行属性的对象
    /// </summary>
    public interface IHasFivePhases
    {
        FivePhases Phase { get; }
    }

    /// <summary>
    /// 五行相克效果
    /// </summary>
    [System.Serializable]
    public class FivePhasesEffect
    {
        public FivePhases sourcePhase;
        public FivePhases targetPhase;
        public float damageMultiplier;
        public string description;

        public FivePhasesEffect(FivePhases sourcePhase, FivePhases targetPhase, float damageMultiplier, string description)
        {
            this.sourcePhase = sourcePhase;
            this.targetPhase = targetPhase;
            this.damageMultiplier = damageMultiplier;
            this.description = description;
        }
    }

    // 兼容性常量（向后兼容）
    public static class PhaseConstants
    {
        public static readonly FivePhases PHASE_METAL = FivePhases.Metal;
        public static readonly FivePhases PHASE_WOOD = FivePhases.Wood;
        public static readonly FivePhases PHASE_WATER = FivePhases.Water;
        public static readonly FivePhases PHASE_FIRE = FivePhases.Fire;
        public static readonly FivePhases PHASE_EARTH = FivePhases.Earth;

        public static readonly string[] PHASE_NAMES = {
            FivePhasesUtils.GetName(FivePhases.Metal),
            FivePhasesUtils.GetName(FivePhases.Wood),
            FivePhasesUtils.GetName(FivePhases.Water),
            FivePhasesUtils.GetName(FivePhases.Fire),
            FivePhasesUtils.GetName(FivePhases.Earth)
        };
    }
}