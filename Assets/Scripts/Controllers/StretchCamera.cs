using UnityEngine;

namespace Immortal.Controllers
{
    /// <summary>
    /// 挂到正交相机上，支持：
    ///   · 横向拉伸（stretchX）
    ///   · Y 轴颠倒（flipY）
    ///   · 斜切正交投影（useOblique）—— Forward 不必与 Right/Up 平面垂直
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class StretchCamera : MonoBehaviour
    {
        // ── 基础参数 ──────────────────────────────────────────────────
        [Tooltip("横向拉伸系数（1 = 原始比例，>1 变宽，<1 变窄）")]
        [Min(0.01f)] public float stretchX = 1f;

        [Tooltip("颠倒 Y 轴（上下翻转画面）")]
        public bool flipY = false;

        // ── 斜切投影参数 ──────────────────────────────────────────────
        [Header("斜切投影 (Oblique)")]
        [Tooltip("启用斜切正交投影，使用下方三个世界空间向量替代相机默认轴")]
        public bool useOblique = false;
        private Camera _cam;

        // ── Unity 回调 ────────────────────────────────────────────────
        private void OnEnable()  { _cam = GetComponent<Camera>(); Apply(); }
        private void OnValidate(){ if (_cam == null) _cam = GetComponent<Camera>(); Apply(); }
        private void LateUpdate(){ Apply(); }   // 每帧刷新，应对窗口缩放
        private void OnDisable() { if (_cam != null) _cam.ResetProjectionMatrix(); }

        // ── 应用投影矩阵 ──────────────────────────────────────────────
        private void Apply()
        {
            if (_cam == null || !_cam.orthographic) return;

            float aspect = (float)Screen.width / Screen.height;

            // 标准正交投影（含拉伸 / 翻转）
            float halfW = _cam.orthographicSize * aspect;
            float halfH = _cam.orthographicSize;
            Matrix4x4 proj = Matrix4x4.Ortho(
                -halfW, halfW,
                flipY ?  halfH : -halfH,
                flipY ? -halfH :  halfH,
                _cam.nearClipPlane, _cam.farClipPlane);

            proj.m00 *= stretchX;
            
            if (useOblique )
            {
                Vector3 obliqueUp = _cam.transform.InverseTransformDirection(Vector3.up);  
                Vector3 normalizedObliqueUp = obliqueUp.normalized;
                normalizedObliqueUp.z = 0;
                float cos = normalizedObliqueUp.normalized.y;  // 与世界 Up 的夹角余弦
                float tan = Mathf.Sqrt(1 - cos * cos) / cos;  // 斜切程度（切线值）
                proj.m01 = tan*proj.m00;
            }
            _cam.projectionMatrix = proj;
            
        }
        
    }
}
