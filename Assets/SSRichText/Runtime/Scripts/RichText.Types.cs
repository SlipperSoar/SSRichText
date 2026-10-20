using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SS.UIComponent
{
    /// <summary>
    /// 标签类型与解析结果的数据结构。
    /// </summary>
    public partial class RichText : Text, IPointerClickHandler
    {

        // 顺序也有排序的用途，主要用于点击时响应的优先级
        public enum RichType
        {
            /// <summary>斜体</summary>
            Italic,

            /// <summary>粗体</summary>
            Bold,

            /// <summary>色彩</summary>
            Color,

            /// <summary>字体大小</summary>
            Size,

            /// <summary>渐变</summary>
            Gradient,

            /// <summary>描边</summary>
            Outline,

            /// <summary>阴影</summary>
            Shadow,

            /// <summary>下划线</summary>
            Underline,

            /// <summary>link</summary>
            Link,

            /// <summary>图标</summary>
            Icon,

            /// <summary>Gif动图</summary>
            Gif,

            /// <summary>删除线</summary>
            StrikeLine,
        }

        /// <summary>
        /// 绘制划线时线的位置基准
        /// </summary>
        public enum LineBasedPos
        {
            Top,
            Middle,
            Bottom,
        }

        private class TagInfo
        {
            public RichType Type;
            public bool IsClose;
            public int Index;
            public int Length;
            public string Content;
            public Color Color;
        }

        public class RichInfo
        {
            public RichType Type;

            /// <summary>富文本开始的位置（包含）</summary>
            public int StartIndex;

            /// <summary>富文本结束的位置（不包含）</summary>
            public int EndIndex;

            /// <summary>根据具体类型决定这个content要怎么使用</summary>
            public string Content;

            /// <summary>顶点颜色</summary>
            public Color Color;

            /// <summary>受影响的文本内容</summary>
            public string EffectedStr;

            /// <summary>
            /// 富文本区域对应的所有矩形
            /// x, y => 左下， z, w => 右上
            /// </summary>
            public List<Vector4> Rects;

            /// <summary>
            /// 富文本区域对应的每个矩形的起止索引
            /// 0 => 开始索引， 1 => 结束索引
            /// </summary>
            public List<int[]> RectIndexes;
        }
        
        public class ShadowRichInfo : RichInfo
        {
            public bool IsXPositive;
            public bool IsYPositive;
        }

        public class DrawingLineRichInfo : RichInfo
        {
            /// <summary> 划线高度 </summary>
            public float LineHeight = 2f;

            /// <summary> 划线的纵向位置 </summary>
            public float YPosFromBase;

            /// <summary>基准位置</summary>
            public LineBasedPos BasedPos;
        }
        
        public class GradientRichInfo : RichInfo
        {
            public Color LeftTopColor;
            public Color RightTopColor;
            public Color LeftBottomColor;
            public Color RightBottomColor;
        }

    }
}
