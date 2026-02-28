using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Immortal.Combat
{
    /// <summary>
    /// 战斗管理器
    /// 负责管理战场上所有单位的AI协调和战斗逻辑
    /// </summary>
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("Combat Settings")]
        [SerializeField] private float updateInterval = 0.1f; // 更新间隔

        private List<object> teamA = new List<object>();  // 队伍A (ActorBase类型)
        private List<object> teamB = new List<object>();  // 队伍B (ActorBase类型)

        private List<CombatAI> allAIs = new List<CombatAI>();

        private bool battleActive = false;
        private float lastUpdateTime = 0f;
        private object scene = null; // Scene类型引用

        // 战斗事件
        public event System.Action OnBattleStarted;
        public event System.Action<bool> OnBattleEnded; // true = teamA wins, false = teamB wins
        public event System.Action<BattleStats> OnBattleStatsUpdated;

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

        public void Init(object scene)
        {
            this.scene = scene;
        }

        /// <summary>
        /// 同时设置两个队伍
        /// </summary>
        public void SetTeams(object[] teamA, object[] teamB)
        {
            this.teamA = teamA.ToList();
            this.teamB = teamB.ToList();
            ReinitializeCombat();
        }

        /// <summary>
        /// 重新初始化战斗（当队伍发生变化时调用）
        /// </summary>
        private void ReinitializeCombat()
        {
            if (teamA.Count > 0 && teamB.Count > 0)
            {
                InitializeCombatUnits();
                if (!battleActive)
                {
                    StartBattle();
                }
            }
        }

        /// <summary>
        /// 初始化战斗单位
        /// </summary>
        private void InitializeCombatUnits()
        {
            // 停用之前所有的AI
            DeactivateAllAIs();

            // 清空之前的数据
            allAIs.Clear();

            // 收集所有AI组件
            CollectAIsFromTeam(teamA);
            CollectAIsFromTeam(teamB);

            Debug.Log($"Combat initialized: Team A: {teamA.Count}, Team B: {teamB.Count}, Total AIs: {allAIs.Count}");
        }

        /// <summary>
        /// 从队伍中收集AI组件
        /// </summary>
        private void CollectAIsFromTeam(List<object> team)
        {
            foreach (var actor in team)
            {
                var ai = GetActorCombatAI(actor);
                if (ai != null)
                {
                    allAIs.Add(ai);
                    // 激活AI
                    ai.ActivateAI();
                }
            }
        }

        /// <summary>
        /// 停用所有AI（包括场景中其他未管理的AI）
        /// </summary>
        private void DeactivateAllAIs()
        {
            // 停用当前管理的AI
            foreach (var ai in allAIs)
            {
                if (ai != null)
                {
                    ai.DeactivateAI();
                }
            }
        }

        /// <summary>
        /// 开始战斗
        /// </summary>
        public void StartBattle()
        {
            battleActive = true;
            Debug.Log("Battle started!");

            // 为每个AI设置不同的配置以增加多样性
            DiversifyAIBehaviors();

            OnBattleStarted?.Invoke();
        }

        /// <summary>
        /// 结束战斗
        /// </summary>
        public void EndBattle()
        {
            battleActive = false;
            Debug.Log("Battle ended!");

            // 让所有单位进入待机状态并停用AI
            foreach (var ai in allAIs)
            {
                if (ai != null && ai.GetActor() != null)
                {
                    var actor = ai.GetActor();
                    ActorIdle(actor);
                    // 停用AI
                    ai.DeactivateAI();
                }
            }

            // 清空AI列表
            allAIs.Clear();
        }

        /// <summary>
        /// 使AI行为多样化
        /// </summary>
        private void DiversifyAIBehaviors()
        {
            for (int index = 0; index < allAIs.Count; index++)
            {
                var ai = allAIs[index];
                if (ai == null) continue;

                var actor = ai.GetActor();
                if (actor == null) continue;

                var cultivator = GetActorCultivator(actor);
                if (cultivator == null) continue;

                float personality = cultivator.personality;
                var config = ai.GetConfig();

                // 根据角色索引和性格设置不同的AI倾向
                switch (index % 3)
                {
                    case 0: // 攻击型
                        var aggressiveConfig = new CombatAIConfig
                        {
                            detectionRange = config.detectionRange + 100f,
                            attackRange = config.attackRange + 50f,
                            retreatHealthThreshold = config.retreatHealthThreshold,
                            fleeHealthThreshold = config.fleeHealthThreshold,
                            supportRange = config.supportRange,
                            aggressiveness = Mathf.Min(1.0f, config.aggressiveness + 0.3f),
                            defensiveness = Mathf.Max(0.1f, config.defensiveness - 0.2f),
                            teamwork = config.teamwork
                        };
                        ai.SetConfig(aggressiveConfig);
                        break;

                    case 1: // 防御型
                        var defensiveConfig = new CombatAIConfig
                        {
                            detectionRange = config.detectionRange,
                            attackRange = config.attackRange,
                            retreatHealthThreshold = config.retreatHealthThreshold + 0.1f,
                            fleeHealthThreshold = config.fleeHealthThreshold,
                            supportRange = config.supportRange,
                            aggressiveness = Mathf.Max(0.2f, config.aggressiveness - 0.2f),
                            defensiveness = Mathf.Min(1.0f, config.defensiveness + 0.3f),
                            teamwork = Mathf.Min(1.0f, config.teamwork + 0.2f)
                        };
                        ai.SetConfig(defensiveConfig);
                        break;

                    case 2: // 支援型
                        var supportConfig = new CombatAIConfig
                        {
                            detectionRange = config.detectionRange,
                            attackRange = config.attackRange,
                            retreatHealthThreshold = config.retreatHealthThreshold,
                            fleeHealthThreshold = config.fleeHealthThreshold,
                            supportRange = config.supportRange + 100f,
                            aggressiveness = Mathf.Max(0.3f, config.aggressiveness - 0.1f),
                            defensiveness = config.defensiveness,
                            teamwork = Mathf.Min(1.0f, config.teamwork + 0.4f)
                        };
                        ai.SetConfig(supportConfig);
                        break;
                }
            }
        }

        private void Update()
        {
            ManualUpdate(Time.deltaTime);
        }

        /// <summary>
        /// 公开的手动帧更新入口，供外部（如 Scene）在自定义节点调用。
        /// 与 Unity 自动调用的 Update() 逻辑完全一致。
        /// </summary>
        public void ManualUpdate(float deltaTime)
        {
            if (!battleActive) return;

            lastUpdateTime += deltaTime;

            // 按固定间隔更新战斗信息
            if (lastUpdateTime >= updateInterval)
            {
                UpdateAIStates();
                CheckBattleEnd();
                lastUpdateTime = 0f;

                // 更新战斗统计
                var stats = GetBattleStats();
                OnBattleStatsUpdated?.Invoke(stats);
            }

            // 更新所有AI
            foreach (var ai in allAIs)
            {
                if (ai != null && ai.IsAIActive())
                {
                    ai.Update(deltaTime);
                }
            }
        }

        /// <summary>
        /// 检查ActorBase是否活跃
        /// </summary>
        private bool IsActorActive(object actor)
        {
            if (actor == null) return false;

            // 基于cultivator的生命值判断是否活跃
            var cultivator = GetActorCultivator(actor);
            if (cultivator != null)
            {
                return cultivator.currentHealth > 0;
            }

            return true; // 没有cultivator的默认为活跃
        }

        /// <summary>
        /// 更新AI状态
        /// </summary>
        private void UpdateAIStates()
        {
            // 为队伍A的AI更新敌我信息
            foreach (var actor in teamA)
            {
                var ai = GetActorCombatAI(actor);
                if (ai != null && IsActorActive(actor))
                {
                    var allies = teamA.Where(a => a != actor && IsActorActive(a)).ToList();
                    var enemies = teamB.Where(a => IsActorActive(a)).ToList();
                    ai.UpdateCombatUnits(allies, enemies);
                }
            }

            // 为队伍B的AI更新敌我信息
            foreach (var actor in teamB)
            {
                var ai = GetActorCombatAI(actor);
                if (ai != null && IsActorActive(actor))
                {
                    var allies = teamB.Where(a => a != actor && IsActorActive(a)).ToList();
                    var enemies = teamA.Where(a => IsActorActive(a)).ToList();
                    ai.UpdateCombatUnits(allies, enemies);
                }
            }
        }

        /// <summary>
        /// 检查战斗是否结束
        /// </summary>
        private void CheckBattleEnd()
        {
            int activeUnitsA = teamA.Count(a => IsActorActive(a));
            int activeUnitsB = teamB.Count(a => IsActorActive(a));

            if (activeUnitsA == 0)
            {
                Debug.Log("Team B wins!");
                OnBattleEnded?.Invoke(false);
                EndBattle();
            }
            else if (activeUnitsB == 0)
            {
                Debug.Log("Team A wins!");
                OnBattleEnded?.Invoke(true);
                EndBattle();
            }
        }

        /// <summary>
        /// 获取战斗统计信息
        /// </summary>
        public BattleStats GetBattleStats()
        {
            return new BattleStats
            {
                teamA = GetTeamStats(teamA),
                teamB = GetTeamStats(teamB)
            };
        }

        /// <summary>
        /// 获取单个队伍的统计信息
        /// </summary>
        private TeamStats GetTeamStats(List<object> team)
        {
            var activeActors = team.Where(a => IsActorActive(a)).ToList();
            var aiControlled = activeActors.Where(a => GetActorCombatAI(a) != null).ToList();
            var playerControlled = activeActors.Where(a => GetActorCombatAI(a) == null).ToList();

            // 统计AI状态
            var states = new Dictionary<CombatState, int>();
            foreach (var actor in aiControlled)
            {
                var ai = GetActorCombatAI(actor);
                if (ai != null)
                {
                    var state = ai.GetCurrentState();
                    if (states.ContainsKey(state))
                        states[state]++;
                    else
                        states[state] = 1;
                }
            }

            return new TeamStats
            {
                total = team.Count,
                active = activeActors.Count,
                aiControlled = aiControlled.Count,
                playerControlled = playerControlled.Count,
                states = states
            };
        }

        /// <summary>
        /// 获取玩家控制的单位（没有AI的单位）
        /// </summary>
        public List<object> GetPlayerControlledUnits()
        {
            return teamA.Concat(teamB)
                .Where(actor => IsActorActive(actor) && GetActorCombatAI(actor) == null)
                .ToList();
        }

        /// <summary>
        /// 获取指定队伍中的玩家控制单位
        /// </summary>
        public List<object> GetPlayerControlledUnitsInTeam(bool teamA)
        {
            var team = teamA ? this.teamA : this.teamB;
            return team.Where(actor => IsActorActive(actor) && GetActorCombatAI(actor) == null).ToList();
        }

        /// <summary>
        /// 检查某个单位是否为玩家控制
        /// </summary>
        public bool IsPlayerControlled(object actor)
        {
            return GetActorCombatAI(actor) == null;
        }

        /// <summary>
        /// 手动激活指定角色的AI
        /// </summary>
        public void ActivateActorAI(object actor)
        {
            var ai = GetActorCombatAI(actor);
            if (ai != null && !allAIs.Contains(ai))
            {
                allAIs.Add(ai);
                ai.ActivateAI();
                var actorName = GetActorCultivator(actor)?.name ?? "Unknown";
                Debug.Log($"Manually activated AI for {actorName}");
            }
        }

        /// <summary>
        /// 手动停用指定角色的AI
        /// </summary>
        public void DeactivateActorAI(object actor)
        {
            var ai = GetActorCombatAI(actor);
            if (ai != null)
            {
                allAIs.Remove(ai);
                ai.DeactivateAI();
                var actorName = GetActorCultivator(actor)?.name ?? "Unknown";
                Debug.Log($"Manually deactivated AI for {actorName}");
            }
        }

        /// <summary>
        /// 获取当前激活的AI数量
        /// </summary>
        public int GetActiveAICount()
        {
            return allAIs.Count(ai => ai != null && ai.IsAIActive());
        }

        /// <summary>
        /// 重置战斗
        /// </summary>
        public void ResetBattle()
        {
            EndBattle();

            // 重置所有单位的血量和状态
            foreach (var actor in teamA.Concat(teamB))
            {
                var cultivator = GetActorCultivator(actor);
                if (cultivator != null)
                {
                    cultivator.ResetStats();
                }
            }

            // 重新开始战斗
            Invoke(nameof(StartBattle), 1.0f);
        }

        // ===== 反射方法辅助函数（避免循环依赖） =====

        private Core.Cultivator GetActorCultivator(object actor)
        {
            if (actor == null) return null;
            var property = actor.GetType().GetProperty("cultivator");
            return property?.GetValue(actor) as Core.Cultivator;
        }

        private CombatAI GetActorCombatAI(object actor)
        {
            if (actor == null) return null;
            var method = actor.GetType().GetMethod("GetCombatAI");
            return method?.Invoke(actor, null) as CombatAI;
        }

        private void ActorIdle(object actor)
        {
            if (actor == null) return;
            var method = actor.GetType().GetMethod("Idle");
            method?.Invoke(actor, null);
        }

        // 公共API方法
        public bool IsBattleActive()
        {
            return battleActive;
        }

        public List<object> GetTeamA()
        {
            return new List<object>(teamA);
        }

        public List<object> GetTeamB()
        {
            return new List<object>(teamB);
        }

        public List<CombatAI> GetAllAIs()
        {
            return new List<CombatAI>(allAIs);
        }
    }

    // 战斗统计数据结构
    [System.Serializable]
    public class BattleStats
    {
        public TeamStats teamA;
        public TeamStats teamB;
    }

    [System.Serializable]
    public class TeamStats
    {
        public int total;
        public int active;
        public int aiControlled;
        public int playerControlled;
        public Dictionary<CombatState, int> states;

        public TeamStats()
        {
            states = new Dictionary<CombatState, int>();
        }
    }
}