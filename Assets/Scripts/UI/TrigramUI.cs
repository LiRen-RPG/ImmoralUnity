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

        // 对应的卦类型
        private EightTrigramsType trigramType;

        // ---------- 方位名称 ----------
        private static readonly string[] DirectionNames =
        {
            "东", "东南", "南", "西南", "西", "西北", "北", "东北"
        };

        // ======================== 初始化 ========================

        public void SetTrigram(EightTrigramsType type, EightTrigramsFormationPlate plate)
        {
            trigramType = type;

            // 标签
            if (trigramNameLabel != null)
                trigramNameLabel.text = type.ToString();   // 或使用中文映射
            if (positionLabel != null)
                positionLabel.text = DirectionNames[(int)type];

            // 爻贴图（用枚举值的二进制位近似阴阳——实际项目可查表）
            UpdateYaoSprites(type);

            // 物品槽
            if (plate != null)
            {
                int[] indices = plate.GetSlotIndicesForTrigram(type);
                for (int i = 0; i < itemSlots.Length && i < indices.Length; i++)
                {
                    if (itemSlots[i] != null)
                    {
                        // 此处只刷新显示；槽位数据由 EightTrigramsFormationUI 统一绑定
                        itemSlots[i].UpdateDisplay();
                    }
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
