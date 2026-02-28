using UnityEngine;
using System.Collections.Generic;

namespace Immortal.Utils
{
    /// <summary>
    /// UI 工具类
    /// 提供通用的UI相关工具函数
    /// </summary>
    public static class UIUtils
    {
        /// <summary>
        /// 检查点是否在UI节点的边界内（世界坐标系）
        /// </summary>
        /// <param name="worldPos">要检查的世界坐标点</param>
        /// <param name="rectTransform">目标UI RectTransform</param>
        /// <returns>如果点在节点边界内返回true，否则返回false</returns>
        public static bool IsPointInNodeBounds(Vector2 worldPos, RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return false;
            }

            // 使用Unity的RectTransformUtility检查点是否在RectTransform内
            return RectTransformUtility.RectangleContainsScreenPoint(
                rectTransform, 
                worldPos, 
                Camera.main
            );
        }

        /// <summary>
        /// 检查点是否在UI节点的边界内（本地坐标系）
        /// </summary>
        /// <param name="localPos">要检查的本地坐标点</param>
        /// <param name="rectTransform">目标UI RectTransform</param>
        /// <returns>如果点在节点边界内返回true，否则返回false</returns>
        public static bool IsPointInNodeBoundsLocal(Vector2 localPos, RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return false;
            }

            // 获取RectTransform的rect
            Rect rect = rectTransform.rect;

            // 检查点是否在边界内
            return rect.Contains(localPos);
        }

        /// <summary>
        /// 在指定父节点下查找包含指定世界坐标点的第一个组件
        /// </summary>
        /// <typeparam name="T">要查找的组件类型</typeparam>
        /// <param name="worldPos">世界坐标点</param>
        /// <param name="parent">父节点</param>
        /// <returns>找到的组件实例，如果没找到返回null</returns>
        public static T FindComponentAtPosition<T>(Vector2 worldPos, Transform parent) where T : Component
        {
            T[] components = parent.GetComponentsInChildren<T>();
            
            foreach (T component in components)
            {
                RectTransform rectTransform = component.GetComponent<RectTransform>();
                if (rectTransform != null && IsPointInNodeBounds(worldPos, rectTransform))
                {
                    return component;
                }
            }
            
            return null;
        }

        /// <summary>
        /// 在指定父节点下查找包含指定世界坐标点的所有组件
        /// </summary>
        /// <typeparam name="T">要查找的组件类型</typeparam>
        /// <param name="worldPos">世界坐标点</param>
        /// <param name="parent">父节点</param>
        /// <returns>找到的所有组件实例数组</returns>
        public static T[] FindAllComponentsAtPosition<T>(Vector2 worldPos, Transform parent) where T : Component
        {
            T[] components = parent.GetComponentsInChildren<T>();
            List<T> result = new List<T>();
            
            foreach (T component in components)
            {
                RectTransform rectTransform = component.GetComponent<RectTransform>();
                if (rectTransform != null && IsPointInNodeBounds(worldPos, rectTransform))
                {
                    result.Add(component);
                }
            }
            
            return result.ToArray();
        }

        /// <summary>
        /// 将世界坐标转换为UI本地坐标
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <param name="rectTransform">目标UI RectTransform</param>
        /// <param name="camera">相机（通常是UI相机）</param>
        /// <returns>转换后的本地坐标</returns>
        public static Vector2 WorldToLocalPosition(Vector3 worldPosition, RectTransform rectTransform, Camera camera = null)
        {
            if (camera == null)
            {
                camera = Camera.main;
            }

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                camera.WorldToScreenPoint(worldPosition),
                camera,
                out localPoint
            );

            return localPoint;
        }

        /// <summary>
        /// 将屏幕坐标转换为UI本地坐标
        /// </summary>
        /// <param name="screenPosition">屏幕坐标</param>
        /// <param name="rectTransform">目标UI RectTransform</param>
        /// <param name="camera">相机（通常是UI相机）</param>
        /// <returns>转换后的本地坐标</returns>
        public static Vector2 ScreenToLocalPosition(Vector2 screenPosition, RectTransform rectTransform, Camera camera = null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                screenPosition,
                camera,
                out localPoint
            );

            return localPoint;
        }

        /// <summary>
        /// 获取RectTransform的世界坐标边界
        /// </summary>
        /// <param name="rectTransform">目标RectTransform</param>
        /// <returns>世界坐标边界Bounds</returns>
        public static Bounds GetWorldBounds(RectTransform rectTransform)
        {
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);

            Vector3 min = corners[0];
            Vector3 max = corners[0];

            foreach (Vector3 corner in corners)
            {
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }

            Vector3 center = (min + max) * 0.5f;
            Vector3 size = max - min;

            return new Bounds(center, size);
        }

        /// <summary>
        /// 检查两个RectTransform是否相交
        /// </summary>
        /// <param name="rect1">第一个RectTransform</param>
        /// <param name="rect2">第二个RectTransform</param>
        /// <returns>如果相交返回true</returns>
        public static bool RectTransformsOverlap(RectTransform rect1, RectTransform rect2)
        {
            Bounds bounds1 = GetWorldBounds(rect1);
            Bounds bounds2 = GetWorldBounds(rect2);
            
            return bounds1.Intersects(bounds2);
        }

        /// <summary>
        /// 设置RectTransform的锚点而不改变位置
        /// </summary>
        /// <param name="rectTransform">目标RectTransform</param>
        /// <param name="newAnchor">新的锚点</param>
        public static void SetAnchorWithoutChangingPosition(RectTransform rectTransform, Vector2 newAnchor)
        {
            Vector3 worldPosition = rectTransform.position;
            rectTransform.anchorMin = newAnchor;
            rectTransform.anchorMax = newAnchor;
            rectTransform.position = worldPosition;
        }

        /// <summary>
        /// 将RectTransform适配到另一个RectTransform的大小
        /// </summary>
        /// <param name="target">要调整的RectTransform</param>
        /// <param name="source">源RectTransform</param>
        /// <param name="padding">边距</param>
        public static void FitToRect(RectTransform target, RectTransform source, Vector2 padding = default)
        {
            target.sizeDelta = source.sizeDelta - padding * 2f;
        }

        /// <summary>
        /// 获取UI元素的深度（在Canvas中的层级）
        /// </summary>
        /// <param name="rectTransform">目标RectTransform</param>
        /// <returns>深度值</returns>
        public static int GetUIDepth(RectTransform rectTransform)
        {
            int depth = 0;
            Transform current = rectTransform;
            
            while (current != null && current.GetComponent<Canvas>() == null)
            {
                depth += current.GetSiblingIndex();
                current = current.parent;
            }
            
            return depth;
        }

        /// <summary>
        /// 平滑移动RectTransform到目标位置
        /// </summary>
        /// <param name="rectTransform">要移动的RectTransform</param>
        /// <param name="targetPosition">目标位置</param>
        /// <param name="duration">持续时间</param>
        /// <returns>协程</returns>
        public static System.Collections.IEnumerator MoveToPosition(
            RectTransform rectTransform, 
            Vector3 targetPosition, 
            float duration)
        {
            Vector3 startPosition = rectTransform.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = Mathf.SmoothStep(0f, 1f, t);
                
                rectTransform.anchoredPosition = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            rectTransform.anchoredPosition = targetPosition;
        }

        /// <summary>
        /// 平滑缩放RectTransform到目标大小
        /// </summary>
        /// <param name="rectTransform">要缩放的RectTransform</param>
        /// <param name="targetScale">目标缩放</param>
        /// <param name="duration">持续时间</param>
        /// <returns>协程</returns>
        public static System.Collections.IEnumerator ScaleTo(
            RectTransform rectTransform, 
            Vector3 targetScale, 
            float duration)
        {
            Vector3 startScale = rectTransform.localScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = Mathf.SmoothStep(0f, 1f, t);
                
                rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
                yield return null;
            }

            rectTransform.localScale = targetScale;
        }

        /// <summary>
        /// 计算UI元素之间的距离
        /// </summary>
        /// <param name="rect1">第一个RectTransform</param>
        /// <param name="rect2">第二个RectTransform</param>
        /// <returns>距离</returns>
        public static float GetDistance(RectTransform rect1, RectTransform rect2)
        {
            return Vector3.Distance(rect1.position, rect2.position);
        }

        /// <summary>
        /// 设置UI元素的透明度（包括所有子元素）
        /// </summary>
        /// <param name="transform">目标Transform</param>
        /// <param name="alpha">透明度值（0-1）</param>
        public static void SetAlphaRecursive(Transform transform, float alpha)
        {
            CanvasGroup canvasGroup = transform.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = transform.gameObject.AddComponent<CanvasGroup>();
            }
            
            canvasGroup.alpha = alpha;
        }
    }
}