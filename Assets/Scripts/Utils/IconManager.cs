using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Immortal.Item;

namespace Immortal.Utils
{
    /// <summary>
    /// 图标管理器
    /// 提供便捷的图标管理功能，处理Resources目录中的图标资源
    /// 包括智能路径解析、预加载、批量管理等
    /// </summary>
    public class IconManager
    {
        private static IconManager instance = null;
        private SpriteLoader spriteLoader;
        
        // 全局默认图标路径
        private string defaultIcon = "Textures/Defaults/item_default";

        private IconManager()
        {
            spriteLoader = SpriteLoader.GetInstance();
        }

        public static IconManager GetInstance()
        {
            if (instance == null)
            {
                instance = new IconManager();
            }
            return instance;
        }

        /// <summary>
        /// 获取物品图标路径
        /// 优先使用icon字段，否则按类型自动推断路径，最终回退到默认图标
        /// </summary>
        public string GetItemIconPath(BaseItemConfig item)
        {
            // 使用icon字段
            if (!string.IsNullOrEmpty(item.icon))
            {
                return item.icon;
            }

            // 丹药：图标位于 Resources/Icons/Pills/<id>
            if (item.type == ItemType.Pill)
            {
                return $"Icons/Pills/{item.id}";
            }

            // 阵盘：所有阵盘共用同一图标
            if (item.type == ItemType.Formation)
            {
                return "Icons/阵盘";
            }

            // 使用默认图标
            return defaultIcon;
        }

        /// <summary>
        /// 预加载物品图标
        /// </summary>
        public async Task PreloadItemIcons(BaseItemConfig[] items)
        {
            List<string> iconPaths = new List<string>();
            
            foreach (var item in items)
            {
                iconPaths.Add(GetItemIconPath(item));
            }

            // 去重
            HashSet<string> uniquePaths = new HashSet<string>(iconPaths);
            string[] uniquePathsArray = new string[uniquePaths.Count];
            uniquePaths.CopyTo(uniquePathsArray);
            
            Debug.Log($"Preloading {uniquePathsArray.Length} item icons...");
            await spriteLoader.PreloadSprites(uniquePathsArray);
            Debug.Log("Item icons preloaded successfully");
        }

        /// <summary>
        /// 预加载默认图标
        /// </summary>
        public async Task PreloadDefaultIcon()
        {
            Debug.Log("Preloading default icon...");
            await spriteLoader.PreloadSprites(new string[] { defaultIcon });
            Debug.Log("Default icon preloaded successfully");
        }

        /// <summary>
        /// 设置默认图标
        /// </summary>
        public void SetDefaultIcon(string iconPath)
        {
            defaultIcon = iconPath;
        }

        /// <summary>
        /// 获取默认图标路径
        /// </summary>
        public string GetDefaultIcon()
        {
            return defaultIcon;
        }

        /// <summary>
        /// 加载物品图标
        /// </summary>
        public async Task<Sprite> LoadItemIcon(BaseItemConfig item)
        {
            string iconPath = GetItemIconPath(item);
            return await spriteLoader.LoadSprite(iconPath);
        }

        /// <summary>
        /// 同步加载物品图标
        /// </summary>
        public Sprite LoadItemIconSync(BaseItemConfig item)
        {
            string iconPath = GetItemIconPath(item);
            return spriteLoader.LoadSpriteSync(iconPath);
        }

        /// <summary>
        /// 检查图标是否已缓存
        /// </summary>
        public bool IsIconCached(BaseItemConfig item)
        {
            string iconPath = GetItemIconPath(item);
            return spriteLoader.IsCached(iconPath);
        }

        /// <summary>
        /// 清除所有图标缓存
        /// </summary>
        public void ClearCache()
        {
            spriteLoader.ClearCache();
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public CacheStats GetCacheStats()
        {
            var stats = spriteLoader.GetCacheStats();
            return new CacheStats
            {
                size = stats.count,
                memoryKB = stats.totalMemoryKB
            };
        }

        /// <summary>
        /// 批量预加载图标（用于游戏启动时）
        /// </summary>
        public async Task InitializeIcons(BaseItemConfig[] items = null)
        {
            Debug.Log("Initializing icon system...");
            
            // 先加载默认图标
            await PreloadDefaultIcon();
            
            // 如果提供了物品列表，预加载这些物品的图标
            if (items != null && items.Length > 0)
            {
                await PreloadItemIcons(items);
            }
            
            Debug.Log("Icon system initialized successfully");
        }

        /// <summary>
        /// 获取图标加载器实例（用于高级用法）
        /// </summary>
        public SpriteLoader GetSpriteLoader()
        {
            return spriteLoader;
        }

        /// <summary>
        /// 根据物品类型预加载常用图标
        /// </summary>
        public async Task PreloadCommonIcons()
        {
            string[] commonIconPaths = new string[]
            {
                defaultIcon,
                "Textures/Items/weapon_default",
                "Textures/Items/pill_default", 
                "Textures/Items/material_default",
                "Textures/Items/talisman_default",
                "Textures/Items/formation_default"
            };

            Debug.Log("Preloading common icons...");
            await spriteLoader.PreloadSprites(commonIconPaths);
            Debug.Log("Common icons preloaded successfully");
        }

        /// <summary>
        /// 异步批量加载多个物品的图标
        /// </summary>
        public async Task<Dictionary<BaseItemConfig, Sprite>> LoadMultipleItemIcons(BaseItemConfig[] items)
        {
            Dictionary<BaseItemConfig, Sprite> result = new Dictionary<BaseItemConfig, Sprite>();
            List<Task<Sprite>> loadTasks = new List<Task<Sprite>>();
            
            // 启动所有加载任务
            foreach (var item in items)
            {
                loadTasks.Add(LoadItemIcon(item));
            }

            // 等待所有任务完成
            Sprite[] sprites = await Task.WhenAll(loadTasks);

            // 组装结果
            for (int i = 0; i < items.Length; i++)
            {
                result[items[i]] = sprites[i];
            }

            return result;
        }

        public struct CacheStats
        {
            public int size;
            public int memoryKB;
        }
    }
}