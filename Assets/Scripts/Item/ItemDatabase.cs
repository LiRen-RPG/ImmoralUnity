using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Immortal.Core;

namespace Immortal.Item
{
    /// <summary>
    /// 从 Resources/Data/Items/ 下按类型分文件直接加载物品实例：
    ///   Ammo.json / Weapon.json / Talisman.json / Pill.json /
    ///   Material.json / Book.json / Treasure.json / Tool.json / Formation.json
    /// 每个 JSON 文件结构：{ "items": [ { ...字段... }, ... ] }
    /// 字段名与对应 Item 类的字段名一致，由 Newtonsoft.Json 直接反序列化。
    /// 首次访问时自动加载，可通过 Reload() 手动重新加载。
    /// </summary>
    public static class ItemDatabase
    {
        private const string Folder = "Data/Items";

        // 对应各 JSON 文件的 { "items": [...] } 结构
        private class ItemList<T> { public List<T> items; }

        private static Dictionary<string, BaseItemConfig> _items;

        private static Dictionary<string, BaseItemConfig> Items
        {
            get { if (_items == null) Load(); return _items; }
        }

        // ── 公共 API ──────────────────────────────────────────────

        /// <summary>按 id 获取物品配置（不存在返回 null）</summary>
        public static BaseItemConfig Get(string id)
        {
            Items.TryGetValue(id, out var item);
            return item;
        }

        /// <summary>获取全部物品配置列表</summary>
        public static IReadOnlyCollection<BaseItemConfig> GetAll() => Items.Values;

        /// <summary>按类型筛选</summary>
        public static List<BaseItemConfig> GetByType(ItemType type)
        {
            var result = new List<BaseItemConfig>();
            foreach (var item in Items.Values)
                if (item.type == type) result.Add(item);
            return result;
        }

        /// <summary>按五行属性筛选</summary>
        public static List<BaseItemConfig> GetByPhase(FivePhases phase)
        {
            var result = new List<BaseItemConfig>();
            foreach (var item in Items.Values)
                if (item.phase == phase) result.Add(item);
            return result;
        }

        /// <summary>按境界筛选</summary>
        public static List<BaseItemConfig> GetByRealm(CultivationRealm realm)
        {
            var result = new List<BaseItemConfig>();
            foreach (var item in Items.Values)
                if (item.requiredRealm == realm) result.Add(item);
            return result;
        }

        /// <summary>组合筛选</summary>
        public static List<BaseItemConfig> GetBy(ItemType? type = null, FivePhases? phase = null, CultivationRealm? realm = null)
        {
            var result = new List<BaseItemConfig>();
            foreach (var item in Items.Values)
            {
                if (type.HasValue  && item.type          != type.Value) continue;
                if (phase.HasValue && item.phase         != phase)      continue;
                if (realm.HasValue && item.requiredRealm != realm)      continue;
                result.Add(item);
            }
            return result;
        }

        /// <summary>重新从 JSON 加载（热更新时使用）</summary>
        public static void Reload() { _items = null; }

        // ── 内部加载 ─────────────────────────────────────────────

        private static void Load()
        {
            _items = new Dictionary<string, BaseItemConfig>();
            int total = 0;

            total += LoadFile<AmmoConfig>     ("Ammo");
            total += LoadFile<WeaponConfig>   ("Weapon");
            total += LoadFile<TalismanConfig> ("Talisman");
            total += LoadFile<PillConfig>     ("Pill");
            total += LoadFile<MaterialConfig> ("Material");
            total += LoadFile<BookConfig>     ("Book");
            total += LoadFile<TreasureConfig> ("Treasure");
            total += LoadFile<ToolConfig>     ("Tool");
            total += LoadFile<FormationConfig>("Formation");

            Debug.Log($"[ItemDatabase] 全部加载完成：共 {total} 条物品");
        }

        private static int LoadFile<T>(string fileName) where T : BaseItemConfig
        {
            string path = $"{Folder}/{fileName}";
            var textAsset = Resources.Load<TextAsset>(path);
            if (textAsset == null)
            {
                Debug.LogWarning($"[ItemDatabase] 未找到 Resources/{path}.json，跳过");
                return 0;
            }

            ItemList<T> file;
            try { file = JsonConvert.DeserializeObject<ItemList<T>>(textAsset.text); }
            catch (Exception ex)
            {
                Debug.LogError($"[ItemDatabase] 解析 {fileName}.json 失败: {ex.Message}");
                return 0;
            }

            if (file?.items == null)
            {
                Debug.LogWarning($"[ItemDatabase] {fileName}.json 中 items 数组为空");
                return 0;
            }

            int loaded = 0;
            foreach (var item in file.items)
            {
                if (item == null || string.IsNullOrEmpty(item.id))
                {
                    Debug.LogWarning($"[ItemDatabase] {fileName}.json 中有条目缺少 id，已跳过");
                    continue;
                }
                if (_items.ContainsKey(item.id))
                {
                    Debug.LogWarning($"[ItemDatabase] 重复 id: {item.id}（{fileName}.json），已跳过");
                    continue;
                }
                _items[item.id] = item;
                loaded++;
            }

            Debug.Log($"[ItemDatabase] {fileName}.json：加载 {loaded} 条");
            return loaded;
        }
    }
}
