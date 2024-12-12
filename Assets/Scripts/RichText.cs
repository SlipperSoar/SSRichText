/*
 * @author SlipperSoar
 * @Created: 2024-12-07
 * @description Unity Text富文本扩展
 */

using System;
using System.Collections;
using System.Collections.Generic;
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
            /// <summary>图标</summary>
            Icon,
            /// <summary>描边</summary>
            Outline,
            /// <summary>阴影</summary>
            Shadow,
            /// <summary>下划线</summary>
            Underline,
            /// <summary>link</summary>
            Link,
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
        private readonly Regex ItalicRegex = new Regex(ItalicRegexText);
        private readonly Regex ItalicEndRegex = new Regex(ItalicEndRegexText);
        // 粗体
        private const string BoldRegexText = @"<b>";
        private const string BoldEndRegexText = @"</b>";
        private readonly Regex BoldRegex = new Regex(BoldRegexText);
        private readonly Regex BoldEndRegex = new Regex(BoldEndRegexText);
        // 颜色
        private static string ColorRegexText => @"<color=((#[0-9a-f]{8})|(#[0-9a-f]{6})|(" + GetColorWords() + @"))>";
        private const string ColorEndRegexText = @"</color>";
        private readonly Regex ColorRegex = new Regex(ColorRegexText);
        private readonly Regex ColorEndRegex = new Regex(ColorEndRegexText);
        // 大小
        private const string SizeRegexText = @"<size=(\d+)>";
        private const string SizeEndRegexText = @"</size>";
        private readonly Regex SizeRegex = new Regex(SizeRegexText);
        private readonly Regex SizeEndRegex = new Regex(SizeEndRegexText);
        
        // 追加的富文本
        // 描边
        private static readonly string OutlineRegexText = @"<outline=((#[0-9a-f]{8})|(#[0-9a-f]{6})|(" + GetColorWords() + @"))>";
        private const string OutlineEndRegexText = @"</outline>";
        private readonly Regex OutlineRegex = new Regex(OutlineRegexText, RegexOptions.IgnoreCase);
        private readonly Regex OutlineEndRegex = new Regex(OutlineEndRegexText);

        // 阴影
        private const string ShadowRegexText = @"<shadow>";
        private const string ShadowEndRegexText = @"</shadow>";
        private readonly Regex ShadowRegex = new Regex(ShadowRegexText);
        private readonly Regex ShadowEndRegex = new Regex(ShadowEndRegexText);
        
        // 下划线
        private static readonly string UnderlineRegexText = @"<underline=((#[0-9a-f]{8})|(#[0-9a-f]{6})|(" + GetColorWords() + @"))>";
        private const string UnderlineEndRegexText = @"</underline>";
        private readonly Regex UnderlineRegex = new Regex(UnderlineRegexText);
        private readonly Regex UnderlineEndRegex = new Regex(UnderlineEndRegexText);
        
        // link（用下划线实现）
        // private static readonly string LinkRegexText = @"<link=([a-zA-z]+://[^\s]*?)>";
        private static readonly string LinkRegexText = @"<link=([a-zA-Z][a-zA-Z0-9+\-.]*://[^\s>]+)>";
        private const string LinkEndRegexText = @"</link>";
        private readonly Regex LinkRegex = new Regex(LinkRegexText);
        private readonly Regex LinkEndRegex = new Regex(LinkEndRegexText);

        #endregion

        readonly UIVertex[] m_TempVerts = new UIVertex[4];
        private float spaceWidth;

        private IIconProvider _iconProvider;

        private IIconProvider iconProvider
        {
            get
            {
                if (_iconProvider == null)
                {
                    _iconProvider = new IconProvider();
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

        private Sprite _whiteQuad;
        private const string UnderlineSpriteName = "<underline>";

        private List<RichInfo> richInfos;

        #endregion

        #region static

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

        #endregion
        
        #region override

        protected override void OnValidate()
        {
            base.OnValidate();

            iconProvider = new IconProvider();
        }

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
            richInfos = ProcessRichText(text, out var resultText);

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
            foreach (var richInfo in richInfos)
            {
                CalculateRects(richInfo, verts);
                switch (richInfo.Type)
                {
                    case RichType.Icon:
                    {
                        var icon = ApplyIcon(richInfo, verts, icons);
                        if (icon.icon != null)
                        {
                            iconInfos.Add(richInfo);
                            icons.TryAdd(richInfo.Content, icon.icon);
                            iconVerts.Add(icon.verts);
                        }
                    }
                        break;
                    case RichType.Outline:
                        ApplyOutlineEffect(richInfo, verts, vertCount);
                        break;
                    case RichType.Shadow:
                        ApplyShadowEffect(richInfo, verts, vertCount);
                        break;
                    // case RichType.Link:
                    case RichType.Underline:
                    {
                        richInfo.Content = UnderlineSpriteName;
                        // 考虑换行，会是多个矩形
                        var underlineVerts = ApplyUnderlineEffect(richInfo, verts, vertCount);
                        CheckAndCreateWhiteQuad();
                        foreach (var uVerts in underlineVerts)
                        {
                            iconInfos.Add(richInfo);
                            icons.TryAdd(richInfo.Content, _whiteQuad);
                            iconVerts.Add(uVerts);
                        }
                    }
                        break;
                }
            }
            
            float unitsPerPixel = 1 / pixelsPerUnit;
            vertCount = verts.Count;

            // We have no verts to process just return (case 1037923)
            if (vertCount <= 0)
            {
                toFill.Clear();
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
            }

            // 避免同时更新渲染
            StartCoroutine(CallIconUpdate(iconInfos, icons, iconVerts));

            m_DisableFontTextureRebuiltCallback = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position,
                eventData.pressEventCamera, out var lp);
            
            RichInfo target = null;
            foreach (var richInfo in richInfos)
            {
                var rects = richInfo.Rects;
                foreach (var rect in rects)
                {
                    if (rect.x <= lp.x && rect.z >= lp.x && rect.y <= lp.y && rect.w >= lp.y)
                    {
                        target = richInfo;
                        break;
                    }
                }

                if (target != null)
                {
                    break;
                }
            }

            if (target != null)
            {
                var message = string.Empty;
                switch (target.Type)
                {
                    case RichType.Link:
                    case RichType.Icon:
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

        private List<RichInfo> ProcessRichText(string richText, out string resultText)
        {
            // 通过将富文本内容提取成仅包含size和color的文本来使用Text的生成器生成顶点信息
            resultText = richText;
            var richInfos = new List<RichInfo>();
            var tags = new List<TagInfo>();
            
            // 图标
            var iconMatches = IconRegex.Matches(resultText);
            for (int i = 0; i < iconMatches.Count; i++)
            {
                var match = iconMatches[i];
                var length = match.Length;
                // 图标对应的索引
                var index = match.Index;
                var iconName = match.Groups[1].Value;
                var tagInfo = new TagInfo()
                {
                    Type = RichType.Icon,
                    IsClose = true,
                    Index = index,
                    Length = length,
                    Content = iconName,
                    Color = Color.white,
                };
                
                tags.Add(tagInfo);
            }

            // 描边
            var outlineMatches = OutlineRegex.Matches(resultText);
            var outlineEndMatches = OutlineEndRegex.Matches(resultText);
            for (int i = 0; i < outlineMatches.Count; i++)
            {
                var startMatch = outlineMatches[i];
                var startIndex = startMatch.Index;
                var startLength = startMatch.Length;

                var colorStr = startMatch.Groups[1].Value;
                Color color;
                if (!Colors.TryGetValue(colorStr, out color))
                {
                    ColorUtility.TryParseHtmlString(colorStr, out color);
                }
                
                var tagInfo = new TagInfo()
                {
                    Type = RichType.Outline,
                    Index = startIndex,
                    Length = startLength,
                    Color = color,
                };
                
                tags.Add(tagInfo);
            }

            for (int i = 0; i < outlineEndMatches.Count; i++)
            {
                var endMatch = outlineEndMatches[i];
                var endIndex = endMatch.Index;
                var endLength = endMatch.Length;
                
                var tagInfo = new TagInfo()
                {
                    Type = RichType.Outline,
                    IsClose = true,
                    Index = endIndex,
                    Length = endLength,
                };

                tags.Add(tagInfo);
            }
            
            // 阴影
            var shadowMatches = ShadowRegex.Matches(resultText);
            var shadowEndMatches = ShadowEndRegex.Matches(resultText);
            for (int i = 0; i < shadowMatches.Count; i++)
            {
                var startMatch = shadowMatches[i];
                var startIndex = startMatch.Index;
                var startLength = startMatch.Length;

                var tagInfo = new TagInfo()
                {
                    Type = RichType.Shadow,
                    Index = startIndex,
                    Length = startLength,
                };
                
                tags.Add(tagInfo);
            }

            for (int i = 0; i < shadowEndMatches.Count; i++)
            {
                var endMatch = shadowEndMatches[i];
                var endIndex = endMatch.Index;
                var endLength = endMatch.Length;

                var tagInfo = new TagInfo()
                {
                    Type = RichType.Shadow,
                    IsClose = true,
                    Index = endIndex,
                    Length = endLength,
                };

                tags.Add(tagInfo);
            }
            
            // 颜色
            var colorMatches = ColorRegex.Matches(resultText);
            var colorEndMatches = ColorEndRegex.Matches(resultText);
            for (int i = 0; i < colorMatches.Count; i++)
            {
                var startMatch = colorMatches[i];
                var startIndex = startMatch.Index;
                var startLength = startMatch.Length;

                var tagInfo = new TagInfo()
                {
                    Type = RichType.Color,
                    Index = startIndex,
                    Length = startLength,
                };
                
                tags.Add(tagInfo);
            }

            for (int i = 0; i < colorEndMatches.Count; i++)
            {
                var endMatch = colorEndMatches[i];
                var endIndex = endMatch.Index;
                var endLength = endMatch.Length;
                
                var tagInfo = new TagInfo()
                {
                    Type = RichType.Color,
                    IsClose = true,
                    Index = endIndex,
                    Length = endLength,
                };

                tags.Add(tagInfo);
            }
            
            // 大小
            var sizeMatches = SizeRegex.Matches(resultText);
            var sizeEndMatches = SizeEndRegex.Matches(resultText);
            for (int i = 0; i < sizeMatches.Count; i++)
            {
                var startMatch = sizeMatches[i];
                var startIndex = startMatch.Index;
                var startLength = startMatch.Length;

                var tagInfo = new TagInfo()
                {
                    Type = RichType.Size,
                    Index = startIndex,
                    Length = startLength,
                };
                
                tags.Add(tagInfo);
            }

            for (int i = 0; i < sizeEndMatches.Count; i++)
            {
                var endMatch = sizeEndMatches[i];
                var endIndex = endMatch.Index;
                var endLength = endMatch.Length;
                
                var tagInfo = new TagInfo()
                {
                    Type = RichType.Size,
                    IsClose = true,
                    Index = endIndex,
                    Length = endLength,
                };

                tags.Add(tagInfo);
            }
            
            // 斜体
            var italicMatches = ItalicRegex.Matches(resultText);
            var italicEndMatches = ItalicEndRegex.Matches(resultText);
            for (int i = 0; i < italicMatches.Count; i++)
            {
                var startMatch = italicMatches[i];
                var startIndex = startMatch.Index;
                var startLength = startMatch.Length;

                var tagInfo = new TagInfo()
                {
                    Type = RichType.Italic,
                    Index = startIndex,
                    Length = startLength,
                };
                
                tags.Add(tagInfo);
            }

            for (int i = 0; i < italicEndMatches.Count; i++)
            {
                var endMatch = italicEndMatches[i];
                var endIndex = endMatch.Index;
                var endLength = endMatch.Length;
                
                var tagInfo = new TagInfo()
                {
                    Type = RichType.Italic,
                    IsClose = true,
                    Index = endIndex,
                    Length = endLength,
                };

                tags.Add(tagInfo);
            }
            
            // 粗体
            var boldMatches = BoldRegex.Matches(resultText);
            var boldEndMatches = BoldEndRegex.Matches(resultText);
            for (int i = 0; i < boldMatches.Count; i++)
            {
                var startMatch = boldMatches[i];
                var startIndex = startMatch.Index;
                var startLength = startMatch.Length;

                var tagInfo = new TagInfo()
                {
                    Type = RichType.Bold,
                    Index = startIndex,
                    Length = startLength,
                };
                
                tags.Add(tagInfo);
            }

            for (int i = 0; i < boldEndMatches.Count; i++)
            {
                var endMatch = boldEndMatches[i];
                var endIndex = endMatch.Index;
                var endLength = endMatch.Length;
                
                var tagInfo = new TagInfo()
                {
                    Type = RichType.Bold,
                    IsClose = true,
                    Index = endIndex,
                    Length = endLength,
                };

                tags.Add(tagInfo);
            }
            
            // 下划线
            var underlineMatches = UnderlineRegex.Matches(resultText);
            var underlineEndMatches = UnderlineEndRegex.Matches(resultText);
            for (int i = 0; i < underlineMatches.Count; i++)
            {
                var startMatch = underlineMatches[i];
                var startIndex = startMatch.Index;
                var startLength = startMatch.Length;
                var colorStr = startMatch.Groups[1].Value;

                Color color;
                if (!Colors.TryGetValue(colorStr, out color))
                {
                    ColorUtility.TryParseHtmlString(colorStr, out color);
                }

                var tagInfo = new TagInfo()
                {
                    Type = RichType.Underline,
                    Index = startIndex,
                    Length = startLength,
                    Color = color,
                };
                
                tags.Add(tagInfo);
            }
            
            for (int i = 0; i < underlineEndMatches.Count; i++)
            {
                var endMatch = underlineEndMatches[i];
                var endIndex = endMatch.Index;
                var endLength = endMatch.Length;
                
                var tagInfo = new TagInfo()
                {
                    Type = RichType.Underline,
                    IsClose = true,
                    Index = endIndex,
                    Length = endLength,
                };

                tags.Add(tagInfo);
            }
            
            // link
            var linkMatches = LinkRegex.Matches(resultText);
            var linkEndMatches = LinkEndRegex.Matches(resultText);
            for (int i = 0; i < linkMatches.Count; i++)
            {
                var startMatch = linkMatches[i];
                var startIndex = startMatch.Index;
                var startLength = startMatch.Length;
                var url = startMatch.Groups[1].Value;

                var tagInfo = new TagInfo()
                {
                    Type = RichType.Link,
                    Index = startIndex,
                    Length = startLength,
                    Content = url,
                };
                
                tags.Add(tagInfo);
            }
            
            for (int i = 0; i < linkEndMatches.Count; i++)
            {
                var endMatch = linkEndMatches[i];
                var endIndex = endMatch.Index;
                var endLength = endMatch.Length;
                
                var tagInfo = new TagInfo()
                {
                    Type = RichType.Link,
                    IsClose = true,
                    Index = endIndex,
                    Length = endLength,
                };

                tags.Add(tagInfo);
            }

            // 空格的位置
            var spaceIndexes = new Queue<int>(GetWhiteSpaceIndexesInString(richText));
            // 按顺序处理索引
            tags.Sort((a, b) => a.Index - b.Index);
            var tagCount = tags.Count;
            var tagStack = new Stack<TagInfo>(tagCount);
            var richInfoStack = new Stack<RichInfo>(tagCount);
            var offset = 0;
            // 子字符串的起止
            var subStrPairs = new List<(int start, int length)>();
            for (int i = 0; i < tagCount; i++)
            {
                var tag = tags[i];
                // 排除不生成顶点的空格的影响
                if (spaceIndexes.Count > 0)
                {
                    var spaceIndex = spaceIndexes.Peek();
                    if (tag.Index > spaceIndex)
                    {
                        offset++;
                        spaceIndexes.Dequeue();
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
                            break;
                        default:
                            subStrPairs.Add((top.Index, top.Length));
                            subStrPairs.Add((tag.Index, tag.Length));
                            break;
                    }
                }
                else
                {
                    tagStack.Push(tag);
                    var richInfo = new RichInfo()
                    {
                        Type = tag.Type,
                        StartIndex = tag.Index - offset,
                        Color = tag.Color,
                        Content = tag.Content,
                    };

                    offset += tag.Length;
                    richInfoStack.Push(richInfo);
                }
            }

            // 最后统一替换
            if (subStrPairs.Count > 0)
            {
                // 从前往后顺序排序
                subStrPairs.Sort((a, b) => a.start - b.start);
                var resultStrBuilder = new StringBuilder();
                var index = 0;
                for (var i = 0; i < subStrPairs.Count; i++)
                {
                    var strPair = subStrPairs[i];
                    resultStrBuilder.Append(resultText[index..strPair.start]);
                    index = strPair.start + strPair.length;
                }
                
                // 加上最后的字符串
                resultStrBuilder.Append(resultText[index..]);

                resultText = resultStrBuilder.ToString();
            }

            // 占位字符，到时候要将alpha设置为0
            // 可以保证有嵌套size的情况下可以生成正确的顶点
            resultText = IconRegex.Replace(resultText, "〇");
            
            // 为了得到准确的字符串，这里要去掉所有的富文本标签了
            var tempText = BoldRegex.Replace(resultText, "");
            tempText = BoldEndRegex.Replace(tempText, "");
            tempText = ItalicRegex.Replace(tempText, "");
            tempText = ItalicEndRegex.Replace(tempText, "");
            tempText = ColorRegex.Replace(tempText, "");
            tempText = ColorEndRegex.Replace(tempText, "");
            tempText = SizeRegex.Replace(tempText, "");
            tempText = SizeEndRegex.Replace(tempText, "");
            // 然后去掉white space
            tempText = WhiteSpaceRegex.Replace(tempText, "");

            foreach (var richInfo in richInfos)
            {
                richInfo.EffectedStr = tempText[richInfo.StartIndex..richInfo.EndIndex];
            }

            return richInfos;
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
        private void ApplyShadowEffect(RichInfo richInfo, IList<UIVertex> verts, int vertCount)
        {
            int start = richInfo.StartIndex * 4;
            int end = Mathf.Min(richInfo.EndIndex * 4, vertCount);
            UIVertex vt;
            for(int i = start; i < end; i++)
            {
                vt = verts[i];
                verts.Add(vt);
                Vector3 v = vt.position;
                v.x += -1;
                v.y += 1;
                vt.position = v;
                vt.color.a = (byte)(vt.color.a / 2);
                verts[i] = vt;
            }
        }

        /// <summary>
        /// 描边效果
        /// </summary>
        private void ApplyOutlineEffect(RichInfo richInfo, IList<UIVertex> verts, int vertCount)
        {
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
        private (Sprite icon, UIVertex[] verts) ApplyIcon(RichInfo richInfo, IList<UIVertex> verts, Dictionary<string, Sprite> iconCache)
        {
            var iconName = richInfo.Content;
            Sprite iconSprite;
            if (!iconCache.TryGetValue(iconName, out iconSprite))
            {
                iconSprite = iconProvider.GetIcon(iconName);
            }
            
            var start = richInfo.StartIndex * 4;
            var end = start + 4;
            var iconVerts = new UIVertex[4];
            for (int i = start; i < end; i++)
            {
                // 将原字符设置为透明
                var vert = verts[i];
                vert.color.a = 0;
                verts[i] = vert;

                // 整理图标
                var iconVert = vert;
                vert.color.a = 255;
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
        /// 下划线效果
        /// </summary>
        private List<UIVertex[]> ApplyUnderlineEffect(RichInfo richInfo, IList<UIVertex> verts, int vertCount)
        {
            var underlineHeight = 2.0f;
            var padding = 2f;

            var result = new List<UIVertex[]>();
            var count = richInfo.Rects.Count;
            for (int i = 0; i < count; i++)
            {
                var rectIndex = richInfo.RectIndexes[i];
                
                // 顶点顺序是左上顺时针到左下
                int start = rectIndex[0] * 4;
                // 要添加下划线的最后一个字符的右下角顶点索引
                int end = Mathf.Min(rectIndex[1] * 4, vertCount) - 2;
                
                // 下划线的四个顶点
                var underlineVerts = new UIVertex[4];
                // 起止坐标
                float startX = verts[start].position.x;
                float endX = verts[end].position.x;
                float endY = verts[end].position.y;
                // 计算下划线的四个顶点
                underlineVerts[0] = new UIVertex
                {
                    position = new Vector3(startX, endY - padding, 0),
                    color = richInfo.Color, // 下划线颜色
                };
                underlineVerts[1] = new UIVertex
                {
                    position = new Vector3(endX, endY - padding, 0),
                    color = richInfo.Color,
                };
                underlineVerts[2] = new UIVertex
                {
                    position = new Vector3(endX, endY - padding - underlineHeight, 0),
                    color = richInfo.Color,
                };
                underlineVerts[3] = new UIVertex
                {
                    position = new Vector3(startX, endY - padding - underlineHeight, 0),
                    color = richInfo.Color,
                };
                
                result.Add(underlineVerts);
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
                Array.Fill(colors, Color.white);
                tex.SetPixels(colors);
                tex.Apply();
                _whiteQuad = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

        private static void CalculateRects(RichInfo richInfo, IList<UIVertex> vertices)
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
        
        #endregion

        #region Coroutine

        private IEnumerator CallIconUpdate(List<RichInfo> iconInfos, Dictionary<string, Sprite> icons, List<UIVertex[]> vertices)
        {
            yield return null;
            // 处理图标
            var needShowIcons = iconInfos.Count > 0;
            iconImage.gameObject.SetActive(needShowIcons);
            if (needShowIcons)
            {
                iconImage.SetIcons(iconInfos, icons, vertices);
            }
        }

        #endregion
    }
}
