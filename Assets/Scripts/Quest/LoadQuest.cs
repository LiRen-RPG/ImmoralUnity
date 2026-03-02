using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Immortal.Core;
using Immortal.Controllers;

namespace Immortal.Quest
{
    /// <summary>
    /// 镜像 CreateQuest.ts 中的 loadQuest() 函数。
    /// 负责从 JSON 生成 NPC / Quest，并驱动事件链（对话 → 战斗）。
    /// </summary>
    public static class LoadQuest
    {
        // ------- JSON 数据结构（对应 CreateQuest.ts 中的 NPC 字段） -------

        [Serializable]
        private class NpcData
        {
            public int    id;
            public string name;
            public int    element;
            public int    gender;       // 0 = Male, 1 = Female
            public float  personality;
            public float  baseAttack;
            public float  baseDefense;
            public float  baseSpeed;
            public float  baseCritRate;
            public float  baseAccuracy;
            public float  appearanceAge;
        }

        // ------- 入口 -------------------------------------------------------

        /// <summary>
        /// 由 Scene.LoadQuest() 调用。
        /// host：用于启动协程的 MonoBehaviour；sceneNode：NPC 挂载父节点。
        /// </summary>
        public static void Execute(MonoBehaviour host, GameObject sceneNode)
        {
            host.StartCoroutine(ExecuteCoroutine(sceneNode));
        }

        // ------- 核心协程 ---------------------------------------------------

        private static IEnumerator ExecuteCoroutine(GameObject sceneNode)
        {
            // 初始化全局 Cultivators 字典（对应 window["cultivators"]）
            if (ActorControl.Cultivators == null)
                ActorControl.Cultivators = new Dictionary<int, ActorControl.CultivatorActorPair>();

            // 加载任务 JSON TextAsset
            var textAsset = Resources.Load<TextAsset>("Quest/DefaultQuest");
            if (textAsset == null)
            {
                Debug.LogError("[LoadQuest] 找不到 Resources/Quest/DefaultQuest.json");
                yield break;
            }

            JObject root;
            try
            {
                root = JObject.Parse(textAsset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[LoadQuest] JSON 解析失败: {e.Message}");
                yield break;
            }

            // ── 1. 生成所有 NPC（跳过 id=0 主角）─────────────────────────────
            var npcs = root["npcs"].ToObject<List<NpcData>>();
            int spawnedCount = 0;

            foreach (var npcData in npcs)
            {
                if (npcData.id == 0) continue;   // 主角由 ActorControl 初始化

                // 创建 Cultivator
                var cultivator = new Cultivator(
                    npcData.name,
                    (FivePhases)npcData.element,
                    npcData.baseAttack,
                    npcData.baseDefense,
                    npcData.baseSpeed,
                    npcData.baseCritRate,
                    npcData.baseAccuracy,
                    (Gender)npcData.gender,
                    npcData.personality,
                    npcData.appearanceAge
                );

                // 先注册，actorBase 稍后填入
                ActorControl.Cultivators[npcData.id] = new ActorControl.CultivatorActorPair
                {
                    actorBase  = null,
                    cultivator = cultivator
                };

                // 加载对应性别的 Prefab（注意 female 路径有 typo：FemalActor）
                string prefabPath = (Gender)npcData.gender == Gender.Female
                    ? "Prefabs/FemalActor"
                    : "Prefabs/MaleActor";

                var prefab = Resources.Load<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[LoadQuest] 找不到 Prefab: {prefabPath}，NPC '{npcData.name}'(id={npcData.id}) 将不实例化");
                    yield return null;
                    continue;
                }

                // 在地图范围内随机生成位置（X: 地图宽度范围，Z: 走廊纵深内）
                float x = UnityEngine.Random.Range(-2f, 2f);
                float z = UnityEngine.Random.Range(1f, 3.5f);
                float spawnX = x < 0 ? x - 5f : x + 5f;
                var spawnPos = new Vector3(spawnX, 0f, z);

                var go = UnityEngine.Object.Instantiate(prefab, spawnPos, Quaternion.identity, sceneNode.transform);
                var actorBase = go.GetComponent<ActorBase>();

                if (actorBase != null)
                {
                    actorBase.InitCultivator(cultivator, true);
                    actorBase.SetOrientation(x < 0 ? 1 : -1); // 面向场景中心
                    ActorControl.Cultivators[npcData.id].actorBase = actorBase;
                    spawnedCount++;
                }
                else
                {
                    Debug.LogWarning($"[LoadQuest] Prefab '{prefabPath}' 上找不到 ActorBase");
                }

                yield return null; // 分帧实例化，避免卡帧
            }

            Debug.Log($"[LoadQuest] NPC 生成完成，共 {spawnedCount} 个");

            // ── 2. 构建并激活 Quest ──────────────────────────────────────────
            var questManager = QuestManager.Instance;
            if (questManager == null)
            {
                Debug.LogError("[LoadQuest] 找不到 QuestManager 单例，请在场景中添加 QuestManager GameObject");
                yield break;
            }

            string questJson = root["quest"].ToString(Formatting.None);
            Quest quest = questManager.BuildAndStartQuest(questJson);
            if (quest == null)
            {
                Debug.LogError("[LoadQuest] Quest 构建失败");
                yield break;
            }

            Debug.Log($"[LoadQuest] 任务 '{quest.title}' 开始");

            // ── 3. 驱动事件链（镜像 TS 中的 for 循环）───────────────────────
            yield return RunEventChain(quest);
        }

        /// <summary>
        /// 按顺序执行任务事件链：播放对话 → 设置战斗队伍。
        /// 对应 CreateQuest.ts loadQuest() 末尾的 for 循环。
        /// </summary>
        private static IEnumerator RunEventChain(Quest quest)
        {
            for (int i = 0; i < quest.eventChain.Count; i++)
            {
                var evt = quest.eventChain[i];
                Debug.Log($"[LoadQuest] 事件 [{evt.eventId}] 开始");

                // 播放每行对话
                foreach (var line in evt.dialogue)
                {
                    // 触发说话动画
                    if (ActorControl.Cultivators.TryGetValue(line.speaker, out var pair)
                        && pair.actorBase != null)
                    {
                        pair.actorBase.PlaySpeakAnimation();
                    }

                    Debug.Log($"  [{line.speaker}]: {line.text}");

                    // 模拟对话等待（替代 TS 中的 await line.playAudio()）
                    yield return new WaitForSeconds(2f);

                    // 还原动画
                    if (ActorControl.Cultivators.TryGetValue(line.speaker, out pair)
                        && pair.actorBase != null)
                    {
                        pair.actorBase.FaceToNormal();
                    }
                }

                // 第一个事件完成后启动战斗条件准备（对应 TS 中 if(index===0) event.completion.preparetoStart()）
                if (i == 0)
                {
                    evt.completion?.PrepareToStart();
                }

                Debug.Log($"[LoadQuest] 事件 [{evt.eventId}] 完成");
            }

            Debug.Log($"[LoadQuest] 任务 '{quest.title}' 事件链执行完毕");
        }
    }
}
