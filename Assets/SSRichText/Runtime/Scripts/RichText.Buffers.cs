using System.Collections.Generic;
using UnityEngine;
using RichInfo = SS.UIComponent.RichText.RichInfo;
using ShadowRichInfo = SS.UIComponent.RichText.ShadowRichInfo;

namespace SS.UIComponent
{
    /// <summary>
    /// 一次 OnPopulateMesh 重建所需的全部临时容器。
    ///
    /// 所有权协议（关键）：
    ///   - pending：本次重建正在填充，尚未交给图标层；
    ///   - active ：已经交给 <see cref="RichTextIconImage"/>，它会长期持有
    ///              _sprites / _vertices / _gifs / _iconShadows（GIF 每帧都在读），
    ///              同时 OnPointerClick 也要读这里的 RichInfos；
    ///   - 只有 active 被新的一帧替换之后，旧缓冲才允许回到池中 Reset 复用。
    ///
    /// 这样打字机式的逐帧重建不再每次新建一整套 List/Dictionary/UIVertex[]，
    /// 又不会让图标层或点击检测读到被清空的数据。
    /// </summary>
    internal sealed class RichTextRebuildBuffers
    {
        public readonly List<RichInfo> RichInfos = new List<RichInfo>();
        public readonly List<UIVertex> EffectedVerts = new List<UIVertex>();
        public readonly List<RichInfo> IconInfos = new List<RichInfo>();
        public readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();
        public readonly List<UIVertex[]> IconVerts = new List<UIVertex[]>();
        public readonly Dictionary<RichInfo, UIVertex[]> Gifs = new Dictionary<RichInfo, UIVertex[]>();
        public readonly Dictionary<int, ShadowRichInfo> IconShadows = new Dictionary<int, ShadowRichInfo>();
        public readonly List<RichInfo> DrawingLineInfos = new List<RichInfo>();
        public readonly List<UIVertex> AllVertices = new List<UIVertex>();
        public readonly List<UIVertex> RawTagVerts = new List<UIVertex>();

        private readonly List<UIVertex[]> m_Quads = new List<UIVertex[]>();
        private int m_QuadsUsed;

        /// <summary>
        /// 图标层会长期持有 RentQuad 交出去的 UIVertex[]（GIF 每帧都读）。
        /// 缓冲回池前必须放弃这批数组，否则下次重建复用时会篡改仍被图标层引用的顶点。
        /// 这里不复用 quad 数组，代价仅是每次重建按图标数量分配几个 4 元素小数组。
        /// </summary>
        public void DetachQuads()
        {
            m_Quads.Clear();
            m_QuadsUsed = 0;
        }

        public void Reset()
        {
            RichInfos.Clear();
            EffectedVerts.Clear();
            IconInfos.Clear();
            Icons.Clear();
            IconVerts.Clear();
            Gifs.Clear();
            IconShadows.Clear();
            DrawingLineInfos.Clear();
            AllVertices.Clear();
            RawTagVerts.Clear();
            m_QuadsUsed = 0;
        }

        /// <summary>
        /// 每次 OnPopulateMesh 开头调用：只清空本次重建要写入的容器，
        /// RichInfos 里的对象保留（由 CloneRichInfos 按索引复用）。
        /// </summary>
        public void ResetForRebuild()
        {
            EffectedVerts.Clear();
            IconInfos.Clear();
            Icons.Clear();
            IconVerts.Clear();
            Gifs.Clear();
            IconShadows.Clear();
            DrawingLineInfos.Clear();
            AllVertices.Clear();
            RawTagVerts.Clear();
            m_QuadsUsed = 0;
        }

        /// <summary>取一个四顶点数组；调用方会写满 4 个元素，无需清零。</summary>
        public UIVertex[] RentQuad()
        {
            if (m_QuadsUsed < m_Quads.Count)
                return m_Quads[m_QuadsUsed++];

            var quad = new UIVertex[4];
            m_Quads.Add(quad);
            m_QuadsUsed++;
            return quad;
        }
    }
}
