using System;
using System.Collections.Generic;
using UnityEngine;

namespace Immortal.Quest
{
    // 条件类型定义
    public enum ConditionType
    {
        Location,
        Item,
        Level,
        Interaction,
        Combat
    }

    // 条件基类
    public abstract class BaseCondition
    {
        protected List<System.Action> listeners = new List<System.Action>();

        public string conditionId;
        public ConditionType type;

        protected BaseCondition(string conditionId, ConditionType type)
        {
            this.conditionId = conditionId;
            this.type = type;
        }

        // 注册监听器
        public void OnConditionMet(System.Action listener)
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

        // 检查条件是否满足（由子类实现）
        public abstract bool Check(object args);

        // 处理参数变化（由外部系统调用）
        public void HandleArgsChange(object args)
        {
            if (Check(args))
            {
                NotifyListeners();
            }
        }

        // 准备开始任务
        public abstract void PrepareToStart();
    }

    // 具体条件子类
    [System.Serializable]
    public class LocationCondition : BaseCondition
    {
        public string areaId;
        public string requiredItem;

        public LocationCondition(string conditionId, string areaId, string requiredItem = null)
            : base(conditionId, ConditionType.Location)
        {
            this.areaId = areaId;
            this.requiredItem = requiredItem;
        }

        public override bool Check(object args)
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

        public override void PrepareToStart()
        {
            Debug.Log($"准备开始位置任务: {conditionId}");
        }
    }

    [System.Serializable]
    public class ItemCondition : BaseCondition
    {
        public string itemId;
        public int requiredQuantity;

        public ItemCondition(string conditionId, string itemId, int requiredQuantity)
            : base(conditionId, ConditionType.Item)
        {
            this.itemId = itemId;
            this.requiredQuantity = requiredQuantity;
        }

        public override bool Check(object args)
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

        public override void PrepareToStart()
        {
            Debug.Log($"准备开始物品任务: {conditionId}");
        }
    }

    [System.Serializable]
    public class CombatCondition : BaseCondition
    {
        public int[] enemies;
        public int[] friends;

        public CombatCondition(string conditionId, int[] enemies, int[] friends)
            : base(conditionId, ConditionType.Combat)
        {
            this.enemies = enemies ?? new int[0];
            this.friends = friends ?? new int[0];
        }

        public override bool Check(object args)
        {
            if (!(args is CombatConditionArgs combatArgs))
            {
                return false;
            }

            int enemyCount = 0;
            foreach (var enemy in combatArgs.combatState.enemies)
            {
                if (Array.IndexOf(enemies, enemy.id) >= 0)
                {
                    enemyCount++;
                }
            }

            int friendCount = 0;
            foreach (var friend in combatArgs.combatState.friends)
            {
                if (Array.IndexOf(friends, friend.id) >= 0)
                {
                    friendCount++;
                }
            }

            return enemyCount > 0 && friendCount > 1;
        }

        public override void PrepareToStart()
        {
            Debug.Log($"准备开始战斗任务: {conditionId}");
            // 这里可以添加战斗任务前的准备逻辑
            
            // 获取修仙者和战斗管理器
            var cultivators = CultivatorManager.Instance?.GetAllCultivators();
            var combatManager = CombatManager.Instance;

            if (cultivators != null && combatManager != null)
            {
                var friendTeam = new List<object>();
                var enemyTeam = new List<object>();

                // 添加玩家角色（ID=0）和朋友
                friendTeam.Add(cultivators[0]); // 假设玩家是ID=0
                foreach (int friendId in friends)
                {
                    if (cultivators.ContainsKey(friendId))
                    {
                        friendTeam.Add(cultivators[friendId]);
                    }
                }

                // 添加敌人
                foreach (int enemyId in enemies)
                {
                    if (cultivators.ContainsKey(enemyId))
                    {
                        enemyTeam.Add(cultivators[enemyId]);
                    }
                }

                // 设置战斗队伍
                combatManager.SetTeams(friendTeam.ToArray(), enemyTeam.ToArray());
            }
        }
    }

    // 条件参数类
    public class LocationConditionArgs
    {
        public string currentArea;
        public List<string> inventory;

        public LocationConditionArgs(string currentArea, List<string> inventory)
        {
            this.currentArea = currentArea;
            this.inventory = inventory ?? new List<string>();
        }
    }

    public class ItemConditionArgs
    {
        public List<string> inventory;

        public ItemConditionArgs(List<string> inventory)
        {
            this.inventory = inventory ?? new List<string>();
        }
    }

    public class CombatConditionArgs
    {
        public CombatState combatState;

        public CombatConditionArgs(CombatState combatState)
        {
            this.combatState = combatState;
        }
    }

    // 战斗状态类
    [System.Serializable]
    public class CombatState
    {
        public List<CombatUnit> enemies;
        public List<CombatUnit> friends;

        public CombatState()
        {
            enemies = new List<CombatUnit>();
            friends = new List<CombatUnit>();
        }
    }

    [System.Serializable]
    public class CombatUnit
    {
        public int id;
        public string name;
        // 其他战斗单位属性

        public CombatUnit(int id, string name)
        {
            this.id = id;
            this.name = name;
        }
    }

    // 占位符类（需要在其他地方实现）
    public class CultivatorManager : MonoBehaviour
    {
        public static CultivatorManager Instance { get; private set; }

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

        public Dictionary<int, object> GetAllCultivators()
        {
            // 实现获取所有修仙者的逻辑
            return new Dictionary<int, object>();
        }
    }

    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

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

        public void SetTeams(object[] friendTeam, object[] enemyTeam)
        {
            // 实现设置战斗队伍的逻辑
            Debug.Log($"设置战斗队伍 - 友方: {friendTeam.Length}, 敌方: {enemyTeam.Length}");
        }
    }
}