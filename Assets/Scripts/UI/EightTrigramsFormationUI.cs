using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Immortal.Item;
using Immortal.Controllers;

namespace Immortal.UI
{
    /// <summary>
    /// 八卦阵盘 UI：
    ///   - 中心：阴阳鱼旋转图 + 内框
    ///   - 外圈：8 个 TrigramUI，均匀分布
    ///   - 每个 TrigramUI 内含 3 个 InventorySlotUI（可拖入物品）
    ///   - 支持切换不同的 EightTrigramsFormationPlate 数据
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
        [SerializeField] private float              radius = 280f;  // 排布半径

        [Header("槽位预制体（供运行时创建用）")]
        [SerializeField] private GameObject slotPrefab;

        [Header("关闭按钮")]
        [SerializeField] private Button closeButton;

        // 内部状态
        private EightTrigramsFormationPlate currentPlate;
        private Action                      onCleared;
        private List<TrigramUI>             trigramUIs = new List<TrigramUI>();

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
        /// 切换到新的阵盘数据并刷新整个 UI。
        /// </summary>
        /// <param name="plate">目标阵盘数据对象</param>
        /// <param name="clearCallback">阵盘被关闭/清空时调用的回调</param>
        public void SwitchToFormation(EightTrigramsFormationPlate plate, Action clearCallback)
        {
            currentPlate = plate;
            onCleared    = clearCallback;

            BuildOrRefreshTrigrams();
        }

        // ======================== 构建卦象 ========================

        private void BuildOrRefreshTrigrams()
        {
            // 首次创建时生成 8 个 TrigramUI
            if (trigramUIs.Count == 0)
                CreateTrigramUIs();

            if (currentPlate == null) return;

            // 刷新每个卦象的数据和槽位显示
            for (int i = 0; i < trigramUIs.Count; i++)
            {
                var type = (EightTrigramsType)i;
                trigramUIs[i].SetTrigram(type, currentPlate);

                // 将槽位 UI 和 Inventory 槽位绑定
                BindTrigramSlots(trigramUIs[i], type);
            }
        }

        private void CreateTrigramUIs()
        {
            if (trigramsRoot == null || trigramPrefab == null) return;

            // 清理旧节点
            foreach (var tui in trigramUIs)
                if (tui != null) Destroy(tui.gameObject);
            trigramUIs.Clear();

            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad;
                var   pos   = new Vector2(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius);

                var go  = Instantiate(trigramPrefab, trigramsRoot);
                var rt  = go.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = pos;

                var tui = go.GetComponent<TrigramUI>();
                if (tui != null) trigramUIs.Add(tui);
            }
        }

        private void BindTrigramSlots(TrigramUI trigramUI, EightTrigramsType type)
        {
            if (currentPlate == null) return;

            int[] indices = currentPlate.GetSlotIndicesForTrigram(type);
            var   slots   = trigramUI.GetItemSlots();

            for (int i = 0; i < slots.Length && i < indices.Length; i++)
            {
                if (slots[i] == null) continue;

                // 取到伪 InventorySlot 包装（直接包装 plate 槽位数据供 SlotUI 显示）
                slots[i].UpdateDisplay();

                // 绑定拖入回调
                int capturedSlotIndex = indices[i];
                slots[i].SetSingleClickCallback((_, sui) =>
                {
                    Debug.Log($"点击阵盘槽位 {capturedSlotIndex}");
                });
            }
        }

        // ======================== 关闭 ========================

        private void OnCloseClicked()
        {
            currentPlate = null;
            gameObject.SetActive(false);

            // 触发外部清理回调（重新启用背包槽交互）
            onCleared?.Invoke();
            onCleared = null;
        }

        // ======================== 外部访问 ========================

        public EightTrigramsFormationPlate GetCurrentPlate() => currentPlate;
        public List<TrigramUI>             GetTrigramUIs()   => trigramUIs;
        public bool                        IsOpen            => gameObject.activeSelf;

        /// <summary>返回指定卦位对应 TrigramUI 的 Transform，供 Formation3D 定位特效节点。</summary>
        public override Transform GetTrigramNode(EightTrigramsType trigram)
        {
            int idx = (int)trigram;
            if (idx < 0 || idx >= trigramUIs.Count) return null;
            return trigramUIs[idx]?.transform;
        }
    }
}
