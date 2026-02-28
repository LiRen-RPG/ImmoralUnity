using UnityEngine;

namespace Immortal.Utils
{
    /// <summary>
    /// L型Mesh生成工具：两个互相垂直的平面，共享一条边
    ///
    ///       竖直面（墙面，XY平面，z=0）
    ///       |\
    ///       | \
    ///       |  \ ← 共享边（X轴方向）
    ///       |___\__________
    ///            水平面（地面，XZ平面，y=0）
    ///
    ///   width  : 两个平面的宽度（X轴）
    ///   hDepth : 水平面的纵深（Z轴）
    ///   vHeight: 竖直面的高度（Y轴）
    /// </summary>
    public static class LShapeMeshGenerator
    {
        /// <summary>
        /// 生成L型Mesh（双面可见）
        /// </summary>
        /// <param name="width">两平面共享的宽度（X轴，单位：米）</param>
        /// <param name="hDepth">水平面纵深（Z轴，单位：米）</param>
        /// <param name="vHeight">竖直面高度（Y轴，单位：米）</param>
        public static Mesh Generate(
            float width   = 2f,
            float hDepth  = 1.5f,
            float vHeight = 2f)
        {
            width   = Mathf.Max(width,   0.01f);
            hDepth  = Mathf.Max(hDepth,  0.01f);
            vHeight = Mathf.Max(vHeight, 0.01f);

            float halfW = width * 0.5f;

            // ── 水平面（y=0，XZ平面，向 -Z 延伸）
            Vector3 h0 = new Vector3(-halfW, 0,        0); // 前左（共享边起点）
            Vector3 h1 = new Vector3( halfW, 0,        0); // 前右（共享边终点）
            Vector3 h2 = new Vector3( halfW, 0, -hDepth); // 后右
            Vector3 h3 = new Vector3(-halfW, 0, -hDepth); // 后左

            // ── 竖直面（z=0，XY平面，法线朝 -Z）
            Vector3 v0 = new Vector3(-halfW, 0,       0); // 下左（共享边起点）
            Vector3 v1 = new Vector3( halfW, 0,       0); // 下右（共享边终点）
            Vector3 v2 = new Vector3( halfW, vHeight, 0); // 上右
            Vector3 v3 = new Vector3(-halfW, vHeight, 0); // 上左

            // 顶点排列：[水平上面0-3][竖直前面4-7]
            Vector3[] verts = new Vector3[]
            {
                h0, h1, h2, h3,  // 水平上面（法线+Y）
                v0, v1, v2, v3,  // 竖直面（法线-Z）
            };

            float totalLen = hDepth + vHeight;
            float sharedV  = hDepth / totalLen;

            Vector2[] uvs = new Vector2[]
            {
                // 水平上面 (h0,h1,h2,h3)
                new Vector2(0, sharedV), new Vector2(1, sharedV), // h0(共享边左), h1(共享边右)
                new Vector2(1, 0),       new Vector2(0, 0),       // h2(外端右),   h3(外端左)
                // 竖直面 (v0,v1,v2,v3)
                new Vector2(0, sharedV), new Vector2(1, sharedV), // v0(底左), v1(底右)
                new Vector2(1, 1),       new Vector2(0, 1),       // v2(顶右), v3(顶左)
            };

            int[] trisH = { 0, 1, 2,  0, 2, 3 };        // 水平面 sub-mesh 0
            int[] trisV = { 4, 6, 5,  4, 7, 6 };        // 竖直面 sub-mesh 1

            Mesh mesh = new Mesh();
            mesh.name        = "LShape";
            mesh.vertices    = verts;
            mesh.uv          = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(trisH, 0);
            mesh.SetTriangles(trisV, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 创建一个带有L型Mesh的GameObject（自动附加MeshFilter / MeshRenderer）
        /// </summary>
        public static GameObject CreateGameObject(
            string name    = "LShape",
            float width    = 2f,
            float hDepth   = 1.5f,
            float vHeight  = 2f,
            Material material = null)
        {
            GameObject go = new GameObject(name);

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = Generate(width, hDepth, vHeight);

            var mr = go.AddComponent<MeshRenderer>();
            mr.material = material != null
                ? material
                : new Material(Shader.Find("Standard"));

            return go;
        }
    }
}
