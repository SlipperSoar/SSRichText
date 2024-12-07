using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using ColorUtility = UnityEngine.ColorUtility;

namespace SS.UIComponent
{
    /// <summary>
    /// 对富文本支持更广泛的UI文本组件
    /// </summary>
    [AddComponentMenu("UI/SS/RichText")]
    public class RichText : Text
    {
        #region inner enum/struct

        private enum RichType
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

        private class RichInfo
        {
            public RichType Type;
            /// <summary>富文本开始的位置（包含）</summary>
            public int StartIndex;
            /// <summary>富文本结束的位置（不包含）</summary>
            public int EndIndex;
            /// <summary>富文本类型，根据具体类型决定这个content要怎么使用</summary>
            public string Content;
            /// <summary>顶点颜色</summary>
            public Color Color;
        }

        #endregion
        
        #region properties

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
        private static string ColorRegexText => @"<color=((#[0-9a-f]{6})|(" + GetColorWords() + @"))>";
        private const string ColorEndRegexText = @"</color>";
        private readonly Regex ColorRegex = new Regex(ColorRegexText);
        private readonly Regex ColorEndRegex = new Regex(ColorEndRegexText);
        // 大小
        private const string SizeRegexText = @"<size=(\d+)>";
        private const string SizeEndRegexText = @"</size>";
        private readonly Regex SizeRegex = new Regex(SizeRegexText);
        private readonly Regex SizeEndRegex = new Regex(SizeEndRegexText);
        
        // 追加的富文本
        private static readonly string OutlineRegexText = @"<outline=((#[0-9a-f]{6})|(" + GetColorWords() + @"))>";
        private const string OutlineEndRegexText = @"</outline>";
        private readonly Regex OutlineRegex = new Regex(OutlineRegexText, RegexOptions.IgnoreCase);
        private readonly Regex OutlineEndRegex = new Regex(OutlineEndRegexText);

        private const string ShadowRegexText = @"<shadow>";
        private const string ShadowEndRegexText = @"</shadow>";
        private readonly Regex ShadowRegex = new Regex(ShadowRegexText);
        private readonly Regex ShadowEndRegex = new Regex(ShadowEndRegexText);

        #endregion

        readonly UIVertex[] m_TempVerts = new UIVertex[4];

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
            var richInfos = ProcessRichText(text, out var resultText);

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
            foreach (var richInfo in richInfos)
            {
                switch (richInfo.Type)
                {
                    case RichType.Icon:
                        break;
                    case RichType.Outline:
                        ApplyOutlineEffect(richInfo, verts, vertCount);
                        break;
                    case RichType.Shadow:
                        ApplyShadowEffect(richInfo, verts, vertCount);
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
            }

            m_DisableFontTextureRebuiltCallback = false;
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
                    Content = iconName
                };
                
                tags.Add(tagInfo);
            }

            // 描边
            var outlineMatches = OutlineRegex.Matches(resultText);
            var outlineEndMatches = OutlineEndRegex.Matches(resultText);
            // 只算成对的
            // var outlineCount = Mathf.Min(outlineMatches.Count, outlineEndMatches.Count);
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
            // 只算成对的
            // var shadowCount = Mathf.Min(shadowMatches.Count, shadowEndMatches.Count);
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
                            StartIndex = tag.Index - offset,
                            EndIndex = tag.Index - offset,
                        });

                        offset += tag.Length;
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
                    // 只删掉自定义的标签
                    switch (richInfo.Type)
                    {
                        case RichType.Shadow:
                        case RichType.Outline:
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

            // 从后往前 倒序排序
            subStrPairs.Sort((a, b) => b.start - a.start);
            // 最后统一替换
            for (var i = 0; i < subStrPairs.Count; i++)
            {
                var strPair = subStrPairs[i];
                resultText = resultText.Remove(strPair.start, strPair.length);
            }

            resultText = IconRegex.Replace(resultText, " ");

            return richInfos;
        }

        private List<int> GetWhiteSpaceIndexesInString(string str)
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
            int end = Mathf.Min(richInfo.EndIndex * 4 + 4, vertCount);
            UIVertex vt;
            for(int i = start; i < end; i++)
            {
                vt = verts[i];
                verts.Add(vt);
                Vector3 v = vt.position;
                v.x += -1;
                v.y += 1;
                vt.position = v;
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

        #endregion
    }
}
