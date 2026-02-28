using UnityEngine;

namespace Immortal.Controllers
{
    /// <summary>
    /// 挂到正交相机上，对原始画面施加横向拉伸。
    /// stretchX = 1.0 → 无拉伸
    /// stretchX = 2.0 → 内容横向拉伸为原来 2 倍（变宽）
    /// stretchX = 0.5 → 内容横向压缩为原来 0.5 倍（变窄）
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class StretchCamera : MonoBehaviour
    {
        [Tooltip("横向拉伸系数（1 = 原始比例，>1 变宽，<1 变窄）")]
        [Min(0.01f)] public float stretchX = 1f;

        private Camera _cam;

        private void OnEnable()
        {
            _cam = GetComponent<Camera>();
            Apply();
        }

        private void OnValidate()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            Apply();
        }

        private void LateUpdate()
        {
            // 屏幕尺寸可能在运行时变化（窗口缩放），每帧重新计算
            Apply();
        }

        private void OnDisable()
        {
            // 恢复默认投影矩阵
            if (_cam != null)
                _cam.ResetProjectionMatrix();
        }

        private void Apply()
        {
            if (_cam == null || !_cam.orthographic) return;

            float screenAspect = (float)Screen.width / Screen.height;

            // 基础正交投影矩阵（与相机默认一致）
            Matrix4x4 proj = Matrix4x4.Ortho(
                -_cam.orthographicSize * screenAspect,
                 _cam.orthographicSize * screenAspect,
                -_cam.orthographicSize,
                 _cam.orthographicSize,
                _cam.nearClipPlane,
                _cam.farClipPlane);

            // 直接对 X 轴施加拉伸系数
            // *=stretchX → m00 变大 → 相机可见世界宽度缩小 → 内容横向被拉伸放大
            proj.m00 *= stretchX;

            _cam.projectionMatrix = proj;
        }
    }
}
