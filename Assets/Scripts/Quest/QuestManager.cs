using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Immortal.Quest
{
    // 任务管理器
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        [SerializeField] private List<Quest> activeQuests = new List<Quest>();
        [SerializeField] private List<Quest> completedQuests = new List<Quest>();
        [SerializeField] private List<Quest> availableQuests = new List<Quest>();

        // 事件
        public event System.Action<Quest> QuestStarted;
        public event System.Action<Quest> QuestCompleted;
        public event System.Action<Quest> QuestFailed;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // 添加可用任务
        public void AddAvailableQuest(Quest quest)
        {
            if (!availableQuests.Contains(quest))
            {
                availableQuests.Add(quest);
                
                // 设置触发器监听
                quest.trigger?.OnTrigger(() => {
                    StartQuest(quest.questId);
                });

                Debug.Log($"添加可用任务: {quest.title}");
            }
        }

        // 开始任务
        public void StartQuest(string questId)
        {
            var quest = availableQuests.Find(q => q.questId == questId);
            if (quest != null)
            {
                availableQuests.Remove(quest);
                activeQuests.Add(quest);
                quest.StartQuest();
                
                QuestStarted?.Invoke(quest);
            }
            else
            {
                Debug.LogWarning($"找不到任务: {questId}");
            }
        }

        // 任务完成回调
        public void OnQuestCompleted(Quest quest)
        {
            if (activeQuests.Contains(quest))
            {
                activeQuests.Remove(quest);
                completedQuests.Add(quest);
                
                QuestCompleted?.Invoke(quest);
            }
        }

        // 检查任务触发条件
        public void CheckQuestTriggers(object args)
        {
            foreach (var quest in availableQuests)
            {
                quest.trigger?.HandleTriggerCheck(args);
            }
        }

        // 检查任务完成条件
        public void CheckQuestCompletions(object args)
        {
            foreach (var quest in activeQuests)
            {
                if (quest.CheckCurrentEventCompletion(args))
                {
                    quest.CompleteCurrentEvent();
                }
            }
        }

        // 获取活跃任务列表
        public List<Quest> GetActiveQuests()
        {
            return new List<Quest>(activeQuests);
        }

        // 获取已完成任务列表
        public List<Quest> GetCompletedQuests()
        {
            return new List<Quest>(completedQuests);
        }

        // 从JSON加载任务配置（添加到可用任务列表，等待触发器激活）
        public void LoadQuestFromJson(string jsonString)
        {
            try
            {
                var questConfig = JsonConvert.DeserializeObject<QuestConfig>(jsonString);
                var quest = CreateQuestFromConfig(questConfig);
                AddAvailableQuest(quest);
            }
            catch (Exception e)
            {
                Debug.LogError($"加载任务配置失败: {e.Message}");
            }
        }

        /// <summary>
        /// 构建任务并立即激活为进行中（跳过触发器机制），返回 Quest 对象供调用方驱动事件链。
        /// 对应 CreateQuest.ts 中直接调用 QuestFactory.createQuestFromJson 后遍历 eventChain 的模式。
        /// </summary>
        public Quest BuildAndStartQuest(string jsonString)
        {
            try
            {
                var questConfig = JsonConvert.DeserializeObject<QuestConfig>(jsonString);
                var quest = CreateQuestFromConfig(questConfig);
                quest.status = QuestStatus.InProgress;
                activeQuests.Add(quest);
                QuestStarted?.Invoke(quest);
                Debug.Log($"[QuestManager] 任务直接激活: {quest.title}");
                return quest;
            }
            catch (Exception e)
            {
                Debug.LogError($"[QuestManager] BuildAndStartQuest 失败: {e.Message}");
                return null;
            }
        }

        // 从配置创建任务
        private Quest CreateQuestFromConfig(QuestConfig config)
        {
            // 创建触发器
            BaseTrigger trigger = null;
            if (config.triggers.Count > 0)
            {
                var triggerConfig = config.triggers[0]; // 使用第一个触发器
                trigger = CreateTriggerFromConfig(triggerConfig);
            }

            // 创建事件链
            var eventChain = new List<QuestEvent>();
            foreach (var eventConfig in config.eventChain)
            {
                var questEvent = CreateEventFromConfig(eventConfig);
                eventChain.Add(questEvent);
            }

            return new Quest(config.questId, config.title, trigger, eventChain);
        }

        private BaseTrigger CreateTriggerFromConfig(TriggerConfig config)
        {
            switch (config.type)
            {
                case "location":
                    return new LocationTrigger(config.conditionId, config.areaId, config.requiredItem);
                case "item":
                    return new ItemTrigger(config.conditionId, config.itemId, config.requiredQuantity);
                default:
                    Debug.LogWarning($"未知的触发器类型: {config.type}");
                    return null;
            }
        }

        private QuestEvent CreateEventFromConfig(EventConfig config)
        {
            // 创建参与者
            var participants = new List<QuestParticipant>();
            foreach (var participantConfig in config.participants)
            {
                participants.Add(new QuestParticipant(participantConfig.npcId.ToString(), ""));
            }

            // 创建对话（这里简化处理，实际应该异步创建）
            var dialogue = new List<DialogueLine>();
            foreach (var lineConfig in config.dialogue)
            {
                dialogue.Add(new DialogueLine(lineConfig.speakerId, lineConfig.text, lineConfig.requiredChoice));
            }

            // 创建完成条件
            BaseCompletion completion = CreateCompletionFromConfig(config.completion);

            return new QuestEvent(config.eventId, participants, dialogue, completion);
        }

        private BaseCompletion CreateCompletionFromConfig(CompletionConfig config)
        {
            switch (config.type)
            {
                case "location":
                    return new LocationCompletion(config.conditionId, config.areaId, config.requiredItem);
                case "item":
                    return new ItemCompletion(config.conditionId, config.itemId, config.requiredQuantity);
                case "combat":
                    return new CombatCompletion(config.conditionId, config.enemies, config.friends, true);
                default:
                    Debug.LogWarning($"未知的完成条件类型: {config.type}");
                    return null;
            }
        }
    }

    // 配置数据类
    [System.Serializable]
    public class QuestConfig
    {
        [JsonProperty("quest_id")]
        public string questId;
        public string title;
        public List<TriggerConfig> triggers;
        [JsonProperty("event_chain")]
        public List<EventConfig> eventChain;
    }

    [System.Serializable]
    public class TriggerConfig
    {
        public string type;
        public string conditionId;
        public string areaId;
        public string requiredItem;
        public string itemId;
        public int requiredQuantity;
    }

    [System.Serializable]
    public class EventConfig
    {
        [JsonProperty("event_id")]
        public string eventId;
        public List<ParticipantConfig> participants;
        public List<LineConfig> dialogue;
        public CompletionConfig completion;
    }

    [System.Serializable]
    public class ParticipantConfig
    {
        [JsonProperty("npc_id")]
        public int npcId;
    }

    [System.Serializable]
    public class LineConfig
    {
        [JsonProperty("speaker_id")]
        public int speakerId;
        public string text;
        [JsonProperty("required_choice")]
        public int requiredChoice;
    }

    [System.Serializable]
    public class CompletionConfig
    {
        public string type;
        public string conditionId;
        public string areaId;
        public string requiredItem;
        public string itemId;
        public int requiredQuantity;
        public int[] enemies;
        public int[] friends;
    }
}
