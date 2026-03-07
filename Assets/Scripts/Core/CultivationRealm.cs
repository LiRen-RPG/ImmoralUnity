using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Immortal.Core
{
    // 修仙境界枚举
    public enum CultivationRealm
    {
        QiRefining = 0,          // 练气
        FoundationBuilding = 1,  // 筑基
        GoldenCore = 2,          // 金丹
        NascentSoul = 3,         // 元婴
        GodTransformation = 4,   // 化神
        VoidReturning = 5,       // 返虚
        TribulationCrossing = 6, // 渡劫
        Mahayana = 7             // 大乘
    }

    // 境界信息
    [System.Serializable]
    public class CultivationRealmInfo
    {
        public CultivationRealm realm;
        public string chineseName;       // 境界中文名
        public float lifespan;           // 寿命上限（年）
        public float attackMultiplier;   // 攻击力倍率（预留，外部系统可读取）
        public float defenseMultiplier;  // 防御力倍率（预留）
        public float speedMultiplier;    // 速度倍率（预留）
        public float healthMultiplier;   // 生命值上限倍率
        public float critRateBonus;      // 暴击率加成（预留）
        public float accuracyBonus;      // 命中率加成（预留）

        public CultivationRealmInfo(CultivationRealm realm, string chineseName, float lifespan,
            float attackMultiplier, float defenseMultiplier, float speedMultiplier,
            float healthMultiplier, float critRateBonus, float accuracyBonus)
        {
            this.realm = realm;
            this.chineseName = chineseName;
            this.lifespan = lifespan;
            this.attackMultiplier = attackMultiplier;
            this.defenseMultiplier = defenseMultiplier;
            this.speedMultiplier = speedMultiplier;
            this.healthMultiplier = healthMultiplier;
            this.critRateBonus = critRateBonus;
            this.accuracyBonus = accuracyBonus;
        }
    }

    // JSON 反序列化用的数据类
    [System.Serializable]
    public class CultivationRealmJsonEntry
    {
        public int realm;
        public string chineseName;
        public float lifespan;
        public float attackMultiplier;
        public float defenseMultiplier;
        public float speedMultiplier;
        public float healthMultiplier;
        public float critRateBonus;
        public float accuracyBonus;
    }

    [System.Serializable]
    public class CultivationRealmDatabase
    {
        public List<CultivationRealmJsonEntry> realms;
    }

    // 境界工具类
    public static class CultivationRealmUtils
    {
        private const string ResourcePath = "Data/CultivationRealmData";

        private static CultivationRealmInfo[] _realmData;

        private static CultivationRealmInfo[] RealmData
        {
            get
            {
                if (_realmData == null) LoadData();
                return _realmData;
            }
        }

        // 从 Resources/Data/CultivationRealmData.json 加载数据
        private static void LoadData()
        {
            TextAsset textAsset = Resources.Load<TextAsset>(ResourcePath);
            if (textAsset == null)
            {
                Debug.LogError($"[CultivationRealmUtils] 找不到配置文件: Resources/{ResourcePath}.json");
                _realmData = new CultivationRealmInfo[0];
                return;
            }

            var db = JsonConvert.DeserializeObject<CultivationRealmDatabase>(textAsset.text);
            if (db == null || db.realms == null)
            {
                Debug.LogError($"[CultivationRealmUtils] 配置文件格式错误: {ResourcePath}.json");
                _realmData = new CultivationRealmInfo[0];
                return;
            }

            _realmData = new CultivationRealmInfo[db.realms.Count];
            for (int i = 0; i < db.realms.Count; i++)
            {
                var e = db.realms[i];
                _realmData[i] = new CultivationRealmInfo(
                    (CultivationRealm)e.realm,
                    e.chineseName,
                    e.lifespan,
                    e.attackMultiplier,
                    e.defenseMultiplier,
                    e.speedMultiplier,
                    e.healthMultiplier,
                    e.critRateBonus,
                    e.accuracyBonus
                );
            }

            Debug.Log($"[CultivationRealmUtils] 已加载 {_realmData.Length} 个境界配置");
        }

        // 重新加载（热更新时调用）
        public static void Reload()
        {
            _realmData = null;
        }

        public static CultivationRealmInfo GetRealmInfo(CultivationRealm realm)
        {
            int index = (int)realm;
            if (index >= 0 && index < RealmData.Length)
                return RealmData[index];
            return RealmData[0];
        }

        public static string GetChineseName(CultivationRealm realm)
        {
            return GetRealmInfo(realm).chineseName;
        }

        public static float GetLifespan(CultivationRealm realm)
        {
            return GetRealmInfo(realm).lifespan;
        }

        // 是否可以晋升
        public static bool CanAdvance(CultivationRealm realm)
        {
            return realm < CultivationRealm.Mahayana;
        }

        // 获取下一境界
        public static CultivationRealm GetNextRealm(CultivationRealm realm)
        {
            if (CanAdvance(realm))
                return (CultivationRealm)((int)realm + 1);
            return realm;
        }
    }
}
