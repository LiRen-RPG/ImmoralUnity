using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Immortal.Utils
{
    /// <summary>
    /// 精灵加载器
    /// 负责从Resources目录加载所有2D UI贴图并缓存
    /// 适用于图标、UI元素、界面背景等各种2D精灵资源
    /// </summary>
    public class SpriteLoader
    {
        private static SpriteLoader instance = null;
        private Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        private Dictionary<string, Task<Sprite>> loadingTasks = new Dictionary<string, Task<Sprite>>();

        // 单例模式
        public static SpriteLoader GetInstance()
        {
            if (instance == null)
            {
                instance = new SpriteLoader();
            }
            return instance;
        }

        /// <summary>
        /// 加载2D精灵并返回Sprite
        /// </summary>
        /// <param name="spritePath">精灵资源路径（相对于Resources目录）</param>
        /// <returns>Task&lt;Sprite&gt;</returns>
        public async Task<Sprite> LoadSprite(string spritePath)
        {
            if (string.IsNullOrEmpty(spritePath))
            {
                return null;
            }

            // 检查缓存
            if (cache.ContainsKey(spritePath))
            {
                return cache[spritePath];
            }

            // 检查是否正在加载
            if (loadingTasks.ContainsKey(spritePath))
            {
                return await loadingTasks[spritePath];
            }

            // 开始加载
            var loadingTask = LoadSpriteInternal(spritePath);
            loadingTasks[spritePath] = loadingTask;

            try
            {
                var sprite = await loadingTask;
                if (sprite != null)
                {
                    cache[spritePath] = sprite;
                }
                return sprite;
            }
            catch (System.Exception error)
            {
                Debug.LogError($"Failed to load sprite from {spritePath}: {error}");
                return null;
            }
            finally
            {
                loadingTasks.Remove(spritePath);
            }
        }

        /// <summary>
        /// 内部加载逻辑 - 仅从Resources目录加载
        /// </summary>
        private async Task<Sprite> LoadSpriteInternal(string spritePath)
        {
            return await LoadFromResources(spritePath);
        }

        /// <summary>
        /// 从Resources资源加载精灵
        /// </summary>
        private async Task<Sprite> LoadFromResources(string path)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // 尝试直接加载Sprite
                    Sprite sprite = Resources.Load<Sprite>(path);
                    if (sprite != null)
                    {
                        return sprite;
                    }

                    // 如果直接加载Sprite失败，尝试加载Texture2D
                    Texture2D texture = Resources.Load<Texture2D>(path);
                    if (texture != null)
                    {
                        // 从Texture2D创建Sprite
                        sprite = Sprite.Create(
                            texture, 
                            new Rect(0, 0, texture.width, texture.height), 
                            new Vector2(0.5f, 0.5f)
                        );
                        return sprite;
                    }

                    Debug.LogError($"Failed to load sprite from resources: {path}");
                    return null;
                }
                catch (System.Exception error)
                {
                    Debug.LogError($"Error loading sprite from resources {path}: {error}");
                    return null;
                }
            });
        }

        /// <summary>
        /// 预加载精灵
        /// </summary>
        /// <param name="spritePaths">精灵资源路径数组</param>
        public async Task PreloadSprites(string[] spritePaths)
        {
            List<Task<Sprite>> tasks = new List<Task<Sprite>>();
            
            foreach (string path in spritePaths)
            {
                tasks.Add(LoadSprite(path));
            }

            await Task.WhenAll(tasks);
            Debug.Log($"Preloaded {spritePaths.Length} sprites");
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public void ClearCache()
        {
            // 释放精灵资源
            foreach (var sprite in cache.Values)
            {
                if (sprite != null && sprite.texture != null)
                {
                    // 注意：只销毁我们创建的精灵，不销毁从Resources直接加载的
                    Object.DestroyImmediate(sprite);
                }
            }
            cache.Clear();
            Debug.Log("Sprite cache cleared");
        }

        /// <summary>
        /// 获取缓存大小
        /// </summary>
        public int GetCacheSize()
        {
            return cache.Count;
        }

        /// <summary>
        /// 检查是否已缓存
        /// </summary>
        public bool IsCached(string spritePath)
        {
            return cache.ContainsKey(spritePath);
        }

        /// <summary>
        /// 从缓存中移除特定精灵
        /// </summary>
        public void RemoveFromCache(string spritePath)
        {
            if (cache.ContainsKey(spritePath))
            {
                var sprite = cache[spritePath];
                if (sprite != null && sprite.texture != null)
                {
                    Object.DestroyImmediate(sprite);
                }
                cache.Remove(spritePath);
            }
        }

        /// <summary>
        /// 同步加载精灵（用于非异步场景）
        /// </summary>
        public Sprite LoadSpriteSync(string spritePath)
        {
            if (string.IsNullOrEmpty(spritePath))
            {
                return null;
            }

            // 检查缓存
            if (cache.ContainsKey(spritePath))
            {
                return cache[spritePath];
            }

            try
            {
                // 尝试直接加载Sprite
                Sprite sprite = Resources.Load<Sprite>(spritePath);
                if (sprite != null)
                {
                    cache[spritePath] = sprite;
                    return sprite;
                }

                // 如果直接加载Sprite失败，尝试加载Texture2D
                Texture2D texture = Resources.Load<Texture2D>(spritePath);
                if (texture != null)
                {
                    sprite = Sprite.Create(
                        texture, 
                        new Rect(0, 0, texture.width, texture.height), 
                        new Vector2(0.5f, 0.5f)
                    );
                    cache[spritePath] = sprite;
                    return sprite;
                }

                Debug.LogError($"Failed to load sprite from resources: {spritePath}");
                return null;
            }
            catch (System.Exception error)
            {
                Debug.LogError($"Error loading sprite from resources {spritePath}: {error}");
                return null;
            }
        }

        /// <summary>
        /// 获取所有已缓存的精灵路径
        /// </summary>
        public string[] GetCachedPaths()
        {
            string[] paths = new string[cache.Count];
            cache.Keys.CopyTo(paths, 0);
            return paths;
        }

        /// <summary>
        /// 获取内存使用统计
        /// </summary>
        public CacheStats GetCacheStats()
        {
            CacheStats stats = new CacheStats();
            stats.count = cache.Count;
            stats.totalMemoryKB = 0;

            foreach (var sprite in cache.Values)
            {
                if (sprite != null && sprite.texture != null)
                {
                    // 估算内存使用量（宽 × 高 × 4字节）
                    int memoryBytes = sprite.texture.width * sprite.texture.height * 4;
                    stats.totalMemoryKB += memoryBytes / 1024;
                }
            }

            return stats;
        }

        public struct CacheStats
        {
            public int count;
            public int totalMemoryKB;
        }
    }
}