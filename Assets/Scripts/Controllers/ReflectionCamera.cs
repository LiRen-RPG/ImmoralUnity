using UnityEngine;

namespace Immortal.Controllers
{
    /// <summary>
    /// 挂到镜像相机（ReflectionCamera）上，让其跟踪另一个相机（sourceCamera）
    /// 关于指定水平面（reflectionPlane）的镜像位置和旋转。<br/><br/>
    /// 典型用途：水面反射、地板反射、等距 2.5D 镜像视角。<br/><br/>
    /// 镜像面由 <b>planeOrigin</b>（面上一点）和 <b>planeNormal</b>（法线，默认 Vector3.up）定义。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class ReflectionCamera : MonoBehaviour
    {
        [Tooltip("被镜像的源相机（主相机或视角相机）")]
        public Camera sourceCamera;

        [Header("镜像面")]
        [Tooltip("镜像面上任意一点（世界空间）")]
        public Vector3 planeOrigin = Vector3.zero;

        [Tooltip("镜像面法线方向（世界空间，默认朝上 = 水平镜像面）")]
        public Vector3 planeNormal = Vector3.up;

        [Header("选项")]
        [Tooltip("同步 FOV / orthographicSize")]
        public bool syncProjection = true;

        [Tooltip("同步近远裁剪面")]
        public bool syncClipPlanes = true;

        private Camera _cam;

        private void OnEnable()  => _cam = GetComponent<Camera>();
        private void LateUpdate() => Sync();

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // 在 Scene 视图画出镜像面（半透明圆盘）
            Vector3 n = planeNormal.normalized;
            if (n == Vector3.zero) return;
            Gizmos.color = new Color(0f, 0.8f, 1f, 0.25f);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, n);
            Gizmos.matrix = Matrix4x4.TRS(planeOrigin, rot, Vector3.one);
            // 用若干矩形近似圆盘
            for (float r = 1f; r <= 5f; r += 1f)
                Gizmos.DrawWireSphere(Vector3.zero, r);
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif

        // ── 核心同步 ─────────────────────────────────────────────────
        private void Sync()
        {
            if (sourceCamera == null) return;
            if (_cam == null) _cam = GetComponent<Camera>();

            Vector3 n = planeNormal.normalized;
            if (n == Vector3.zero) return;

            // ── 1. 位置镜像 ──────────────────────────────────────────
            // 将 sourcePos 关于平面（origin, n）做反射：
            //   P' = P - 2 * dot(P - origin, n) * n
            Vector3 srcPos = sourceCamera.transform.position;
            float dist = Vector3.Dot(srcPos - planeOrigin, n);
            Vector3 reflPos = srcPos - 2f * dist * n;
            transform.position = reflPos;

            // ── 2. 旋转镜像 ──────────────────────────────────────────
            // 对 forward 和 up 向量施加相同的反射操作，再重建旋转。
            // 反射向量：V' = V - 2 * dot(V, n) * n  （法线分量取反，切线分量不变）
            Vector3 srcFwd = sourceCamera.transform.forward;
            Vector3 srcUp  = sourceCamera.transform.up;

            Vector3 reflFwd = ReflectVector(srcFwd, n);
            Vector3 reflUp  = ReflectVector(srcUp,  n);

            // 防止退化（forward 与 up 平行时相机会翻转）
            if (reflFwd.sqrMagnitude > 1e-6f && reflUp.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(reflFwd, reflUp);

            // ── 3. 投影参数同步 ──────────────────────────────────────
            if (syncProjection)
            {
                _cam.orthographic       = sourceCamera.orthographic;
                _cam.fieldOfView        = sourceCamera.fieldOfView;
                _cam.orthographicSize   = sourceCamera.orthographicSize;
            }

            if (syncClipPlanes)
            {
                _cam.nearClipPlane = sourceCamera.nearClipPlane;
                _cam.farClipPlane  = sourceCamera.farClipPlane;
            }
        }

        // ── 工具：关于法线 n 反射向量 V ──────────────────────────────
        // V' = V - 2*(V·n)*n
        private static Vector3 ReflectVector(Vector3 v, Vector3 n)
            => v - 2f * Vector3.Dot(v, n) * n;

        // ── 静态工具：构建 4x4 反射矩阵（可供外部使用） ─────────────
        /// <summary>
        /// 构建关于平面（origin, normal）的世界空间 4×4 反射矩阵。<br/>
        /// 可直接用于 <c>GL.LoadProjectionMatrix</c> 或自定义绘制。
        /// </summary>
        public static Matrix4x4 BuildReflectionMatrix(Vector3 origin, Vector3 normal)
        {
            Vector3 n = normal.normalized;
            // plane equation: n·x + d = 0, d = -n·origin
            float d = -Vector3.Dot(n, origin);

            // 标准反射矩阵（对应平面 ax+by+cz+d=0）：
            //   | 1-2a²   -2ab   -2ac  -2ad |
            //   | -2ab   1-2b²   -2bc  -2bd |
            //   | -2ac    -2bc  1-2c²  -2cd |
            //   |   0       0     0      1  |
            float a = n.x, b = n.y, c = n.z;
            return new Matrix4x4(
                new Vector4(1 - 2*a*a,   -2*a*b,   -2*a*c, 0),
                new Vector4(  -2*a*b, 1 - 2*b*b,   -2*b*c, 0),
                new Vector4(  -2*a*c,   -2*b*c, 1 - 2*c*c, 0),
                new Vector4(-2*a*d,     -2*b*d,   -2*c*d,  1)
            );
        }
    }
}
