using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Immortal.Controllers;

namespace Immortal.Combat
{
    // 战斗状态枚举
    public enum CombatState
    {
        IDLE,              // 待机
        PATROL,            // 巡逻
        CHASE,             // 追击
        ATTACK,            // 攻击
        RETREAT,           // 撤退
        DEFEND,            // 防御
        SUPPORT,           // 支援队友
        FLEE               // 逃跑
    }

    // 战斗AI配置
    [System.Serializable]
    public class CombatAIConfig
    {
        public float detectionRange = 100f;     // 检测范围
        public float attackRange = 2f;         // 攻击范围
        public float retreatHealthThreshold = 0.3f; // 撤退血量阈值
        public float fleeHealthThreshold = 0.15f;    // 逃跑血量阈值
        public float supportRange = 4f;        // 支援范围
        public float aggressiveness = 0.7f;      // 攻击性 (0-1)
        public float defensiveness = 0.5f;       // 防御性 (0-1)
        public float teamwork = 0.6f;           // 团队协作性 (0-1)
        public float attackInterval = 1.5f;     // 攻击间隔（秒）
    }

    // 战斗决策结果
    public class CombatDecision
    {
        public string action;
        public Immortal.Controllers.ActorBase target;
        public Vector3? direction;
        public float duration;
        public float speedFactor = 0.6f; // 速度因子（默认巡逻/支援速度）

        public CombatDecision(string action)
        {
            this.action = action;
        }
    }

    public class CombatAI
    {
        private ActorBase actor;
        private CombatState currentState = CombatState.IDLE;
        private CombatState previousState = CombatState.IDLE;
        private float stateTimer = 0f;
        private float decisionCooldown = 0f;

        // AI激活状态 - 只有被CombatManager管理的AI才会更新
        private bool isActive = false;

        // AI配置
        private CombatAIConfig config = new CombatAIConfig();

        // 攻击冷却计时
        private float attackCooldown = 0f;

        // 战斗单位列表
        private List<ActorBase> allies = new List<ActorBase>();
        private List<ActorBase> enemies = new List<ActorBase>();
        private ActorBase currentTarget = null;

        // AI状态存储
        private Dictionary<ActorBase, float> threatLevels = new Dictionary<ActorBase, float>();

        // 仇恨值系统
        private Dictionary<ActorBase, float> hatredLevels = new Dictionary<ActorBase, float>();
        private float maxHatred = 100f; // 最大仇恨值
        private float hatredDecayRate = 1f; // 每秒仇恨值衰减

        // 巡逻相关
        private List<Vector3> patrolPoints = new List<Vector3>();
        private int currentPatrolIndex = 0;
        private float patrolRadius = 300f;

        public CombatAI(ActorBase actor)
        {
            this.actor = actor;

            // 初始化巡逻点
            InitializePatrolPoints();

            // 根据cultivator性格调整AI配置
            AdjustConfigByPersonality();
        }

        public ActorBase GetActor()
        {
            return actor;
        }

        /// <summary>
        /// 根据cultivator的性格调整AI配置
        /// </summary>
        private void AdjustConfigByPersonality()
        {
            var cultivator = GetActorCultivator(actor);
            if (cultivator == null) return;

            float personality = cultivator.personality;

            // 性格影响AI行为倾向
            config.aggressiveness = Mathf.Clamp(personality * 1.2f, 0.2f, 1.0f);
            config.defensiveness = Mathf.Clamp((1 - personality) * 1.2f, 0.2f, 1.0f);
            config.teamwork = Mathf.Clamp(0.5f + personality * 0.5f, 0.3f, 1.0f);

            // 强势性格更倾向于攻击，弱势性格更倾向于防御和逃跑
            if (personality > 0.7f)
            {
                config.retreatHealthThreshold *= 0.7f;
                config.fleeHealthThreshold *= 0.5f;
            }
            else if (personality < 0.3f)
            {
                config.retreatHealthThreshold *= 1.3f;
                config.fleeHealthThreshold *= 1.5f;
            }
        }

        /// <summary>
        /// 初始化巡逻点
        /// </summary>
        private void InitializePatrolPoints()
        {
            Vector3 centerPos = GetActorPosition(actor);
            int points = 4;

            for (int i = 0; i < points; i++)
            {
                float angle = (float)i / points * Mathf.PI * 2;
                float x = centerPos.x + Mathf.Cos(angle) * patrolRadius;
                float z = centerPos.z + Mathf.Sin(angle) * patrolRadius;
                patrolPoints.Add(new Vector3(x, centerPos.y, z));
            }
        }

        /// <summary>
        /// 更新战斗单位信息
        /// </summary>
        public void UpdateCombatUnits(List<ActorBase> allies, List<ActorBase> enemies)
        {
            this.allies = allies.Where(actor => IsActorActive(actor)).ToList();
            this.enemies = enemies.Where(actor => IsActorActive(actor)).ToList();

            // 更新威胁等级
            UpdateThreatLevels();
        }

        /// <summary>
        /// 检查ActorBase是否活跃
        /// </summary>
        private bool IsActorActive(ActorBase actor)
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
        /// 获取ActorBase的生命值比例
        /// </summary>
        private float GetHealthRatio(ActorBase actor)
        {
            var cultivator = GetActorCultivator(actor);
            if (cultivator == null) return 1.0f;

            return Mathf.Max(0f, cultivator.currentHealth / cultivator.maxHealth);
        }

        /// <summary>
        /// 更新威胁等级
        /// </summary>
        private void UpdateThreatLevels()
        {
            Vector3 myPos = GetActorPosition(actor);

            foreach (var enemy in enemies)
            {
                float distance = Vector3.Distance(myPos, GetActorPosition(enemy));
                float healthRatio = GetHealthRatio(enemy);
                float distanceFactor = Mathf.Max(0f, 1f - distance / config.detectionRange);

                // 获取敌人的AI状态来判断当前行动
                var enemyAI = GetActorCombatAI(enemy);
                bool isAttacking = enemyAI != null && enemyAI.GetCurrentState() == CombatState.ATTACK;

                float threatLevel = healthRatio * distanceFactor * (isAttacking ? 1.5f : 1.0f);
                threatLevels[enemy] = threatLevel;
            }

            // 按威胁等级排序
            enemies.Sort((a, b) => 
            {
                float threatA = threatLevels.ContainsKey(a) ? threatLevels[a] : 0f;
                float threatB = threatLevels.ContainsKey(b) ? threatLevels[b] : 0f;
                return threatB.CompareTo(threatA);
            });
        }

        /// <summary>
        /// 更新仇恨值系统
        /// </summary>
        private void UpdateHatredLevels(float deltaTime)
        {
            // 仇恨值自然衰减
            var keys = hatredLevels.Keys.ToList();
            foreach (var enemy in keys)
            {
                float newHatred = Mathf.Max(0f, hatredLevels[enemy] - hatredDecayRate * deltaTime);
                if (newHatred <= 0f)
                {
                    hatredLevels.Remove(enemy);
                }
                else
                {
                    hatredLevels[enemy] = newHatred;
                }
            }

            // 移除已死亡或不活跃的敌人的仇恨值
            var activeEnemies = new HashSet<ActorBase>(enemies);
            var toRemove = new List<ActorBase>();
            foreach (var kvp in hatredLevels)
            {
                if (!activeEnemies.Contains(kvp.Key) || !IsActorActive(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var enemy in toRemove)
            {
                hatredLevels.Remove(enemy);
            }
        }

        /// <summary>
        /// 增加对特定敌人的仇恨值
        /// </summary>
        public void AddHatred(ActorBase enemy, float amount)
        {
            if (enemy == null || !IsActorActive(enemy)) return;

            float currentHatred = hatredLevels.ContainsKey(enemy) ? hatredLevels[enemy] : 0f;
            float newHatred = Mathf.Min(maxHatred, currentHatred + amount);
            hatredLevels[enemy] = newHatred;

            var actorName = GetActorCultivator(actor)?.name ?? "Unknown";
            var enemyName = GetActorCultivator(enemy)?.name ?? "Unknown";
            Debug.Log($"{actorName} 对 {enemyName} 的仇恨值增加到 {newHatred}");
        }

        /// <summary>
        /// 获取对特定敌人的仇恨值
        /// </summary>
        public float GetHatred(ActorBase enemy)
        {
            return hatredLevels.ContainsKey(enemy) ? hatredLevels[enemy] : 0f;
        }

        /// <summary>
        /// 计算追击速度因子
        /// </summary>
        private float CalculateChaseSpeedFactor(ActorBase enemy)
        {
            float hatred = GetHatred(enemy);
            // 仇恨值越高速度越快：0仇恨=0.8，满仇恨=1.0
            return Mathf.Lerp(0.8f, 1.0f, hatred / maxHatred);
        }

        /// <summary>
        /// 主要AI更新逻辑
        /// </summary>
        public void Update(float deltaTime)
        {
            // 只有激活状态的AI才会更新
            if (actor == null || !isActive) return;

            stateTimer += deltaTime;
            decisionCooldown -= deltaTime;
            attackCooldown -= deltaTime;

            // 更新仇恨值系统
            UpdateHatredLevels(deltaTime);

            // 每0.2秒做一次决策
            if (decisionCooldown <= 0f)
            {
                MakeDecision();
                decisionCooldown = 0.2f;
            }

            // 执行当前状态的行为
            ExecuteCurrentState(deltaTime);
        }

        /// <summary>
        /// 做出战斗决策
        /// </summary>
        private void MakeDecision()
        {
            var decision = AnalyzeAndDecide();
            ExecuteDecision(decision);
        }

        /// <summary>
        /// 分析情况并做出决策
        /// </summary>
        private CombatDecision AnalyzeAndDecide()
        {
            float myHealth = GetCurrentHealthRatio();
            ActorBase nearestEnemy = FindNearestEnemy();
            ActorBase nearestAlly = FindNearestAlly();

            // 检查是否需要逃跑
            if (myHealth < config.fleeHealthThreshold)
            {
                return DecideFlee();
            }

            // 检查是否需要撤退
            if (myHealth < config.retreatHealthThreshold && nearestEnemy != null)
            {
                return DecideRetreat(nearestEnemy);
            }

            // 检查是否需要支援队友
            if (config.teamwork > 0.5f)
            {
                ActorBase allyNeedsHelp = FindAllyNeedingHelp();
                if (allyNeedsHelp != null)
                {
                    return DecideSupport(allyNeedsHelp);
                }
            }

            // 检查是否可以攻击
            if (nearestEnemy != null)
            {
                float distance = Vector3.Distance(GetActorPosition(actor), GetActorPosition(nearestEnemy));

                if (distance <= config.attackRange)
                {
                    return DecideAttack(nearestEnemy);
                }
                else if (distance <= config.detectionRange)
                {
                    return DecideChase(nearestEnemy);
                }
            }

            // 默认巡逻
            return DecidePatrol();
        }

        /// <summary>
        /// 决定逃跑
        /// </summary>
        private CombatDecision DecideFlee()
        {
            Vector3 fleeDirection = CalculateFleeDirection();
            ChangeState(CombatState.FLEE);

            return new CombatDecision("flee")
            {
                direction = fleeDirection,
                speedFactor = 0.8f
            };
        }

        /// <summary>
        /// 决定撤退
        /// </summary>
        private CombatDecision DecideRetreat(ActorBase enemy)
        {
            Vector3 retreatDirection = CalculateRetreatDirection(enemy);
            ChangeState(CombatState.RETREAT);

            return new CombatDecision("retreat")
            {
                direction = retreatDirection,
                target = enemy,
                speedFactor = 0.8f
            };
        }

        /// <summary>
        /// 决定支援队友
        /// </summary>
        private CombatDecision DecideSupport(ActorBase ally)
        {
            ChangeState(CombatState.SUPPORT);

            Vector3 direction = (GetActorPosition(ally) - GetActorPosition(actor)).normalized;
            return new CombatDecision("support")
            {
                target = ally,
                direction = direction
            };
        }

        /// <summary>
        /// 决定攻击
        /// </summary>
        private CombatDecision DecideAttack(ActorBase enemy)
        {
            ChangeState(CombatState.ATTACK);
            currentTarget = enemy;

            return new CombatDecision("attack")
            {
                target = enemy
            };
        }

        /// <summary>
        /// 决定追击
        /// </summary>
        private CombatDecision DecideChase(ActorBase enemy)
        {
            ChangeState(CombatState.CHASE);
            currentTarget = enemy;

            // 计算追击速度因子
            float speedFactor = CalculateChaseSpeedFactor(enemy);

            Vector3 direction = (GetActorPosition(enemy) - GetActorPosition(actor)).normalized;
            return new CombatDecision("chase")
            {
                target = enemy,
                direction = direction,
                speedFactor = speedFactor
            };
        }

        /// <summary>
        /// 决定巡逻
        /// </summary>
        private CombatDecision DecidePatrol()
        {
            ChangeState(CombatState.PATROL);

            Vector3 targetPoint = patrolPoints[currentPatrolIndex];
            Vector3 direction = (targetPoint - GetActorPosition(actor)).normalized;

            return new CombatDecision("patrol")
            {
                direction = direction
            };
        }

        /// <summary>
        /// 执行决策
        /// </summary>
        private void ExecuteDecision(CombatDecision decision)
        {
            switch (decision.action)
            {
                case "flee":
                case "retreat":
                case "patrol":
                case "support":
                    if (decision.direction.HasValue)
                    {
                        // 设置基础速度因子
                        float speedFactor = decision.speedFactor;
                        SetActorSpeedFactor(actor, speedFactor);
                        ActorWalk(actor, decision.direction.Value);
                    }
                    break;

                case "chase":
                    if (decision.direction.HasValue && decision.target != null)
                    {
                        // 设置追击速度
                        float speedFactor = decision.speedFactor;
                        SetActorSpeedFactor(actor, speedFactor);
                        ActorWalk(actor, decision.direction.Value);

                        var actorName = GetActorCultivator(actor)?.name ?? "Unknown";
                        var targetName = GetActorCultivator(decision.target)?.name ?? "Unknown";
                        Debug.Log($"{actorName} 以 {(speedFactor * 100).ToString("F0")}% 速度追击 {targetName}");
                    }
                    break;

                case "attack":
                    ExecuteAttack(decision.target);
                    break;
            }
        }

        /// <summary>
        /// 执行攻击
        /// </summary>
        private void ExecuteAttack(ActorBase target)
        {
            if (target == null) return;

            // 面向目标
            Vector3 direction = GetActorPosition(target) - GetActorPosition(actor);
            SetActorOrientation(actor, direction.x > 0 ? 1 : -1);

            // 攻击冷却检查
            if (attackCooldown > 0f) return;

            // 执行攻击并重置冷却
            ActorAttack(actor);
            attackCooldown = config.attackInterval;
        }

        /// <summary>
        /// 执行当前状态的行为
        /// </summary>
        private void ExecuteCurrentState(float deltaTime)
        {
            switch (currentState)
            {
                case CombatState.PATROL:
                    ExecutePatrol();
                    break;

                case CombatState.CHASE:
                    ExecuteChase();
                    break;

                case CombatState.ATTACK:
                    // 攻击状态向目标走动并保持面向
                    if (currentTarget != null)
                    {
                        Vector3 direction = (GetActorPosition(currentTarget) - GetActorPosition(actor));
                        SetActorOrientation(actor, direction.x > 0 ? 1 : -1);
                        // 向目标方向移动
                        ActorWalk(actor, direction.normalized);
                    }
                    break;
            }
        }

        /// <summary>
        /// 执行追击行为
        /// </summary>
        private void ExecuteChase()
        {
            if (currentTarget == null || !IsActorActive(currentTarget))
            {
                // 目标丢失或死亡，切换到巡逻状态
                ChangeState(CombatState.PATROL);
                currentTarget = null;
                return;
            }

            float distance = Vector3.Distance(GetActorPosition(actor), GetActorPosition(currentTarget));

            // 检查是否进入攻击范围
            if (distance <= config.attackRange)
            {
                ChangeState(CombatState.ATTACK);
                return;
            }

            // 检查是否超出检测范围
            if (distance > config.detectionRange)
            {
                ChangeState(CombatState.PATROL);
                currentTarget = null;
                return;
            }

            // 动态调整追击速度
            float speedFactor = CalculateChaseSpeedFactor(currentTarget);
            SetActorSpeedFactor(actor, speedFactor);

            // 更新追击方向
            Vector3 direction = (GetActorPosition(currentTarget) - GetActorPosition(actor)).normalized;
            ActorWalk(actor, direction);

            // 设置朝向
            SetActorOrientation(actor, direction.x > 0 ? 1 : -1);
        }

        /// <summary>
        /// 执行巡逻行为
        /// </summary>
        private void ExecutePatrol()
        {
            Vector3 targetPoint = patrolPoints[currentPatrolIndex];
            float distance = Vector3.Distance(GetActorPosition(actor), targetPoint);

            if (distance < 50f)
            {
                // 到达巡逻点，切换到下一个
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
            }
        }

        /// <summary>
        /// 改变状态
        /// </summary>
        private void ChangeState(CombatState newState)
        {
            if (currentState != newState)
            {
                previousState = currentState;
                currentState = newState;
                stateTimer = 0f;

                Debug.Log($"AI State changed: {previousState} -> {currentState}");
            }
        }

        /// <summary>
        /// 辅助方法：获取当前血量比例
        /// </summary>
        private float GetCurrentHealthRatio()
        {
            var cultivator = GetActorCultivator(actor);
            if (cultivator == null) return 1.0f;

            return cultivator.GetHealthPercentage();
        }

        /// <summary>
        /// 查找最近的敌人（考虑仇恨值）
        /// </summary>
        private ActorBase FindNearestEnemy()
        {
            if (enemies.Count == 0) return null;

            ActorBase bestTarget = null;
            float bestScore = -1f;

            foreach (var enemy in enemies)
            {
                float distance = Vector3.Distance(GetActorPosition(actor), GetActorPosition(enemy));
                if (distance > config.detectionRange) continue;

                // 计算目标优先级分数
                float hatred = GetHatred(enemy);
                float distanceFactor = Mathf.Max(0f, 1f - distance / config.detectionRange);
                float hatredFactor = hatred / maxHatred;

                // 综合评分：距离越近分数越高，仇恨值越高分数越高
                float score = distanceFactor * 0.6f + hatredFactor * 0.4f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = enemy;
                }
            }

            return bestTarget;
        }

        /// <summary>
        /// 查找最近的队友
        /// </summary>
        private ActorBase FindNearestAlly()
        {
            if (allies.Count == 0) return null;

            ActorBase nearest = null;
            float minDistance = float.MaxValue;

            foreach (var ally in allies)
            {
                float distance = Vector3.Distance(GetActorPosition(actor), GetActorPosition(ally));
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = ally;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 查找需要帮助的队友
        /// </summary>
        private ActorBase FindAllyNeedingHelp()
        {
            foreach (var ally in allies)
            {
                float distance = Vector3.Distance(GetActorPosition(actor), GetActorPosition(ally));
                float healthRatio = GetHealthRatio(ally);

                // 检查队友的AI状态
                var allyAI = GetActorCombatAI(ally);
                bool isNotFleeing = allyAI == null || allyAI.GetCurrentState() != CombatState.FLEE;

                if (distance <= config.supportRange && healthRatio < 0.5f && isNotFleeing)
                {
                    return ally;
                }
            }
            return null;
        }

        /// <summary>
        /// 计算逃跑方向
        /// </summary>
        private Vector3 CalculateFleeDirection()
        {
            Vector3 myPos = GetActorPosition(actor);
            Vector3 dangerDirection = Vector3.zero;

            // 计算所有敌人的威胁方向
            foreach (var enemy in enemies)
            {
                Vector3 direction = myPos - GetActorPosition(enemy);
                float distance = direction.magnitude;
                if (distance > 0f)
                {
                    direction.Normalize();
                    direction *= 1f / Mathf.Max(distance, 50f); // 距离越近权重越大
                    dangerDirection += direction;
                }
            }

            return dangerDirection.normalized;
        }

        /// <summary>
        /// 计算撤退方向
        /// </summary>
        private Vector3 CalculateRetreatDirection(ActorBase enemy)
        {
            Vector3 retreatDirection = (GetActorPosition(actor) - GetActorPosition(enemy)).normalized;

            // 寻找最近的队友位置作为撤退目标
            ActorBase nearestAlly = FindNearestAlly();
            if (nearestAlly != null)
            {
                Vector3 allyDirection = (GetActorPosition(nearestAlly) - GetActorPosition(actor)).normalized;

                // 混合撤退方向和队友方向
                retreatDirection = (retreatDirection + allyDirection * 0.5f).normalized;
            }

            return retreatDirection;
        }

        /// <summary>
        /// 获取当前状态
        /// </summary>
        public CombatState GetCurrentState()
        {
            return currentState;
        }

        /// <summary>
        /// 设置AI配置
        /// </summary>
        public void SetConfig(CombatAIConfig newConfig)
        {
            if (newConfig != null)
            {
                config.detectionRange = newConfig.detectionRange;
                config.attackRange = newConfig.attackRange;
                config.retreatHealthThreshold = newConfig.retreatHealthThreshold;
                config.fleeHealthThreshold = newConfig.fleeHealthThreshold;
                config.supportRange = newConfig.supportRange;
                config.aggressiveness = newConfig.aggressiveness;
                config.defensiveness = newConfig.defensiveness;
                config.teamwork = newConfig.teamwork;
            }
        }

        /// <summary>
        /// 获取AI配置
        /// </summary>
        public CombatAIConfig GetConfig()
        {
            return config;
        }

        /// <summary>
        /// 激活AI（由CombatManager调用）
        /// </summary>
        public void ActivateAI()
        {
            isActive = true;
            var actorName = GetActorCultivator(actor)?.name ?? "Unknown";
            Debug.Log($"AI activated for {actorName}");
        }

        /// <summary>
        /// 停用AI（由CombatManager调用）
        /// </summary>
        public void DeactivateAI()
        {
            isActive = false;
            // 停用时让角色进入待机状态
            ChangeState(CombatState.IDLE);
            ActorIdle(actor);
            
            var actorName = GetActorCultivator(actor)?.name ?? "Unknown";
            Debug.Log($"AI deactivated for {actorName}");
        }

        /// <summary>
        /// 检查AI是否激活
        /// </summary>
        public bool IsAIActive()
        {
            return isActive;
        }

        /// <summary>
        /// 获取当前仇恨值列表（用于调试）
        /// </summary>
        public Dictionary<ActorBase, float> GetHatredLevels()
        {
            return new Dictionary<ActorBase, float>(hatredLevels);
        }

        /// <summary>
        /// 清除所有仇恨值
        /// </summary>
        public void ClearAllHatred()
        {
            hatredLevels.Clear();
            var actorName = GetActorCultivator(actor)?.name ?? "Unknown";
            Debug.Log($"{actorName} 的所有仇恨值已清除");
        }

        /// <summary>
        /// 获取AI状态信息（用于调试）
        /// </summary>
        public string GetAIStatus()
        {
            string target = currentTarget != null ? (GetActorCultivator(currentTarget)?.name ?? "无") : "无";
            float hatred = currentTarget != null ? GetHatred(currentTarget) : 0f;
            float speedFactor = currentTarget != null ? CalculateChaseSpeedFactor(currentTarget) : 0f;

            return $"状态: {currentState} | 目标: {target} | 仇恨值: {hatred} | 速度因子: {speedFactor:F2}";
        }

        // ===== ActorBase 直接调用辅助函数 =====

        private Core.Cultivator GetActorCultivator(ActorBase actor) => actor?.cultivator;

        private Vector3 GetActorPosition(ActorBase actor) => actor?.transform.position ?? Vector3.zero;

        private CombatAI GetActorCombatAI(ActorBase actor) => actor?.GetCombatAI();

        private void SetActorSpeedFactor(ActorBase actor, float speedFactor) => actor?.SetSpeedFactor(speedFactor);

        private void ActorWalk(ActorBase actor, Vector3 direction) => actor?.Walk(direction);

        private void SetActorOrientation(ActorBase actor, int orientation) => actor?.SetOrientation(orientation);

        private void ActorAttack(ActorBase actor) => actor?.Attack(null);

        private void ActorIdle(ActorBase actor) => actor?.Idle();
    }
}