using System.Collections.Generic;
using UnityEngine;
using Immortal.Utils;
namespace Immortal.Controllers
{
    /// <summary>
    /// 将此组件挂到任意 GameObject 上，即可在编辑模式和运行模式下自动生成 L 型 Mesh。
    /// 修改 Inspector 中任意参数时，Mesh 实时更新。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class LShapeComponent : MonoBehaviour
    {
        [Header("L型尺寸（两平面垂直相交）")]
        [Tooltip("两平面共享边的宽度（X 轴，单位：米）")]
        [Min(0.01f)] public float width   = 2f;

        [Tooltip("水平面的纵深（Z 轴，单位：米）")]
        [Min(0.01f)] public float depth = 1.5f;

        [Tooltip("L型总长度（水平纵深 + 竖直高度，Y 轴，单位：米）")]
        [Min(0.01f)] public float totalLength = 2f;

        private MeshFilter _meshFilter;

        private void OnEnable()
        {
            _meshFilter = GetComponent<MeshFilter>();
            Rebuild();
        }

        private void OnValidate()
        {
            // 保证 depth <= totalLength - 0.1
            depth = Mathf.Min(depth, Mathf.Max(totalLength - 0.1f, 0.01f));

            if (_meshFilter == null)
                _meshFilter = GetComponent<MeshFilter>();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null) Rebuild();
            };
#endif
        }

        private void Rebuild()
        {
            // 运行时直接使用编辑器已生成的资源，不重建
            if (Application.isPlaying) return;

            if (_meshFilter == null) return;

            Mesh old = _meshFilter.sharedMesh;
            if (old != null && old.name == "LShape")
                DestroyImmediate(old);

            float vHeight = Mathf.Max(totalLength - depth, 0.01f);
            _meshFilter.sharedMesh = LShapeMeshGenerator.Generate(width, depth, vHeight);

            // 若挂有 MeshCollider，同步更新其 sharedMesh
            var mc = GetComponent<MeshCollider>();
            if (mc != null)
                mc.sharedMesh = _meshFilter.sharedMesh;

            RebuildColliders(depth, vHeight);
        }

        private const float ColliderThickness = 0.02f;

        /// <summary>
        /// 按 mesh 尺寸自动设置 BoxCollider：
        ///   子物体 "ground"    — 地板（水平面，y=0）
        ///   子物体 "wall_far"  — 远竖面（z=depth 处，封闭内侧）
        ///   子物体 "wall_near" — 近竖面（z=0 处，封闭外侧）
        /// </summary>
        private void RebuildColliders(float d, float h)
        {
            const float pad = 0.1f;

            // 地板子物体
            var ground   = GetOrCreateChild("ground", LayerMask.NameToLayer("Ground"));
            var box0     = ground.GetComponent<BoxCollider>();
            if (box0 == null) box0 = ground.AddComponent<BoxCollider>();
            box0.center  = new Vector3(0f, 0f, d * 0.5f);
            box0.size    = new Vector3(width, ColliderThickness, d + pad * 2f);

            // 远竖面子物体
            var wallFar  = GetOrCreateChild("wall_far", 0);
            var box1     = wallFar.GetComponent<BoxCollider>();
            if (box1 == null) box1 = wallFar.AddComponent<BoxCollider>();
            box1.center  = new Vector3(0f, h * 0.5f, d + pad + 2.5f);
            box1.size    = new Vector3(width, h, 5f);

            // 近竖面子物体
            var wallNear = GetOrCreateChild("wall_near", 0);
            var box2     = wallNear.GetComponent<BoxCollider>();
            if (box2 == null) box2 = wallNear.AddComponent<BoxCollider>();
            box2.center  = new Vector3(0f, h * 0.5f, -pad);
            box2.size    = new Vector3(width, h, ColliderThickness);
        }

        private GameObject GetOrCreateChild(string childName, int layer = 0)
        {
            Transform child = transform.Find(childName);
            if (child != null) return child.gameObject;
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.layer = layer;
            return go;
        }

        private void OnDestroy()
        {
            // 运行时 mesh 由场景管理，无需手动清理
            // 编辑模式下移除组件时销毁动态生成的 mesh
            if (!Application.isPlaying && _meshFilter != null)
            {
                Mesh m = _meshFilter.sharedMesh;
                if (m != null && m.name == "LShape")
                    DestroyImmediate(m);
            }
        }
    }
}
