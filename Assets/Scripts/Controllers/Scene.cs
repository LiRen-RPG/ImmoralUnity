using UnityEngine;
using System.Collections.Generic;
using Immortal.MapSystem;

namespace Immortal.Controllers
{
    public class Scene : MonoBehaviour
    {
        [SerializeField] private GameObject roomPrefab;
        [SerializeField] private GameObject openSpacePrefab;
        [SerializeField] private GameObject corridorPrefab;
        [SerializeField] private GameObject pillarNearPrefab;
        [SerializeField] private GameObject pillarFarPrefab;

        // 地图数据
        private GameMapStructure mapData;
        private float mapWidth = 0f;
        private Immortal.Combat.CombatManager combatManager;

        private void Start()
        {
            // 初始化CombatManager
            combatManager = FindObjectOfType<Immortal.Combat.CombatManager>();
            if (combatManager == null)
            {
                // 如果场景中没有CombatManager，创建一个
                GameObject combatManagerObj = new GameObject("CombatManager");
                combatManager = combatManagerObj.AddComponent<Immortal.Combat.CombatManager>();
            }
            combatManager.Init(this);

            // 加载预制体
            LoadPrefabs();
        }

        private void LoadPrefabs()
        {
            // Unity中直接通过Resources加载预制体
            roomPrefab = Resources.Load<GameObject>("Prefabs/Scene/Room");
            openSpacePrefab = Resources.Load<GameObject>("Prefabs/Scene/OpenSpace");
            corridorPrefab = Resources.Load<GameObject>("Prefabs/Scene/Corridor");
            pillarNearPrefab = Resources.Load<GameObject>("Prefabs/Scene/PillarNear");
            pillarFarPrefab = Resources.Load<GameObject>("Prefabs/Scene/PillarFar");

            // 检查是否所有预制体都加载成功
            if (roomPrefab == null || openSpacePrefab == null || corridorPrefab == null || 
                pillarNearPrefab == null || pillarFarPrefab == null)
            {
                Debug.LogError("Failed to load one or more scene prefabs!");
                return;
            }

            // 生成地图数据
            mapData = RandomMapGenerator.Generate(3, 3);
            LoadMapRow(0);
            
            // 加载任务系统
            LoadQuest();
            
            // 预加载死亡特效
            Resources.Load<GameObject>("Prefabs/Effects/Death");
        }

        private void LoadQuest()
        {
            // Unity中加载任务系统的逻辑
            // 这里可以调用Quest系统的初始化方法
            var questManager = FindObjectOfType<Immortal.Quest.QuestManager>();
            if (questManager != null)
            {
                // questManager.LoadQuests(gameObject);
            }
        }

        public void LoadMapRow(int rowIdx)
        {
            if (mapData == null || mapData.areas == null) return;
            
            if (rowIdx < 0 || rowIdx >= mapData.areas.Count) return;
            
            var row = mapData.areas[rowIdx];
            if (row == null) return;

            AreaType? lastType = null;
            GameObject lastNode = null;

            for (int col = 0; col < row.Count; col++)
            {
                GameObject prefab = null;
                
                switch (row[col].type)
                {
                    case AreaType.ROOM:
                        prefab = roomPrefab;
                        break;
                    case AreaType.OPEN_SPACE:
                        prefab = openSpacePrefab;
                        break;
                    case AreaType.CORRIDOR:
                    default:
                        prefab = corridorPrefab;
                        break;
                }

                if (prefab != null)
                {
                    GameObject node = Instantiate(prefab, transform);
                    
                    // 获取节点大小（Unity中通过Renderer或Collider获取）
                    Renderer nodeRenderer = node.GetComponent<Renderer>();
                    Vector3 size = Vector3.one;
                    if (nodeRenderer != null)
                    {
                        size = nodeRenderer.bounds.size;
                    }
                    else
                    {
                        // 如果没有Renderer，尝试从Collider获取
                        Collider nodeCollider = node.GetComponent<Collider>();
                        if (nodeCollider != null)
                        {
                            size = nodeCollider.bounds.size;
                        }
                    }

                    // 设置节点位置
                    Vector3 nodePosition = node.transform.position;
                    nodePosition.x = col * size.x - (row.Count - 1) * size.x / 2f;
                    nodePosition.z = nodePosition.z; // 保持Z坐标
                    node.transform.position = nodePosition;

                    // 如果当前区域和前一个区域类型不同，添加柱子
                    if (lastType.HasValue && lastType.Value != row[col].type)
                    {
                        // pillarNear放在当前节点左侧
                        GameObject pillarNear = Instantiate(pillarNearPrefab, transform);
                        Vector3 pillarNearPos = pillarNear.transform.position;
                        pillarNearPos.x = nodePosition.x - size.x / 2f;
                        pillarNear.transform.position = pillarNearPos;

                        // pillarFar放在同一位置（深度不同）
                        GameObject pillarFar = Instantiate(pillarFarPrefab, transform);
                        Vector3 pillarFarPos = pillarFar.transform.position;
                        pillarFarPos.x = pillarNearPos.x;
                        pillarFar.transform.position = pillarFarPos;
                    }

                    lastType = row[col].type;
                    lastNode = node;
                }
            }

            // 计算地图总宽度
            if (row != null && row.Count > 0)
            {
                GameObject tempPrefab = roomPrefab ?? openSpacePrefab ?? corridorPrefab;
                if (tempPrefab != null)
                {
                    // 创建临时节点来获取尺寸
                    GameObject tempNode = Instantiate(tempPrefab);
                    
                    Renderer tempRenderer = tempNode.GetComponent<Renderer>();
                    Vector3 tempSize = Vector3.one;
                    if (tempRenderer != null)
                    {
                        tempSize = tempRenderer.bounds.size;
                    }
                    
                    mapWidth = row.Count * tempSize.x;
                    
                    // 销毁临时节点
                    Destroy(tempNode);
                }
            }
        }

        private void Update()
        {
            if (combatManager != null)
            {
                combatManager.ManualUpdate(Time.deltaTime);
            }
        }

        // 获取地图宽度
        public float GetMapWidth()
        {
            return mapWidth;
        }

        // 获取地图数据
        public GameMapStructure GetMapData()
        {
            return mapData;
        }

        // 设置地图数据（用于外部设置自定义地图）
        public void SetMapData(GameMapStructure newMapData)
        {
            mapData = newMapData;
        }

        // 重新加载整个地图
        public void ReloadMap()
        {
            // 清除现有的地图节点
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }

            // 重新生成地图
            if (mapData != null)
            {
                for (int i = 0; i < mapData.areas.Count; i++)
                {
                    LoadMapRow(i);
                }
            }
        }

        // 在运行时更换地图
        public void ChangeMap(int width, int height)
        {
            // 生成新的地图数据
            mapData = RandomMapGenerator.Generate(width, height);
            ReloadMap();
        }
    }
}