using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SS.UIComponent
{
    /// <summary>
    /// 网格构建：把解析结果应用到顶点上（描边、阴影、渐变、图标、GIF、下划线/删除线）。
    /// </summary>
    public partial class RichText : Text, IPointerClickHandler
    {

        /// <summary>
        /// 投影效果
        /// </summary>
        /// <param name="richInfo">富文本信息</param>
        /// <param name="verts">顶点信息</param>
        /// <param name="effectedVerts">特效渲染顶点信息</param>
        /// <param name="vertCount">顶点数</param>
        /// <param name="iconIndexList">需要应用阴影的Icon（占位）字符的索引与对应的阴影信息</param>
        private void ApplyShadowEffect(RichInfo richInfo, IList<UIVertex> verts, List<UIVertex> effectedVerts, int vertCount,
            Dictionary<int, ShadowRichInfo> iconIndexList)
        {
            // 先检查能不能被渲染出来，不能那就算了
            if (richInfo.StartIndex * 4 >= vertCount)
            {
                return;
            }

            if (richInfo is not ShadowRichInfo shadowRichInfo)
            {
                Debug.LogError("richInfo is not ShadowRichInfo");
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

            // 为确保顶点三角形正确，需要将先渲染的阴影完整放入列表
            // 再将后渲染的原字符通过此列表中转添加至特效顶点列表
            var rawVertices = new List<UIVertex>((richInfo.EndIndex - richInfo.StartIndex) * 4);
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
                    iconIndexList.Add(iconIndexes.Dequeue(), shadowRichInfo);
                    continue;
                }

                // 单个字符的四个顶点为[start, end)
                var vt1 = verts[start];
                var vt2 = verts[start + 2];
                // 从左上顺时针到左下，vt1.y > vt2.y
                var offset = Mathf.Max(1, (vt1.position.y - vt2.position.y) / 16);
                var end = start + 4;
                UIVertex vt;
                for (var j = start; j < end; j++)
                {
                    vt = verts[j];
                    // 阴影和原字符都作为特效顶点
                    var tempVt = vt;
                    var v = vt.position;
                    v.x += offset * (shadowRichInfo.IsXPositive ? 1 : -1);
                    v.y += offset * (shadowRichInfo.IsYPositive ? 1 : -1);
                    vt.position = v;
                    // 阴影必须比原字符透明度更低吧
                    vt.color.a = (byte)(vt.color.a / 2);
                    // 先加阴影再加原字符
                    effectedVerts.Add(vt);
                    rawVertices.Add(tempVt);
                }
            }

            effectedVerts.AddRange(rawVertices);
        }

        /// <summary>
        /// 描边效果
        /// </summary>
        private void ApplyOutlineEffect(RichInfo richInfo, IList<UIVertex> verts, List<UIVertex> effectedVerts, int vertCount)
        {
            // 先检查能不能被渲染出来，不能那就算了
            if (richInfo.StartIndex * 4 >= vertCount)
            {
                return;
            }

            UIVertex vt;
            for (var i = richInfo.StartIndex; i < richInfo.EndIndex; i++)
            {
                var start = i * 4;
                var end = start + 4;
                // 单个字符的四个顶点为[start, end)
                var vt1 = verts[start];
                var vt2 = verts[start + 2];
                // 从左上顺时针到左下，vt1.y > vt2.y
                var size = vt1.position.y - vt2.position.y;
                var offset = Mathf.Clamp(size / 64, 1, 2);
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int y = -1; y <= 1; y += 2)
                    {
                        for (int j = start; j < end; j++)
                        {
                            vt = verts[j];
                            Vector3 v = vt.position;
                            v.x += x * offset;
                            v.y += y * offset;
                            var newColor = richInfo.Color;
                            newColor.a = (newColor.a * verts[j].color.a) / 255f;
                            vt.color = newColor;
                            vt.position = v;
                            effectedVerts.Add(vt);
                        }
                    }
                }
            }

            var startAt = richInfo.StartIndex * 4;
            var endAt = Mathf.Min(richInfo.EndIndex * 4, vertCount);
            for (var i = startAt; i < endAt; i++)
                effectedVerts.Add(verts[i]);
        }

        /// <summary>
        /// 图标效果
        /// </summary>
        private (Sprite icon, UIVertex[] verts) ApplyIcon(RichInfo richInfo, IList<UIVertex> verts,
            Dictionary<string, Sprite> iconCache, int vertCount)
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
        /// 划线效果
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
                int startAt = rectIndex[0] * 4;
                // 要添加下划线的最后一个字符的右下角顶点索引
                int endAt = Mathf.Min(rectIndex[1] * 4, vertCount) - 1;

                float xMin, xMax, yMin, yMax;
                var startPos = verts[startAt].position;
                xMin = startPos.x;
                xMax = startPos.x;
                yMin = startPos.y;
                yMax = startPos.y;
                for (int j = startAt + 1; j < endAt; j++)
                {
                    // 此位置是基于pivot的位置
                    var pos = verts[j].position;
                    xMin = Mathf.Min(pos.x, xMin);
                    xMax = Mathf.Max(pos.x, xMax);
                    yMin = Mathf.Min(pos.y, yMin);
                    yMax = Mathf.Max(pos.y, yMax);
                }
                
                // 划线的四个顶点
                var lineVerts = new UIVertex[4];
                float basedY = 0;
                switch (lineRichInfo.BasedPos)
                {
                    case LineBasedPos.Top:
                        basedY = yMax;
                        break;
                    case LineBasedPos.Middle:
                        basedY = (yMax + yMin) / 2;
                        break;
                    case LineBasedPos.Bottom:
                        basedY = yMin;
                        break;
                }

                // 计算划线的四个顶点
                lineVerts[0] = new UIVertex
                {
                    position = new Vector3(xMin, basedY + padding + underlineHeight / 2, 0),
                    color = richInfo.Color, // 划线颜色
                };
                lineVerts[1] = new UIVertex
                {
                    position = new Vector3(xMax, basedY + padding + underlineHeight / 2, 0),
                    color = richInfo.Color,
                };
                lineVerts[2] = new UIVertex
                {
                    position = new Vector3(xMax, basedY + padding - underlineHeight / 2, 0),
                    color = richInfo.Color,
                };
                lineVerts[3] = new UIVertex
                {
                    position = new Vector3(xMin, basedY + padding - underlineHeight / 2, 0),
                    color = richInfo.Color,
                };

                result.Add(lineVerts);
            }

            return result;
        }

        private void ApplyGradientEffect(RichInfo richInfo, IList<UIVertex> verts, int vertCount)
        {
            // 先检查能不能被渲染出来，不能那就算了
            if (richInfo.StartIndex * 4 >= vertCount)
            {
                return;
            }
            
            // 转换为渐变专用的类型
            if (richInfo is not GradientRichInfo gradientRichInfo)
            {
                Debug.LogError($"Cant Cast to GradientRichInfo: {richInfo}");
                return;
            }

            Color GetColor(float x, float y)
            {
                // 先获取x轴颜色
                var topColor = Color.Lerp(gradientRichInfo.LeftTopColor, gradientRichInfo.RightTopColor, x);
                var bottomColor = Color.Lerp(gradientRichInfo.LeftBottomColor, gradientRichInfo.RightBottomColor, x);

                var finalColor = Color.Lerp(bottomColor, topColor, y);
                return finalColor;
            }

            var count = richInfo.Rects.Count;
            // 总有效长度，用于计算每个矩形生效的渐变范围
            float totalLength = richInfo.RectIndexes.Sum(index => index[1] - index[0]);
            float rateOffset = 0;
            for (int i = 0; i < count; i++)
            {
                var rectIndex = richInfo.RectIndexes[i];
                var gradientRate = (rectIndex[1] - rectIndex[0]) / totalLength;
                // 计算该矩形对应的坐标范围
                // 顶点顺序是左上顺时针到左下
                int startAt = rectIndex[0] * 4;
                // 最后一个顶点索引
                int endAt = Mathf.Min(rectIndex[1] * 4, vertCount) - 1;
                float xMin, xMax, yMin, yMax;
                var startPos = verts[startAt].position;
                xMin = startPos.x;
                xMax = startPos.x;
                yMin = startPos.y;
                yMax = startPos.y;
                for (int j = startAt + 1; j < endAt; j++)
                {
                    // 此位置是基于pivot的位置
                    var pos = verts[j].position;
                    xMin = Mathf.Min(pos.x, xMin);
                    xMax = Mathf.Max(pos.x, xMax);
                    yMin = Mathf.Min(pos.y, yMin);
                    yMax = Mathf.Max(pos.y, yMax);
                }

                for (int j = rectIndex[0]; j < rectIndex[1]; j++)
                {
                    // 顶点顺序是左上顺时针到左下
                    int start = j * 4;
                    // 左上
                    var vt = verts[start];
                    vt.color = GetColor(rateOffset + gradientRate * (vt.position.x - xMin) / (xMax - xMin),
                        (vt.position.y - yMin) / (yMax - yMin));
                    verts[start] = vt;
                    // 右上
                    vt = verts[start + 1];
                    vt.color = GetColor(rateOffset + gradientRate * (vt.position.x - xMin) / (xMax - xMin),
                        (vt.position.y - yMin) / (yMax - yMin));
                    verts[start + 1] = vt;
                    // 右下
                    vt = verts[start + 2];
                    vt.color = GetColor(rateOffset + gradientRate * (vt.position.x - xMin) / (xMax - xMin),
                        (vt.position.y - yMin) / (yMax - yMin));
                    verts[start + 2] = vt;
                    // 左下
                    vt = verts[start + 3];
                    vt.color = GetColor(rateOffset + gradientRate * (vt.position.x - xMin) / (xMax - xMin),
                        (vt.position.y - yMin) / (yMax - yMin));
                    verts[start + 3] = vt;
                }

                rateOffset += gradientRate;
            }
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

        private static IList<UIVertex> ProcessRawTags(IList<UIVertex> vertices, string resultText,
            string textWithoutTag)
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

    }
}
