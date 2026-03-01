using UnityEngine;

namespace Immortal.UI
{
    /// <summary>
    /// 八卦阵法 UI 抽象基类，提供卦位节点查询接口。
    /// Formation3D 通过此接口获取各卦位的 Transform。
    /// </summary>
    public abstract class BaseEightTrigramsUI : MonoBehaviour
    {
        /// <summary>获取指定卦类型对应的 Transform 节点</summary>
        public abstract Transform GetTrigramNode(Immortal.Controllers.EightTrigramsType trigram);
    }
}
