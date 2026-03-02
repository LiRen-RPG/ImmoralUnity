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
            openSpacePrefab = Resources.Load<GameObject>("Prefabs/Scene/Open");
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
            // 镜像 Scene.ts 中的 loadQuest(this.node) 调用
            Immortal.Quest.LoadQuest.Execute(this, gameObject);
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
                    Vector3 size = GetPrefabSize(node);

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
                    Vector3 tempSize = GetPrefabSize(tempNode);
                    
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

        /// <summary>
        /// 从已实例化的 GameObject 读取本地尺寸。
        /// 优先使用 MeshFilter.sharedMesh（本地包围盒，实例化后立即可用），
        /// 其次使用 SpriteRenderer.sprite.bounds，最后退回 Collider.bounds。
        /// </summary>
        /// <summary>
        /// 读取 Tile Prefab 的布局尺寸。
        /// 优先从 LShapeComponent 读取（width=X, depth=Z），这是 Prefab 上
        /// 已有的权威数据，实例化后立即可用，无需任何渲染或物理更新。
        /// 兜底使用根节点 BoxCollider.size（同样在 Inspector 中手填）。
        /// </summary>
        private static Vector3 GetPrefabSize(GameObject go)
        {
            // 1. LShapeComponent：Prefab 自身携带的设计尺寸，最可靠
            LShapeComponent ls = go.GetComponent<LShapeComponent>();
            if (ls != null)
            {
                Vector3 s = go.transform.lossyScale;
                return new Vector3(ls.width * s.x, ls.totalLength * s.y, ls.depth * s.z);
            }

            // 2. 根节点 BoxCollider.size（子节点的 Collider 可能是墙/地等局部碰撞体，不能代表整体）
            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box != null)
            {
                Vector3 s = go.transform.lossyScale;
                return new Vector3(box.size.x * s.x, box.size.y * s.y, box.size.z * s.z);
            }

            return Vector3.one;
        }
    }
}
