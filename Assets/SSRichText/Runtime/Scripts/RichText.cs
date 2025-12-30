/*
 * @author SlipperSoar
 * @Created: 2024-12-07
 * @description Unity Text富文本扩展
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ColorUtility = UnityEngine.ColorUtility;

namespace SS.UIComponent
{
    /// <summary>
    /// 对富文本支持更广泛的UI文本组件
    /// </summary>
    [AddComponentMenu("UI/SS/RichText")]
    public class RichText : Text, IPointerClickHandler
    {
        #region inner enum/struct

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
        
        public class DrawingLineRichInfo : RichInfo
        {
            /// <summary> 划线高度 </summary>
            public float LineHeight = 2f;
            /// <summary> 划线的纵向位置 </summary>
            public float YPosFromBase;
            /// <summary>基准位置</summary>
            public LineBasedPos BasedPos;
        }

        #endregion
        
        #region properties

        public event Action<RichType, string> OnClick;
        
        /// <summary>可以直接使用的颜色单词</summary>
        private static Dictionary<string, Color> Colors = new Dictionary<string, Color>()
        {
            { "white", Color.white },
            { "black", Color.black },
            { "red", Color.red },
            { "green", Color.green },
            { "blue", Color.blue },
            { "yellow", Color.yellow },
            { "cyan", Color.cyan },
            { "magenta", Color.magenta },
            { "gray", Color.gray },
            { "grey", Color.grey },
        };

        #region regex

        private static readonly Regex IconRegex = new Regex(@"<icon=([a-zA-Z0-9_\-\(\)\.]+)/>");

        // white space
        private readonly Regex WhiteSpaceRegex = new Regex(@"\s+");
        
        // Unity基础的富文本
        // 斜体
        private const string ItalicRegexText = @"<i>";
        private const string ItalicEndRegexText = @"</i>";
        private static readonly Regex ItalicRegex = new Regex(ItalicRegexText);
        private static readonly Regex ItalicEndRegex = new Regex(ItalicEndRegexText);
        // 粗体
        private const string BoldRegexText = @"<b>";
        private const string BoldEndRegexText = @"</b>";
        private static readonly Regex BoldRegex = new Regex(BoldRegexText);
        private static readonly Regex BoldEndRegex = new Regex(BoldEndRegexText);
        // 颜色
        private static string ColorRegexText => @"<color=((#[0-9a-f]{8})|(#[0-9a-f]{6})|(" + GetColorWords() + @"))>";
        private const string ColorEndRegexText = @"</color>";
        private static readonly Regex ColorRegex = new Regex(ColorRegexText);
        private static readonly Regex ColorEndRegex = new Regex(ColorEndRegexText);
        // 大小
        private const string SizeRegexText = @"<size=(\d+)>";
        private const string SizeEndRegexText = @"</size>";
        private static readonly Regex SizeRegex = new Regex(SizeRegexText);
        private static readonly Regex SizeEndRegex = new Regex(SizeEndRegexText);
        
        // 追加的富文本
        // 描边
        private static readonly string OutlineRegexText = @"<outline=((#[0-9a-f]{8})|(#[0-9a-f]{6})|(" + GetColorWords() + @"))>";
        private const string OutlineEndRegexText = @"</outline>";
        private static readonly Regex OutlineRegex = new Regex(OutlineRegexText, RegexOptions.IgnoreCase);
        private static readonly Regex OutlineEndRegex = new Regex(OutlineEndRegexText);

        // 阴影
        private const string ShadowRegexText = @"<shadow>";
        private const string ShadowEndRegexText = @"</shadow>";
        private static readonly Regex ShadowRegex = new Regex(ShadowRegexText);
        private static readonly Regex ShadowEndRegex = new Regex(ShadowEndRegexText);
        
        // 下划线
        private static readonly string UnderlineRegexText = @"<underline=((#[0-9a-f]{8})|(#[0-9a-f]{6})|(" + GetColorWords() + @"))>";
        private const string UnderlineEndRegexText = @"</underline>";
        private static readonly Regex UnderlineRegex = new Regex(UnderlineRegexText);
        private static readonly Regex UnderlineEndRegex = new Regex(UnderlineEndRegexText);
        
        // 删除线
        private const string StrikeLineRegexText = @"<s>";
        private const string StrikeLineEndRegexText = @"</s>";
        private static readonly Regex StrikeLineRegex = new Regex(StrikeLineRegexText);
        private static readonly Regex StrikeLineEndRegex = new Regex(StrikeLineEndRegexText);
        
        // link
        // private static readonly string LinkRegexText = @"<link=([a-zA-z]+://[^\s]*?)>";
        private static readonly string LinkRegexText = @"<link=([a-zA-Z][a-zA-Z0-9+\-.]*://[^\s>]+)>";
        private const string LinkEndRegexText = @"</link>";
        private static readonly Regex LinkRegex = new Regex(LinkRegexText);
        private static readonly Regex LinkEndRegex = new Regex(LinkEndRegexText);
        
        // GIF动图
        private static readonly Regex GifRegex = new Regex(@"<gif=([a-zA-Z0-9_\-\(\)\.]+)/>");

        #endregion

        // 占位字符，到时候要将alpha设置为0
        const string IconReplaceChar = "\u25a0";//■
        readonly UIVertex[] m_TempVerts = new UIVertex[4];
        private float spaceWidth;

        public static IIconProvider GlobalIconProvider = new IconProvider();
        
        private IIconProvider _iconProvider;

        private IIconProvider iconProvider
        {
            get
            {
                if (_iconProvider == null)
                {
                    _iconProvider = GlobalIconProvider;
                }

                return _iconProvider;
            }
            set => _iconProvider = value;
        }
        
        private RichTextIconImage _iconImage;
        private RichTextIconImage iconImage
        {
            get
            {
                if (_iconImage == null)
                {
                    var image = GetComponentInChildren<RichTextIconImage>();
                    if (image != null)
                    {
                        _iconImage = image;
                    }
                    else
                    {
                        var go = new GameObject("RichText IconImage");
                        go.transform.SetParent(transform, false);
                        var rectTrans = go.AddComponent<RectTransform>();
                        rectTrans.anchorMin = Vector2.zero;
                        rectTrans.anchorMax = Vector2.one;
                        rectTrans.sizeDelta = Vector2.zero;
                        rectTrans.pivot = rectTransform.pivot;
                        _iconImage = go.AddComponent<RichTextIconImage>();
                        _iconImage.raycastTarget = false;
                    }
                }
                
                return _iconImage;
            }
        }

        private static Sprite _whiteQuad;
        private const string UnderlineSpriteName = "<underline>";
        private Vector4[] lineUVs;

        private List<RichInfo> richInfos;

        #endregion

        #region static

#if UNITY_EDITOR
        [MenuItem("GameObject/UI/SS/Rich Text", false, 20)]
        static void CreateRichText()
        {
            // 获取当前选中的 GameObject
            GameObject selectedObject = Selection.activeGameObject;

            if (selectedObject == null)
            {
                Debug.LogError("Please select a UI Canvas or a GameObject with a Canvas component.");
                return;
            }

            // 创建一个新的 GameObject 并命名为 "Rich Text"
            GameObject richTextObject = new GameObject("Rich Text");

            // 将新创建的 GameObject 设置为选中对象的子对象
            richTextObject.transform.SetParent(selectedObject.transform, false);

            // 添加 RectTransform 组件
            var rectTransform = richTextObject.AddComponent<RectTransform>();

            // 添加 RichText 组件
            var text = richTextObject.AddComponent<RichText>();
            text.color = Color.black;

            // 设置 RectTransform 的默认大小和位置
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(200, 50);

            // 选择新创建的 GameObject
            Selection.activeGameObject = richTextObject;
        }
#endif

        #endregion
        
        #region override

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            if (!supportRichText)
            {
                base.OnPopulateMesh(toFill);
                return;
            }
            
            if (font == null)
                return;

            // 文本处理
            richInfos = ProcessRichText(text, out var resultText, out var textWithoutTag);

            // We don't care if we the font Texture changes while we are doing our Update.
            // The end result of cachedTextGenerator will be valid for this instance.
            // Otherwise, we can get issues like Case 619238.
            m_DisableFontTextureRebuiltCallback = true;

            Vector2 extents = rectTransform.rect.size;

            var settings = GetGenerationSettings(extents);
            cachedTextGenerator.PopulateWithErrors(resultText, settings, gameObject);

            // Apply the offset to the vertices
            var verts = cachedTextGenerator.verts;
            var vertCount = verts.Count;
            
            // 处理富文本效果
            // 图标的（下划线也用）
            var iconInfos = new List<RichInfo>();
            var icons = new Dictionary<string, Sprite>();
            var iconVerts = new List<UIVertex[]>();
            // Gif动图的
            var gifs = new Dictionary<RichInfo, UIVertex[]>();
            
            // 图标阴影。这个每个元素只用一次就remove，用LinkedList可以避免数组整体移动，也用不着哈希计算
            var iconShadows = new LinkedList<int>();
            // 划线暂存，含下划线、删除线等
            var drawingLineInfos = new List<RichInfo>();
            // 获取最后一个可显示出来的字符顶点，以免超出显示区域后显示错乱
            verts = ProcessRawTags(verts, resultText, textWithoutTag);
            vertCount = verts.Count;
            foreach (var richInfo in richInfos)
            {
                CalculateRects(richInfo, verts, vertCount);
                switch (richInfo.Type)
                {
                    case RichType.Icon:
                    {
                        var icon = ApplyIcon(richInfo, verts, icons, vertCount);
                        if (icon.icon != null)
                        {
                            iconInfos.Add(richInfo);
                            icons.TryAddToDictionary(richInfo.Content, icon.icon);
                            iconVerts.Add(icon.verts);
                        }
                    }
                        break;
                    case RichType.Gif:
                    {
                        var gifVerts = ApplyGif(richInfo, verts, vertCount);
                        if (gifVerts != null)
                        {
                            gifs.Add(richInfo, gifVerts);
                        }
                    }
                        break;
                    case RichType.Outline:
                        ApplyOutlineEffect(richInfo, verts, vertCount);
                        break;
                    case RichType.Shadow:
                        ApplyShadowEffect(richInfo, verts, vertCount, iconShadows);
                        break;
                    case RichType.Underline:
                    {
                        drawingLineInfos.Add(richInfo);
                    }
                        break;
                    case RichType.StrikeLine:
                    {
                        drawingLineInfos.Add(richInfo);
                    }
                        break;
                }
            }

            // 尝试渲染 划线 含下划线、删除线
            // 当存在图标时，可以通过图标的占位字符拿到铺满颜色的uv区域
            // 当不存在图标时，没有占位字符可以用，就用图标的渲染方式来渲染下划线
            if (lineUVs == null)
            {
                foreach (var richInfo in drawingLineInfos)
                {
                    richInfo.Content = UnderlineSpriteName;
                    // 考虑换行，会是多个矩形
                    var underlineVerts = ApplyDrawingLineEffect(richInfo, verts, vertCount);
                    CheckAndCreateWhiteQuad();
                    foreach (var uVerts in underlineVerts)
                    {
                        iconInfos.Add(richInfo);
                        icons.TryAddToDictionary(richInfo.Content, _whiteQuad);
                        iconVerts.Add(uVerts);
                    }
                }
            }
            else
            {
                foreach (var richInfo in drawingLineInfos)
                {
                    // 考虑换行，会是多个矩形
                    var underlineVerts = ApplyDrawingLineEffect(richInfo, verts, vertCount);
                    foreach (var uVerts in underlineVerts)
                    {
                        for (int i = 0; i < uVerts.Length; i++)
                        {
                            var vert = uVerts[i];
                            vert.uv0 = lineUVs[i];
                            verts.Add(vert);
                        }
                    }
                }
            }
            
            float unitsPerPixel = 1 / pixelsPerUnit;
            vertCount = verts.Count;

            // We have no verts to process just return (case 1037923)
            if (vertCount <= 0)
            {
                toFill.Clear();
                // 清理icon
                // 计算完把划线用的uv清掉
                lineUVs = null;
                // 避免同时更新渲染
                StartCoroutine(CallIconUpdate(iconInfos, icons, iconVerts, iconShadows, gifs));
                return;
            }

            Vector2 roundingOffset = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
            roundingOffset = PixelAdjustPoint(roundingOffset) - roundingOffset;
            toFill.Clear();
            if (roundingOffset != Vector2.zero)
            {
                for (int i = 0; i < vertCount; ++i)
                {
                    int tempVertsIndex = i & 3;
                    m_TempVerts[tempVertsIndex] = verts[i];
                    m_TempVerts[tempVertsIndex].position *= unitsPerPixel;
                    m_TempVerts[tempVertsIndex].position.x += roundingOffset.x;
                    m_TempVerts[tempVertsIndex].position.y += roundingOffset.y;
                    if (tempVertsIndex == 3)
                        toFill.AddUIVertexQuad(m_TempVerts);
                }

                for (int i = 0; i < iconVerts.Count; i++)
                {
                    var tempVerts = iconVerts[i];

                    for (int j = 0; j < 4; j++)
                    {
                        tempVerts[j].position *= unitsPerPixel;
                        tempVerts[j].position.x += roundingOffset.x;
                        tempVerts[j].position.y += roundingOffset.y;
                    }
                }

                foreach (var gif in gifs)
                {
                    var tempVerts = gif.Value;
                    
                    for (int j = 0; j < 4; j++)
                    {
                        tempVerts[j].position *= unitsPerPixel;
                        tempVerts[j].position.x += roundingOffset.x;
                        tempVerts[j].position.y += roundingOffset.y;
                    }
                }
            }
            else
            {
                for (int i = 0; i < vertCount; ++i)
                {
                    int tempVertsIndex = i & 3;
                    m_TempVerts[tempVertsIndex] = verts[i];
                    m_TempVerts[tempVertsIndex].position *= unitsPerPixel;
                    if (tempVertsIndex == 3)
                        toFill.AddUIVertexQuad(m_TempVerts);
                }
                
                for (int i = 0; i < iconVerts.Count; i++)
                {
                    var tempVerts = iconVerts[i];

                    for (int j = 0; j < 4; j++)
                    {
                        tempVerts[j].position *= unitsPerPixel;
                    }
                }
                
                foreach (var gif in gifs)
                {
                    var tempVerts = gif.Value;
                    
                    for (int j = 0; j < 4; j++)
                    {
                        tempVerts[j].position *= unitsPerPixel;
                    }
                }
            }

            // 计算完把下划线用的uv清掉
            lineUVs = null;
            // 避免同时更新渲染
            StartCoroutine(CallIconUpdate(iconInfos, icons, iconVerts, iconShadows, gifs));

            m_DisableFontTextureRebuiltCallback = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position,
                eventData.pressEventCamera, out var lp);
            
            var targets = new List<RichInfo>();
            foreach (var richInfo in richInfos)
            {
                var rects = richInfo.Rects;
                foreach (var rect in rects)
                {
                    if (rect.x <= lp.x && rect.z >= lp.x && rect.y <= lp.y && rect.w >= lp.y)
                    {
                        targets.Add(richInfo);
                        break;
                    }
                }
            }

            if (targets.Count > 0)
            {
                var target = targets.OrderByDescending(t => t.Type).First();
                string message;
                switch (target.Type)
                {
                    case RichType.Link:
                    case RichType.Icon:
                    case RichType.Gif:
                        message = target.Content;
                        break;
                    default:
                        message = target.EffectedStr;
                        break;
                }

                OnClick?.Invoke(target.Type, message);
            }
        }

        #endregion

        #region Public Methods

        public void SetIconProvider(IIconProvider provider)
        {
            iconProvider = provider;
        }

        #endregion
        
        #region Private Methods

        private List<RichInfo> ProcessRichText(string richText, out string resultText, out string textWithoutTag)
        {
            // 通过将富文本内容提取成仅包含size和color的文本来使用Text的生成器生成顶点信息
            resultText = richText;
            var richInfos = new List<RichInfo>();
            var tags = new List<TagInfo>();

            Color GetColor(string colorStr)
            {
                if (!Colors.TryGetValue(colorStr, out var tagColor))
                {
                    ColorUtility.TryParseHtmlString(colorStr, out tagColor);
                }
                return tagColor;
            }
            
            // 图标
            tags.AddRange(GetTags(resultText, IconRegex, RichType.Icon, true, (str, info) =>
            {
                info.Content = str;
                info.Color = Color.white;
            }));
            
            // Gif动图
            tags.AddRange(GetTags(resultText, GifRegex, RichType.Gif, true, (str, info) =>
            {
                info.Content = str;
                info.Color = Color.white;
            }));

            // 描边
            tags.AddRange(GetTags(resultText, OutlineRegex, RichType.Outline,
                paramProcessor: (str, info) => { info.Color = GetColor(str); }));
            tags.AddRange(GetTags(resultText, OutlineEndRegex, RichType.Outline, isCloseTag: true));
            
            // 阴影
            tags.AddRange(GetTags(resultText, ShadowRegex, RichType.Shadow,
                paramProcessor: (str, info) => { info.Color = GetColor(str); }));
            tags.AddRange(GetTags(resultText, ShadowEndRegex, RichType.Shadow, isCloseTag: true));
            
            // 颜色
            tags.AddRange(GetTags(resultText, ColorRegex, RichType.Color,
                paramProcessor: (str, info) => { info.Color = GetColor(str); }));
            tags.AddRange(GetTags(resultText, ColorEndRegex, RichType.Color, isCloseTag: true));
            
            // 大小
            tags.AddRange(GetTags(resultText, SizeRegex, RichType.Size, paramProcessor: (str, info) => { info.Content = str; }));
            tags.AddRange(GetTags(resultText, SizeEndRegex, RichType.Size, isCloseTag: true));
            
            // 斜体
            tags.AddRange(GetTags(resultText, ItalicRegex, RichType.Italic));
            tags.AddRange(GetTags(resultText, ItalicEndRegex, RichType.Italic, isCloseTag: true));
            
            // 粗体
            tags.AddRange(GetTags(resultText, BoldRegex, RichType.Bold));
            tags.AddRange(GetTags(resultText, BoldEndRegex, RichType.Bold, isCloseTag: true));
            
            // 下划线
            tags.AddRange(GetTags(resultText, UnderlineRegex, RichType.Underline,
                paramProcessor: (str, info) => { info.Color = GetColor(str); }));
            tags.AddRange(GetTags(resultText, UnderlineEndRegex, RichType.Underline, isCloseTag: true));
            
            // 删除线
            tags.AddRange(GetTags(resultText, StrikeLineRegex, RichType.StrikeLine));
            tags.AddRange(GetTags(resultText, StrikeLineEndRegex, RichType.StrikeLine, isCloseTag: true));

            // link
            tags.AddRange(GetTags(resultText, LinkRegex, RichType.Link,
                paramProcessor: (str, info) => { info.Content = str; }));
            tags.AddRange(GetTags(resultText, LinkEndRegex, RichType.Link, isCloseTag: true));

            // 空格的位置
            var spaceIndexes = new Queue<int>(GetWhiteSpaceIndexesInString(richText));
            // 按顺序处理索引
            tags.Sort((a, b) => a.Index - b.Index);
            var tagCount = tags.Count;
            var tagStack = new Stack<TagInfo>(tagCount);
            var richInfoStack = new Stack<RichInfo>(tagCount);
            var offset = 0;
            // 子字符串的起止
            var subStrPairs = new List<(int start, int length, bool isRaw)>();
            for (int i = 0; i < tagCount; i++)
            {
                var tag = tags[i];
                // 排除不生成顶点的空格的影响
                if (spaceIndexes.Count > 0)
                {
                    var spaceIndex = spaceIndexes.Peek();
                    while (tag.Index > spaceIndex)
                    {
                        spaceIndexes.Dequeue();
                        offset++;
                        if (spaceIndexes.Count > 0)
                        {
                            spaceIndex = spaceIndexes.Peek();
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                if (tag.IsClose)
                {
                    // 匹配到关闭标签
                    // icon特殊处理
                    if (tag.Type == RichType.Icon)
                    {
                        richInfos.Add(new RichInfo()
                        {
                            Type = RichType.Icon,
                            Content = tag.Content,
                            StartIndex = tag.Index - offset,
                            EndIndex = tag.Index - offset + 1,
                            Color = tag.Color,
                        });

                        // 去掉替换出来的一个字符
                        offset += tag.Length - 1;
                        // subStrPairs.Add((tag.Index, tag.Length));
                        continue;
                    }
                    // Gif同理，特殊处理
                    else if (tag.Type == RichType.Gif)
                    {
                        richInfos.Add(new RichInfo()
                        {
                            Type = RichType.Gif,
                            Content = tag.Content,
                            StartIndex = tag.Index - offset,
                            EndIndex = tag.Index - offset + 1,
                            Color = tag.Color,
                        });
                        
                        offset += tag.Length - 1;
                        continue;
                    }

                    if (tagStack.Count == 0)
                    {
                        continue;
                    }

                    var top = tagStack.Pop();
                    if (top.Type != tag.Type)
                    {
                        // TODO: 匹配失败，抛出异常
                        continue;
                    }
                    // 匹配成功，记录关闭标签的索引
                    var richInfo = richInfoStack.Pop();
                    richInfo.EndIndex = tag.Index - offset;
                    offset += tag.Length;
                    richInfos.Add(richInfo);
                    // 只删掉自定义的标签 = 不动原有标签
                    switch (richInfo.Type)
                    {
                        case RichType.Bold:
                        case RichType.Color:
                        case RichType.Italic:
                        case RichType.Size:
                            subStrPairs.Add((top.Index, top.Length, true));
                            subStrPairs.Add((tag.Index, tag.Length, true));
                            break;
                        default:
                            subStrPairs.Add((top.Index, top.Length, false));
                            subStrPairs.Add((tag.Index, tag.Length, false));
                            break;
                    }
                }
                else
                {
                    tagStack.Push(tag);
                    RichInfo richInfo = null;
                    switch (tag.Type)
                    {
                        case RichType.Underline:
                            richInfo = new DrawingLineRichInfo()
                            {
                                Type = tag.Type,
                                StartIndex = tag.Index - offset,
                                Color = tag.Color,
                                Content = tag.Content,
                                LineHeight = 2f,
                                YPosFromBase = -3f,
                                BasedPos = LineBasedPos.Bottom
                            };
                            break;
                        case RichType.StrikeLine:
                            richInfo = new DrawingLineRichInfo()
                            {
                                Type = tag.Type,
                                StartIndex = tag.Index - offset,
                                Color = color,
                                Content = tag.Content,
                                LineHeight = Mathf.Max(fontSize / 8f, 1.5f),
                                BasedPos = LineBasedPos.Middle
                            };
                            break;
                        default:
                            richInfo = new RichInfo()
                            {
                                Type = tag.Type,
                                StartIndex = tag.Index - offset,
                                Color = tag.Color,
                                Content = tag.Content,
                            };
                            break;
                    }

                    offset += tag.Length;
                    richInfoStack.Push(richInfo);
                }
            }

            // 最后统一替换
            // 为了得到准确的字符串，要有一个去掉所有的富文本标签的文本
            textWithoutTag = string.Empty;
            if (subStrPairs.Count > 0)
            {
                // 从前往后顺序排序
                subStrPairs.Sort((a, b) => a.start - b.start);
                var resultStrBuilder = new StringBuilder();
                var tempStrBuilder = new StringBuilder();
                var index = 0;
                var tempIndex = 0;
                for (var i = 0; i < subStrPairs.Count; i++)
                {
                    var strPair = subStrPairs[i];
                    if (!strPair.isRaw)
                    {
                        resultStrBuilder.Append(resultText.GetSubString(index, strPair.start));
                        index = strPair.start + strPair.length;
                    }
                    
                    tempStrBuilder.Append(resultText.GetSubString(tempIndex, strPair.start));
                    tempIndex = strPair.start + strPair.length;
                }
                
                // 加上最后的字符串
                resultStrBuilder.Append(resultText.GetSubString(index));
                tempStrBuilder.Append(resultText.GetSubString(tempIndex));

                resultText = resultStrBuilder.ToString();
                textWithoutTag = tempStrBuilder.ToString();
            }
            else
            {
                textWithoutTag = resultText;
            }

            // 可以保证有嵌套size的情况下可以生成正确的顶点
            resultText = IconRegex.Replace(resultText, IconReplaceChar);
            resultText = GifRegex.Replace(resultText, IconReplaceChar);

            // temp去掉white space
            textWithoutTag = IconRegex.Replace(textWithoutTag, IconReplaceChar);
            textWithoutTag = GifRegex.Replace(textWithoutTag, IconReplaceChar);
            textWithoutTag = WhiteSpaceRegex.Replace(textWithoutTag, "");

            foreach (var richInfo in richInfos)
            {
                richInfo.EffectedStr = textWithoutTag.GetSubString(richInfo.StartIndex, richInfo.EndIndex);
            }

            return richInfos;
        }

        private List<TagInfo> GetTags(string richText, Regex tagRegex, RichType richType, bool isCloseTag = false, Action<string, TagInfo> paramProcessor = null)
        {
            var tags = new List<TagInfo>();
            var matches = tagRegex.Matches(richText);
            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                var index = match.Index;
                var length = match.Length;
                
                var tagInfo = new TagInfo()
                {
                    Type = richType,
                    Index = index,
                    Length = length,
                    IsClose = isCloseTag,
                };
                
                paramProcessor?.Invoke(match.Groups[1].Value, tagInfo);
                
                tags.Add(tagInfo);
            }

            return tags;
        }
        
        private static List<int> GetWhiteSpaceIndexesInString(string str)
        {
            var result = new List<int>();
            for (int i = 0; i < str.Length; i++)
            {
                if (char.IsWhiteSpace(str[i]))
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private static string GetColorWords()
        {
            return string.Join("|", Colors.Keys);
        }
        
        /// <summary>
        /// 投影效果
        /// </summary>
        /// <param name="iconIndexList">需要应用阴影的Icon（占位）字符的索引列表</param>
        private void ApplyShadowEffect(RichInfo richInfo, IList<UIVertex> verts, int vertCount, LinkedList<int> iconIndexList)
        {
            // 先检查能不能被渲染出来，不能那就算了
            if (richInfo.StartIndex * 4 >= vertCount)
            {
                return;
            }
            
            // 计算图标阴影所处的索引
            var iconIndexes = new Queue<int>();
            for (var i = 0; i < richInfo.EffectedStr.Length; i++)
            {
                var @char = richInfo.EffectedStr[i];
                if (@char == IconReplaceChar[0])
                {
                    iconIndexes.Enqueue(i + richInfo.StartIndex);
                }
            }

            for (var i = richInfo.StartIndex; i < richInfo.EndIndex; i++)
            {
                // 为了检查是否需要渲染，先算start
                var start = i * 4;
                if (start >= vertCount)
                {
                    break;
                }

                // 跳过图标阴影
                if (iconIndexes.Count != 0 && i == iconIndexes.Peek())
                {
                    iconIndexList.AddLast(iconIndexes.Dequeue());
                    continue;
                }

                var end = start + 4;
                UIVertex vt;
                for (var j = start; j < end; j++)
                {
                    vt = verts[j];
                    verts.Add(vt);
                    Vector3 v = vt.position;
                    v.x += -1;
                    v.y += 1;
                    vt.position = v;
                    // 先把本身透明的给改成不透明
                    // 这是为图标准备的，但图标不在这里计算阴影了
                    // if (vt.color.a == 0)
                    // {
                    //     vt.color.a = 255;
                    // }
                    vt.color.a = (byte)(vt.color.a / 2);
                    verts[j] = vt;
                }
            }
        }

        /// <summary>
        /// 描边效果
        /// </summary>
        private void ApplyOutlineEffect(RichInfo richInfo, IList<UIVertex> verts, int vertCount)
        {
            // 先检查能不能被渲染出来，不能那就算了
            if (richInfo.StartIndex * 4 >= vertCount)
            {
                return;
            }

            int start = richInfo.StartIndex * 4;
            int end = Mathf.Min(richInfo.EndIndex * 4, vertCount);
            UIVertex vt;
            for(int x = -1; x <= 1; x += 2)
            {
                for(int y = -1; y <= 1; y += 2)
                {
                    for(int i = start; i < end; i++)
                    {
                        vt = verts[i];
                        Vector3 v = vt.position;
                        v.x += x;
                        v.y += y;
                        var newColor = richInfo.Color;
                        newColor.a = (newColor.a * verts[i].color.a) / 255f;
                        vt.color = newColor;
                        verts.Add(vt);
                    }
                }
            }

            for(int i = start; i < end; i++)
                verts.Add(verts[i]);
        }

        /// <summary>
        /// 图标效果
        /// </summary>
        private (Sprite icon, UIVertex[] verts) ApplyIcon(RichInfo richInfo, IList<UIVertex> verts, Dictionary<string, Sprite> iconCache, int vertCount)
        {
            // 先检查能不能被渲染出来，不能那就算了
            if (richInfo.StartIndex * 4 >= vertCount)
            {
                return (null, null);
            }
            
            var iconName = richInfo.Content;
            Sprite iconSprite;
            if (!iconCache.TryGetValue(iconName, out iconSprite))
            {
                iconSprite = iconProvider.GetIcon(iconName);
            }

            if (lineUVs == null || lineUVs.Length == 0)
            {
                lineUVs = new Vector4[4];
            }
            
            var start = richInfo.StartIndex * 4;
            var end = start + 4;
            var iconVerts = new UIVertex[4];
            // 图标一定是单个字符，这里把uv收缩50%应该会很合适
            // index + 2 一定是对角
            var vert1 = verts[start];
            var vert2 = verts[start + 2];
            var uvCenterX = (vert1.uv0.x + vert2.uv0.x) / 2;
            var uvCenterY = (vert1.uv0.y + vert2.uv0.y) / 2;
            for (int i = start; i < end; i++)
            {
                // 将原字符设置为透明
                var vert = verts[i];
                vert.color.a = 0;
                var uv = vert.uv0;
                uv.x = (uv.x + uvCenterX) / 2;
                uv.y = (uv.y + uvCenterY) / 2;
                vert.uv0 = uv;
                verts[i] = vert;
                lineUVs[i - start] = uv;

                // 整理图标
                var iconVert = vert;
                iconVert.color.a = 255;
                iconVerts[i - start] = iconVert;
            }

            // 找不到图标就不走后续了
            if (iconSprite == null)
            {
                return (null, null);
            }
            
            return (iconSprite, iconVerts);
        }

        /// <summary>
        /// 图标效果
        /// </summary>
        private UIVertex[] ApplyGif(RichInfo richInfo, IList<UIVertex> verts, int vertCount)
        {
            // 先检查能不能被渲染出来，不能那就算了
            if (richInfo.StartIndex * 4 >= vertCount)
            {
                return null;
            }

            if (lineUVs == null || lineUVs.Length == 0)
            {
                lineUVs = new Vector4[4];
            }
            
            var start = richInfo.StartIndex * 4;
            var end = start + 4;
            var gifVerts = new UIVertex[4];
            // 图标一定是单个字符，这里把uv收缩50%应该会很合适
            // index + 2 一定是对角
            var vert1 = verts[start];
            var vert2 = verts[start + 2];
            var uvCenterX = (vert1.uv0.x + vert2.uv0.x) / 2;
            var uvCenterY = (vert1.uv0.y + vert2.uv0.y) / 2;
            for (int i = start; i < end; i++)
            {
                // 将原字符设置为透明
                var vert = verts[i];
                vert.color.a = 0;
                var uv = vert.uv0;
                uv.x = (uv.x + uvCenterX) / 2;
                uv.y = (uv.y + uvCenterY) / 2;
                vert.uv0 = uv;
                verts[i] = vert;
                lineUVs[i - start] = uv;

                // 整理图标
                var iconVert = vert;
                iconVert.color.a = 255;
                gifVerts[i - start] = iconVert;
            }
            
            return gifVerts;
        }
        
        /// <summary>
        /// 下划线效果
        /// </summary>
        private List<UIVertex[]> ApplyDrawingLineEffect(RichInfo richInfo, IList<UIVertex> verts, int vertCount)
        {
            var result = new List<UIVertex[]>();
            // 转换为划线专用的类型
            if (richInfo is not DrawingLineRichInfo lineRichInfo)
            {
                Debug.LogError($"Cant Cast to DrawingLineRichInfo: {richInfo}");
                return result;
            }

            var underlineHeight = lineRichInfo.LineHeight;
            var padding = lineRichInfo.YPosFromBase;

            var count = richInfo.Rects.Count;
            for (int i = 0; i < count; i++)
            {
                var rectIndex = richInfo.RectIndexes[i];
                
                // 顶点顺序是左上顺时针到左下
                int start = rectIndex[0] * 4;
                // 要添加下划线的最后一个字符的右下角顶点索引
                int end = Mathf.Min(rectIndex[1] * 4, vertCount) - 2;
                
                // 划线的四个顶点
                var lineVerts = new UIVertex[4];
                // 起止坐标
                float startX = verts[start].position.x;
                float startY = verts[start].position.y;
                float endX = verts[end].position.x;
                float endY = verts[end].position.y;
                float basedY = 0;
                switch (lineRichInfo.BasedPos)
                {
                    case LineBasedPos.Top:
                        basedY = startY;
                        break;
                    case LineBasedPos.Middle:
                        basedY = (startY + endY) / 2;
                        break;
                    case LineBasedPos.Bottom:
                        basedY = endY;
                        break;
                }
                // 计算下划线的四个顶点
                lineVerts[0] = new UIVertex
                {
                    position = new Vector3(startX, basedY + padding + underlineHeight / 2, 0),
                    color = richInfo.Color, // 划线颜色
                };
                lineVerts[1] = new UIVertex
                {
                    position = new Vector3(endX, basedY + padding + underlineHeight / 2, 0),
                    color = richInfo.Color,
                };
                lineVerts[2] = new UIVertex
                {
                    position = new Vector3(endX, basedY + padding - underlineHeight / 2, 0),
                    color = richInfo.Color,
                };
                lineVerts[3] = new UIVertex
                {
                    position = new Vector3(startX, basedY + padding - underlineHeight / 2, 0),
                    color = richInfo.Color,
                };
                
                result.Add(lineVerts);
            }

            return result;
        }

        /// <summary>
        /// 检查并在需要时生成白色纹理
        /// </summary>
        private void CheckAndCreateWhiteQuad()
        {
            if (_whiteQuad == null)
            {
                var size = 8;
                Texture2D tex = new Texture2D(size, size);
                Color[] colors = new Color[size * size];
                colors.FillArray(Color.white);
                tex.SetPixels(colors);
                tex.Apply();
                _whiteQuad = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

        private static void CalculateRects(RichInfo richInfo, IList<UIVertex> vertices, int vertCount)
        {
            richInfo.Rects = new List<Vector4>();
            richInfo.RectIndexes = new List<int[]>();
            var startIndex = richInfo.StartIndex;
            var endIndex = richInfo.EndIndex;

            Vector4 rect = -Vector4.one;
            int[] indexes = new int[2];
            // 0: 找开始， 1: 找结束
            var state = 0;
            // 顶点是从左上的顺时针
            for (var i = startIndex; i < endIndex; i++)
            {
                // 不显示的部分不处理
                if (i * 4 >= vertCount)
                {
                    // 将最后一个位置作为收尾
                    if (state == 1 && rect.z < 0)
                    {
                        var vert = vertices[vertCount - 3];
                        rect.z = vert.position.x;
                        rect.w = vert.position.y;
                        state = 0;
                        indexes[1] = i;
                        richInfo.Rects.Add(rect);
                        richInfo.RectIndexes.Add(indexes);
                    }
                    break;
                }

                if (state == 0)
                {
                    if (rect.x < 0)
                    {
                        var vert = vertices[i * 4 + 3];
                        rect.x = vert.position.x;
                        rect.y = vert.position.y;
                        state = 1;
                        indexes[0] = i;
                    }
                }
                else
                {
                    // 上一个字符的右下
                    var lastVert = vertices[i * 4 - 2];
                    // 当前字符的右上
                    var currentVert = vertices[i * 4 + 1];
                    // 当前字符比前一个字符要早（都是从左往右），就是换行了
                    if (currentVert.position.x <= lastVert.position.x)
                    {
                        if (rect.z < 0)
                        {
                            var vert = vertices[i * 4 - 3];
                            rect.z = vert.position.x;
                            rect.w = vert.position.y;
                            state = 0;
                            indexes[1] = i;
                            richInfo.Rects.Add(rect);
                            richInfo.RectIndexes.Add(indexes);
                            
                            // 重置一下，继续找下一个
                            rect = -Vector4.one;
                            indexes = new int[2];
                            // 避免换行后的第一个字符没被算入开头，这里计算完上一行的闭口后回退一步
                            i--;
                        }
                    }
                }
            }
            
            // 说明找到最后没闭上，也就是最后一行没算上
            if (state == 1)
            {
                var vert = vertices[endIndex * 4 - 3];
                rect.z = vert.position.x;
                rect.w = vert.position.y;
                indexes[1] = endIndex;
                richInfo.Rects.Add(rect);
                richInfo.RectIndexes.Add(indexes);
            }
        }

        private static IList<UIVertex> ProcessRawTags(IList<UIVertex> vertices, string resultText, string textWithoutTag)
        {
            // 得先确认顶点是否需要修改
            if (vertices.Count == textWithoutTag.Length * 4)
            {
                return vertices;
            }

            // 通过记录的标签信息得到真实的顶点信息
            var newVertices = new List<UIVertex>(vertices.Count);
            for (int i = 0, j = 0; i < resultText.Length; i++)
            {
                var char1 = resultText[i];
                var char2 = textWithoutTag[j];
                if (char1 != char2)
                {
                    continue;
                }
                
                // 在部分行无法显示时，顶点虽然包含标签但不包含不显示的内容，这里要做判断
                if (vertices.Count <= i * 4)
                {
                    break;
                }

                j++;
                newVertices.Add(vertices[i * 4]);
                newVertices.Add(vertices[i * 4 + 1]);
                newVertices.Add(vertices[i * 4 + 2]);
                newVertices.Add(vertices[i * 4 + 3]);
            }

            return newVertices;
        }
        
        #endregion

        #region Coroutine

        private IEnumerator CallIconUpdate(List<RichInfo> iconInfos, Dictionary<string, Sprite> icons, List<UIVertex[]> vertices, LinkedList<int> iconShadows, Dictionary<RichInfo, UIVertex[]> gifs)
        {
            yield return null;
            // 处理图标
            var needShowIcons = iconInfos.Count > 0 || gifs.Count > 0;
            iconImage.gameObject.SetActive(needShowIcons);
            if (needShowIcons)
            {
                iconImage.SetIcons(iconInfos, icons, vertices, iconShadows, gifs);
            }
            else
            {
                iconImage.Clear();
            }
        }

        #endregion
    }
}
