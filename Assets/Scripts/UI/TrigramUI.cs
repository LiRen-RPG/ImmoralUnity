using UnityEngine;
using UnityEngine.UI;
using Immortal.Controllers;

namespace Immortal.UI
{
    /// <summary>
    /// 单个卦象 UI：显示三爻（阴/阳 Sprite）+ 卦名 + 方位标签。
    /// 挂在 Trigram Prefab 根节点上。
    /// </summary>
    public class TrigramUI : MonoBehaviour
    {
        [Header("三爻 Sprite（从下到上：yao1=最下，yao3=最上）")]
        [SerializeField] private Image yao1Sprite;
        [SerializeField] private Image yao2Sprite;
        [SerializeField] private Image yao3Sprite;

        [Header("爻贴图")]
        [SerializeField] private Sprite yangYaoFrame;   // 阳爻 —
        [SerializeField] private Sprite yinYaoFrame;    // 阴爻 - -

        [Header("标签")]
        [SerializeField] private Text trigramNameLabel;
        [SerializeField] private Text positionLabel;

        [Header("物品槽（每卦 3 个）")]
        [SerializeField] private InventorySlotUI[] itemSlots = new InventorySlotUI[3];

        [Header("槽位透明度")]
        [SerializeField, Range(0f, 1f)] private float slotsAlpha = 1f;

        // 对应的卦类型
        private EightTrigramsType trigramType;

        // ---------- 卦名（与 EightTrigramsType 顺序一致）----------
        private static readonly string[] TrigramNames =
        {
            "震", "巽", "离", "坤", "兑", "乾", "坎", "艮"
        };

        // ---------- 方位名称 ----------
        private static readonly string[] DirectionNames =
        {
            "东", "东南", "南", "西南", "西", "西北", "北", "东北"
        };

        // ======================== 初始化 ========================

        public void SetTrigram(EightTrigramsType type, FormationInstance instance)
        {
            trigramType = type;

            // 标签
            if (trigramNameLabel != null)
                trigramNameLabel.text = TrigramNames[(int)type];
            if (positionLabel != null)
                positionLabel.text = DirectionNames[(int)type];

            // 爻贴图
            UpdateYaoSprites(type);

            // 物品槽
            if (instance != null)
            {
                for (int i = 0; i < itemSlots.Length && i < 3; i++)
                {
                    if (itemSlots[i] != null)
                        itemSlots[i].UpdateDisplay();
                }
            }
        }

        private static readonly bool[,] YaoTable =
        {
            // 震巽离坤兑乾坎艮 各三爻（true=阳，false=阴）
            // 序号对应 EightTrigramsType 值
            { true,  false, false }, // 震 (0)
            { false, true,  true  }, // 巽 (1)
            { true,  false, true  }, // 离 (2)
            { false, false, false }, // 坤 (3)
            { true,  true,  false }, // 兑 (4)
            { true,  true,  true  }, // 乾 (5)
            { false, true,  false }, // 坎 (6)
            { false, false, true  }, // 艮 (7)
        };

        private void UpdateYaoSprites(EightTrigramsType type)
        {
            if (yangYaoFrame == null || yinYaoFrame == null) return;
            int row = (int)type;

            SetYao(yao1Sprite, YaoTable[row, 0]);
            SetYao(yao2Sprite, YaoTable[row, 1]);
            SetYao(yao3Sprite, YaoTable[row, 2]);
        }

        private void SetYao(Image img, bool isYang)
        {
            if (img == null) return;
            img.sprite = isYang ? yangYaoFrame : yinYaoFrame;
        }

        /// <summary>根据自身 up 方向同步标签朝向，旋转设置后调用。</summary>
        public void UpdateLabelOrientation()
        {
            float labelRot = 0;
            Vector3 up = transform.up;
            if(up.y < -0.1f) labelRot = 180f;
            else if(Mathf.Abs(up.y) <0.01f) {
                labelRot = 90f * up.x; // 水平放置时标签旋转90度（可选，根据美术设计调整）
            }
            if (trigramNameLabel != null)
                trigramNameLabel.transform.localEulerAngles = new Vector3(0f, 0f, labelRot);
            if (positionLabel != null)
                positionLabel.transform.localEulerAngles = new Vector3(0f, 0f, labelRot);
        }

        // ======================== 透明度 ========================

        /// <summary>将 slotsAlpha 应用到所有物品槽的 CanvasGroup（不存在则自动添加）。</summary>
        public void ApplySlotsAlpha()
        {
            foreach (var slot in itemSlots)
            {
                if (slot == null) continue;
                var cg = slot.GetComponent<CanvasGroup>();
                if (cg == null) cg = slot.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = slotsAlpha;
            }
        }

        private void OnValidate() => ApplySlotsAlpha();

        // ======================== 高亮 ========================

        public void SetHighlight(int yaoIndex, bool highlight)
        {
            Image[] sprites = { yao1Sprite, yao2Sprite, yao3Sprite };
            if (yaoIndex < 0 || yaoIndex >= sprites.Length) return;
            if (sprites[yaoIndex] == null) return;
            sprites[yaoIndex].color = highlight
                ? new Color(1f, 0.9f, 0.2f)    // 金黄色高亮
                : Color.white;
        }

        public void ClearHighlight()
        {
            SetHighlight(0, false);
            SetHighlight(1, false);
            SetHighlight(2, false);
        }

        // ======================== 访问器 ========================

        public EightTrigramsType GetTrigramType() => trigramType;
        public InventorySlotUI[] GetItemSlots()   => itemSlots;
    }
}
