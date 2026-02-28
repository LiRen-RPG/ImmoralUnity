using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Immortal.Core;

namespace Immortal.Quest
{
    // 对话行类
    [System.Serializable]
    public class DialogueLine
    {
        public int speaker;
        public string text;
        public int requiredChoice;
        [System.NonSerialized]
        public AudioClip audioClip;

        public DialogueLine(int speaker, string text, int requiredChoice = 0, AudioClip audioClip = null)
        {
            this.speaker = speaker;
            this.text = text;
            this.requiredChoice = requiredChoice;
            this.audioClip = audioClip;
        }

        /// <summary>
        /// 播放当前对话行的音频
        /// </summary>
        public async Task PlayAudio()
        {
            if (audioClip != null)
            {
                // Unity音频播放逻辑
                AudioSource.PlayClipAtPoint(audioClip, Vector3.zero);
                
                // 等待音频播放完毕
                float duration = audioClip.length;
                await Task.Delay((int)(duration * 1000));
            }
            else
            {
                Debug.LogWarning("音频剪辑未加载，无法播放");
            }
        }

        /// <summary>
        /// 异步创建对话行（包含TTS音频生成）
        /// </summary>
        public static async Task<DialogueLine> Create(int speakerId, string text, int requiredChoice = 0)
        {
            try
            {
                // 获取说话者信息
                var cultivators = CultivatorManager.Instance?.GetAllCultivators();
                if (cultivators != null && cultivators.ContainsKey(speakerId))
                {
                    var cultivator = cultivators[speakerId] as Cultivator;
                    if (cultivator != null)
                    {
                        // 根据修仙者属性选择声音类型
                        string voiceType = GetVoiceByGenderAgeAndPersonality(
                            cultivator.gender,
                            cultivator.appearanceAge,
                            cultivator.personality
                        );

                        // 生成TTS音频（这里需要实现具体的TTS逻辑）
                        AudioClip audioClip = await GenerateTTS(text, voiceType);
                        return new DialogueLine(speakerId, text, requiredChoice, audioClip);
                    }
                }

                // 如果无法获取修仙者信息，创建无音频的对话行
                return new DialogueLine(speakerId, text, requiredChoice, null);
            }
            catch (Exception error)
            {
                Debug.LogError($"创建对话行时发生错误: {error.Message}");
                return new DialogueLine(speakerId, text, requiredChoice, null);
            }
        }

        private static string GetVoiceByGenderAgeAndPersonality(Gender gender, float appearanceAge, float personality)
        {
            // 实现根据性别、年龄和性格选择声音类型的逻辑
            // 这里是占位符实现
            if (gender == Gender.Female)
            {
                return appearanceAge < 30 ? "young_female" : "mature_female";
            }
            else
            {
                return appearanceAge < 30 ? "young_male" : "mature_male";
            }
        }

        private static async Task<AudioClip> GenerateTTS(string text, string voiceType)
        {
            // 这里需要实现TTS音频生成逻辑
            // 占位符实现
            await Task.Delay(100); // 模拟异步操作
            return null; // 实际实现中应该返回生成的AudioClip
        }
    }

    // 任务事件类
    [System.Serializable]
    public class QuestEvent
    {
        public string eventId;
        public List<QuestParticipant> participants;
        public List<DialogueLine> dialogue;
        public BaseCompletion completion;

        public QuestEvent(string eventId, List<QuestParticipant> participants, 
                         List<DialogueLine> dialogue, BaseCompletion completion)
        {
            this.eventId = eventId;
            this.participants = participants ?? new List<QuestParticipant>();
            this.dialogue = dialogue ?? new List<DialogueLine>();
            this.completion = completion;
        }

        /// <summary>
        /// 播放对话
        /// </summary>
        public async Task PlayDialogue(System.Action onDialogueComplete = null)
        {
            Debug.Log($"开始对话: {eventId}");
            
            foreach (var currentLine in dialogue)
            {
                Debug.Log($"{currentLine.speaker}: {currentLine.text}");
                
                if (currentLine.requiredChoice > 0)
                {
                    Debug.Log($"需要选择: {currentLine.requiredChoice}");
                }

                // 播放音频
                await currentLine.PlayAudio();
            }

            onDialogueComplete?.Invoke();
        }
    }

    // 任务参与者类
    [System.Serializable]
    public class QuestParticipant
    {
        public string npcId;
        public string role;

        public QuestParticipant(string npcId, string role)
        {
            this.npcId = npcId;
            this.role = role;
        }
    }

    // 任务完成条件基类
    public abstract class BaseCompletion
    {
        public string completionId;
        public ConditionType type;

        protected BaseCompletion(string completionId, ConditionType type)
        {
            this.completionId = completionId;
            this.type = type;
        }

        // 检查是否完成（由子类实现）
        public abstract bool IsCompleted(object args);

        // 执行完成操作（由子类实现）
        public abstract void Execute();
    }

    // 位置完成条件
    public class LocationCompletion : BaseCompletion
    {
        public string targetAreaId;
        public string requiredItem;

        public LocationCompletion(string completionId, string targetAreaId, string requiredItem = null)
            : base(completionId, ConditionType.Location)
        {
            this.targetAreaId = targetAreaId;
            this.requiredItem = requiredItem;
        }

        public override bool IsCompleted(object args)
        {
            if (!(args is LocationConditionArgs locationArgs))
            {
                return false;
            }

            bool isInArea = locationArgs.currentArea == targetAreaId;
            if (string.IsNullOrEmpty(requiredItem))
            {
                return isInArea;
            }

            bool hasItem = locationArgs.inventory.Contains(requiredItem);
            return isInArea && hasItem;
        }

        public override void Execute()
        {
            Debug.Log($"位置任务完成: 到达 {targetAreaId}");
        }
    }

    // 物品完成条件
    public class ItemCompletion : BaseCompletion
    {
        public string itemId;
        public int requiredQuantity;

        public ItemCompletion(string completionId, string itemId, int requiredQuantity)
            : base(completionId, ConditionType.Item)
        {
            this.itemId = itemId;
            this.requiredQuantity = requiredQuantity;
        }

        public override bool IsCompleted(object args)
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

        public override void Execute()
        {
            Debug.Log($"物品任务完成: 收集 {itemId} x{requiredQuantity}");
        }
    }

    // 战斗完成条件
    public class CombatCompletion : BaseCompletion
    {
        public int[] enemyIds;
        public bool mustWin;

        public CombatCompletion(string completionId, int[] enemyIds, bool mustWin = true)
            : base(completionId, ConditionType.Combat)
        {
            this.enemyIds = enemyIds ?? new int[0];
            this.mustWin = mustWin;
        }

        public override bool IsCompleted(object args)
        {
            if (!(args is CombatCompletionArgs combatArgs))
            {
                return false;
            }

            if (mustWin && !combatArgs.playerWon)
            {
                return false;
            }

            // 检查是否击败了指定的敌人
            foreach (int enemyId in enemyIds)
            {
                if (!combatArgs.defeatedEnemies.Contains(enemyId))
                {
                    return false;
                }
            }

            return true;
        }

        public override void Execute()
        {
            Debug.Log($"战斗任务完成: 击败敌人");
        }
    }

    // 战斗完成参数类
    public class CombatCompletionArgs
    {
        public bool playerWon;
        public List<int> defeatedEnemies;

        public CombatCompletionArgs(bool playerWon, List<int> defeatedEnemies)
        {
            this.playerWon = playerWon;
            this.defeatedEnemies = defeatedEnemies ?? new List<int>();
        }
    }
}