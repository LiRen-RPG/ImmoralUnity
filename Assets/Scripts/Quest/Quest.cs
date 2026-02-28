using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Immortal.Quest
{
    // 触发器基类
    public abstract class BaseTrigger
    {
        public string triggerId;
        protected List<System.Action> listeners = new List<System.Action>();

        protected BaseTrigger(string triggerId)
        {
            this.triggerId = triggerId;
        }

        // 注册监听器
        public void OnTrigger(System.Action listener)
        {
            listeners.Add(listener);
        }

        // 通知监听器
        protected void NotifyListeners()
        {
            foreach (var listener in listeners)
            {
                listener?.Invoke();
            }
        }

        // 检查触发条件（由子类实现）
        public abstract bool CheckTrigger(object args);

        // 处理参数变化（由外部系统调用）
        public void HandleTriggerCheck(object args)
        {
            if (CheckTrigger(args))
            {
                NotifyListeners();
            }
        }
    }

    // 位置触发器
    public class LocationTrigger : BaseTrigger
    {
        public string areaId;
        public string requiredItem;

        public LocationTrigger(string triggerId, string areaId, string requiredItem = null)
            : base(triggerId)
        {
            this.areaId = areaId;
            this.requiredItem = requiredItem;
        }

        public override bool CheckTrigger(object args)
        {
            if (!(args is LocationConditionArgs locationArgs))
            {
                return false;
            }

            bool isInArea = locationArgs.currentArea == areaId;
            if (string.IsNullOrEmpty(requiredItem))
            {
                return isInArea;
            }

            bool hasItem = locationArgs.inventory.Contains(requiredItem);
            return isInArea && hasItem;
        }
    }

    // 物品触发器
    public class ItemTrigger : BaseTrigger
    {
        public string itemId;
        public int requiredQuantity;

        public ItemTrigger(string triggerId, string itemId, int requiredQuantity)
            : base(triggerId)
        {
            this.itemId = itemId;
            this.requiredQuantity = requiredQuantity;
        }

        public override bool CheckTrigger(object args)
        {
            if (!(args is ItemConditionArgs itemArgs))
            {
                return false;
            }

            int currentQuantity = 0;
            foreach (string item in itemArgs.inventory)
            {
                if (item == itemId)
                {
                    currentQuantity++;
                }
            }
            return currentQuantity >= requiredQuantity;
        }
    }

    // 任务类
    [System.Serializable]
    public class Quest
    {
        public string questId;
        public string title;
        [System.NonSerialized]
        public BaseTrigger trigger;
        public List<QuestEvent> eventChain;

        // 任务状态
        public QuestStatus status = QuestStatus.NotStarted;
        public int currentEventIndex = 0;

        public Quest(string questId, string title, BaseTrigger trigger, List<QuestEvent> eventChain)
        {
            this.questId = questId;
            this.title = title;
            this.trigger = trigger;
            this.eventChain = eventChain ?? new List<QuestEvent>();
        }

        // 开始任务
        public void StartQuest()
        {
            if (status != QuestStatus.NotStarted)
            {
                Debug.LogWarning($"任务 {questId} 已经开始或完成");
                return;
            }

            status = QuestStatus.InProgress;
            currentEventIndex = 0;
            Debug.Log($"开始任务: {title}");

            // 执行第一个事件
            if (eventChain.Count > 0)
            {
                ExecuteCurrentEvent();
            }
        }

        // 执行当前事件
        private async void ExecuteCurrentEvent()
        {
            if (currentEventIndex >= eventChain.Count)
            {
                CompleteQuest();
                return;
            }

            var currentEvent = eventChain[currentEventIndex];
            Debug.Log($"执行任务事件: {currentEvent.eventId}");

            // 播放对话
            await currentEvent.PlayDialogue(() => {
                // 对话完成后的回调
                Debug.Log($"事件 {currentEvent.eventId} 对话完成");
            });

            // 这里可以等待完成条件满足
            // 实际实现中应该监听完成条件的变化
        }

        // 检查当前事件是否完成
        public bool CheckCurrentEventCompletion(object args)
        {
            if (currentEventIndex >= eventChain.Count)
            {
                return true;
            }

            var currentEvent = eventChain[currentEventIndex];
            return currentEvent.completion?.IsCompleted(args) ?? false;
        }

        // 完成当前事件，进入下一个事件
        public void CompleteCurrentEvent()
        {
            if (currentEventIndex >= eventChain.Count)
            {
                return;
            }

            var completedEvent = eventChain[currentEventIndex];
            completedEvent.completion?.Execute();

            currentEventIndex++;
            Debug.Log($"完成事件: {completedEvent.eventId}");

            if (currentEventIndex >= eventChain.Count)
            {
                CompleteQuest();
            }
            else
            {
                ExecuteCurrentEvent();
            }
        }

        // 完成任务
        private void CompleteQuest()
        {
            status = QuestStatus.Completed;
            Debug.Log($"任务完成: {title}");

            // 触发任务完成事件
            QuestManager.Instance?.OnQuestCompleted(this);
        }

        // 获取当前事件
        public QuestEvent GetCurrentEvent()
        {
            if (currentEventIndex >= 0 && currentEventIndex < eventChain.Count)
            {
                return eventChain[currentEventIndex];
            }
            return null;
        }
    }

    // 任务状态枚举
    public enum QuestStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Failed
    }

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
            var completedEvents = new List<Quest>();
            
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

        // 从JSON加载任务配置
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
                participants.Add(new QuestParticipant(participantConfig.npcId, ""));
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
                    return new CombatCompletion(config.conditionId, config.enemies, true);
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
        public string questId;
        public string title;
        public List<TriggerConfig> triggers;
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
        public string eventId;
        public List<ParticipantConfig> participants;
        public List<LineConfig> dialogue;
        public CompletionConfig completion;
    }

    [System.Serializable]
    public class ParticipantConfig
    {
        public string npcId;
    }

    [System.Serializable]
    public class LineConfig
    {
        public int speakerId;
        public string text;
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