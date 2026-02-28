using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Immortal.Controllers
{
    public class Orders : MonoBehaviour
    {
        // Unity中的Layer定义（对应Cocos Creator的Layer）
        private const int CustomUILayer = 1;
        private const int SceneBackgroundLayer = 2;
        private const int OutputDepthLayer = 4;
        private const int NoDepthLayer = 8;

        /// <summary>
        /// 根据层级和Z坐标重新排序子对象
        /// </summary>
        public void ReorderChildren()
        {
            List<Transform> sceneBackgroundNodes = new List<Transform>();
            List<Transform> outputDepthNodes = new List<Transform>();
            List<Transform> noDepthNodes = new List<Transform>();
            List<Transform> defaultNodes = new List<Transform>();

            // 根据层级分类子对象
            foreach (Transform child in transform)
            {
                // Unity中使用gameObject.layer来判断层级
                if (child.gameObject.layer == SceneBackgroundLayer)
                {
                    sceneBackgroundNodes.Add(child);
                }
                else if (child.gameObject.layer == OutputDepthLayer)
                {
                    outputDepthNodes.Add(child);
                }
                else if (child.gameObject.layer == NoDepthLayer)
                {
                    noDepthNodes.Add(child);
                }
                else
                {
                    defaultNodes.Add(child);
                }
            }

            // 根据Z坐标排序（Z值越大越靠后）
            sceneBackgroundNodes.Sort((a, b) => -b.position.z.CompareTo(-a.position.z));
            outputDepthNodes.Sort((a, b) => -b.position.z.CompareTo(-a.position.z));
            noDepthNodes.Sort((a, b) => -b.position.z.CompareTo(-a.position.z));

            // 合并所有已排序的节点
            var sortedChildren = new List<Transform>();
            sortedChildren.AddRange(defaultNodes);
            sortedChildren.AddRange(sceneBackgroundNodes);
            sortedChildren.AddRange(outputDepthNodes);
            sortedChildren.AddRange(noDepthNodes);

            // 重新设置子对象的顺序
            for (int i = 0; i < sortedChildren.Count; i++)
            {
                sortedChildren[i].SetSiblingIndex(i);
            }
        }

        private void Start()
        {
            ReorderChildren();
        }

        private void Update()
        {
            // 每帧都重新排序（如果需要性能优化，可以改为仅在位置改变时排序）
            ReorderChildren();
        }

        /// <summary>
        /// 手动触发重新排序（用于性能优化，只在需要时调用）
        /// </summary>
        public void ManualReorder()
        {
            ReorderChildren();
        }

        /// <summary>
        /// 添加子对象并自动排序
        /// </summary>
        /// <param name="child">要添加的子对象</param>
        public void AddChildWithReorder(Transform child)
        {
            child.SetParent(transform);
            ReorderChildren();
        }

        /// <summary>
        /// 移除子对象并重新排序
        /// </summary>
        /// <param name="child">要移除的子对象</param>
        public void RemoveChildWithReorder(Transform child)
        {
            if (child.parent == transform)
            {
                child.SetParent(null);
                ReorderChildren();
            }
        }

        /// <summary>
        /// 设置子对象的层级并重新排序
        /// </summary>
        /// <param name="child">目标子对象</param>
        /// <param name="layer">新的层级</param>
        public void SetChildLayerAndReorder(Transform child, int layer)
        {
            if (child.parent == transform)
            {
                child.gameObject.layer = layer;
                ReorderChildren();
            }
        }

        /// <summary>
        /// 获取指定层级的所有子对象
        /// </summary>
        /// <param name="layer">层级</param>
        /// <returns>该层级的所有子对象</returns>
        public List<Transform> GetChildrenByLayer(int layer)
        {
            List<Transform> result = new List<Transform>();
            
            foreach (Transform child in transform)
            {
                if (child.gameObject.layer == layer)
                {
                    result.Add(child);
                }
            }
            
            return result;
        }

        /// <summary>
        /// 根据Z坐标获取最前面的子对象
        /// </summary>
        /// <returns>Z坐标最小（最前面）的子对象</returns>
        public Transform GetFrontmostChild()
        {
            Transform frontmost = null;
            float minZ = float.MaxValue;

            foreach (Transform child in transform)
            {
                if (child.position.z < minZ)
                {
                    minZ = child.position.z;
                    frontmost = child;
                }
            }

            return frontmost;
        }

        /// <summary>
        /// 根据Z坐标获取最后面的子对象
        /// </summary>
        /// <returns>Z坐标最大（最后面）的子对象</returns>
        public Transform GetBackmostChild()
        {
            Transform backmost = null;
            float maxZ = float.MinValue;

            foreach (Transform child in transform)
            {
                if (child.position.z > maxZ)
                {
                    maxZ = child.position.z;
                    backmost = child;
                }
            }

            return backmost;
        }

        /// <summary>
        /// 启用或禁用自动排序
        /// </summary>
        /// <param name="enable">是否启用自动排序</param>
        public void SetAutoReorderEnabled(bool enable)
        {
            enabled = enable;
        }

        /// <summary>
        /// 检查是否需要重新排序（性能优化用）
        /// </summary>
        private bool NeedsReorder()
        {
            // 这里可以实现更复杂的逻辑来判断是否需要重新排序
            // 例如检查子对象的位置是否发生变化
            return true; // 简化实现，总是返回true
        }

        /// <summary>
        /// 优化版的Update，只在需要时重新排序
        /// </summary>
        private void OptimizedUpdate()
        {
            if (NeedsReorder())
            {
                ReorderChildren();
            }
        }
    }
}