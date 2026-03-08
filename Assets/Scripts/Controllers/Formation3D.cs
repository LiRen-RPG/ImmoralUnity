using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Immortal.UI;
using Immortal.Item;

namespace Immortal.Controllers
{
    public class Formation3D : MonoBehaviour
    {
        // 特效等级数量常数
        private const int MAX_EFFECT_LEVELS = 3;

        [SerializeField] private BaseEightTrigramsUI trigramsUI;

        // 每个卦配置一个数组，数组下标为等级，长度为MAX_EFFECT_LEVELS
        [SerializeField] private GameObject[] zhenEffectPrefabs = new GameObject[MAX_EFFECT_LEVELS];
        [SerializeField] private GameObject[] xunEffectPrefabs = new GameObject[MAX_EFFECT_LEVELS];
        [SerializeField] private GameObject[] liEffectPrefabs = new GameObject[MAX_EFFECT_LEVELS];
        [SerializeField] private GameObject[] kunEffectPrefabs = new GameObject[MAX_EFFECT_LEVELS];
        [SerializeField] private GameObject[] duiEffectPrefabs = new GameObject[MAX_EFFECT_LEVELS];
        [SerializeField] private GameObject[] qianEffectPrefabs = new GameObject[MAX_EFFECT_LEVELS];
        [SerializeField] private GameObject[] kanEffectPrefabs = new GameObject[MAX_EFFECT_LEVELS];
        [SerializeField] private GameObject[] genEffectPrefabs = new GameObject[MAX_EFFECT_LEVELS];

        // 当前阵盘实例
        private FormationInstance formationInstance;
        
        // 当前激活的特效节点，格式：[卦][等级] => GameObject[]
        private Dictionary<EightTrigramsType, List<GameObject>> activeEffectNodes = 
            new Dictionary<EightTrigramsType, List<GameObject>>();
        
        // 正在播放的技能特效节点
        private HashSet<GameObject> playingSkillEffects = new HashSet<GameObject>();

        private void Start()
        {
            // 可选：初始化或自动查找阵盘信息
        }

        /// <summary>
        /// 设置阵盘实例并刷新特效显示
        /// </summary>
        public void SetFormationInstance(FormationInstance instance)
        {
            formationInstance = instance;
            RefreshEffects();
        }

        /// <summary>
        /// 获取指定卦类型的特效预制体数组
        /// </summary>
        private GameObject[] GetEffectPrefabs(EightTrigramsType trigramType)
        {
            switch (trigramType)
            {
                case EightTrigramsType.Zhen: return zhenEffectPrefabs;
                case EightTrigramsType.Xun:  return xunEffectPrefabs;
                case EightTrigramsType.Li:   return liEffectPrefabs;
                case EightTrigramsType.Kun:  return kunEffectPrefabs;
                case EightTrigramsType.Dui:  return duiEffectPrefabs;
                case EightTrigramsType.Qian: return qianEffectPrefabs;
                case EightTrigramsType.Kan:  return kanEffectPrefabs;
                case EightTrigramsType.Gen:  return genEffectPrefabs;
                default: return new GameObject[0];
            }
        }

        /// <summary>
        /// 根据阵盘物品摆放信息刷新特效显示
        /// 只有卦位槽中有物品时才生成特效，同时每个有物品的卦位都会获得一个技能
        /// 特效由卦位激发，但属于整个八卦阵盘，挂载在Formation3D节点上
        /// </summary>
        public void RefreshEffects()
        {
            if (formationInstance == null || trigramsUI == null) return;

            // 清理所有已激活的特效节点
            foreach (var nodeList in activeEffectNodes.Values)
            {
                foreach (var node in nodeList)
                {
                    if (node != null) Destroy(node);
                }
            }
            activeEffectNodes.Clear();

            // 遍历八卦类型，只处理有物品的卦位
            for (int trigramType = 0; trigramType < 8; trigramType++)
            {
                EightTrigramsType trigramEnum = (EightTrigramsType)trigramType;

                // 检查该卦位是否有物品
                bool hasItems = CheckTrigramHasItems(trigramEnum);
                if (!hasItems) continue;

                // 获取物品等级来决定特效等级
                int? level = formationInstance.GetTrigramLevel(trigramEnum);
                if (level == null) continue;

                // 获取对应的特效预制体
                GameObject[] effectPrefabs = GetEffectPrefabs(trigramEnum);
                if (level.Value >= effectPrefabs.Length || effectPrefabs[level.Value] == null) continue;

                GameObject effectPrefab = effectPrefabs[level.Value];
                GameObject effectNode = Instantiate(effectPrefab, transform);

                // 记录激活的特效节点
                if (!activeEffectNodes.ContainsKey(trigramEnum))
                    activeEffectNodes[trigramEnum] = new List<GameObject>();
                activeEffectNodes[trigramEnum].Add(effectNode);

                // 由于有物品，该卦位会自动获得技能
                SkillConfig skill = formationInstance.GetTrigramSkill(trigramEnum);
                if (skill != null)
                    Debug.Log($"卦位 {trigramEnum} 激活阵盘特效和技能: {skill.name}");
            }
        }

        /// <summary>
        /// 检查指定卦位是否有物品
        /// </summary>
        private bool CheckTrigramHasItems(EightTrigramsType trigramType)
        {
            for (int i = 0; i < 3; i++)
                if (formationInstance.GetItem(trigramType, i) != null) return true;
            return false;
        }

        /// <summary>
        /// 播放技能特效
        /// </summary>
        /// <param name="skill">技能配置</param>
        /// <param name="trigramType">对应的卦位类型</param>
        /// <param name="targetPosition">目标位置（可选，用于技能指向特效）</param>
        public void PlaySkillEffect(SkillConfig skill, EightTrigramsType trigramType, Vector3? targetPosition = null)
        {
            if (formationInstance == null || trigramsUI == null)
            {
                Debug.LogWarning("Formation3D: 阵盘或UI未设置，无法播放技能特效");
                return;
            }

            // 计算技能威力等级 (0-2)
            int powerLevel = CalculateSkillPowerLevel(skill);

            // 获取对应卦位的特效预制体数组
            GameObject[] effectPrefabs = GetEffectPrefabs(trigramType);

            // 播放对应威力等级的特效
            PlayEffectsByPowerLevel(trigramType, effectPrefabs, powerLevel, targetPosition);

            Debug.Log($"播放技能特效: {skill.name} [{trigramType}] 威力等级:{powerLevel}");
        }

        /// <summary>
        /// 根据技能威力计算特效等级 (0-2)
        /// </summary>
        private int CalculateSkillPowerLevel(SkillConfig skill)
        {
            // 根据技能威力范围划分等级
            float basePower = 20f; // 基础威力

            if (skill.power <= basePower + 10f)
            {
                return 0; // 低等级特效
            }
            else if (skill.power <= basePower + 25f)
            {
                return 1; // 中等级特效
            }
            else
            {
                return 2; // 高等级特效
            }
        }

        /// <summary>
        /// 根据威力等级播放特效
        /// </summary>
        private void PlayEffectsByPowerLevel(EightTrigramsType trigramType, GameObject[] effectPrefabs, int powerLevel, Vector3? targetPosition)
        {
            Transform trigramNode = trigramsUI.GetTrigramNode(trigramType);
            if (trigramNode == null)
            {
                Debug.LogWarning($"Formation3D: 找不到卦位节点 {trigramType}");
                return;
            }

            // 根据威力等级决定播放的特效数量和类型
            GameObject[] effectsToPlay = GetEffectsToPlay(effectPrefabs, powerLevel);

            foreach (GameObject effectPrefab in effectsToPlay)
            {
                if (effectPrefab != null)
                {
                    InstantiateAndPlayEffect(effectPrefab, trigramNode, targetPosition);
                }
            }
        }

        /// <summary>
        /// 根据威力等级确定要播放的特效
        /// </summary>
        private GameObject[] GetEffectsToPlay(GameObject[] effectPrefabs, int powerLevel)
        {
            List<GameObject> effects = new List<GameObject>();

            switch (powerLevel)
            {
                case 0: // 低威力：播放1个基础特效
                    if (effectPrefabs.Length > 0 && effectPrefabs[0] != null)
                        effects.Add(effectPrefabs[0]);
                    break;

                case 1: // 中威力：播放前2个特效
                    if (effectPrefabs.Length > 0 && effectPrefabs[0] != null)
                        effects.Add(effectPrefabs[0]);
                    if (effectPrefabs.Length > 1 && effectPrefabs[1] != null)
                        effects.Add(effectPrefabs[1]);
                    break;

                case 2: // 高威力：播放全部3个特效
                    if (effectPrefabs.Length > 0 && effectPrefabs[0] != null)
                        effects.Add(effectPrefabs[0]);
                    if (effectPrefabs.Length > 1 && effectPrefabs[1] != null)
                        effects.Add(effectPrefabs[1]);
                    if (effectPrefabs.Length > 2 && effectPrefabs[2] != null)
                        effects.Add(effectPrefabs[2]);
                    break;
            }

            return effects.ToArray();
        }

        /// <summary>
        /// 实例化并播放单个特效
        /// </summary>
        private void InstantiateAndPlayEffect(GameObject effectPrefab, Transform parentNode, Vector3? targetPosition)
        {
            GameObject effectNode = Instantiate(effectPrefab, transform);

            // 记录正在播放的特效
            playingSkillEffects.Add(effectNode);

            // 设置特效位置为相对于触发卦位的位置
            Vector3 parentWorldPos = parentNode.position;
            Vector3 formationWorldPos = transform.position;
            Vector3 relativePos = parentWorldPos - formationWorldPos;
            effectNode.transform.localPosition = relativePos;

            // 设置特效朝向
            if (targetPosition.HasValue)
            {
                SetupEffectDirection(effectNode, targetPosition.Value);
            }

            // 播放特效动画
            StartCoroutine(PlayEffectAnimation(effectNode));
        }

        /// <summary>
        /// 设置特效方向（朝向目标位置）
        /// </summary>
        private void SetupEffectDirection(GameObject effectNode, Vector3 targetPosition)
        {
            // 计算朝向角度
            Vector3 currentPos = effectNode.transform.position;
            Vector3 direction = (targetPosition - currentPos).normalized;

            // 设置节点朝向
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            effectNode.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        /// <summary>
        /// 播放特效动画
        /// </summary>
        private IEnumerator PlayEffectAnimation(GameObject effectNode)
        {
            // 获取所有渲染器组件
            Renderer[] renderers = effectNode.GetComponentsInChildren<Renderer>();
            
            // 初始设置
            effectNode.transform.localScale = Vector3.one * 0.5f;
            SetRenderersAlpha(renderers, 0f);

            // 特效淡入和缩放
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                effectNode.transform.localScale = Vector3.Lerp(Vector3.one * 0.5f, Vector3.one, t);
                SetRenderersAlpha(renderers, Mathf.Lerp(0f, 1f, t));
                
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 设置最终值
            effectNode.transform.localScale = Vector3.one;
            SetRenderersAlpha(renderers, 1f);

            // 持续显示1.5秒
            yield return new WaitForSeconds(1.5f);

            // 淡出和缩小
            duration = 0.3f;
            elapsed = 0f;
            Vector3 startScale = Vector3.one;
            Vector3 endScale = Vector3.one * 0.8f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                effectNode.transform.localScale = Vector3.Lerp(startScale, endScale, t);
                SetRenderersAlpha(renderers, Mathf.Lerp(1f, 0f, t));
                
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 动画结束后清理
            playingSkillEffects.Remove(effectNode);
            Destroy(effectNode);
        }

        /// <summary>
        /// 设置渲染器组件的透明度
        /// </summary>
        private void SetRenderersAlpha(Renderer[] renderers, float alpha)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer.material != null)
                {
                    Color color = renderer.material.color;
                    color.a = alpha;
                    renderer.material.color = color;
                }
            }
        }

        /// <summary>
        /// 停止所有正在播放的技能特效
        /// </summary>
        public void StopAllSkillEffects()
        {
            foreach (GameObject effectNode in playingSkillEffects)
            {
                if (effectNode != null)
                {
                    StopAllCoroutines(); // 停止所有动画协程
                    Destroy(effectNode);
                }
            }
            playingSkillEffects.Clear();
        }

        /// <summary>
        /// 播放指定卦位的技能特效（根据当前技能配置）
        /// </summary>
        public void PlayTrigramSkillEffect(EightTrigramsType trigramType, Vector3? targetPosition = null)
        {
            if (formationInstance == null)
            {
                Debug.LogWarning("Formation3D: 阵盘未设置");
                return;
            }

            SkillConfig skill = formationInstance.GetTrigramSkill(trigramType);
            if (skill == null)
            {
                Debug.LogWarning($"Formation3D: 卦位 {trigramType} 无可用技能");
                return;
            }

            PlaySkillEffect(skill, trigramType, targetPosition);
        }

        /// <summary>
        /// 获取当前正在播放的技能特效数量
        /// </summary>
        public int GetPlayingEffectsCount()
        {
            return playingSkillEffects.Count;
        }

        /// <summary>
        /// 清理无效的特效节点引用
        /// </summary>
        private void CleanUpInvalidEffects()
        {
            // 清理playingSkillEffects中已销毁的对象
            playingSkillEffects.RemoveWhere(effect => effect == null);

            // 清理activeEffectNodes中已销毁的对象
            foreach (var kvp in activeEffectNodes)
            {
                kvp.Value.RemoveAll(effect => effect == null);
            }
        }

        private void Update()
        {
            // 定期清理无效引用
            if (Time.frameCount % 60 == 0) // 每60帧清理一次
            {
                CleanUpInvalidEffects();
            }
        }
    }
}