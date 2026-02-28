using UnityEngine;

namespace Immortal.Controllers
{
    /// <summary>
    /// 挂载在 shadow 子对象上。
    /// 禁用 MeshRenderer，改用 Graphics.DrawMesh 在 Camera.onPreRender 中以手动 TRS 矩阵直接提交渲染，
    /// 完全绕过 Transform 层级，shadow 的 X/Z 跟随 target，Y 恒为 0。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ShadowFollow : MonoBehaviour
    {
        [Tooltip("跟随的目标（留空时自动使用父对象）")]
        public Transform target;

        private void Awake()
        {
            if (target == null && transform.parent != null)
                target = transform.parent;

        }

        private void OnEnable()
        {
            Camera.onPreRender += DrawShadow;
        }

        private void OnDisable()
        {
            Camera.onPreRender -= DrawShadow;
        }

        private void DrawShadow(Camera cam)
        {
          transform.position = new Vector3(target.position.x, 0.01f, target.position.z);
        }
    }
}
