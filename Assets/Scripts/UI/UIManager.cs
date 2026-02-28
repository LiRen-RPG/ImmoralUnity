using System;
using System.Collections.Generic;
using UnityEngine;
using Immortal.Item;

namespace Immortal.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private InventoryUI inventoryUI;
        [SerializeField] private QuickBarUI quickBarUI;
        [SerializeField] private EightTrigramsFormationUI formationUI;

        [Header("Prefabs")]
        [SerializeField] private GameObject eightTrigramsEffectPrefab;

        [Header("References")]
        [SerializeField] private Canvas sceneCanvas;

        [Header("Settings")]
        [SerializeField] private int inventoryCapacity = 30;

        // 当前显示背包的Actor
        private object currentActor; // ActorBase类型，使用object避免循环依赖
        private static UIManager instance;

        // 单例模式
        public static UIManager Instance => instance;

        private void Awake()
        {
            // 设置单例
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // 初始隐藏背包面板
            if (inventoryUI != null)
            {
                inventoryUI.GetInventoryPanel().OpenPanel();
            }

            // 注册键盘事件监听器
            RegisterKeyboardEvents();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }

            // 移除键盘事件监听器
            UnregisterKeyboardEvents();
        }

        // 显示指定Actor的背包
        public void ShowActorInventory(object actor)
        {
            if (actor == null)
            {
                Debug.LogWarning("Actor不存在");
                return;
            }

            // 通过反射获取Actor的背包（避免循环依赖）
            var getInventoryMethod = actor.GetType().GetMethod("GetInventory");
            if (getInventoryMethod == null)
            {
                Debug.LogWarning("Actor没有GetInventory方法");
                return;
            }

            var inventory = getInventoryMethod.Invoke(actor, null) as Inventory;
            if (inventory == null)
            {
                Debug.LogWarning("Actor的背包不存在");
                return;
            }

            currentActor = actor;

            // 绑定UI到Actor的背包
            if (inventoryUI != null)
            {
                inventoryUI.GetInventoryPanel().BindToInventory(inventory);
            }

            // 绑定快捷栏UI到Actor的背包
            if (quickBarUI != null)
            {
                quickBarUI.BindToInventory(inventory);
            }

            // 显示背包面板
            OpenInventory();

            var actorName = ((MonoBehaviour)actor).gameObject.name;
            Debug.Log($"显示 {actorName} 的背包");
        }

        // 获取当前显示的Actor
        public object GetCurrentActor()
        {
            return currentActor;
        }

        // 获取当前显示的背包
        public Inventory GetCurrentInventory()
        {
            if (currentActor == null) return null;

            var getInventoryMethod = currentActor.GetType().GetMethod("GetInventory");
            return getInventoryMethod?.Invoke(currentActor, null) as Inventory;
        }

        // 切换背包显示
        public void ToggleInventory()
        {
            if (inventoryUI == null) return;

            bool isActive = inventoryUI.gameObject.activeSelf;
            if (!isActive)
            {
                inventoryUI.GetInventoryPanel().OpenPanel();
            }
            else
            {
                inventoryUI.GetInventoryPanel().ClosePanel();
            }
        }

        // 打开背包
        public void OpenInventory()
        {
            if (inventoryUI != null)
            {
                inventoryUI.GetInventoryPanel().OpenPanel();
            }
        }

        // 关闭背包
        public void CloseInventory()
        {
            if (inventoryUI != null)
            {
                inventoryUI.GetInventoryPanel().ClosePanel();
            }
        }

        // 获取快捷栏UI
        public QuickBarUI GetQuickBarUI()
        {
            return quickBarUI;
        }

        // 注册键盘事件
        private void RegisterKeyboardEvents()
        {
            // Unity使用Input系统处理键盘事件，在Update中处理
        }

        // 移除键盘事件监听器
        private void UnregisterKeyboardEvents()
        {
            // Unity中无需手动移除事件监听器
        }

        private void Update()
        {
            HandleKeyboardInput();
        }

        // 处理键盘输入
        private void HandleKeyboardInput()
        {
            var inventory = GetCurrentInventory();
            if (inventory == null) return;

            // 数字键1-9和0对应快捷栏位置0-9
            for (int i = 0; i < 10; i++)
            {
                KeyCode keyCode = i == 9 ? KeyCode.Alpha0 : (KeyCode)((int)KeyCode.Alpha1 + i);
                if (Input.GetKeyDown(keyCode))
                {
                    int slotIndex = i == 9 ? 9 : i; // 0键对应第10个位置（索引9）
                    inventory.UseQuickBarItem(slotIndex);
                }
            }

            // Tab键切换背包显示
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                ToggleInventory();
            }
        }

        /// <summary>
        /// 获取阵盘UI组件
        /// </summary>
        public EightTrigramsFormationUI GetFormationUI()
        {
            return formationUI;
        }

        /// <summary>
        /// 显示阵盘UI
        /// </summary>
        public void ShowFormationUI()
        {
            if (formationUI != null)
            {
                formationUI.gameObject.SetActive(true);
                Debug.Log("阵盘UI已显示");
            }
        }

        /// <summary>
        /// 隐藏阵盘UI
        /// </summary>
        public void HideFormationUI()
        {
            if (formationUI != null)
            {
                formationUI.gameObject.SetActive(false);
                Debug.Log("阵盘UI已隐藏");
            }
        }

        /// <summary>
        /// 切换阵盘UI显示状态
        /// </summary>
        public void ToggleFormationUI()
        {
            if (formationUI != null)
            {
                bool isActive = formationUI.gameObject.activeSelf;
                formationUI.gameObject.SetActive(!isActive);
                Debug.Log($"阵盘UI {(!isActive ? "已显示" : "已隐藏")}");
            }
        }

        /// <summary>
        /// 创建八卦特效
        /// 当阵盘双击时调用此方法
        /// </summary>
        public GameObject CreateEightTrigramsEffect(Vector3? position = null)
        {
            if (eightTrigramsEffectPrefab == null)
            {
                Debug.LogWarning("八卦特效预制体未设置");
                return null;
            }

            // 实例化特效预制体
            GameObject effectObject = Instantiate(eightTrigramsEffectPrefab);

            // 添加到场景根节点
            if (sceneCanvas != null)
            {
                effectObject.transform.SetParent(sceneCanvas.transform);
            }

            // 设置世界坐标位置
            if (position.HasValue)
            {
                effectObject.transform.position = position.Value;
            }
            else
            {
                // 默认位置设为世界坐标原点
                effectObject.transform.position = Vector3.zero;
            }

            string positionText = position.HasValue 
                ? $"世界坐标: ({position.Value.x}, {position.Value.y}, {position.Value.z})" 
                : "默认位置";
            Debug.Log($"八卦特效已创建 {positionText}");

            return effectObject;
        }

        /// <summary>
        /// 在指定位置创建八卦特效
        /// </summary>
        public GameObject CreateEightTrigramsEffectAtPosition(float x, float y, float z = 0f)
        {
            return CreateEightTrigramsEffect(new Vector3(x, y, z));
        }
    }

    // 基础UI面板类（模拟Cocos的行为）
    public abstract class BaseUIPanel : MonoBehaviour
    {
        public virtual void OpenPanel()
        {
            gameObject.SetActive(true);
        }

        public virtual void ClosePanel()
        {
            gameObject.SetActive(false);
        }

        public virtual bool IsOpen()
        {
            return gameObject.activeSelf;
        }
    }

    // 背包UI类（占位符，需要根据实际UI系统实现）
    public class InventoryUI : MonoBehaviour
    {
        private BaseInventoryPanel inventoryPanel;

        private void Awake()
        {
            inventoryPanel = GetComponent<BaseInventoryPanel>();
            if (inventoryPanel == null)
            {
                inventoryPanel = gameObject.AddComponent<BaseInventoryPanel>();
            }
        }

        public BaseInventoryPanel GetInventoryPanel()
        {
            return inventoryPanel;
        }
    }

    // 基础背包面板类（占位符）
    public class BaseInventoryPanel : BaseUIPanel
    {
        protected Inventory boundInventory;

        public virtual void BindToInventory(Inventory inventory)
        {
            boundInventory = inventory;
            // 在这里实现背包UI的数据绑定逻辑
            RefreshUI();
        }

        protected virtual void RefreshUI()
        {
            if (boundInventory == null) return;

            // 刷新UI显示
            // 这里应该实现具体的UI更新逻辑
        }
    }

    // 快捷栏UI类（占位符）
    public class QuickBarUI : MonoBehaviour
    {
        protected Inventory boundInventory;

        public virtual void BindToInventory(Inventory inventory)
        {
            boundInventory = inventory;
            // 在这里实现快捷栏UI的数据绑定逻辑
            RefreshUI();
        }

        protected virtual void RefreshUI()
        {
            if (boundInventory == null) return;

            // 刷新快捷栏UI显示
            // 这里应该实现具体的UI更新逻辑
        }
    }

    /// <summary>
    /// 八卦阵法UI抽象基类，提供卦位节点查询接口
    /// </summary>
    public abstract class BaseEightTrigramsUI : MonoBehaviour
    {
        /// <summary>
        /// 获取指定卦类型对应的 Transform 节点
        /// </summary>
        public abstract Transform GetTrigramNode(Immortal.Controllers.EightTrigramsType trigram);
    }

    // 八卦阵法UI类（占位符）
    public class EightTrigramsFormationUI : BaseEightTrigramsUI
    {
        // 各卦位对应的子节点（在Inspector中拖入）
        [SerializeField] private Transform zhenNode;
        [SerializeField] private Transform xunNode;
        [SerializeField] private Transform liNode;
        [SerializeField] private Transform kunNode;
        [SerializeField] private Transform duiNode;
        [SerializeField] private Transform qianNode;
        [SerializeField] private Transform kanNode;
        [SerializeField] private Transform genNode;

        public override Transform GetTrigramNode(Immortal.Controllers.EightTrigramsType trigram)
        {
            switch (trigram)
            {
                case Immortal.Controllers.EightTrigramsType.Zhen: return zhenNode;
                case Immortal.Controllers.EightTrigramsType.Xun:  return xunNode;
                case Immortal.Controllers.EightTrigramsType.Li:   return liNode;
                case Immortal.Controllers.EightTrigramsType.Kun:  return kunNode;
                case Immortal.Controllers.EightTrigramsType.Dui:  return duiNode;
                case Immortal.Controllers.EightTrigramsType.Qian: return qianNode;
                case Immortal.Controllers.EightTrigramsType.Kan:  return kanNode;
                case Immortal.Controllers.EightTrigramsType.Gen:  return genNode;
                default: return null;
            }
        }
    }
}