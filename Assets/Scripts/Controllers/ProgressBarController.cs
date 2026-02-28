using UnityEngine;

namespace Immortal.Controllers
{
    // 进度条控制器（通过 RectMask2D.padding 右边距裁剪控制圆角血条进度）
    public class ProgressBarController : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.RectMask2D fillMask;

        private float fullWidth;

#if UNITY_EDITOR
        [Header("调试")]
        [Range(0f, 1f)]
        [SerializeField] private float progress = 1f;

        private void OnValidate()
        {
            if (fillMask == null)
            {
                Transform t = transform.Find("Canvas/FillMask");
                if (t != null) fillMask = t.GetComponent<UnityEngine.UI.RectMask2D>();
            }
            if (fillMask != null && fullWidth == 0f)
                fullWidth = ((RectTransform)fillMask.transform).rect.width;
            SetProgress(progress);
        }
#endif

        private void Awake()
        {
            if (fillMask == null)
            {
                Transform t = transform.Find("Canvas/FillMask");
                if (t != null) fillMask = t.GetComponent<UnityEngine.UI.RectMask2D>();
            }
            if (fillMask != null)
                fullWidth = ((RectTransform)fillMask.transform).rect.width;
        }

        public void SetProgress(float progress)
        {
            if (fillMask == null) return;
            // 通过右边距裁剪：rightPadding = fullWidth * (1 - progress)
            float rightPad = fullWidth * (1f - Mathf.Clamp01(progress));
            fillMask.padding = new Vector4(0f, 0f, rightPad, 0f);
        }
    }
}
