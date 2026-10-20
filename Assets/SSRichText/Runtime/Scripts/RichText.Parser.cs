using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ColorUtility = UnityEngine.ColorUtility;

namespace SS.UIComponent
{
    /// <summary>
    /// 文本解析：正则定义、标签匹配、颜色/链接参数处理与解析缓存。
    /// </summary>
    public partial class RichText : Text, IPointerClickHandler
    {

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
        private static string ColorRegexText => @"<color=((#[0-9a-f]{6,8})|(" + GetColorWords() + @"))>";
        private const string ColorEndRegexText = @"</color>";
        private static readonly Regex ColorRegex = new Regex(ColorRegexText, RegexOptions.IgnoreCase);

        private static readonly Regex ColorEndRegex = new Regex(ColorEndRegexText);

        // 大小
        private const string SizeRegexText = @"<size=(\d+)>";
        private const string SizeEndRegexText = @"</size>";
        private static readonly Regex SizeRegex = new Regex(SizeRegexText);
        private static readonly Regex SizeEndRegex = new Regex(SizeEndRegexText);

        // 追加的富文本
        // 渐变
        private const string GradientRegexText = @"<gradient\s+([^>]+)>";

        private static readonly string GradientColorRegexText = $"(lt|rt|lb|rb)=({GetColorRegexGroup()})";

        private const string GradientEndRegexText = @"</gradient>";
        private static readonly Regex GradientRegex = new Regex(GradientRegexText, RegexOptions.IgnoreCase);
        private static readonly Regex GradientColorRegex = new Regex(GradientColorRegexText);
        private static readonly Regex GradientEndRegex = new Regex(GradientEndRegexText);
        
        // 描边
        private static readonly string OutlineRegexText =
            @"<outline=((#[0-9a-f]{6,8})|(" + GetColorWords() + @"))>";

        private const string OutlineEndRegexText = @"</outline>";
        private static readonly Regex OutlineRegex = new Regex(OutlineRegexText, RegexOptions.IgnoreCase);
        private static readonly Regex OutlineEndRegex = new Regex(OutlineEndRegexText);

        // 阴影
        private const string ShadowRegexText = @"<shadow=(lt|rt|lb|rb)>";
        private const string ShadowEndRegexText = @"</shadow>";
        private static readonly Regex ShadowRegex = new Regex(ShadowRegexText);
        private static readonly Regex ShadowEndRegex = new Regex(ShadowEndRegexText);

        // 下划线
        private static readonly string UnderlineRegexText =
            @"<underline=((#[0-9a-f]{6,8})|(" + GetColorWords() + @"))>";

        private const string UnderlineEndRegexText = @"</underline>";
        private static readonly Regex UnderlineRegex = new Regex(UnderlineRegexText, RegexOptions.IgnoreCase);
        private static readonly Regex UnderlineEndRegex = new Regex(UnderlineEndRegexText);

        // 删除线
        private const string StrikeLineRegexText = @"<s>";
        private const string StrikeLineEndRegexText = @"</s>";
        private static readonly Regex StrikeLineRegex = new Regex(StrikeLineRegexText);
        private static readonly Regex StrikeLineEndRegex = new Regex(StrikeLineEndRegexText);

        // link
        // 支持两种写法：
        // 1. 带协议头：https://...、file://...、mailto:... 以及自定义 scheme
        // 2. 不带协议头：www.example.com、example.com/path?q=1、localhost:8080、127.0.0.1:8080
        private static readonly string LinkRegexText =
            @"<link=([a-zA-Z][a-zA-Z0-9+\-.]*:[^\s>]+" +
            @"|(?:(?:(?:[a-zA-Z0-9](?:[a-zA-Z0-9\-]*[a-zA-Z0-9])?\.)+[a-zA-Z]{2,})|localhost|(?:\d{1,3}(?:\.\d{1,3}){3}))" +
            @"(?::\d{1,5})?(?:[/?#][^\s>]*)?)>";
        private const string LinkEndRegexText = @"</link>";
        private static readonly Regex LinkRegex = new Regex(LinkRegexText);
        private static readonly Regex LinkEndRegex = new Regex(LinkEndRegexText);

        // 给不带协议头的 link 补全协议时用
        private static readonly Regex AbsoluteUrlRegex = new Regex(@"^[a-zA-Z][a-zA-Z0-9+\-.]*://");

        private static readonly Regex NonHierarchicalSchemeRegex =
            new Regex(@"^(mailto|tel|sms|geo|file|data|urn|magnet):", RegexOptions.IgnoreCase);

        private static readonly Regex BareHostRegex =
            new Regex(
                @"^(?:(?:(?:[a-zA-Z0-9](?:[a-zA-Z0-9\-]*[a-zA-Z0-9])?\.)+[a-zA-Z]{2,})|localhost|(?:\d{1,3}(?:\.\d{1,3}){3}))" +
                @"(?::\d{1,5})?(?:[/?#].*)?$", RegexOptions.IgnoreCase);

        // 图标（名称支持 Resources 下的子目录，如 Icons/heart）
        private static readonly Regex IconRegex = new Regex(@"<icon=([a-zA-Z0-9_\-\(\)\.\/\\]+)/>");

        // GIF动图（名称支持 Resources 下的子目录，如 Gifs/cat）
        private static readonly Regex GifRegex = new Regex(@"<gif=([a-zA-Z0-9_\-\(\)\.\/\\]+)/>");


        private List<RichInfo> ProcessRichText(string richText, out string resultText, out string textWithoutTag)
        {
            // 通过将富文本内容提取成仅包含size和color的文本来使用Text的生成器生成顶点信息
            resultText = richText;
            var richInfos = new List<RichInfo>();
            var tags = new List<TagInfo>();

            // 图标
            tags.AddRange(GetTags(resultText, IconRegex, RichType.Icon, true, (str, info) =>
            {
                // 统一成 Resources.Load 使用的路径分隔符
                info.Content = str.Replace('\\', '/');
                info.Color = Color.white;
            }));

            // Gif动图
            tags.AddRange(GetTags(resultText, GifRegex, RichType.Gif, true, (str, info) =>
            {
                info.Content = str.Replace('\\', '/');
                info.Color = Color.white;
            }));

            // 描边
            tags.AddRange(GetTags(resultText, OutlineRegex, RichType.Outline,
                paramProcessor: (str, info) => { info.Color = GetColor(str); }));
            tags.AddRange(GetTags(resultText, OutlineEndRegex, RichType.Outline, isCloseTag: true));

            // 阴影
            tags.AddRange(GetTags(resultText, ShadowRegex, RichType.Shadow,
                paramProcessor: (str, info) => info.Content = str));
            tags.AddRange(GetTags(resultText, ShadowEndRegex, RichType.Shadow, isCloseTag: true));

            // 颜色
            tags.AddRange(GetTags(resultText, ColorRegex, RichType.Color,
                paramProcessor: (str, info) => { info.Color = GetColor(str); }));
            tags.AddRange(GetTags(resultText, ColorEndRegex, RichType.Color, isCloseTag: true));

            // 大小
            tags.AddRange(GetTags(resultText, SizeRegex, RichType.Size,
                paramProcessor: (str, info) => { info.Content = str; }));
            tags.AddRange(GetTags(resultText, SizeEndRegex, RichType.Size, isCloseTag: true));

            // 斜体
            tags.AddRange(GetTags(resultText, ItalicRegex, RichType.Italic));
            tags.AddRange(GetTags(resultText, ItalicEndRegex, RichType.Italic, isCloseTag: true));

            // 粗体
            tags.AddRange(GetTags(resultText, BoldRegex, RichType.Bold));
            tags.AddRange(GetTags(resultText, BoldEndRegex, RichType.Bold, isCloseTag: true));

            // 渐变
            tags.AddRange(GetTags(resultText, GradientRegex, RichType.Gradient,
                paramProcessor: (str, info) => info.Content = str));
            tags.AddRange(GetTags(resultText, GradientEndRegex, RichType.Gradient, isCloseTag: true));

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

                // 不能包含标签中的空格
                if (spaceIndexes.Count > 0)
                {
                    var spaceIndex = spaceIndexes.Peek();
                    while (tag.Index < spaceIndex && tag.Index + tag.Length > spaceIndex)
                    {
                        spaceIndexes.Dequeue();
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

                    // top：开启标签，tag：闭合标签
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
                        case RichType.Gradient:
                        {
                            // 获取渐变的四个颜色
                            var gradientRichInfo = new GradientRichInfo()
                            {
                                Type = tag.Type,
                                StartIndex = tag.Index - offset,
                                Color = color,
                            };
                            
                            ProcessGradientParams(tag.Content, gradientRichInfo);
                            richInfo = gradientRichInfo;
                        }
                            break;
                        case RichType.Shadow:
                        {
                            var shadowRichInfo = new ShadowRichInfo()
                            {
                                Type = tag.Type,
                                StartIndex = tag.Index - offset,
                                Color = tag.Color,
                            };

                            ProcessShadowParams(tag.Content, shadowRichInfo);
                            richInfo = shadowRichInfo;
                            break;
                        }
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

        /// <summary>
        /// 复制一份解析结果：缓存里保存的是模板，网格构建和图标更新协程只操作副本
        /// </summary>
        private static List<RichInfo> CloneRichInfos(List<RichInfo> source)
        {
            var result = new List<RichInfo>(source.Count);
            foreach (var info in source)
            {
                result.Add(CloneRichInfo(info));
            }

            return result;
        }

        private static RichInfo CloneRichInfo(RichInfo source)
        {
            RichInfo clone;
            switch (source)
            {
                case ShadowRichInfo shadow:
                    clone = new ShadowRichInfo
                    {
                        IsXPositive = shadow.IsXPositive,
                        IsYPositive = shadow.IsYPositive,
                    };
                    break;
                case DrawingLineRichInfo line:
                    clone = new DrawingLineRichInfo
                    {
                        LineHeight = line.LineHeight,
                        YPosFromBase = line.YPosFromBase,
                        BasedPos = line.BasedPos,
                    };
                    break;
                case GradientRichInfo gradient:
                    clone = new GradientRichInfo
                    {
                        LeftTopColor = gradient.LeftTopColor,
                        RightTopColor = gradient.RightTopColor,
                        LeftBottomColor = gradient.LeftBottomColor,
                        RightBottomColor = gradient.RightBottomColor,
                    };
                    break;
                default:
                    clone = new RichInfo();
                    break;
            }

            clone.Type = source.Type;
            clone.StartIndex = source.StartIndex;
            clone.EndIndex = source.EndIndex;
            clone.Content = source.Content;
            clone.Color = source.Color;
            clone.EffectedStr = source.EffectedStr;

            return clone;
        }

        private List<TagInfo> GetTags(string richText, Regex tagRegex, RichType richType, bool isCloseTag = false,
            Action<string, TagInfo> paramProcessor = null)
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

        private static string GetColorRegexGroup()
        {
            return "(#[0-9a-f]{6,8})|(" + GetColorWords() + @")";
        }

        /// <summary>
        /// 获取渐变颜色组
        /// </summary>
        /// <param name="content">参数字符串</param>
        /// <param name="gradientRichInfo">渐变富文本信息</param>
        /// <returns>渐变颜色数组，依次为lt、rt、lb、rb</returns>
        private static void ProcessGradientParams(string content, GradientRichInfo gradientRichInfo)
        {
            var matches = GradientColorRegex.Matches(content);
            foreach (Match match in matches)
            {
                var groups = match.Groups;
                // 0: 原字符串，1：第一个括号，2：第二个括号 ...
                switch (groups[1].Value)
                {
                    case "lt":
                        gradientRichInfo.LeftTopColor = GetColor(groups[2].Value);
                        break;
                    case "rt":
                        gradientRichInfo.RightTopColor = GetColor(groups[2].Value);
                        break;
                    case "lb":
                        gradientRichInfo.LeftBottomColor = GetColor(groups[2].Value);
                        break;
                    case "rb":
                        gradientRichInfo.RightBottomColor = GetColor(groups[2].Value);
                        break;
                }
            }
        }

        private static void ProcessShadowParams(string content, ShadowRichInfo shadowRichInfo)
        {
            var direction = content.Trim();
            switch (direction)
            {
                case "lt":
                    shadowRichInfo.IsXPositive = false;
                    shadowRichInfo.IsYPositive = true;
                    break;
                case "rt":
                    shadowRichInfo.IsXPositive = true;
                    shadowRichInfo.IsYPositive = true;
                    break;
                case "lb":
                    shadowRichInfo.IsXPositive = false;
                    shadowRichInfo.IsYPositive = false;
                    break;
                case "rb":
                    shadowRichInfo.IsXPositive = true;
                    shadowRichInfo.IsYPositive = false;
                    break;
                default:
                    shadowRichInfo.IsXPositive = true;
                    shadowRichInfo.IsYPositive = false;
                    break;
            }
        }
        
        /// <summary>
        /// 字符串转换为颜色
        /// </summary>
        /// <param name="colorStr">颜色字符串</param>
        /// <returns>颜色</returns>
        private static Color GetColor(string colorStr)
        {
            if (!Colors.TryGetValue(colorStr, out var tagColor))
            {
                // 如果是7位代码，把最后一位补齐到8位
                if (colorStr.Length == 8)
                {
                    colorStr += colorStr[7];
                }

                // 无法正确转换，则返回透明色（按理来说不可能的）
                if (!ColorUtility.TryParseHtmlString(colorStr, out tagColor))
                {
                    Debug.LogError($"cant parse str: {colorStr}");
                    tagColor = Color.clear;
                }
            }

            return tagColor;
        }

        /// <summary>
        /// 补全 link 的协议头：已带协议头的地址原样返回，裸域名 / 本地地址补上 https://
        /// </summary>
        /// <param name="url">标签中填写的地址</param>
        /// <returns>可以直接打开或访问的绝对地址</returns>
        private static string NormalizeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }

            // http://、https://、file:// 等带 :// 的协议，以及 mailto:、tel: 等不需要 // 的协议
            if (AbsoluteUrlRegex.IsMatch(url) || NonHierarchicalSchemeRegex.IsMatch(url))
            {
                return url;
            }

            // www.example.com、example.com/path?q=1、localhost:8080 等不带协议头的地址
            return BareHostRegex.IsMatch(url) ? $"https://{url}" : url;
        }
    }
}
