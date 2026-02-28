using UnityEngine;

namespace Immortal.Controllers
{
    public class Camera3DScene : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera; // 需要同步的目标相机

        private Camera selfCamera;

        private void Start()
        {
            selfCamera = GetComponent<Camera>();
            
            if (selfCamera == null)
            {
                Debug.LogError("Camera3DScene: 当前对象没有Camera组件!");
                enabled = false;
                return;
            }

            if (targetCamera == null)
            {
                Debug.LogWarning("Camera3DScene: 目标相机未设置!");
            }
        }

        private void Update()
        {
            if (selfCamera == null || targetCamera == null) return;

            SyncCameraParameters();
        }

        /// <summary>
        /// 同步相机参数
        /// </summary>
        private void SyncCameraParameters()
        {
            // 同步正交相机高度（如果是正交模式）
            if (targetCamera.orthographic)
            {
                selfCamera.orthographicSize = targetCamera.orthographicSize;
            }

            // 同步视野角度（如果是透视模式）
            selfCamera.fieldOfView = targetCamera.fieldOfView;

            // 同步近裁剪面和远裁剪面
            selfCamera.nearClipPlane = targetCamera.nearClipPlane;
            selfCamera.farClipPlane = targetCamera.farClipPlane;

            // 同步投影模式（正交/透视）
            selfCamera.orthographic = targetCamera.orthographic;

            // 同步相机视口矩形
            selfCamera.rect = targetCamera.rect;

            // 同步渲染路径
            selfCamera.renderingPath = targetCamera.renderingPath;

            // 同步深度
            selfCamera.depth = targetCamera.depth;

            // 同步渲染目标
            selfCamera.targetTexture = targetCamera.targetTexture;

            // 同步背景颜色
            selfCamera.backgroundColor = targetCamera.backgroundColor;

            // 同步清除标志
            selfCamera.clearFlags = targetCamera.clearFlags;

            // 同步天空盒（Unity Camera 无 skybox 属性，由 RenderSettings 统一管理，此处跳过）
            // selfCamera.skybox = targetCamera.skybox;

            // 同步HDR设置
            selfCamera.allowHDR = targetCamera.allowHDR;

            // 同步MSAA设置
            selfCamera.allowMSAA = targetCamera.allowMSAA;

            // 同步动态分辨率
            selfCamera.allowDynamicResolution = targetCamera.allowDynamicResolution;
        }

        /// <summary>
        /// 设置目标相机
        /// </summary>
        /// <param name="target">目标相机</param>
        public void SetTargetCamera(Camera target)
        {
            targetCamera = target;
        }

        /// <summary>
        /// 获取目标相机
        /// </summary>
        /// <returns>当前的目标相机</returns>
        public Camera GetTargetCamera()
        {
            return targetCamera;
        }

        /// <summary>
        /// 获取自身相机
        /// </summary>
        /// <returns>自身的相机组件</returns>
        public Camera GetSelfCamera()
        {
            return selfCamera;
        }

        /// <summary>
        /// 启用或禁用相机参数同步
        /// </summary>
        /// <param name="enable">是否启用同步</param>
        public void SetSyncEnabled(bool enable)
        {
            enabled = enable;
        }

        /// <summary>
        /// 手动执行一次相机参数同步
        /// </summary>
        public void SyncOnce()
        {
            if (selfCamera != null && targetCamera != null)
            {
                SyncCameraParameters();
            }
        }

        /// <summary>
        /// 同步相机的世界变换（位置、旋转、缩放）
        /// </summary>
        public void SyncTransform()
        {
            if (selfCamera != null && targetCamera != null)
            {
                transform.position = targetCamera.transform.position;
                transform.rotation = targetCamera.transform.rotation;
                transform.localScale = targetCamera.transform.localScale;
            }
        }

        /// <summary>
        /// 检查是否有有效的目标相机
        /// </summary>
        /// <returns>如果目标相机有效则返回true</returns>
        public bool HasValidTarget()
        {
            return targetCamera != null && selfCamera != null;
        }

        /// <summary>
        /// 重置相机参数到默认值
        /// </summary>
        public void ResetToDefault()
        {
            if (selfCamera == null) return;

            // 重置到Unity默认相机参数
            selfCamera.fieldOfView = 60f;
            selfCamera.nearClipPlane = 0.3f;
            selfCamera.farClipPlane = 1000f;
            selfCamera.orthographic = false;
            selfCamera.orthographicSize = 5f;
            selfCamera.rect = new Rect(0, 0, 1, 1);
            selfCamera.depth = -1;
            selfCamera.backgroundColor = Color.blue;
            selfCamera.clearFlags = CameraClearFlags.Skybox;
            selfCamera.allowHDR = true;
            selfCamera.allowMSAA = true;
            selfCamera.allowDynamicResolution = false;
        }

        private void OnValidate()
        {
            // 在Inspector中修改参数时自动验证
            if (Application.isPlaying && HasValidTarget())
            {
                SyncOnce();
            }
        }
    }
}