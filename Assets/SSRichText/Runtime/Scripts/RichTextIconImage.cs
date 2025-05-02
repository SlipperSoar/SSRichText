/*
 * @author SlipperSoar
 * @Created: 2024-12-08
 * @description Unity Text富文本扩展用的RawImage扩展
 */

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using RichInfo = SS.UIComponent.RichText.RichInfo;

namespace SS.UIComponent
{
    /// <summary>
    /// 处理富文本的图标内容
    /// </summary>
    public class RichTextIconImage : RawImage
    {
        #region properties

        private List<RichInfo> _sprites;
        private List<UIVertex[]> _vertices;
        private Dictionary<RichInfo, UIVertex[]> _gifs;
        private LinkedList<int> _iconShadows;
        private static readonly int OffsetScale = Shader.PropertyToID("_OffsetScale");

        private int textureWidth;
        private int textureHeight;
        private RenderTexture rt;
        private Dictionary<string, Coroutine> gifCoroutines = new();
        private Material blitMaterial;

        #endregion
        
        #region Public Methods

        public void SetIcons(List<RichInfo> iconInfos, Dictionary<string, Sprite> icons, List<UIVertex[]> vertices, LinkedList<int> iconShadows, Dictionary<RichInfo, UIVertex[]> gifs)
        {
            _sprites = iconInfos;
            _vertices = vertices;
            _iconShadows = iconShadows;
            _gifs = gifs;
            // 先把上次的停了（如果有）
            StopAllGifCoroutines();
            if (_sprites.Count == 1 && _gifs.Count == 0)
            {
                ApplySingleSprite(icons[_sprites[0].Content].texture);
            }
            else if (_sprites.Count == 0 && _gifs.Count == 1)
            {
                // 单个动图
                foreach (var gif in gifs)
                {
                    ApplySingleGif(gif.Key, gif.Value);
                    break;
                }
            }
            else
            {
                rt = CombineSprites(icons);
                texture = rt;
            }
        }

        public void Clear()
        {
            _vertices = null;
            _sprites = null;
            _gifs = null;
            _iconShadows = null;
            StopAllGifCoroutines();
            SetAllDirty();
        }
        
        #endregion
        
        #region unity

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            toFill.Clear();

            if (_sprites == null || _vertices == null)
            {
                return;
            }

            // 这里处理图标的渲染
            for (int i = 0; i < _sprites.Count; i++)
            {
                var info = _sprites[i];
                var verts = _vertices[i];
                
                verts[0].color = color * info.Color;
                verts[1].color = color * info.Color;
                verts[2].color = color * info.Color;
                verts[3].color = color * info.Color;

                if (_iconShadows.Remove(info.StartIndex))
                {
                    ApplyIconShadow(verts, toFill);
                }
                else
                {
                    AddIconTriangle(verts, toFill);
                }
            }

            if (_gifs == null)
            {
                return;
            }
            
            // 这里处理Gif的渲染
            foreach (var gif in _gifs)
            {
                var info = gif.Key;
                var verts = gif.Value;
                
                verts[0].color = color * info.Color;
                verts[1].color = color * info.Color;
                verts[2].color = color * info.Color;
                verts[3].color = color * info.Color;

                if (_iconShadows.Remove(info.StartIndex))
                {
                    ApplyIconShadow(verts, toFill);
                }
                else
                {
                    AddIconTriangle(verts, toFill);
                }
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            
            StopAllGifCoroutines();
        }

        #endregion

        #region Private Methods

        private void ApplySingleSprite(Texture2D texture2D)
        {
            foreach (var vertex in _vertices)
            {
                // 左上顺时针到左下
                vertex[0].uv0 = new Vector4(0, 1);
                vertex[1].uv0 = new Vector4(1, 1);
                vertex[2].uv0 = new Vector4(1, 0);
                vertex[3].uv0 = new Vector4(0, 0);
            }

            texture = texture2D;
            SetAllDirty();
        }
        
        private RenderTexture CombineSprites(Dictionary<string, Sprite> icons)
        {
            textureWidth = 0;
            textureHeight = 0;

            (textureWidth, textureHeight) =
                CalcOffsetForCombine(icons, out var iconOffsetSizes, out var gifOffsetSizes);
#if UNITY_EDITOR
            Debug.Log($"Texture Size: {textureWidth}x{textureHeight}");
#endif
            var totalWidth = (float)textureWidth;
            var totalHeight = (float)textureHeight;
            // TODO: 当图超过支持的最大大小时，会创建一个支持的最大大小，需要计算缩放
            // 大概不用计算缩放
            var renderTexture = RenderTexture.GetTemporary(textureWidth, textureHeight);
            renderTexture.format = RenderTextureFormat.ARGB32;
            var prevRT = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);
            // Material blitMaterial = new Material(Shader.Find("Custom/TextureCombineUV"));
            if (!blitMaterial)
            {
                blitMaterial = new Material(Shader.Find("Custom/TextureCombineVert"));
            }

            var iconUvs = new Dictionary<string, Vector4>(icons.Count);
            foreach (var iconOffsetSize in iconOffsetSizes)
            {
                var offsetSize = iconOffsetSize.Value;
                var sprite = icons[iconOffsetSize.Key];
                var texture = sprite.texture;
                var spriteRect = sprite.rect;
                
                // 计算图标的UV坐标
                var uv = new Vector4((spriteRect.x + offsetSize.x) / totalWidth, (spriteRect.y + offsetSize.y) / totalHeight,
                    (offsetSize.z + offsetSize.x) / totalWidth, (offsetSize.w + offsetSize.y) / totalHeight);
                iconUvs.TryAddToDictionary(iconOffsetSize.Key, uv);
                
                // 计算 UV 偏移和缩放
                // 偏移和缩放均是指将texture的uv映射到renderTexture上，也就是计算rt（renderTexture）的uv对应的texture的uv值
                // texture的原始uv是[0, 1]，原封不动（scale = 1）时会铺满rt
                // 可以把uv的缩放和偏移按照数轴上的区间来理解
                // scale = rt.wdith / texture.wdith（height同理） 则是将uv缩放到与rt1：1的大小（[0,1]的uv拉伸到[0, scale]）
                // 此时[0,1]即是rt上从左开始的texture原始比例大小
                // [0, scale]在偏移(x, y)后，对应的就是[x, sclae + x]，[y, scale + y]
                // 而texture的采样只能从0到1（[0, 1]），所以当x<0时，texture对应的区间会右移（y同理）
                // 原本当uv<0或>1时，采样会有repeat、mirror、clamp等模式，使在这之外的区域被填充texture上对应模式下被采样到的像素
                // 但可以通过丢弃片元的方式放弃对该像素的渲染，即可保留原本的渲染像素
                // Vector2 offset = new Vector2(-currentX / spriteRect.width, 0);
                // Vector2 scale = new Vector2(totalWidth / spriteRect.width, maxHeight / spriteRect.height);
                
                // 计算顶点位置偏移和缩放
                // offset是从左到右的宽度比例
                var offsetScale = new Vector4(offsetSize.x / totalWidth, offsetSize.y / totalHeight, offsetSize.z / totalWidth, offsetSize.w / totalHeight);
                blitMaterial.SetVector(OffsetScale, offsetScale);
                Graphics.Blit(texture, renderTexture, blitMaterial);
            }

            var gifUvs = new Dictionary<string, Vector4>(gifOffsetSizes.Count);
            foreach (var gifOffsetSize in gifOffsetSizes)
            {
                var offsetSize = gifOffsetSize.Value;
                // 计算图标的UV坐标
                var uv = new Vector4(offsetSize.x / totalWidth, offsetSize.y / totalHeight,
                    (offsetSize.z + offsetSize.x) / totalWidth, (offsetSize.w + offsetSize.y) / totalHeight);
                gifUvs.TryAddToDictionary(gifOffsetSize.Key, uv);
            }

            for (int i = 0; i < _sprites.Count; i++)
            {
                var info = _sprites[i];
                var verts = _vertices[i];
                var uv = iconUvs[info.Content];
                // uv是x y左下， z w右上，但这里顶点实际上是从左上开始的顺时针
                verts[0].uv0 = new Vector2(uv.x, uv.w);
                verts[1].uv0 = new Vector2(uv.z, uv.w);
                verts[2].uv0 = new Vector2(uv.z, uv.y);
                verts[3].uv0 = new Vector2(uv.x, uv.y);
            }

            foreach (var gif in _gifs)
            {
                var uv = gifUvs[gif.Key.Content];
                var verts = gif.Value;
                verts[0].uv0 = new Vector2(uv.x, uv.w);
                verts[1].uv0 = new Vector2(uv.z, uv.w);
                verts[2].uv0 = new Vector2(uv.z, uv.y);
                verts[3].uv0 = new Vector2(uv.x, uv.y);

                var gifName = gif.Key.Content;
                GifLoadManager.Instance.LoadGif(gifName, frames =>
                {
                    if (gifCoroutines.ContainsKey(gifName))
                    {
                        return;
                    }

                    gifCoroutines.Add(gifName, StartCoroutine(PlayGif(gif.Key, frames)));
                });
            }

            RenderTexture.active = prevRT;
            return renderTexture;
        }

        /// <summary>
        /// 计算图像拼接时的偏移
        /// </summary>
        /// <param name="icons">所有的图标</param>
        /// <param name="iconOffsetSizes">图标的偏移+尺寸信息</param>
        /// <param name="gifOffsetSizes">gif的偏移+尺寸信息</param>
        /// <returns>图像的宽高</returns>
        private (int width, int height) CalcOffsetForCombine(Dictionary<string, Sprite> icons, out Dictionary<string, Vector4> iconOffsetSizes, out Dictionary<string, Vector4> gifOffsetSizes)
        {
            // 主要是担心icon和gif会存在重名，所以分开处理
            iconOffsetSizes = new(icons.Count);
            gifOffsetSizes = new(_gifs.Count);

            if (icons.Count == 0 && _gifs.Count == 0)
            {
#if UNITY_EDITOR
                Debug.Log($"Dont has icons and gifs");
#endif
                return (0, 0);
            }
            
            var totalWidth = 0;
            var maxHeight = 0;
            
            // 首先对gif进行去重+初始化
            foreach (var gif in _gifs)
            {
                var gifSize = GifLoadManager.Instance.GetGifSize(gif.Key.Content);
                gifOffsetSizes.TryAddToDictionary(gif.Key.Content, new Vector4(0, 0, gifSize.x, gifSize.y));
                totalWidth += gifSize.x;
                maxHeight = Mathf.Max(maxHeight, gifSize.y);
            }

            // 然后对icon进行初始化
            foreach (var icon in icons)
            {
                var width = icon.Value.rect.width;
                var height = icon.Value.rect.height;
                iconOffsetSizes.TryAddToDictionary(icon.Key, new Vector4(0, 0, width, height));
                totalWidth += (int)width;
                maxHeight = Mathf.Max(maxHeight, (int)height);
            }
            
            // 按行拼，计算需要有几行
            var rowCount = Mathf.Max(1, totalWidth / maxHeight);
            // n行需要至少有n个图
            var totalCount = iconOffsetSizes.Count + gifOffsetSizes.Count;
            if (totalCount < rowCount)
            {
                rowCount = totalCount;
            }

            // 先按照图像宽度排序，true=icon，false=gif
            var sortedIndexes = new LinkedList<(bool, string, Vector4)>();
            // 先拿icon排序
            foreach (var offsetSize in iconOffsetSizes)
            {
                if (sortedIndexes.Count == 0)
                {
                    sortedIndexes.AddFirst((true, offsetSize.Key, offsetSize.Value));
                }
                else
                {
                    var node = sortedIndexes.First;
                    bool isAdded = false;
                    do
                    {
                        if (node.Value.Item3.z < offsetSize.Value.z)
                        {
                            sortedIndexes.AddBefore(node, (true, offsetSize.Key, offsetSize.Value));
                            isAdded = true;
                            break;
                        }
                        else
                        {
                            node = node.Next;
                        }
                    } while (node != null);

                    if (!isAdded)
                    {
                        sortedIndexes.AddLast((true, offsetSize.Key, offsetSize.Value));
                    }
                }
            }
            
            // 再拿gif排序
            foreach (var offsetSize in gifOffsetSizes)
            {
                if (sortedIndexes.Count == 0)
                {
                    sortedIndexes.AddFirst((false, offsetSize.Key, offsetSize.Value));
                }
                else
                {
                    var node = sortedIndexes.First;
                    bool isAdded = false;
                    do
                    {
                        if (node.Value.Item3.z < offsetSize.Value.z)
                        {
                            sortedIndexes.AddBefore(node, (false, offsetSize.Key, offsetSize.Value));
                            isAdded = true;
                            break;
                        }
                        else
                        {
                            node = node.Next;
                        }
                    } while (node != null);
                    
                    if (!isAdded)
                    {
                        sortedIndexes.AddLast((false, offsetSize.Key, offsetSize.Value));
                    }
                }
            }
            
            // 合并计算位置
            var totalHeight = 0;
            totalWidth = 0;
            {
                // 每行的当前偏移(x y 当前行最高高度)
                var offsets = new Vector3[rowCount];
                // 先计算每一行的x偏移和当前行最高高度
                var currentRow = 0;
                var node = sortedIndexes.First;
                do
                {
                    var offset = offsets[currentRow];
                    var nodeValue = node.Value;
                    if (nodeValue.Item1)
                    {
                        var offsetSize = iconOffsetSizes[nodeValue.Item2];
                        offsetSize.x = offset.x;
                        iconOffsetSizes[nodeValue.Item2] = offsetSize;
                        // 更新x偏移
                        offset.x += offsetSize.z;
                        offset.z = Mathf.Max(offset.z, offsetSize.w);
                        offsets[currentRow] = offset;
                        totalWidth = Mathf.Max(totalWidth, (int)offset.x);
                    }
                    else
                    {
                        var offsetSize = gifOffsetSizes[nodeValue.Item2];
                        offsetSize.x = offset.x;
                        gifOffsetSizes[nodeValue.Item2] = offsetSize;
                        // 更新x偏移
                        offset.x += offsetSize.z;
                        offset.z = Mathf.Max(offset.z, offsetSize.w);
                        offsets[currentRow] = offset;
                        totalWidth = Mathf.Max(totalWidth, (int)offset.x);
                    }

                    currentRow = (currentRow + 1) % rowCount;
                    node = node.Next;
                } while (node != null);

                // 再计算y偏移
                for (int i = 1; i < offsets.Length; i++)
                {
                    var offset = offsets[i];
                    var lastOffset = offsets[i - 1];
                    offset.y = lastOffset.y + lastOffset.z;
                    offsets[i] = offset;
                }
                
                currentRow = 0;
                node = sortedIndexes.First;
                do
                {
                    var offset = offsets[currentRow];
                    var nodeValue = node.Value;
                    if (nodeValue.Item1)
                    {
                        var offsetSize = iconOffsetSizes[nodeValue.Item2];
                        offsetSize.y = offset.y;
                        iconOffsetSizes[nodeValue.Item2] = offsetSize;
                    }
                    else
                    {
                        var offsetSize = gifOffsetSizes[nodeValue.Item2];
                        offsetSize.y = offset.y;
                        gifOffsetSizes[nodeValue.Item2] = offsetSize;
                    }

                    currentRow = (currentRow + 1) % rowCount;
                    node = node.Next;
                } while (node != null);
                
                var topRowOffset = offsets[rowCount - 1];
                totalHeight = (int)(topRowOffset.y + topRowOffset.z);
            }

            return (totalWidth, totalHeight);
        }
        
        private void ApplySingleGif(RichInfo richInfo, UIVertex[] vertices)
        {
            // 左上顺时针到左下
            vertices[0].uv0 = new Vector4(0, 1);
            vertices[1].uv0 = new Vector4(1, 1);
            vertices[2].uv0 = new Vector4(1, 0);
            vertices[3].uv0 = new Vector4(0, 0);
            GifLoadManager.Instance.LoadGif(richInfo.Content, frames =>
            {
                gifCoroutines.Add(richInfo.Content, StartCoroutine(PlaySingleGif(frames)));
            });
        }

        private void ApplyIconShadow(UIVertex[] verts, VertexHelper toFill)
        {
            UIVertex vt;
            UIVertex[] shadowVerts = new UIVertex[4];
            for (int i = 0; i < 4; i++)
            {
                vt = verts[i];
                Vector3 v = vt.position;
                v.x += -1;
                v.y += 1;
                vt.position = v;
                var alpha = vt.color.a;
                vt.color = Color.black;
                vt.color.a = (byte)(alpha / 2);
                
                shadowVerts[i] = vt;
            }
            
            // 先加阴影再加图标本身
            AddIconTriangle(shadowVerts, toFill);
            AddIconTriangle(verts, toFill);
        }

        private void AddIconTriangle(UIVertex[] verts, VertexHelper toFill)
        {
            toFill.AddVert(verts[0]);
            toFill.AddVert(verts[1]);
            toFill.AddVert(verts[2]);
            toFill.AddVert(verts[3]);

            var vertCount = toFill.currentVertCount;
            // 添加图标的四个顶点所组成的矩形
            toFill.AddTriangle(vertCount - 4, vertCount - 3, vertCount - 2);
            toFill.AddTriangle(vertCount - 4, vertCount - 2, vertCount - 1);
        }

        /// <summary>
        /// 将uv通过数学关系转换为偏移和缩放
        /// </summary>
        /// <param name="uv">图像的uv</param>
        /// <param name="rect">图像sprite的rect</param>
        /// <returns>图像的渲染偏移和缩放，offset=(x, y)，scale=(z, w)</returns>
        private Vector4 UV2OffsetScale(Vector4 uv, Rect rect)
        {
            var os = new Vector4
            {
                // offset
                x = uv.x - rect.x / textureWidth,
                y = uv.y - rect.y / textureHeight,
                // scale
                z = uv.z - uv.x + rect.x / textureWidth,
                w = uv.w - uv.y + rect.y / textureHeight
            };

            return os;
        }
        
        /// <summary>
        /// 将uv通过数学关系转换为偏移和缩放（不含rect，即默认0偏移）
        /// </summary>
        /// <param name="uv">图像的uv</param>
        /// <returns>图像的渲染偏移和缩放，offset=(x, y)，scale=(z, w)</returns>
        private Vector4 UV2OffsetScale(Vector4 uv)
        {
            var os = new Vector4
            {
                // offset
                x = uv.x,
                y = uv.y,
                // scale
                z = uv.z - uv.x,
                w = uv.w - uv.y
            };

            return os;
        }
        
        /// <summary>
        /// 将uv通过数学关系转换为偏移和缩放（不含rect，即默认0偏移）
        /// </summary>
        /// <param name="uvX">图像的uv的x</param>
        /// <param name="uvY">图像的uv的y</param>
        /// <param name="uvZ">图像的uv的z</param>
        /// <param name="uvW">图像的uv的w</param>
        /// <returns>图像的渲染偏移和缩放，offset=(x, y)，scale=(z, w)</returns>
        private Vector4 UV2OffsetScale(float uvX, float uvY, float uvZ, float uvW)
        {
            var os = new Vector4
            {
                // offset
                x = uvX,
                y = uvY,
                // scale
                z = uvZ - uvX,
                w = uvW - uvY
            };

            return os;
        }

        private void StopAllGifCoroutines()
        {
            if (gifCoroutines == null)
            {
                return;
            }
            
            if (gifCoroutines.Count > 0)
            {
                foreach (var gifCoroutine in gifCoroutines.Where(gifCoroutine => gifCoroutine.Value != null))
                {
                    StopCoroutine(gifCoroutine.Value);
                }
            }
            
            gifCoroutines.Clear();
        }
        
        #endregion
        
        #region Coroutine

        IEnumerator PlaySingleGif(List<GifData> frames)
        {
            var index = 1;
            var countDown = 0f;
        
            texture = frames[0].FrameTexture;
        
            while (true)
            {
                if (index >= frames.Count)
                {
                    index %= frames.Count;
                }
            
                var frame = frames[index];
                if (countDown >= frame.DelaySecond)
                {
                    texture = frame.FrameTexture;
                    countDown -= frame.DelaySecond;
                    index++;
                }
            
                countDown += Time.deltaTime;
            
                yield return null;
            }
        }
        
        IEnumerator PlayGif(RichInfo gifInfo, List<GifData> frames)
        {
            var index = 1;
            var countDown = 0f;

            if (!_gifs.TryGetValue(gifInfo, out var verts))
            {
#if UNITY_EDITOR
                Debug.Log($"Can't Play Gif: {gifInfo.Content}, not found in _gifs");
#endif
                yield break;
            }

            var offsetScale = UV2OffsetScale(verts[3].uv0.x, verts[3].uv0.y, verts[1].uv0.x, verts[1].uv0.y);
#if UNITY_EDITOR
            Debug.Log($"Play Gif: {gifInfo.Content}, offset and scale: {offsetScale}");
#endif
            blitMaterial.SetVector(OffsetScale, offsetScale);
            Graphics.Blit(frames[0].FrameTexture, rt, blitMaterial);
        
            while (true)
            {
                if (index >= frames.Count)
                {
                    index %= frames.Count;
                }
            
                var frame = frames[index];
                if (countDown >= frame.DelaySecond)
                {
                    blitMaterial.SetVector(OffsetScale, offsetScale);
                    Graphics.Blit(frame.FrameTexture, rt, blitMaterial);
                    countDown -= frame.DelaySecond;
                    index++;
                }
            
                countDown += Time.deltaTime;
            
                yield return null;
            }
        }

        #endregion
    }
}