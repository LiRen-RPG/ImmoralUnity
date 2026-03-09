using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Immortal.UI
{
    /// <summary>
    /// 全局唯一的拖拽幻影，由 UIManager 持有。
    /// 每次拖拽开始时调用 Begin()，拖拽过程中调用 Move()，结束时调用 End()。
    /// isFormationSlot 与 stopIndicator 作为当前拖拽的状态封装在此类中。
    /// </summary>
    public class DragProxy
    {
        private GameObject proxyGO;
        private GameObject stopIndicator;
        private Canvas     rootCanvas;

        /// <summary>当前拖拽是否来自阵盘槽位。</summary>
        public bool IsFormationSlot { get; private set; }

        /// <summary>是否有正在进行中的拖拽幻影。</summary>
        public bool IsActive => proxyGO != null;

        /// <summary>
        /// 开始一次新的拖拽幻影。若上一次未正常结束会先销毁旧幻影。
        /// </summary>
        /// <param name="canvas">根 Canvas，用于挂载幻影并做坐标转换。</param>
        /// <param name="itemSprite">物品图标。</param>
        /// <param name="size">幻影尺寸（取自物品图标的 rect.size）。</param>
        /// <param name="formationSlot">是否来自阵盘槽位（会创建禁止覆盖层）。</param>
        /// <param name="stopSprite">禁止图标 Sprite（为 null 时不创建覆盖层）。</param>
        public void Begin(Canvas canvas, Sprite itemSprite, Vector2 size,
                          bool formationSlot, Sprite stopSprite)
        {
            End(); // 确保旧代理已清除

            rootCanvas      = canvas;
            IsFormationSlot = formationSlot;

            proxyGO = new GameObject("DragProxy");
            proxyGO.transform.SetParent(canvas.transform, false);
            proxyGO.transform.SetAsLastSibling();

            var rt       = proxyGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            var img           = proxyGO.AddComponent<Image>();
            img.sprite        = itemSprite;
            img.raycastTarget = false;

            var cg            = proxyGO.AddComponent<CanvasGroup>();
            cg.alpha          = 0.8f;
            cg.blocksRaycasts = false;

            // 阵盘槽位：创建禁止图标覆盖层（初始隐藏）
            if (formationSlot && stopSprite != null)
            {
                stopIndicator = new GameObject("StopIndicator");
                stopIndicator.transform.SetParent(proxyGO.transform, false);

                var srt       = stopIndicator.AddComponent<RectTransform>();
                srt.anchorMin = new Vector2(0.5f, 0.5f);
                srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot     = new Vector2(0.5f, 0.5f);
                srt.sizeDelta = size;

                var sImg           = stopIndicator.AddComponent<Image>();
                sImg.sprite        = stopSprite;
                sImg.raycastTarget = false;

                stopIndicator.SetActive(false);
            }
        }

        /// <summary>
        /// 更新幻影跟随鼠标的位置，并（若来自阵盘槽）刷新禁止覆盖层的可见性。
        /// </summary>
        public void Move(PointerEventData eventData)
        {
            if (proxyGO == null || rootCanvas == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootCanvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            ((RectTransform)proxyGO.transform).anchoredPosition = localPoint;

            if (IsFormationSlot && stopIndicator != null)
            {
                bool inFormation = UIManager.Instance?.IsInFormationUI(eventData.position) ?? true;
                stopIndicator.SetActive(!inFormation);
            }
        }

        /// <summary>销毁幻影 GameObject 并重置全部状态。</summary>
        public void End()
        {
            if (proxyGO != null)
            {
                Object.Destroy(proxyGO);
                proxyGO = null;
            }
            stopIndicator   = null;
            IsFormationSlot = false;
            rootCanvas      = null;
        }
    }
}
