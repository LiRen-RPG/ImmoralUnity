using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Immortal.Controllers;

namespace Immortal.UI
{
    /// <summary>
    /// 八卦阵盘 UI（View）：
    ///   - 中心：阴阳鱼旋转图 + 内框
    ///   - 外圈：8 个 TrigramUI，均匀分布
    ///   - 每个 TrigramUI 内含 3 个 InventorySlotUI（可拖入物品）
    ///   - 以 FormationInstance 作为数据模型（MVC Model）
    /// </summary>
    public class EightTrigramsFormationUI : BaseEightTrigramsUI
    {
        [Header("中心图像")]
        [SerializeField] private Image     yinYangFishImage;   // 阴阳鱼
        [SerializeField] private Image     innerFrameImage;    // 内框
        [SerializeField] private float     spinSpeed = 30f;    // 旋转速度（度/秒）

        [Header("外圈卦象")]
        [SerializeField] private Transform          trigramsRoot;   // 8 个 TrigramUI 的父节点
        [SerializeField] private GameObject         trigramPrefab;  // TrigramUI 预制体

        [Header("槽位预制体（供运行时创建用）")]
        [SerializeField] private GameObject slotPrefab;

        [Header("关闭按钮")]
        [SerializeField] private Button closeButton;

        // 内部状态
        private FormationInstance currentInstance;
        private Action            onCleared;
        private List<TrigramUI>   trigramUIs = new List<TrigramUI>();

        // ======================== 生命周期 ========================

        private void Awake()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);

            gameObject.SetActive(false);
        }

        private void Update()
        {
            // 阴阳鱼旋转
            if (yinYangFishImage != null)
                yinYangFishImage.transform.Rotate(0, 0, spinSpeed * Time.deltaTime);
        }

        // ======================== 切换阵盘 ========================

        /// <summary>
        /// 切换到新的阵盘实例并刷新整个 UI。
        /// </summary>
        /// <param name="instance">目标阵盘数据模型</param>
        /// <param name="clearCallback">阵盘被关闭时调用的回调</param>
        public void SwitchToFormation(FormationInstance instance, Action clearCallback)
        {
            currentInstance = instance;
            onCleared       = clearCallback;

            BuildOrRefreshTrigrams();
        }

        // ======================== 构建卦象 ========================

        private void BuildOrRefreshTrigrams()
        {
            // 首次创建时生成 8 个 TrigramUI
            if (trigramUIs.Count == 0)
                CreateTrigramUIs();

            if (currentInstance == null) return;

            // 刷新每个卦象的数据和槽位显示
            for (int i = 0; i < trigramUIs.Count; i++)
            {
                var type = (EightTrigramsType)i;
                trigramUIs[i].SetTrigram(type, currentInstance);
                BindTrigramSlots(trigramUIs[i], type);
            }
        }

        [ContextMenu("生成卦象UI")]
        private void CreateTrigramUIs()
        {
            if (trigramsRoot == null || trigramPrefab == null) return;

            // 清理旧节点（含编辑器模式预生成的子物体）
            for (int c = trigramsRoot.childCount - 1; c >= 0; c--)
            {
                var child = trigramsRoot.GetChild(c).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(child);
                else
#endif
                    Destroy(child);
            }
            trigramUIs.Clear();

            float radius = yinYangFishImage != null ? yinYangFishImage.rectTransform.rect.width / 2f : 280f;

            for (int i = 0; i < 8; i++)
            {
                float angle = (i - 2) * 45f * Mathf.Deg2Rad;
                var   pos   = new Vector2(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);

                var go  = Instantiate(trigramPrefab, trigramsRoot);
                var rt  = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = pos;
                    rt.localEulerAngles = new Vector3(0f, 0f, -(i - 2) * 45f);
                }

                var tui = go.GetComponent<TrigramUI>();
                if (tui != null)
                {
                    tui.UpdateLabelOrientation();
                    trigramUIs.Add(tui);
                }
            }
        }

        private void BindTrigramSlots(TrigramUI trigramUI, EightTrigramsType type)
        {
            if (currentInstance == null) return;

            var slots = trigramUI.GetItemSlots();
            for (int i = 0; i < slots.Length && i < 3; i++)
            {
                if (slots[i] == null) continue;

                slots[i].UpdateDisplay();

                int capturedSlot = i;
                EightTrigramsType capturedType = type;
                slots[i].SetSingleClickCallback((_, sui) =>
                {
                    Debug.Log($"点击阵盘槽位 [{capturedType}][{capturedSlot}]");
                });
            }
        }

        // ======================== 关闭 ========================

        private void OnCloseClicked()
        {
            currentInstance = null;
            gameObject.SetActive(false);

            // 触发外部清理回调（重新启用背包槽交互）
            onCleared?.Invoke();
            onCleared = null;
        }

        // ======================== 外部访问 ========================

        public FormationInstance GetCurrentInstance() => currentInstance;
        public List<TrigramUI>   GetTrigramUIs()       => trigramUIs;
        public bool              IsOpen                => gameObject.activeSelf;

        /// <summary>返回指定卦位对应 TrigramUI 的 Transform，供 Formation3D 定位特效节点。</summary>
        public override Transform GetTrigramNode(EightTrigramsType trigram)
        {
            int idx = (int)trigram;
            if (idx < 0 || idx >= trigramUIs.Count) return null;
            return trigramUIs[idx]?.transform;
        }
    }
}
