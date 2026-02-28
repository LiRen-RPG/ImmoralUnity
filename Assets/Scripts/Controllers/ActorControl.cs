using UnityEngine;
using System.Collections.Generic;
using Immortal.Core;
using Immortal.Item;
using Immortal.UI;

namespace Immortal.Controllers
{
    public class ActorControl : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Scene scene;

        private ActorBase actorBase;
        private HashSet<KeyCode> pressedKeys = new HashSet<KeyCode>();
        private KeyCode lastKey = KeyCode.None;
        private float lastKeyTime = 0f;
        private float mouseUpTime = 0f;

        // 全局cultivators字典（模拟window对象）
        public static Dictionary<int, CultivatorActorPair> Cultivators = new Dictionary<int, CultivatorActorPair>();

        [System.Serializable]
        public class CultivatorActorPair
        {
            public ActorBase actorBase;
            public Cultivator cultivator;
        }

        private Vector3 cameraInitialPosition;
        private Vector3 actorInitialPosition;

        private void Start()
        {
            actorBase = GetComponent<ActorBase>();
            if (playerCamera == null)
                Debug.LogError("[ActorControl] 未找到 Camera，请在场景中添加相机或在 Inspector 中手动赋值。");
            if (scene == null)
                scene = FindObjectOfType<Scene>();

            // 记录相机和角色的初始位置
            if (playerCamera != null)
                cameraInitialPosition = playerCamera.transform.position;
            actorInitialPosition = transform.position;
            
            // 初始化全局cultivators字典
            if (Cultivators == null)
            {
                Cultivators = new Dictionary<int, CultivatorActorPair>();
            }

            // 创建主角Cultivator（速度单位为 Unity 世界坐标单位/秒，原像素值 ÷ 400 换算）
            Cultivator mainCharacter = new Cultivator("主角", FivePhases.Metal, 1000f, 1000f, 10f, 1000f, 1000f, Gender.Male, 0.5f, 20f);
            
            // 将主角添加到cultivators中
            Cultivators[0] = new CultivatorActorPair
            {
                actorBase = this.actorBase,
                cultivator = mainCharacter
            };

            // 初始化ActorBase
            actorBase.InitCultivator(mainCharacter, false);

            // 初始化UI
            UIManager.Instance?.ShowActorInventory(actorBase);
            UIManager.Instance?.GetQuickBarUI()?.BindToInventory(actorBase.GetInventory());

            // 添加示例物品
            AddExampleItems();
            
            // 创建八卦阵盘并添加到背包
            CreateAndAddFormations();

            Debug.Log("主角初始化完成，已添加示例物品和八卦阵盘到背包中");
        }

        private void AddExampleItems()
        {
            // 添加示例物品（这里需要根据实际的Item系统实现）
            var inventory = actorBase.GetInventory();
            
            // 示例：添加一些基础物品
            // InventoryUtils.AddItem(inventory, exampleItems[i], 1);
            // 具体实现取决于Item系统的完整转换
        }

        private void CreateAndAddFormations()
        {
            var inventory = actorBase.GetInventory();
            
            // 创建八卦阵盘（这里需要根据实际的ContainerUtils实现）
            // var customFormation = ContainerUtils.CreateEightTrigramsFormation(ItemRarity.Epic, "玄天八卦阵盘");
            // InventoryUtils.AddItem(inventory, customFormation, 1);
        }

        private void OnDestroy()
        {
            // Unity会自动处理事件清理
            if (actorBase != null)
            {
                Destroy(actorBase.gameObject);
            }
        }

        private void Update()
        {
            HandleInput();
            UpdateCameraFollow();
        }

        private void HandleInput()
        {
            // 处理键盘按下事件
            HandleKeyDown();
            
            // 处理键盘释放事件
            HandleKeyUp();
            
            // 处理鼠标点击事件
            HandleMouseInput();
            
            // 更新移动方向
            UpdateMovementDirection();
        }

        private void HandleKeyDown()
        {
            float currentTime = Time.time * 1000f; // 转换为毫秒
            
            // 检查WASD键的双击
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) || 
                Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S))
            {
                KeyCode currentKey = GetCurrentPressedKey();
                
                if (currentKey == lastKey && currentTime - lastKeyTime < 300f)
                {
                    actorBase.SetSpeedFactor(0.5f); // 快速移动
                }
                
                lastKey = currentKey;
                lastKeyTime = currentTime;
                pressedKeys.Add(currentKey);
            }

            // 跳跃（仅 Space）
            if (Input.GetKeyDown(KeyCode.Space))
            {
                actorBase.Jump();
            }

            // 添加其他按下的键
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(key))
                {
                    pressedKeys.Add(key);
                }
            }
        }

        private void HandleKeyUp()
        {
            // 处理WASD键释放
            if (Input.GetKeyUp(KeyCode.A) || Input.GetKeyUp(KeyCode.D) || 
                Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.S))
            {
                KeyCode releasedKey = GetCurrentReleasedKey();
                pressedKeys.Remove(releasedKey);
                
                // 如果没有移动键被按下，恢复正常速度
                if (!IsAnyMovementKeyPressed())
                {
                    actorBase.SetSpeedFactor(0.25f);
                }
            }

            // 移除其他释放的键
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyUp(key))
                {
                    pressedKeys.Remove(key);
                }
            }
        }

        private void HandleMouseInput()
        {
            // 左键攻击
            if (Input.GetMouseButtonDown(0))
            {
                actorBase.Attack(null);
            }

            if (Input.GetMouseButtonUp(0))
            {
                mouseUpTime = Time.time;
            }
        }

        private KeyCode GetCurrentPressedKey()
        {
            if (Input.GetKeyDown(KeyCode.A)) return KeyCode.A;
            if (Input.GetKeyDown(KeyCode.D)) return KeyCode.D;
            if (Input.GetKeyDown(KeyCode.W)) return KeyCode.W;
            if (Input.GetKeyDown(KeyCode.S)) return KeyCode.S;
            return KeyCode.None;
        }

        private KeyCode GetCurrentReleasedKey()
        {
            if (Input.GetKeyUp(KeyCode.A)) return KeyCode.A;
            if (Input.GetKeyUp(KeyCode.D)) return KeyCode.D;
            if (Input.GetKeyUp(KeyCode.W)) return KeyCode.W;
            if (Input.GetKeyUp(KeyCode.S)) return KeyCode.S;
            return KeyCode.None;
        }

        private bool IsAnyMovementKeyPressed()
        {
            return pressedKeys.Contains(KeyCode.A) || pressedKeys.Contains(KeyCode.D) ||
                   pressedKeys.Contains(KeyCode.W) || pressedKeys.Contains(KeyCode.S);
        }

        private void UpdateMovementDirection()
        {
            // 斜45度横板：A/D 控制 X 轴左右，W/S 控制 Z 轴前后
            float x = 0f;
            float z = 0f;

            if (Input.GetKey(KeyCode.A)) x -= 1f;
            if (Input.GetKey(KeyCode.D)) x += 1f;
            if (Input.GetKey(KeyCode.W)) z += 1f; // W 向屏幕内（远离镜头,左手坐标系）
            if (Input.GetKey(KeyCode.S)) z -= 1f; // S 向屏幕外（靠近镜头）

            if (x != 0f || z != 0f)
            {
                Vector3 dir = new Vector3(x, 0f, z).normalized;
                actorBase.Walk(dir);
            }
            else
            {
                actorBase.Idle(1.0f);
            }
        }

        private void UpdateCameraFollow()
        {
            if (playerCamera == null) return;

            // 仅同步 X 轴，Y/Z 保持初始值
            float actorDeltaX = transform.position.x - actorInitialPosition.x;
            playerCamera.transform.position = new Vector3(
                cameraInitialPosition.x + actorDeltaX,
                cameraInitialPosition.y,
                cameraInitialPosition.z);
        }

        // 获取当前控制的ActorBase
        public ActorBase GetActorBase()
        {
            return actorBase;
        }

        // 获取主角的Cultivator
        public Cultivator GetMainCultivator()
        {
            if (Cultivators.ContainsKey(0))
            {
                return Cultivators[0].cultivator;
            }
            return null;
        }

        // 静态方法：获取指定ID的Cultivator
        public static Cultivator GetCultivator(int id)
        {
            if (Cultivators.ContainsKey(id))
            {
                return Cultivators[id].cultivator;
            }
            return null;
        }

        // 静态方法：添加Cultivator
        public static void AddCultivator(int id, ActorBase actorBase, Cultivator cultivator)
        {
            Cultivators[id] = new CultivatorActorPair
            {
                actorBase = actorBase,
                cultivator = cultivator
            };
        }

        // 静态方法：移除Cultivator
        public static void RemoveCultivator(int id)
        {
            if (Cultivators.ContainsKey(id))
            {
                Cultivators.Remove(id);
            }
        }

        // 设置摄像机引用（用于在Inspector中设置或运行时设置）
        public void SetCamera(Camera camera)
        {
            playerCamera = camera;
        }

        // 设置场景引用
        public void SetScene(Scene sceneRef)
        {
            scene = sceneRef;
        }
    }
}