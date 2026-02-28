/**
 * @file MapData.cs
 * @description Unity版地图数据结构，用于横版跳台类游戏的地图数据。
 * 
 * 地图数据结构核心：
 * - `areas`: 一个二维数组 (MapArea[][])，代表了游戏的区域布局。每个元素是一个较大的区域（如房间、走廊）。
 * - 行内连接: 数组同一行内的相邻区域，如果其类型不是障碍物，则在逻辑上被视为相连，允许角色在这些区域间水平移动。
 * - 跨行连接: 不同行（代表不同垂直层面）的区域之间的连接，或特殊的区域内连接（如传送门），通过 `MapArea` 对象中的 `connections` 属性显式定义。
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Immortal.MapSystem
{
    /// <summary>
    /// 定义地图区域的类型。每个类型代表一个较大范围的场景元素。
    /// 枚举地图区域/区块的类型。
    /// </summary>
    public enum AreaType
    {
        OPEN_SPACE = 0,     // 开放区域，如室外空地、大型洞穴的开阔部分，角色可自由移动
        CORRIDOR = 1,       // 走廊或狭窄通道
        ROOM = 2            // 房间或相对封闭的空间
        // 可以根据游戏场景的宏观结构添加更多类型，例如：
        // BOSS_ARENA = 3,         // Boss战场区域
        // PUZZLE_ROOM = 4,        // 解谜房间
        // VILLAGE_AREA = 5,       // 村庄区域
        // TRANSITION_POINT = 6    // 用于场景切换或特殊传送的区域
    }

    /// <summary>
    /// 定义地图连接的类型。
    /// </summary>
    public enum ConnectionType
    {
        VERTICAL_SHAFT = 0,                // 标准垂直通道 (如梯子、竖井，连接同一列的上下层)
        CAVE_PASSAGE_SAME_COLUMN = 1,      // 特殊洞穴通道 (连接同一列的上下层)
        TELEPORTER_SPECIFIC_TARGET = 2,    // 传送到指定行列的区域 (需要 targetRow 和 targetCol)
        DOOR_WAY_EAST = 3                  // 向东的门道 (可能连接到相邻列或通过 targetCol 指定列)
        // 可以根据需要添加更多连接类型
    }

    /// <summary>
    /// 定义地图上一个连接点的具体信息。
    /// 描述从一个瓦片到另一个瓦片的特定连接点。
    /// </summary>
    [Serializable]
    public class AreaConnection
    {
        [SerializeField] public int targetRow;              // 目标区域的行索引 (y-coordinate)。目标列默认为当前区域的列。
        [SerializeField] public ConnectionType connectionType;  // 连接的类型。
        [SerializeField] public float targetEntryPointX = 0.5f; // 目标区域的水平入口位置（归一化值，0.0 - 1.0）。例如，0.5代表目标区域的中心。

        public AreaConnection(int targetRow, ConnectionType connectionType, float targetEntryPointX = 0.5f)
        {
            this.targetRow = targetRow;
            this.connectionType = connectionType;
            this.targetEntryPointX = targetEntryPointX;
        }
    }

    /// <summary>
    /// 定义地图上一个区域的数据结构。
    /// 单个地图区域/区块的接口。
    /// </summary>
    [Serializable]
    public class MapArea
    {
        [SerializeField] public AreaType type;          // 区域的基本类型。该类型隐式定义了区域是否可通行。

        /// <summary>
        /// 定义此区域到其他区域的特殊连接，特别是用于连接不同行（层面）的区域。
        /// 字典的键可以是描述连接方向或交互点的字符串 (例如 'ascend_point', 'descend_point', 'north_exit', 'secret_passage_A')。
        /// </summary>
        [SerializeField] public Dictionary<string, AreaConnection> connections;

        public MapArea(AreaType type)
        {
            this.type = type;
            this.connections = new Dictionary<string, AreaConnection>();
        }

        public MapArea(AreaType type, Dictionary<string, AreaConnection> connections)
        {
            this.type = type;
            this.connections = connections ?? new Dictionary<string, AreaConnection>();
        }

        /// <summary>
        /// 添加连接
        /// </summary>
        public void AddConnection(string connectionName, AreaConnection connection)
        {
            if (connections == null)
                connections = new Dictionary<string, AreaConnection>();
            
            connections[connectionName] = connection;
        }

        /// <summary>
        /// 获取连接
        /// </summary>
        public AreaConnection GetConnection(string connectionName)
        {
            if (connections == null || !connections.ContainsKey(connectionName))
                return null;
            
            return connections[connectionName];
        }

        /// <summary>
        /// 检查是否有指定连接
        /// </summary>
        public bool HasConnection(string connectionName)
        {
            return connections != null && connections.ContainsKey(connectionName);
        }
    }

    /// <summary>
    /// 定义整个游戏地图的数据结构。
    /// 整个游戏地图的接口。
    /// </summary>
    [Serializable]
    public class GameMapStructure
    {
        [SerializeField] public int width;              // 地图的宽度 (列数)。
        [SerializeField] public int height;             // 地图的高度 (行数或层数)。
        [SerializeField] public List<List<MapArea>> areas;  // 存储所有地图区域的二维数组。
                                                       // 通过 areas[row][col] 访问特定区域，其中 row 是行索引（层面），col 是列索引（水平位置）。

        // 可选：地图的元数据
        [SerializeField] public string mapName;        // 地图名称。
        [SerializeField] public Vector2Int defaultStartPosition; // 玩家默认出生点。
        [SerializeField] public List<string> backgroundLayers;   // 背景层图像资源键。
        [SerializeField] public string musicTrack;     // 背景音乐资源键。

        public GameMapStructure(int width, int height)
        {
            this.width = width;
            this.height = height;
            this.areas = new List<List<MapArea>>();
            
            // 初始化二维数组
            for (int r = 0; r < height; r++)
            {
                var row = new List<MapArea>();
                for (int c = 0; c < width; c++)
                {
                    row.Add(new MapArea(AreaType.OPEN_SPACE));
                }
                areas.Add(row);
            }

            // 初始化可选属性
            this.backgroundLayers = new List<string>();
            this.defaultStartPosition = new Vector2Int(0, 0);
        }

        /// <summary>
        /// 获取指定位置的区域
        /// </summary>
        public MapArea GetArea(int row, int col)
        {
            if (row < 0 || row >= height || col < 0 || col >= width)
                return null;
            
            return areas[row][col];
        }

        /// <summary>
        /// 设置指定位置的区域
        /// </summary>
        public void SetArea(int row, int col, MapArea area)
        {
            if (row >= 0 && row < height && col >= 0 && col < width)
            {
                areas[row][col] = area;
            }
        }

        /// <summary>
        /// 检查位置是否有效
        /// </summary>
        public bool IsValidPosition(int row, int col)
        {
            return row >= 0 && row < height && col >= 0 && col < width;
        }
    }

    /// <summary>
    /// 随机地图生成器
    /// </summary>
    public static class RandomMapGenerator
    {
        /// <summary>
        /// 随机选择枚举值
        /// </summary>
        public static T RandomEnum<T>() where T : Enum
        {
            Array values = Enum.GetValues(typeof(T));
            return (T)values.GetValue(Random.Range(0, values.Length));
        }

        /// <summary>
        /// 生成随机地图
        /// </summary>
        public static GameMapStructure Generate(int width, int height)
        {
            var gameMap = new GameMapStructure(width, height);
            
            for (int r = 0; r < height; r++)
            {
                // 随机选定本行的room和open_space位置
                int roomIdx = Random.Range(0, width);
                bool hasOpenSpace = Random.Range(0f, 1f) < 0.7f; // 70%概率有open_space
                int openSpaceIdx = -1;
                
                if (hasOpenSpace)
                {
                    do
                    {
                        openSpaceIdx = Random.Range(0, width);
                    } while (openSpaceIdx == roomIdx && width > 1);
                }

                for (int c = 0; c < width; c++)
                {
                    AreaType type;
                    if (c == roomIdx)
                    {
                        type = AreaType.ROOM;
                    }
                    else if (c == openSpaceIdx)
                    {
                        type = AreaType.OPEN_SPACE;
                    }
                    else
                    {
                        type = AreaType.CORRIDOR;
                    }
                    
                    gameMap.SetArea(r, c, new MapArea(type));
                }
            }

            // 只生成上下方向的对称连接
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    // 向上连接
                    if (r > 0 && Random.Range(0f, 1f) < 0.3f)
                    {
                        ConnectionType connectionType = RandomEnum<ConnectionType>();
                        float targetEntryPointX = Random.Range(0f, 1f);
                        
                        var currentArea = gameMap.GetArea(r, c);
                        var targetArea = gameMap.GetArea(r - 1, c);
                        
                        currentArea.AddConnection("up", new AreaConnection(r - 1, connectionType, targetEntryPointX));
                        targetArea.AddConnection("down", new AreaConnection(r, connectionType, Random.Range(0f, 1f)));
                    }
                }
            }
            
            return gameMap;
        }

        /// <summary>
        /// 生成地图并转换为JSON
        /// </summary>
        public static string GenerateJSON(int width, int height)
        {
            var gameMap = Generate(width, height);
            return JsonUtility.ToJson(gameMap, true);
        }

        /// <summary>
        /// 从JSON创建地图
        /// </summary>
        public static GameMapStructure FromJSON(string json)
        {
            return JsonUtility.FromJson<GameMapStructure>(json);
        }
    }

    /// <summary>
    /// 示例地图创建工具
    /// </summary>
    public static class MapDataUtilities
    {
        /// <summary>
        /// 创建一个示例游戏地图。
        /// </summary>
        /// <returns>一个填充了示例数据的游戏地图对象。</returns>
        public static GameMapStructure CreateSampleGameMap()
        {
            const int numRows = 3; // 代表3个垂直层面
            const int numCols = 5; // 代表水平方向上5个大的区域块
            
            var gameMap = new GameMapStructure(numCols, numRows);

            // 2. 定义特定区域类型和连接
            // 层面 0 (最上层)
            gameMap.SetArea(0, 0, new MapArea(AreaType.ROOM));
            
            var area_0_1 = new MapArea(AreaType.ROOM);
            area_0_1.AddConnection("descend_shaft", new AreaConnection(1, ConnectionType.VERTICAL_SHAFT, 0.5f));
            gameMap.SetArea(0, 1, area_0_1);
            
            gameMap.SetArea(0, 2, new MapArea(AreaType.CORRIDOR));
            gameMap.SetArea(0, 3, new MapArea(AreaType.OPEN_SPACE));
            gameMap.SetArea(0, 4, new MapArea(AreaType.OPEN_SPACE));

            // 层面 1 (中间层)
            gameMap.SetArea(1, 0, new MapArea(AreaType.OPEN_SPACE));
            
            var area_1_1 = new MapArea(AreaType.CORRIDOR);
            area_1_1.AddConnection("ascend_shaft", new AreaConnection(0, ConnectionType.VERTICAL_SHAFT, 0.5f));
            area_1_1.AddConnection("descend_passage", new AreaConnection(2, ConnectionType.CAVE_PASSAGE_SAME_COLUMN, 0.3f));
            gameMap.SetArea(1, 1, area_1_1);
            
            gameMap.SetArea(1, 2, new MapArea(AreaType.CORRIDOR));
            gameMap.SetArea(1, 3, new MapArea(AreaType.OPEN_SPACE));
            gameMap.SetArea(1, 4, new MapArea(AreaType.OPEN_SPACE));

            // 层面 2 (最下层)
            gameMap.SetArea(2, 0, new MapArea(AreaType.OPEN_SPACE));
            
            var area_2_1 = new MapArea(AreaType.ROOM);
            area_2_1.AddConnection("ascend_passage", new AreaConnection(1, ConnectionType.CAVE_PASSAGE_SAME_COLUMN, 0.3f));
            gameMap.SetArea(2, 1, area_2_1);
            
            gameMap.SetArea(2, 2, new MapArea(AreaType.OPEN_SPACE));
            gameMap.SetArea(2, 3, new MapArea(AreaType.OPEN_SPACE));
            gameMap.SetArea(2, 4, new MapArea(AreaType.OPEN_SPACE));

            // 设置地图元数据
            gameMap.mapName = "示例地图";
            gameMap.defaultStartPosition = new Vector2Int(0, 0);
            gameMap.backgroundLayers.Add("background_layer_1");
            gameMap.musicTrack = "background_music_track_1";

            return gameMap;
        }
    }
}