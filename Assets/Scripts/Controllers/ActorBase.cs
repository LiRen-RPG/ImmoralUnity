using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Spine.Unity;
using Spine;

namespace Immortal.Controllers
{
    public delegate void AttackCallback(ActorBase hitActor);

    public class ActorBase : MonoBehaviour
    {
        [SerializeField] private SkeletonAnimation skeleton;
        
        // 血条相关属性
        private ProgressBarController healthBar;
        private Vector3 healthBarOffset = new Vector3(0, 2.2f, 0); // 血条在角色头顶的偏移

        protected Vector3 direction = Vector3.zero;
        public float moveSpeed = 1.4f; // Movement speed (~1.4 m/s, normal human walking speed)
        protected int orientation = -1; // 1 for right, -1 for left
        protected int genderOrientation = -1; // 1 for right, -1 for left

        protected bool isJumping = false;
        protected float gravity = -9.81f; // Standard Earth gravity (m/s²)
        protected float jumpForce = 4.43f; // Initial jump velocity for ~1m height: v=sqrt(2*9.81*1)≈4.43 m/s

        protected Vector3 logicalPosition = Vector3.zero;
        protected float timescaleBeforeJump = 0;

        protected Rigidbody rigidBody;
        protected GameObject shadow;
        protected Vector3 lastVelocity = Vector3.zero;
        
        public Immortal.Core.Cultivator cultivator;
        public Immortal.Item.Inventory inventory;
        protected Immortal.Combat.CombatAI combatAI;

        protected virtual void Awake()
        {
            // 在 Awake 中初始化背包，确保在所有 Start() 之前完成
            InitializeInventory();
        }

        protected virtual void Start()
        {
            // 优先在自身查找，找不到则向子节点查找（Spine 作为子节点时使用）
            if (skeleton == null)
                skeleton = GetComponent<SkeletonAnimation>();
            if (skeleton == null)
                skeleton = GetComponentInChildren<SkeletonAnimation>();

            logicalPosition = transform.position;
            
            // 加载攻击预制体（Unity资源加载方式不同）
            LoadAttackPrefab();
            
            rigidBody = GetComponent<Rigidbody>();
            if (rigidBody != null)
            {
                rigidBody.interpolation = RigidbodyInterpolation.Interpolate; // Unity equivalent of CCD
            }
            
            shadow = transform.Find("shadow")?.gameObject;
            Idle(); // Play idle animation
            
            // Unity碰撞事件设置
            SetupCollisionEvents();
            CreateHealthBar();
        }

        public Immortal.Combat.CombatAI GetCombatAI()
        {
            return combatAI;
        }

        // 初始化背包系统
        protected virtual void InitializeInventory()
        {
            int inventoryCapacity = GetInventoryCapacity();
            inventory = new Immortal.Item.Inventory(inventoryCapacity);
            Debug.Log($"{gameObject.name} 的背包初始化完成，容量: {inventoryCapacity}");
        }

        // 获取背包容量（子类可重写）
        protected virtual int GetInventoryCapacity()
        {
            return 70; // 默认背包容量
        }

        // 获取背包实例
        public Immortal.Item.Inventory GetInventory()
        {
            return inventory;
        }

        protected virtual void OnDestroy()
        {
            // 清理血条节点
            if (healthBar != null)
            {
                Destroy(healthBar.gameObject);
            }
        }

        public void Walk(Vector3 direction)
        {
            if (isJumping) return;
            
            if (rigidBody != null)
            {
                rigidBody.velocity = new Vector3(direction.x * moveSpeed, rigidBody.velocity.y, direction.z * moveSpeed);
            }
            else
            {
                transform.Translate(direction * moveSpeed * Time.deltaTime);
            }
            
            this.direction = direction;

            if (direction.x != 0)
            {
                orientation = direction.x > 0 ? 1 : -1;
            }

            if (skeleton != null)
            {
                if (skeleton.AnimationName != "walk_at_0")
                {
                    skeleton.AnimationState.SetAnimation(0, "walk_at_0", true);
                }
                skeleton.timeScale = moveSpeed / 0.8f; // Adjusted for Unity scale
                FlipSprite();
            }
        }

        /// <summary>
        /// 设置移动速度
        /// </summary>
        /// <param name="factor">速度因子（0.25 = 慢走，0.5 = 快走）</param>
        public void SetSpeedFactor(float factor)
        {
            moveSpeed = cultivator.currentSpeed * factor;
        }

        public void Idle(float timeScale = 1.0f)
        {
            direction = Vector3.zero;

            if (skeleton != null)
            {
                skeleton.AnimationState.SetAnimation(0, "idle", true);
                skeleton.timeScale = timeScale;
            }
        }

        protected void FlipSprite()
        {
            if (skeleton != null)
            {
                Vector3 scale = skeleton.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (-orientation * genderOrientation);
                skeleton.transform.localScale = scale;
            }
        }

        public void Attack(AttackCallback callback)
        {
            if (skeleton != null)
            {
                var trackEntry = skeleton.AnimationState.SetAnimation(1, "attack1", false);
                Vector3 position = transform.position;
                var skillInstance = cultivator.CreateSkillInstance(0);
                
                // Unity中需要通过不同方式处理攻击实例
                CreateAttackInstance(new Vector3(position.x, position.y + 2f, position.z), skillInstance, callback);
            }
        }

        private void CreateAttackInstance(Vector3 position, Immortal.Core.SkillInstance skillInstance, AttackCallback callback)
        {
            // Unity攻击实例创建逻辑
            GameObject attackPrefab = Resources.Load<GameObject>("Prefabs/Attack/AttackSprite");
            if (attackPrefab != null)
            {
                GameObject attackInstance = Instantiate(attackPrefab, position, Quaternion.identity);
                var attackComponent = attackInstance.GetComponent<Immortal.Controllers.Attack>();
                if (attackComponent != null)
                {
                    string direction = orientation > 0 ? "right" : "left";
                    attackComponent.TriggerAttackEffect(direction, 10f, skillInstance, callback);
                }
            }
        }

        public void Jump()
        {
            if (!isJumping && rigidBody != null)
            {
                isJumping = true;
                timescaleBeforeJump = skeleton.timeScale;
                skeleton.timeScale = 0;
                
                // J = m * Δv，乘以质量将目标速度转为真实冲量（ForceMode.Impulse 会除以 mass，需预先补偿）
                Vector3 jumpImpulse = new Vector3(0, jumpForce * rigidBody.mass, 0);
                rigidBody.AddForce(jumpImpulse, ForceMode.Impulse);
            }
        }

        private void SetupCollisionEvents()
        {
            // Unity碰撞事件需要通过OnCollisionEnter/Exit方法处理
            // 这里可以添加碰撞体组件的设置
        }

        protected virtual void OnCollisionEnter(Collision collision)
        {
            if(collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
            {
                HandleLandingCollision(collision);
            }
        }

        private void HandleLandingCollision(Collision collision)
        {
            if (!isJumping) return;
            
            if (collision.rigidbody != null && !collision.rigidbody.isKinematic) return;
            
            if (lastVelocity.magnitude < 2f) return;
            
            // 只有在下落阶段（Y 速度向下）才处理落地，
            // 防止跳跃施加冲量的同帧 OnCollisionStay 误触发
            if (lastVelocity.y > 0f) return;
            
            foreach (ContactPoint contact in collision.contacts)
            {
                if (Vector3.Dot(contact.normal, Vector3.up) < Mathf.Cos(45f * Mathf.Deg2Rad))
                {
                    continue;
                }
                
                isJumping = false;
                skeleton.timeScale = timescaleBeforeJump;
                
                if (direction != Vector3.zero && rigidBody != null)
                {
                    rigidBody.velocity = new Vector3(direction.x * moveSpeed, rigidBody.velocity.y, direction.z * moveSpeed);
                }
                break;
            }
        }

        protected virtual void OnCollisionExit(Collision collision)
        {
            // Handle collision exit if needed
        }

        protected virtual void Update()
        {
            combatAI?.Update(Time.deltaTime);
            
            // Store the last velocity
            if (rigidBody != null)
            {
                lastVelocity = rigidBody.velocity;
            }

            // Handle movement forces
            if (direction != Vector3.zero && !isJumping && rigidBody != null)
            {
                Vector3 gravity = Physics.gravity;
                Vector3 force = direction.normalized * gravity.magnitude * rigidBody.mass;
                rigidBody.AddForce(force);
            }

            // Handle attack animation completion
            HandleAnimationCompletion();
        }

        private void HandleAnimationCompletion()
        {
            if (skeleton != null)
            {
                var trackEntry1 = skeleton.AnimationState.GetCurrent(1);
                if (trackEntry1 != null && trackEntry1.IsComplete)
                {
                    trackEntry1.Alpha = 0;
                }
                
                var trackEntry2 = skeleton.AnimationState.GetCurrent(2);
                if (trackEntry2 != null && trackEntry2.IsComplete)
                {
                    trackEntry2.Alpha = 0;
                }
            }
        }

        public void TakeDamage(Immortal.Core.SkillInstance skillInstance, Vector3 impulse = default)
        {
            if (cultivator != null)
            {
                float actualDamage = cultivator.TakeDamage(skillInstance);
                var casterActorBase = skillInstance.caster.actorBase as ActorBase;
                var targetAI = casterActorBase?.GetCombatAI();
                if (targetAI != null)
                {
                    targetAI.AddHatred(this, actualDamage);
                }
                Debug.Log($"{cultivator.name} 受到了 {actualDamage} 点伤害");
                
                UpdateHealthBar();
            }

            // Handle visual feedback
            if (skeleton != null)
            {
                skeleton.AnimationState.SetAnimation(2, "pain", false);
            }
            
            CancelInvoke(nameof(FaceToNormal));
            Invoke(nameof(FaceToNormal), 0.5f);
            
            if (rigidBody != null)
            {
                rigidBody.AddForce(impulse, ForceMode.Impulse);
            }
        }

        public void PlaySpeakAnimation(float duration = 1.0f)
        {
            if (skeleton != null)
            {
                skeleton.AnimationState.SetAnimation(2, "speak", true);
            }
        }

        public void FaceToNormal()
        {
            if (skeleton != null)
            {
                skeleton.AnimationState.SetAnimation(2, "normal", false);
            }
        }

        /// <summary>
        /// 静态工厂方法：通过Prefab异步创建ActorBase对象
        /// </summary>
        public static void CreateFromPrefab(Immortal.Core.Gender gender, System.Action<ActorBase> callback)
        {
            string prefabPath = gender == Immortal.Core.Gender.Female ? "Prefabs/Actors/FemaleActor" : "Prefabs/Actors/MaleActor";
            
            GameObject prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"Failed to load ActorBase prefab at path: {prefabPath}");
                callback?.Invoke(null);
                return;
            }
            
            GameObject instance = Instantiate(prefab);
            ActorBase actorBase = instance.GetComponent<ActorBase>();
            if (actorBase == null)
            {
                Debug.LogError("Prefab does not contain ActorBase component");
                callback?.Invoke(null);
                return;
            }
            
            callback?.Invoke(actorBase);
        }

        /// <summary>
        /// 初始化ActorBase的游戏逻辑对象
        /// </summary>
        public void InitCultivator(Immortal.Core.Cultivator cultivator, bool needAI)
        {
            if (cultivator != null)
            {
                this.cultivator = cultivator;
                cultivator.actorBase = this;
                genderOrientation = cultivator.gender == Immortal.Core.Gender.Female ? -1 : 1;
                FlipSprite();
            }
            
            if (needAI)
            {
                combatAI = new Immortal.Combat.CombatAI(this);
            }

            SetSpeedFactor(0.25f);
            UpdateHealthBar();
        }

        /// <summary>
        /// 设置角色朝向
        /// </summary>
        public void SetOrientation(int orientation)
        {
            this.orientation = orientation > 0 ? 1 : -1;
            FlipSprite();
        }

        /// <summary>
        /// 获取当前朝向
        /// </summary>
        public int GetOrientation()
        {
            return orientation;
        }

        /// <summary>
        /// 创建血条UI
        /// </summary>
        private void CreateHealthBar()
        {
            GameObject healthBarPrefab = Resources.Load<GameObject>("Prefabs/UI/HealthBar");
            if (healthBarPrefab == null)
            {
                Debug.LogError("Failed to load health bar prefab");
                return;
            }
            
            GameObject healthBarInstance = Instantiate(healthBarPrefab, transform);
            healthBar = healthBarInstance.GetComponent<ProgressBarController>();
            healthBarInstance.transform.localPosition = healthBarOffset;
            
            if (healthBar != null)
            {
                healthBar.SetProgress(1f); // 初始化血条进度为100%
            }
            
            UpdateHealthBar();
        }

        /// <summary>
        /// 更新血条显示
        /// </summary>
        public void UpdateHealthBar()
        {
            if (healthBar == null || cultivator == null) return;
            
            float healthPercentage = cultivator.GetHealthPercentage();
            healthBar.SetProgress(healthPercentage);
        }

        /// <summary>
        /// 设置血条可见性
        /// </summary>
        public void SetHealthBarVisible(bool visible)
        {
            if (healthBar != null)
            {
                healthBar.gameObject.SetActive(visible);
            }
        }

        /// <summary>
        /// Cultivator死亡时的回调处理
        /// </summary>
        public void OnCultivatorDeath()
        {
            Debug.Log($"ActorBase: {cultivator?.name} 死亡，开始处理死亡逻辑");
            
            // 1. 播放死亡动画
            StartCoroutine(DeathAnimation());
            
            // 2. 停止所有移动
            if (skeleton != null)
            {
                skeleton.timeScale = 0;
            }
            if (rigidBody != null)
            {
                rigidBody.velocity = Vector3.zero;
            }
            
            // 3. 隐藏血条
            SetHealthBarVisible(false);
            
            // 4. 停用AI
            if (combatAI != null)
            {
                combatAI.DeactivateAI();
            }
            
            // 5. 创建死亡特效
            CreateDeathEffect();
            
            // 6. 延迟清理
            Invoke(nameof(HandleDeathCleanup), 5.0f);
        }

        private IEnumerator DeathAnimation()
        {
            float duration = 0.5f;
            float elapsed = 0f;
            
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Color[] originalColors = new Color[renderers.Length];
            
            // 保存原始颜色
            for (int i = 0; i < renderers.Length; i++)
            {
                originalColors[i] = renderers[i].material.color;
            }
            
            while (elapsed < duration)
            {
                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                
                for (int i = 0; i < renderers.Length; i++)
                {
                    Color color = originalColors[i];
                    color.a = alpha;
                    renderers[i].material.color = color;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // 隐藏角色
            if (skeleton != null)
            {
                skeleton.gameObject.SetActive(false);
            }
        }

        private void CreateDeathEffect()
        {
            GameObject deathEffectPrefab = Resources.Load<GameObject>("Prefabs/Effects/Death");
            if (deathEffectPrefab != null)
            {
                GameObject deathEffect = Instantiate(deathEffectPrefab, transform);
                deathEffect.transform.localPosition = new Vector3(0, 2f, 0);
                
                // 1.5秒后销毁特效
                Destroy(deathEffect, 1.5f);
            }
        }

        /// <summary>
        /// 死亡后的清理处理
        /// </summary>
        private void HandleDeathCleanup()
        {
            Debug.Log($"开始清理死亡角色: {cultivator?.name}");
            
            // 标记为尸体状态
            gameObject.name = $"[CORPSE]{gameObject.name}";
            
            // 发送死亡事件
            SendMessage("OnActorDeath", this, SendMessageOptions.DontRequireReceiver);
        }

        /// <summary>
        /// 复活角色
        /// </summary>
        public void Revive(float healthPercentage = 1.0f)
        {
            if (cultivator == null) return;
            
            Debug.Log($"复活角色: {cultivator.name}");
            
            // 恢复生命值
            cultivator.currentHealth = Mathf.FloorToInt(cultivator.maxHealth * healthPercentage);
            cultivator.isAlive = true;
            
            // 恢复视觉效果
            if (skeleton != null)
            {
                skeleton.gameObject.SetActive(true);
                skeleton.AnimationState.SetAnimation(0, "idle", true);
            }
            
            // 恢复渲染器颜色
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var renderer in renderers)
            {
                Color color = renderer.material.color;
                color.a = 1f;
                renderer.material.color = color;
            }
            
            // 显示血条
            SetHealthBarVisible(true);
            UpdateHealthBar();
            
            // 恢复节点名称
            gameObject.name = gameObject.name.Replace("[CORPSE]", "");
            
            // 发送复活事件
            SendMessage("OnActorRevive", this, SendMessageOptions.DontRequireReceiver);
        }

        /// <summary>
        /// 检查角色是否死亡
        /// </summary>
        public bool IsDead()
        {
            return cultivator != null ? !cultivator.IsAliveCheck() : false;
        }

        /// <summary>
        /// 检查角色是否活着
        /// </summary>
        public bool IsAlive()
        {
            return cultivator != null ? cultivator.IsAliveCheck() : false;
        }

        /// <summary>
        /// 获取角色的生命值百分比
        /// </summary>
        public float GetHealthPercentage()
        {
            return cultivator != null ? cultivator.GetHealthPercentage() : 0f;
        }

        private void LoadAttackPrefab()
        {
            // Unity中预制体加载的处理
            // 这里可以预加载攻击相关的预制体
        }
    }

}