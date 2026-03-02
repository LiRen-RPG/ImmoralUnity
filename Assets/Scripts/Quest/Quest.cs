using System;
using System.Collections.Generic;
using UnityEngine;

namespace Immortal.Quest
{
    // 瑙﹀彂鍣ㄥ熀绫?
    public abstract class BaseTrigger
    {
        public string triggerId;
        protected List<System.Action> listeners = new List<System.Action>();

        protected BaseTrigger(string triggerId)
        {
            this.triggerId = triggerId;
        }

        // 娉ㄥ唽鐩戝惉鍣?
        public void OnTrigger(System.Action listener)
        {
            listeners.Add(listener);
        }

        // 閫氱煡鐩戝惉鍣?
        protected void NotifyListeners()
        {
            foreach (var listener in listeners)
            {
                listener?.Invoke();
            }
        }

        // 妫€鏌ヨЕ鍙戞潯浠讹紙鐢卞瓙绫诲疄鐜帮級
        public abstract bool CheckTrigger(object args);

        // 澶勭悊鍙傛暟鍙樺寲锛堢敱澶栭儴绯荤粺璋冪敤锛?
        public void HandleTriggerCheck(object args)
        {
            if (CheckTrigger(args))
            {
                NotifyListeners();
            }
        }
    }

    // 浣嶇疆瑙﹀彂鍣?
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

    // 鐗╁搧瑙﹀彂鍣?
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

    // 浠诲姟绫?
    [System.Serializable]
    public class Quest
    {
        public string questId;
        public string title;
        [System.NonSerialized]
        public BaseTrigger trigger;
        public List<QuestEvent> eventChain;

        // 浠诲姟鐘舵€?
        public QuestStatus status = QuestStatus.NotStarted;
        public int currentEventIndex = 0;

        public Quest(string questId, string title, BaseTrigger trigger, List<QuestEvent> eventChain)
        {
            this.questId = questId;
            this.title = title;
            this.trigger = trigger;
            this.eventChain = eventChain ?? new List<QuestEvent>();
        }

        // 寮€濮嬩换鍔?
        public void StartQuest()
        {
            if (status != QuestStatus.NotStarted)
            {
                Debug.LogWarning($"浠诲姟 {questId} 宸茬粡寮€濮嬫垨瀹屾垚");
                return;
            }

            status = QuestStatus.InProgress;
            currentEventIndex = 0;
            Debug.Log($"寮€濮嬩换鍔? {title}");

            // 鎵ц绗竴涓簨浠?
            if (eventChain.Count > 0)
            {
                ExecuteCurrentEvent();
            }
        }

        // 鎵ц褰撳墠浜嬩欢
        private async void ExecuteCurrentEvent()
        {
            if (currentEventIndex >= eventChain.Count)
            {
                CompleteQuest();
                return;
            }

            var currentEvent = eventChain[currentEventIndex];
            Debug.Log($"鎵ц浠诲姟浜嬩欢: {currentEvent.eventId}");

            // 鎾斁瀵硅瘽
            await currentEvent.PlayDialogue(() => {
                // 瀵硅瘽瀹屾垚鍚庣殑鍥炶皟
                Debug.Log($"浜嬩欢 {currentEvent.eventId} 瀵硅瘽瀹屾垚");
            });

            // 杩欓噷鍙互绛夊緟瀹屾垚鏉′欢婊¤冻
            // 瀹為檯瀹炵幇涓簲璇ョ洃鍚畬鎴愭潯浠剁殑鍙樺寲
        }

        // 妫€鏌ュ綋鍓嶄簨浠舵槸鍚﹀畬鎴?
        public bool CheckCurrentEventCompletion(object args)
        {
            if (currentEventIndex >= eventChain.Count)
            {
                return true;
            }

            var currentEvent = eventChain[currentEventIndex];
            return currentEvent.completion?.IsCompleted(args) ?? false;
        }

        // 瀹屾垚褰撳墠浜嬩欢锛岃繘鍏ヤ笅涓€涓簨浠?
        public void CompleteCurrentEvent()
        {
            if (currentEventIndex >= eventChain.Count)
            {
                return;
            }

            var completedEvent = eventChain[currentEventIndex];
            completedEvent.completion?.Execute();

            currentEventIndex++;
            Debug.Log($"瀹屾垚浜嬩欢: {completedEvent.eventId}");

            if (currentEventIndex >= eventChain.Count)
            {
                CompleteQuest();
            }
            else
            {
                ExecuteCurrentEvent();
            }
        }

        // 瀹屾垚浠诲姟
        private void CompleteQuest()
        {
            status = QuestStatus.Completed;
            Debug.Log($"浠诲姟瀹屾垚: {title}");

            // 瑙﹀彂浠诲姟瀹屾垚浜嬩欢
            QuestManager.Instance?.OnQuestCompleted(this);
        }

        // 鑾峰彇褰撳墠浜嬩欢
        public QuestEvent GetCurrentEvent()
        {
            if (currentEventIndex >= 0 && currentEventIndex < eventChain.Count)
            {
                return eventChain[currentEventIndex];
            }
            return null;
        }
    }

    // 浠诲姟鐘舵€佹灇涓?
    public enum QuestStatus
    {
        NotStarted,
        InProgress,
        Completed,
        Failed
    }

}
