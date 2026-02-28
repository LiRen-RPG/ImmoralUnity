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
        [Min(0.01f)] public float hDepth  = 1.5f;

        [Tooltip("竖直面的高度（Y 轴，单位：米）")]
        [Min(0.01f)] public float vHeight = 2f;

        private MeshFilter _meshFilter;

        private void OnEnable()
        {
            _meshFilter = GetComponent<MeshFilter>();
            Rebuild();
        }

        private void OnValidate()
        {
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

            _meshFilter.sharedMesh = LShapeMeshGenerator.Generate(width, hDepth, vHeight);

            // 若挂有 MeshCollider，同步更新其 sharedMesh
            var mc = GetComponent<MeshCollider>();
            if (mc != null)
                mc.sharedMesh = _meshFilter.sharedMesh;
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
