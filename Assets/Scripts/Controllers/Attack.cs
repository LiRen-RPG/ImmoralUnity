using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

namespace Immortal.Controllers
{
    // 飞行轨迹类型枚举
    public enum FlightPathType
    {
        Linear,     // 直线
        Accelerate, // 加速
        Decelerate, // 减速
        Circular,   // 绕圈
        Curve,      // 曲线
        Bounce,     // 弹跳
        Homing      // 追踪
    }

    // 视觉效果配置
    [System.Serializable]
    public class VisualEffectConfig
    {
        public string prefabPath;               // 预制体路径
        public Vector3 scale = Vector3.one;     // 缩放
        public Vector3 rotation = Vector3.zero; // 旋转
        public Color color = Color.white;       // 颜色
        public float fadeInDuration = 0f;       // 淡入时间
        public float fadeOutDuration = 0.5f;    // 淡出时间
        public string trailEffect = "";         // 拖尾效果
        public string hitEffect = "";           // 击中效果
    }

    // 飞行轨迹配置
    [System.Serializable]
    public class FlightPathConfig
    {
        public FlightPathType type = FlightPathType.Linear;
        public float speed = 10f;                // 基础速度
        public float duration = 2f;              // 飞行持续时间
        public float acceleration = 2f;          // 加速度（加速/减速类型）
        public float radius = 2f;                // 半径（绕圈类型）
        public float amplitude = 3f;             // 振幅（弹跳/曲线类型）
        public float frequency = 3f;             // 频率（弹跳/曲线类型）
        public Transform targetTransform;        // 追踪目标（追踪类型）
        public Vector3[] controlPoints;          // 控制点（曲线类型）
    }

    // 技能效果配置
    [System.Serializable]
    public class SkillEffectConfig
    {
        public enum DirectionType { Left, Right, Vector }
        public DirectionType directionType;
        public Vector3 targetPosition;           // 目标位置
        public FlightPathConfig flightPath;     // 飞行轨迹配置
        public VisualEffectConfig visual;       // 视觉效果配置
        public AttackCallback callback;         // 攻击回调
    }

    public class Attack : MonoBehaviour
    {
        // 静态预制体缓存
        private static Dictionary<string, GameObject> prefabCache = new Dictionary<string, GameObject>();

        private Collider attackCollider;
        private Rigidbody rigidBody;
        private Vector3 velocity = Vector3.zero;
        private ActorBase actor;
        private Immortal.Core.SkillInstance skillInstance;
        private AttackCallback attackCallback;

        // 飞行轨迹控制属性
        private FlightPathConfig flightConfig;
        private VisualEffectConfig visualConfig;
        private float startTime = 0f;
        private Vector3 startPosition = Vector3.zero;
        private Vector3 targetPosition = Vector3.zero;
        private bool isFlying = false;
        private float flightStartTime = 0f;
        private float totalElapsedTime = 0f;
        private float currentDeltaTime = 0f;

        // 淡出效果控制属性
        private bool isFading = false;
        private float fadeStartTime = 0f;
        private float fadeDuration = 0.5f;
        private bool isFadeIn = false;
        private Color originalColor = Color.white;
        private float targetAlpha = 0f;

        private Renderer[] renderers;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                originalColor = renderers[0].material.color;
            }
        }

        /// <summary>
        /// 设置飞行路径配置
        /// </summary>
        public void SetFlightConfig(FlightPathConfig config)
        {
            flightConfig = config;
        }

        /// <summary>
        /// 设置视觉效果配置
        /// </summary>
        public void SetVisualConfig(VisualEffectConfig config)
        {
            visualConfig = config;
        }

        private void Start()
        {
            // Unity初始化
        }

        private void OnTriggerEnter(Collider other)
        {
            ActorBase actorBase = other.GetComponent<ActorBase>();
            if (actorBase != null && actorBase != actor) // 忽略攻击者
            {
                // 处理被攻击逻辑
                Vector3 impulseVector = rigidBody != null ? rigidBody.velocity * rigidBody.mass : Vector3.zero;
                actorBase.TakeDamage(skillInstance, impulseVector);
                attackCallback?.Invoke(actorBase);
            }
        }

        private void OnDestroy()
        {
            // Unity自动处理事件清理
        }

        private void Update()
        {
            // 处理飞行轨迹更新
            if (isFlying && flightConfig != null && rigidBody != null)
            {
                currentDeltaTime = Time.deltaTime;
                totalElapsedTime += currentDeltaTime;

                float duration = flightConfig.duration;

                // 检查是否飞行完成
                if (totalElapsedTime >= duration)
                {
                    rigidBody.velocity = Vector3.zero;
                    isFlying = false;
                    OnFlightComplete();
                    return;
                }

                // 计算并应用速度向量
                Vector3 velocity = CalculateTrajectoryVelocity(totalElapsedTime, targetPosition);
                rigidBody.velocity = velocity;
            }

            // 处理淡出效果更新
            if (isFading)
            {
                UpdateFadeEffect(Time.deltaTime);
            }
        }

        /// <summary>
        /// 根据轨迹类型计算速度向量
        /// </summary>
        private Vector3 CalculateTrajectoryVelocity(float elapsedTime, Vector3 targetPosition)
        {
            if (flightConfig == null || !isFlying) return Vector3.zero;

            float duration = flightConfig.duration;
            float progress = Mathf.Min(elapsedTime / duration, 1.0f);

            switch (flightConfig.type)
            {
                case FlightPathType.Linear:
                    return CalculateLinearVelocity(progress, targetPosition);
                case FlightPathType.Accelerate:
                    return CalculateAcceleratedVelocity(progress, targetPosition);
                case FlightPathType.Decelerate:
                    return CalculateDeceleratedVelocity(progress, targetPosition);
                case FlightPathType.Circular:
                    return CalculateCircularVelocity(progress, targetPosition);
                case FlightPathType.Curve:
                    return CalculateCurveVelocity(progress, targetPosition);
                case FlightPathType.Bounce:
                    return CalculateBounceVelocity(progress, targetPosition);
                case FlightPathType.Homing:
                    return CalculateHomingVelocity(elapsedTime, targetPosition);
                default:
                    return CalculateLinearVelocity(progress, targetPosition);
            }
        }

        /// <summary>
        /// 线性速度计算
        /// </summary>
        private Vector3 CalculateLinearVelocity(float progress, Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - startPosition;
            float distance = direction.magnitude;
            float duration = flightConfig.duration;

            if (distance == 0) return Vector3.zero;

            direction.Normalize();
            float speed = distance / duration;

            return direction * speed;
        }

        /// <summary>
        /// 加速速度计算
        /// </summary>
        private Vector3 CalculateAcceleratedVelocity(float progress, Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - startPosition;
            float distance = direction.magnitude;
            float duration = flightConfig.duration;

            if (distance == 0) return Vector3.zero;

            direction.Normalize();
            float averageSpeed = distance / duration;
            float currentSpeed = averageSpeed * Mathf.Pow(1 + progress, flightConfig.acceleration);

            return direction * currentSpeed;
        }

        /// <summary>
        /// 减速速度计算
        /// </summary>
        private Vector3 CalculateDeceleratedVelocity(float progress, Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - startPosition;
            float distance = direction.magnitude;
            float duration = flightConfig.duration;

            if (distance == 0) return Vector3.zero;

            direction.Normalize();
            float averageSpeed = distance / duration;
            float currentSpeed = averageSpeed * Mathf.Pow(1 - progress * 0.8f, flightConfig.acceleration);

            return direction * currentSpeed;
        }

        /// <summary>
        /// 圆形轨迹速度计算
        /// </summary>
        private Vector3 CalculateCircularVelocity(float progress, Vector3 targetPosition)
        {
            float radius = flightConfig.radius;
            float frequency = flightConfig.frequency;
            float duration = flightConfig.duration;

            // 计算切向速度（圆周运动）
            float angularVelocity = frequency * 2 * Mathf.PI / duration;
            float tangentialSpeed = angularVelocity * radius;

            // 计算当前角度
            float angle = progress * frequency * 2 * Mathf.PI;

            // 切向方向（垂直于半径方向）
            Vector3 tangentialDirection = new Vector3(-Mathf.Sin(angle), 0, Mathf.Cos(angle));

            // 向目标的径向速度
            Vector3 centerToTarget = targetPosition - startPosition;
            Vector3 radialDirection = centerToTarget.normalized;
            float radialSpeed = centerToTarget.magnitude / duration;

            // 合成速度
            Vector3 tangentialVelocity = tangentialDirection * tangentialSpeed;
            Vector3 radialVelocity = radialDirection * radialSpeed;

            return tangentialVelocity + radialVelocity;
        }

        /// <summary>
        /// 曲线速度计算
        /// </summary>
        private Vector3 CalculateCurveVelocity(float progress, Vector3 targetPosition)
        {
            if (flightConfig.controlPoints == null || flightConfig.controlPoints.Length == 0)
            {
                return CalculateParabolicVelocity(progress, targetPosition);
            }

            // 二次贝塞尔曲线的导数（切向量）
            if (flightConfig.controlPoints.Length >= 1)
            {
                Vector3 P0 = startPosition;
                Vector3 P1 = flightConfig.controlPoints[0];
                Vector3 P2 = targetPosition;
                float t = progress;

                // 贝塞尔曲线导数：B'(t) = 2(1-t)(P1-P0) + 2t(P2-P1)
                Vector3 term1 = 2 * (1 - t) * (P1 - P0);
                Vector3 term2 = 2 * t * (P2 - P1);
                Vector3 tangent = term1 + term2;

                // 标准化并应用速度
                tangent.Normalize();
                return tangent * flightConfig.speed;
            }

            return CalculateLinearVelocity(progress, targetPosition);
        }

        /// <summary>
        /// 弹跳速度计算
        /// </summary>
        private Vector3 CalculateBounceVelocity(float progress, Vector3 targetPosition)
        {
            float amplitude = flightConfig.amplitude;
            float frequency = flightConfig.frequency;
            float duration = flightConfig.duration;

            // 水平方向的恒定速度
            Vector3 horizontalDirection = targetPosition - startPosition;
            horizontalDirection.y = 0; // 只保留水平分量
            horizontalDirection.Normalize();
            float horizontalSpeed = (targetPosition - startPosition).magnitude / duration;

            // 垂直方向的弹跳速度（正弦波的导数）
            float verticalSpeed = amplitude * frequency * Mathf.PI * Mathf.Cos(frequency * Mathf.PI * progress) / duration;

            Vector3 velocity = horizontalDirection * horizontalSpeed;
            velocity.y = verticalSpeed;

            return velocity;
        }

        /// <summary>
        /// 抛物线速度计算
        /// </summary>
        private Vector3 CalculateParabolicVelocity(float progress, Vector3 targetPosition)
        {
            float amplitude = flightConfig.amplitude;
            float duration = flightConfig.duration;

            // 水平方向的恒定速度
            Vector3 horizontalDirection = targetPosition - startPosition;
            horizontalDirection.y = 0;
            horizontalDirection.Normalize();
            float horizontalSpeed = (targetPosition - startPosition).magnitude / duration;

            // 垂直方向的抛物线速度：d/dt[4h*t*(1-t)] = 4h*(1-2t)
            float verticalSpeed = 4 * amplitude * (1 - 2 * progress) / duration;

            Vector3 velocity = horizontalDirection * horizontalSpeed;
            velocity.y = verticalSpeed;

            return velocity;
        }

        /// <summary>
        /// 追踪速度计算
        /// </summary>
        private Vector3 CalculateHomingVelocity(float elapsedTime, Vector3 targetPosition)
        {
            Vector3 currentPosition = transform.position;

            // 如果指定了追踪目标，使用该目标的位置
            Vector3 actualTarget = flightConfig.targetTransform != null ? 
                flightConfig.targetTransform.position : targetPosition;

            // 计算朝向目标的方向
            Vector3 toTarget = actualTarget - currentPosition;
            float distance = toTarget.magnitude;

            if (distance < 0.1f)
            {
                return Vector3.zero; // 已到达目标
            }

            toTarget.Normalize();
            return toTarget * flightConfig.speed;
        }

        /// <summary>
        /// 开始飞行动画
        /// </summary>
        private void StartFlightAnimation(Vector3 targetPosition)
        {
            if (flightConfig == null) return;

            isFlying = true;
            flightStartTime = Time.time;
            startPosition = transform.position;
            this.targetPosition = targetPosition;
            totalElapsedTime = 0f;
        }

        /// <summary>
        /// 飞行完成时的回调
        /// </summary>
        private void OnFlightComplete()
        {
            // 停止物理运动
            if (rigidBody != null)
            {
                rigidBody.velocity = Vector3.zero;
            }

            // 触发击中效果
            if (flightConfig != null && flightConfig.type != FlightPathType.Homing)
            {
                PlayHitEffect();
            }

            // 检查是否需要开始淡出效果
            if (visualConfig != null && visualConfig.fadeOutDuration > 0)
            {
                StartFadeOut(visualConfig.fadeOutDuration, 0);
            }
            else
            {
                // 如果没有淡出效果，直接销毁对象
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 播放击中效果
        /// </summary>
        private void PlayHitEffect()
        {
            if (visualConfig != null && !string.IsNullOrEmpty(visualConfig.hitEffect))
            {
                GameObject hitEffectPrefab = Resources.Load<GameObject>(visualConfig.hitEffect);
                if (hitEffectPrefab != null)
                {
                    Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
                }
            }
        }

        /// <summary>
        /// 更新淡出效果
        /// </summary>
        private void UpdateFadeEffect(float deltaTime)
        {
            fadeStartTime += deltaTime;
            float progress = Mathf.Min(fadeStartTime / fadeDuration, 1.0f);

            if (renderers.Length > 0)
            {
                float currentAlpha;

                if (isFadeIn)
                {
                    // 淡入：从0到目标透明度
                    currentAlpha = targetAlpha * progress;
                }
                else
                {
                    // 淡出：从原始透明度到目标透明度
                    float startAlpha = originalColor.a;
                    currentAlpha = startAlpha + (targetAlpha - startAlpha) * progress;
                }

                // 更新颜色
                Color newColor = originalColor;
                newColor.a = currentAlpha;

                foreach (var renderer in renderers)
                {
                    if (renderer.material != null)
                    {
                        renderer.material.color = newColor;
                    }
                }
            }

            // 检查淡出是否完成
            if (progress >= 1.0f)
            {
                isFading = false;
                OnFadeComplete();
            }
        }

        /// <summary>
        /// 开始淡入效果
        /// </summary>
        private void StartFadeIn(float duration, float targetAlpha = 1f)
        {
            if (renderers.Length == 0) return;

            originalColor = renderers[0].material.color;
            isFading = true;
            fadeStartTime = 0f;
            fadeDuration = duration;
            isFadeIn = true;
            this.targetAlpha = targetAlpha;

            // 设置初始透明度为0
            Color startColor = originalColor;
            startColor.a = 0;
            foreach (var renderer in renderers)
            {
                if (renderer.material != null)
                {
                    renderer.material.color = startColor;
                }
            }
        }

        /// <summary>
        /// 开始淡出效果
        /// </summary>
        private void StartFadeOut(float duration, float targetAlpha = 0f)
        {
            if (renderers.Length == 0) return;

            originalColor = renderers[0].material.color;
            isFading = true;
            fadeStartTime = 0f;
            fadeDuration = duration;
            isFadeIn = false;
            this.targetAlpha = targetAlpha;
        }

        /// <summary>
        /// 淡出完成时的回调
        /// </summary>
        private void OnFadeComplete()
        {
            if (!isFadeIn && targetAlpha <= 0)
            {
                // 如果是淡出到完全透明，销毁对象
                if (rigidBody != null)
                {
                    rigidBody.velocity = Vector3.zero;
                }
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 新的技能效果触发方法，支持复杂的飞行轨迹和视觉效果
        /// </summary>
        public void TriggerSkillEffect(SkillEffectConfig skillConfig, Immortal.Core.SkillInstance skillInstance)
        {
            this.actor = skillInstance.caster.actorBase as ActorBase;
            this.skillInstance = skillInstance;
            this.attackCollider = GetComponent<Collider>();
            this.rigidBody = GetComponent<Rigidbody>();

            // 配置物理体用于飞行轨迹
            if (rigidBody != null)
            {
                rigidBody.mass = 0.5f;
                rigidBody.useGravity = false; // 禁用重力
                rigidBody.drag = 0f;          // 禁用线性阻尼
                rigidBody.angularDrag = 0f;   // 禁用角度阻尼
            }

            attackCallback = skillConfig.callback;

            // 设置飞行配置和视觉配置
            SetFlightConfig(skillConfig.flightPath);
            SetVisualConfig(skillConfig.visual);

            // 计算目标位置
            Vector3 targetPosition;
            if (skillConfig.directionType == SkillEffectConfig.DirectionType.Vector)
            {
                targetPosition = skillConfig.targetPosition;
            }
            else
            {
                // 根据方向计算目标位置
                float distance = 100f; // Unity scale adjusted
                float directionMultiplier = skillConfig.directionType == SkillEffectConfig.DirectionType.Right ? 1 : -1;
                targetPosition = new Vector3(
                    transform.position.x + distance * directionMultiplier,
                    transform.position.y,
                    transform.position.z
                );
            }

            // 应用视觉效果
            ApplyVisualEffect(skillConfig.visual);

            // 开始飞行动画
            StartFlightAnimation(targetPosition);
        }

        /// <summary>
        /// 应用视觉效果
        /// </summary>
        private void ApplyVisualEffect(VisualEffectConfig visualConfig)
        {
            if (visualConfig == null) return;

            // 应用缩放
            transform.localScale = visualConfig.scale;

            // 应用旋转
            transform.eulerAngles = visualConfig.rotation;

            // 应用颜色
            foreach (var renderer in renderers)
            {
                if (renderer.material != null)
                {
                    renderer.material.color = visualConfig.color;
                }
            }

            // 淡入效果
            if (visualConfig.fadeInDuration > 0)
            {
                StartFadeIn(visualConfig.fadeInDuration, 1f);
            }

            // 加载拖尾效果
            if (!string.IsNullOrEmpty(visualConfig.trailEffect))
            {
                LoadTrailEffect(visualConfig.trailEffect);
            }
        }

        /// <summary>
        /// 加载拖尾效果
        /// </summary>
        private void LoadTrailEffect(string trailEffectPath)
        {
            GameObject trailPrefab = Resources.Load<GameObject>(trailEffectPath);
            if (trailPrefab != null)
            {
                GameObject trailInstance = Instantiate(trailPrefab, transform);
            }
        }

        /// <summary>
        /// 兼容性方法：保持原有的接口
        /// </summary>
        public void TriggerAttackEffect(string direction, float speed, Immortal.Core.SkillInstance skillInstance, AttackCallback callback)
        {
            // 创建默认的技能效果配置
            SkillEffectConfig defaultSkillConfig = new SkillEffectConfig
            {
                directionType = direction == "left" ? SkillEffectConfig.DirectionType.Left : SkillEffectConfig.DirectionType.Right,
                flightPath = new FlightPathConfig
                {
                    type = FlightPathType.Linear,
                    speed = speed,
                    duration = 0.5f
                },
                visual = new VisualEffectConfig
                {
                    prefabPath = "", // 使用当前对象
                    fadeOutDuration = 0.5f
                },
                callback = callback
            };

            // 调用新的技能效果方法
            TriggerSkillEffect(defaultSkillConfig, skillInstance);

            // 兼容原有的视觉逻辑
            gameObject.SetActive(true);
            Vector3 scale = transform.localScale;
            scale.x = direction == "left" ? 1 : -1;
            transform.localScale = scale;
        }

        // ===== 技能效果配置工厂方法 =====

        /// <summary>
        /// 创建线性飞行技能效果
        /// </summary>
        public static SkillEffectConfig CreateLinearSkillEffect(string direction, float speed = 10f, string prefabPath = "", AttackCallback callback = null)
        {
            return new SkillEffectConfig
            {
                directionType = direction == "left" ? SkillEffectConfig.DirectionType.Left : SkillEffectConfig.DirectionType.Right,
                flightPath = new FlightPathConfig
                {
                    type = FlightPathType.Linear,
                    speed = speed,
                    duration = 3f
                },
                visual = new VisualEffectConfig
                {
                    prefabPath = prefabPath,
                    fadeOutDuration = 0.3f
                },
                callback = callback
            };
        }

        /// <summary>
        /// 创建抛物线技能效果
        /// </summary>
        public static SkillEffectConfig CreateParabolicSkillEffect(string direction, float speed = 8f, float height = 3f, string prefabPath = "", AttackCallback callback = null)
        {
            return new SkillEffectConfig
            {
                directionType = direction == "left" ? SkillEffectConfig.DirectionType.Left : SkillEffectConfig.DirectionType.Right,
                flightPath = new FlightPathConfig
                {
                    type = FlightPathType.Curve,
                    speed = speed,
                    duration = 2.5f,
                    amplitude = height
                },
                visual = new VisualEffectConfig
                {
                    prefabPath = prefabPath,
                    fadeInDuration = 0.2f,
                    fadeOutDuration = 0.3f
                },
                callback = callback
            };
        }

        /// <summary>
        /// 创建追踪技能效果
        /// </summary>
        public static SkillEffectConfig CreateHomingSkillEffect(Transform target, float speed = 6f, string prefabPath = "", AttackCallback callback = null)
        {
            return new SkillEffectConfig
            {
                directionType = SkillEffectConfig.DirectionType.Vector,
                targetPosition = target.position,
                flightPath = new FlightPathConfig
                {
                    type = FlightPathType.Homing,
                    speed = speed,
                    duration = 4f,
                    targetTransform = target
                },
                visual = new VisualEffectConfig
                {
                    prefabPath = prefabPath,
                    trailEffect = "Effects/HomingTrail"
                },
                callback = callback
            };
        }

        /// <summary>
        /// 创建圆形轨迹技能效果
        /// </summary>
        public static SkillEffectConfig CreateCircularSkillEffect(string direction, float speed = 5f, float radius = 2f, float circles = 1f, string prefabPath = "", AttackCallback callback = null)
        {
            return new SkillEffectConfig
            {
                directionType = direction == "left" ? SkillEffectConfig.DirectionType.Left : SkillEffectConfig.DirectionType.Right,
                flightPath = new FlightPathConfig
                {
                    type = FlightPathType.Circular,
                    speed = speed,
                    duration = 3f,
                    radius = radius,
                    frequency = circles
                },
                visual = new VisualEffectConfig
                {
                    prefabPath = prefabPath,
                    trailEffect = "Effects/CircularTrail"
                },
                callback = callback
            };
        }

        // ===== 静态方法 =====

        /// <summary>
        /// 加载攻击预制体
        /// </summary>
        public static void LoadAttackPrefab(string path, System.Action<Exception, GameObject> callback)
        {
            if (prefabCache.ContainsKey(path))
            {
                callback?.Invoke(null, prefabCache[path]);
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                callback?.Invoke(new Exception($"Failed to load attack prefab: {path}"), null);
                return;
            }

            prefabCache[path] = prefab;
            callback?.Invoke(null, prefab);
        }

        /// <summary>
        /// 创建攻击实例
        /// </summary>
        public static Attack CreateAttackInstance(string path, Vector3 startPosition)
        {
            if (prefabCache.ContainsKey(path))
            {
                GameObject prefab = prefabCache[path];
                GameObject attackObject = Instantiate(prefab, startPosition, Quaternion.identity);
                return EnsureAttackComponent(attackObject);
            }
            return null;
        }

        /// <summary>
        /// 异步创建攻击实例
        /// </summary>
        public static void CreateAttackInstanceAsync(string path, Vector3 startPosition, System.Action<Exception, Attack> callback)
        {
            LoadAttackPrefab(path, (err, prefab) =>
            {
                if (err != null)
                {
                    callback?.Invoke(err, null);
                    return;
                }

                Attack attack = CreateAttackInstance(path, startPosition);
                if (attack != null)
                {
                    callback?.Invoke(null, attack);
                }
                else
                {
                    callback?.Invoke(new Exception("Failed to create attack instance"), null);
                }
            });
        }

        /// <summary>
        /// 确保对象拥有Attack组件
        /// </summary>
        private static Attack EnsureAttackComponent(GameObject obj)
        {
            Attack attackComponent = obj.GetComponent<Attack>();
            if (attackComponent == null)
            {
                attackComponent = obj.AddComponent<Attack>();
            }
            return attackComponent;
        }

        /// <summary>
        /// 清理预制体缓存
        /// </summary>
        public static void ClearPrefabCache()
        {
            prefabCache.Clear();
        }

        /// <summary>
        /// 获取缓存的预制体数量
        /// </summary>
        public static int GetCacheSize()
        {
            return prefabCache.Count;
        }
    }
}