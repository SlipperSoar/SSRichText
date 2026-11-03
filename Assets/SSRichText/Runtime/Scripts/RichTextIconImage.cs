/*
 * @author SlipperSoar
 * @Created: 2024-12-08
 * @description Unity Text富文本扩展用的RawImage扩展
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using RichInfo = SS.UIComponent.RichText.RichInfo;
using ShadowRichInfo = SS.UIComponent.RichText.ShadowRichInfo;

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
        private Dictionary<string, Vector4> _gifOffsetSclaes = new Dictionary<string, Vector4>();
        private Dictionary<int, ShadowRichInfo> _iconShadows;
        private static readonly int OffsetScale = Shader.PropertyToID("_OffsetScale");
        private static readonly int SrcScaleId = Shader.PropertyToID("_SrcScale");
        private static readonly int SrcOffsetId = Shader.PropertyToID("_SrcOffset");

        private int textureWidth;
        private int textureHeight;
        private RenderTexture rt;
        private Material blitMaterial;
        private Material replaceMaterial;

        // 图标/动图集合没变时直接复用合并纹理，避免每次文本重建都重新 Blit
        private string combineSignature;
        private Dictionary<string, Vector4> cachedIconUvs;
        private Dictionary<string, Vector4> cachedGifUvs;
        private bool usingSingleGif;

        // 静态图标集合跨实例共享同一张图集：同一组表情在多个 RichText 上只合成一次
        private static readonly Dictionary<string, SharedAtlas> SharedAtlases =
            new Dictionary<string, SharedAtlas>(StringComparer.Ordinal);

        private string sharedKey;
        private SharedAtlas sharedAtlas;

        private sealed class SharedAtlas
        {
            public RenderTexture Texture;
            public int Refs;
            public Dictionary<string, Vector4> IconUvs;
        }

        private sealed class AtlasItem
        {
            public string Id;
            public int Width;
            public int Height;
            public int X;
            public int Y;
            public readonly List<AtlasContent> Contents = new List<AtlasContent>();
        }

        private struct AtlasContent
        {
            public readonly string Content;
            public readonly bool IsGif;

            public AtlasContent(string content, bool isGif)
            {
                Content = content;
                IsGif = isGif;
            }
        }

        #endregion
        
        #region Public Methods

        public void SetIcons(List<RichInfo> iconInfos, Dictionary<string, Sprite> icons, List<UIVertex[]> vertices, Dictionary<int, ShadowRichInfo> iconShadows, Dictionary<RichInfo, UIVertex[]> gifs)
        {
            _sprites = iconInfos;
            _vertices = vertices;
            _iconShadows = iconShadows;
            // 先把上次的停了（如果有）
            StopAllGifs();
            _gifs = gifs;
            // 一口气加载
            if (Application.isPlaying)
            {
                PrepareGifs();
            }

            if (_sprites.Count == 0 && _gifs.Count == 0)
            {
                ReleaseSharedAtlas();
                ReleaseRenderTexture();
                texture = null;
                SetAllDirty();
                return;
            }

            // 单个“整图”Sprite：不需要图集，直接用原图（最省的一条路）
            if (_sprites.Count == 1 && _gifs.Count == 0)
            {
                if (icons.TryGetValue(_sprites[0].Content, out var single) &&
                    single != null && IsFullTextureSprite(single))
                {
                    ReleaseSharedAtlas();
                    ReleaseRenderTexture();
                    ApplySingleSprite(single.texture);
                    return;
                }
            }

            var signature = BuildCombineSignature(icons);
            var canShare = _gifs.Count == 0;

            // 纯静态图标：同一集合优先复用/共享图集
            if (canShare)
            {
                if (sharedAtlas != null && sharedKey == signature && TryApplySharedAtlas())
                {
                    texture = sharedAtlas.Texture;
                    SetAllDirty();
                    return;
                }

                ReleaseRenderTexture();
                ReleaseSharedAtlas();

                if (SharedAtlases.TryGetValue(signature, out var cached) && cached.Texture != null)
                {
                    cached.Refs++;
                    sharedAtlas = cached;
                    sharedKey = signature;
                    ApplyUvsFrom(cached.IconUvs);
                    texture = cached.Texture;
                    SetAllDirty();
                    return;
                }

                rt = CombineSprites(icons);
                if (rt == null)
                {
                    ReleaseSharedAtlas();
                    texture = null;
                    SetAllDirty();
                    return;
                }

                sharedAtlas = new SharedAtlas
                {
                    Texture = rt,
                    Refs = 1,
                    IconUvs = cachedIconUvs ?? new Dictionary<string, Vector4>()
                };
                sharedKey = signature;
                SharedAtlases[signature] = sharedAtlas;

                // 所有权转移给共享图集：之后由 ReleaseSharedAtlas 释放
                texture = rt;
                rt = null;
                SetAllDirty();
                return;
            }

            // 含 GIF：图集独立（GIF 逐帧写入，不能跨实例共享）
            ReleaseSharedAtlas();
            if (rt != null && combineSignature == signature && TryApplyCachedCombine())
            {
                texture = rt;
                SetAllDirty();
                return;
            }

            ReleaseRenderTexture();
            rt = CombineSprites(icons);
            combineSignature = rt != null ? signature : null;
            texture = rt;
            SetAllDirty();
        }

        public void Clear()
        {
            StopAllGifs();
            ReleaseSharedAtlas();
            ReleaseRenderTexture();
            texture = null;
            _vertices = null;
            _sprites = null;
            _gifs = null;
            _iconShadows = null;
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

                if (_iconShadows != null && _iconShadows.TryGetValue(info.StartIndex, out var shadowRichInfo))
                {
                    ApplyIconShadow(shadowRichInfo, verts, toFill);
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

                if (_iconShadows != null && _iconShadows.TryGetValue(info.StartIndex, out var shadowRichInfo))
                {
                    ApplyIconShadow(shadowRichInfo, verts, toFill);
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

            StopAllGifs();
            ReleaseSharedAtlas();
            ReleaseRenderTexture();
            DestroyNativeObject(blitMaterial);
            blitMaterial = null;
            DestroyNativeObject(replaceMaterial);
            replaceMaterial = null;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// 释放合并图标/动图时申请的临时 RenderTexture
        /// </summary>
        private void ReleaseRenderTexture()
        {
            combineSignature = null;
            cachedIconUvs = null;
            cachedGifUvs = null;
            _gifOffsetSclaes.Clear();

            if (rt == null)
            {
                return;
            }

            if (texture == rt)
            {
                texture = null;
            }

            RenderTexture.ReleaseTemporary(rt);
            rt = null;
        }

        /// <summary>
        /// 归还共享图集的引用，引用归零时销毁纹理
        /// </summary>
        private void ReleaseSharedAtlas()
        {
            if (sharedAtlas == null)
            {
                return;
            }

            if (texture == sharedAtlas.Texture)
            {
                texture = null;
            }

            sharedAtlas.Refs--;
            if (sharedAtlas.Refs <= 0)
            {
                if (sharedAtlas.Texture != null)
                {
                    RenderTexture.ReleaseTemporary(sharedAtlas.Texture);
                }

                if (!string.IsNullOrEmpty(sharedKey) &&
                    SharedAtlases.TryGetValue(sharedKey, out var registered) &&
                    ReferenceEquals(registered, sharedAtlas))
                {
                    SharedAtlases.Remove(sharedKey);
                }
            }

            sharedAtlas = null;
            sharedKey = null;
        }

        private bool TryApplySharedAtlas()
        {
            if (sharedAtlas == null || sharedAtlas.Texture == null)
            {
                return false;
            }

            return ApplyUvsFrom(sharedAtlas.IconUvs);
        }

        private bool ApplyUvsFrom(Dictionary<string, Vector4> uvs)
        {
            if (uvs == null || _sprites == null || _vertices == null)
            {
                return false;
            }

            for (var i = 0; i < _sprites.Count; i++)
            {
                if (!uvs.ContainsKey(_sprites[i].Content))
                {
                    return false;
                }
            }

            for (var i = 0; i < _sprites.Count; i++)
            {
                ApplyUv(_vertices[i], uvs[_sprites[i].Content]);
            }

            return true;
        }

        /// <summary>
        /// 图标集合 + 动图集合的签名，用于判断合并纹理能否复用
        /// </summary>
        private string BuildCombineSignature(Dictionary<string, Sprite> icons)
        {
            var parts = new List<string>(_sprites.Count + _gifs.Count + 1);
            foreach (var info in _sprites)
            {
                if (!icons.TryGetValue(info.Content, out var sprite) || sprite == null)
                {
                    continue;
                }

                parts.Add(info.Content + ":" + sprite.GetInstanceID());
            }

            parts.Sort(StringComparer.Ordinal);
            parts.Add("#");

            var gifParts = new List<string>(_gifs.Count);
            foreach (var gif in _gifs)
            {
                gifParts.Add(gif.Key.Content);
            }

            gifParts.Sort(StringComparer.Ordinal);
            parts.AddRange(gifParts);
            return string.Join("|", parts);
        }

        /// <summary>
        /// 复用合并纹理时，把缓存的 UV 重新应用到新的顶点上
        /// </summary>
        private bool TryApplyCachedCombine()
        {
            if (cachedIconUvs == null || cachedGifUvs == null)
            {
                return false;
            }

            usingSingleGif = false;
            for (var i = 0; i < _sprites.Count; i++)
            {
                if (!cachedIconUvs.TryGetValue(_sprites[i].Content, out var uv))
                {
                    return false;
                }

                ApplyUv(_vertices[i], uv);
            }

            foreach (var gif in _gifs)
            {
                if (!cachedGifUvs.TryGetValue(gif.Key.Content, out var uv))
                {
                    return false;
                }

                ApplyUv(gif.Value, uv);
                if (Application.isPlaying)
                {
                    RefreshGifOffsetScale(gif.Key);
                    GifLoadManager.Instance.AddGifPlayer(gif.Key.Content, PlayGif);
                }
            }

            return true;
        }

        private static void ApplyUv(UIVertex[] verts, Vector4 uv)
        {
            verts[0].uv0 = new Vector2(uv.x, uv.w);
            verts[1].uv0 = new Vector2(uv.z, uv.w);
            verts[2].uv0 = new Vector2(uv.z, uv.y);
            verts[3].uv0 = new Vector2(uv.x, uv.y);
        }

        private static void DestroyNativeObject(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(obj);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        private void PrepareGifs()
        {
            if (_gifs == null)
            {
                return;
            }

            foreach (var gif in _gifs)
            {
                GifLoadManager.Instance.PrepareGif(gif.Key.Content);
            }
        }
        
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

        /// <summary>
        /// Sprite 是否就是整张贴图（只有这种才能免图集直接用）
        /// </summary>
        private static bool IsFullTextureSprite(Sprite sprite)
        {
            if (sprite.packed)
            {
                return false;
            }

            var spriteTexture = sprite.texture;
            if (spriteTexture == null)
            {
                return false;
            }

            var rect = sprite.textureRect;
            return Mathf.Approximately(rect.x, 0f) &&
                   Mathf.Approximately(rect.y, 0f) &&
                   Mathf.Approximately(rect.width, spriteTexture.width) &&
                   Mathf.Approximately(rect.height, spriteTexture.height);
        }

        private RenderTexture CombineSprites(Dictionary<string, Sprite> icons)
        {
            textureWidth = 0;
            textureHeight = 0;

            (textureWidth, textureHeight) =
                CalcOffsetForCombine(icons, out var iconOffsetSizes, out var gifOffsetSizes);

            if (textureWidth <= 0 || textureHeight <= 0)
            {
                return null;
            }

            var renderTexture = RenderTexture.GetTemporary(textureWidth, textureHeight, 0, RenderTextureFormat.ARGB32);
            var prevRT = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);

            if (!blitMaterial)
            {
                blitMaterial = new Material(Shader.Find("Custom/TextureCombineVert"));
            }

            var totalWidth = (float)textureWidth;
            var totalHeight = (float)textureHeight;
            var iconUvs = new Dictionary<string, Vector4>(iconOffsetSizes.Count);
            var drawnSprites = new HashSet<int>();

            foreach (var iconOffsetSize in iconOffsetSizes)
            {
                var offsetSize = iconOffsetSize.Value;
                var uv = new Vector4(offsetSize.x / totalWidth, offsetSize.y / totalHeight,
                    (offsetSize.z + offsetSize.x) / totalWidth, (offsetSize.w + offsetSize.y) / totalHeight);
                iconUvs.TryAddToDictionary(iconOffsetSize.Key, uv);

                if (!icons.TryGetValue(iconOffsetSize.Key, out var sprite) || sprite == null)
                {
                    continue;
                }

                // 同一个 Sprite 被多处引用时只合成一次
                if (!drawnSprites.Add(sprite.GetInstanceID()))
                {
                    continue;
                }

                var spriteTexture = sprite.texture;
                if (spriteTexture == null)
                {
                    continue;
                }

                // 只取 Sprite 自己的区域，图集/多子图里的 Sprite 也能正确合成
                var spriteRect = sprite.textureRect;
                blitMaterial.SetVector(SrcScaleId, new Vector4(
                    spriteRect.width / spriteTexture.width,
                    spriteRect.height / spriteTexture.height, 0f, 0f));
                blitMaterial.SetVector(SrcOffsetId, new Vector4(
                    spriteRect.x / spriteTexture.width,
                    spriteRect.y / spriteTexture.height, 0f, 0f));
                blitMaterial.SetVector(OffsetScale, new Vector4(
                    offsetSize.x / totalWidth, offsetSize.y / totalHeight,
                    offsetSize.z / totalWidth, offsetSize.w / totalHeight));
                Graphics.Blit(spriteTexture, renderTexture, blitMaterial);
            }

            var gifUvs = new Dictionary<string, Vector4>(gifOffsetSizes.Count);
            foreach (var gifOffsetSize in gifOffsetSizes)
            {
                var offsetSize = gifOffsetSize.Value;
                var uv = new Vector4(offsetSize.x / totalWidth, offsetSize.y / totalHeight,
                    (offsetSize.z + offsetSize.x) / totalWidth, (offsetSize.w + offsetSize.y) / totalHeight);
                gifUvs.TryAddToDictionary(gifOffsetSize.Key, uv);
            }

            for (int i = 0; i < _sprites.Count; i++)
            {
                if (iconUvs.TryGetValue(_sprites[i].Content, out var uv))
                {
                    ApplyUv(_vertices[i], uv);
                }
            }

            usingSingleGif = false;
            foreach (var gif in _gifs)
            {
                if (!gifUvs.TryGetValue(gif.Key.Content, out var uv))
                {
                    continue;
                }

                ApplyUv(gif.Value, uv);

                if (Application.isPlaying)
                {
                    RefreshGifOffsetScale(gif.Key);
                    GifLoadManager.Instance.AddGifPlayer(gif.Key.Content, PlayGif);
                }
            }

            RenderTexture.active = prevRT;
            cachedIconUvs = iconUvs;
            cachedGifUvs = gifUvs;
            return renderTexture;
        }

        /// <summary>
        /// 计算图像拼接时的偏移与尺寸。
        /// 采用 shelf packing，并按 SystemInfo.maxTextureSize 限制尺寸，避免拼出硬件不支持的超大图集。
        /// </summary>
        /// <param name="icons">所有的图标</param>
        /// <param name="iconOffsetSizes">图标的偏移+尺寸信息</param>
        /// <param name="gifOffsetSizes">gif的偏移+尺寸信息</param>
        /// <returns>图像的宽高</returns>
        private (int width, int height) CalcOffsetForCombine(Dictionary<string, Sprite> icons, out Dictionary<string, Vector4> iconOffsetSizes, out Dictionary<string, Vector4> gifOffsetSizes)
        {
            iconOffsetSizes = new Dictionary<string, Vector4>(icons.Count);
            gifOffsetSizes = new Dictionary<string, Vector4>(_gifs.Count);

            if (icons.Count == 0 && _gifs.Count == 0)
            {
                return (0, 0);
            }

            var maxSize = Mathf.Min(SystemInfo.maxTextureSize, 8192);
            var items = new List<AtlasItem>(icons.Count + _gifs.Count);
            var byId = new Dictionary<string, AtlasItem>(StringComparer.Ordinal);

            // 先按 Sprite/GIF 去重：同一张贴图只占一块位置
            foreach (var icon in icons)
            {
                var sprite = icon.Value;
                if (sprite == null)
                {
                    continue;
                }

                var id = "i" + sprite.GetInstanceID();
                if (!byId.TryGetValue(id, out var item))
                {
                    var rect = sprite.rect;
                    item = new AtlasItem
                    {
                        Id = id,
                        Width = Mathf.Max(1, Mathf.RoundToInt(rect.width)),
                        Height = Mathf.Max(1, Mathf.RoundToInt(rect.height))
                    };
                    byId.Add(id, item);
                    items.Add(item);
                }

                item.Contents.Add(new AtlasContent(icon.Key, false));
            }

            foreach (var gif in _gifs)
            {
                var id = "g" + gif.Key.Content;
                if (!byId.TryGetValue(id, out var item))
                {
                    var size = GifLoadManager.Instance.GetGifSize(gif.Key.Content);
                    item = new AtlasItem
                    {
                        Id = id,
                        Width = Mathf.Max(1, size.x),
                        Height = Mathf.Max(1, size.y)
                    };
                    byId.Add(id, item);
                    items.Add(item);
                }

                item.Contents.Add(new AtlasContent(gif.Key.Content, true));
            }

            if (items.Count == 0)
            {
                return (0, 0);
            }

            // 按高度降序排列，shelf packing 的碎片更少
            items.Sort((a, b) => b.Height.CompareTo(a.Height));

            long area = 0;
            foreach (var item in items)
            {
                area += (long)item.Width * item.Height;
            }

            var targetWidth = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Sqrt(area))), 64, maxSize);

            var x = 0;
            var y = 0;
            var rowHeight = 0;
            var usedWidth = 0;

            foreach (var item in items)
            {
                var w = item.Width;
                var h = item.Height;

                if (x > 0 && x + w > targetWidth)
                {
                    y += rowHeight;
                    x = 0;
                    rowHeight = 0;
                }

                item.X = x;
                item.Y = y;
                x += w;
                rowHeight = Mathf.Max(rowHeight, h);
                usedWidth = Mathf.Max(usedWidth, x);
            }

            var totalWidth = Mathf.Max(1, usedWidth);
            var totalHeight = Mathf.Max(1, y + rowHeight);

            // 超出硬件上限时整体等比缩小：图标会被降采样，但布局与 UV 仍然一致
            var scale = 1f;
            if (totalWidth > maxSize || totalHeight > maxSize)
            {
                scale = Mathf.Min((float)maxSize / totalWidth, (float)maxSize / totalHeight);
                Debug.LogWarning($"[RichText] Icon atlas {totalWidth}x{totalHeight} exceeds {maxSize}, scaled to {scale:0.###}.");
            }

            foreach (var item in items)
            {
                var rect = new Vector4(item.X * scale, item.Y * scale, item.Width * scale, item.Height * scale);
                foreach (var content in item.Contents)
                {
                    if (content.IsGif)
                    {
                        gifOffsetSizes[content.Content] = rect;
                    }
                    else
                    {
                        iconOffsetSizes[content.Content] = rect;
                    }
                }
            }

            return (Mathf.Max(1, Mathf.RoundToInt(totalWidth * scale)), Mathf.Max(1, Mathf.RoundToInt(totalHeight * scale)));
        }
        
        private void ApplySingleGif(RichInfo richInfo, UIVertex[] vertices)
        {
            // 左上顺时针到左下
            vertices[0].uv0 = new Vector4(0, 1);
            vertices[1].uv0 = new Vector4(1, 1);
            vertices[2].uv0 = new Vector4(1, 0);
            vertices[3].uv0 = new Vector4(0, 0);
            usingSingleGif = true;
            if (Application.isPlaying)
            {
                GifLoadManager.Instance.AddGifPlayer(richInfo.Content, PlaySingleGif);
            }
        }

        private void ApplyIconShadow(ShadowRichInfo shadowRichInfo, UIVertex[] verts, VertexHelper toFill)
        {
            UIVertex vt;
            UIVertex[] shadowVerts = new UIVertex[4];
            // 单个字符的四个顶点为[start, end)
            var vt1 = verts[0];
            var vt2 = verts[2];
            // 从左上顺时针到左下，vt1.y > vt2.y
            var offset = Mathf.Max(1, (vt1.position.y - vt2.position.y) / 16);
            for (int i = 0; i < 4; i++)
            {
                vt = verts[i];
                Vector3 v = vt.position;
                v.x += offset * (shadowRichInfo.IsXPositive ? 1 : -1);
                v.y += offset * (shadowRichInfo.IsYPositive ? 1 : -1);
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

        private void StopAllGifs()
        {
            if (_gifs == null)
            {
                return;
            }

            // 用记录的模式移除，避免“单 GIF + 图标”走图集路径时移除错误的回调
            if (usingSingleGif)
            {
                foreach (var gif in _gifs)
                {
                    GifLoadManager.Instance.RemoveGifPlayer(gif.Key.Content, PlaySingleGif);
                }
            }
            else
            {
                foreach (var gif in _gifs)
                {
                    GifLoadManager.Instance.RemoveGifPlayer(gif.Key.Content, PlayGif);
                }
            }
        }

        private void PlaySingleGif(string gifName, GifData gifData)
        {
            texture = gifData.FrameTexture;
        }

        private void RefreshGifOffsetScale(RichInfo gifInfo)
        {
            if (!_gifs.TryGetValue(gifInfo, out var verts))
            {
                Debug.LogWarning($"Can't play GIF '{gifInfo.Content}': not found in the current icon image.");
                return;
            }

            var offsetScale = UV2OffsetScale(verts[3].uv0.x, verts[3].uv0.y, verts[1].uv0.x, verts[1].uv0.y);
            _gifOffsetSclaes[gifInfo.Content] = offsetScale;
        }

        private void PlayGif(string gifName, GifData gifData)
        {
            if (rt == null || !_gifOffsetSclaes.TryGetValue(gifName, out var offsetScale))
            {
                return;
            }

            // 用不混合的材质整块覆盖 GIF 子区域：否则半透明帧会和上一帧叠加出残影
            if (replaceMaterial == null)
            {
                replaceMaterial = new Material(Shader.Find("Custom/TextureCombineReplace"));
            }

            replaceMaterial.SetVector(OffsetScale, offsetScale);
            replaceMaterial.SetVector(SrcScaleId, new Vector4(1f, 1f, 0f, 0f));
            replaceMaterial.SetVector(SrcOffsetId, Vector4.zero);
            Graphics.Blit(gifData.FrameTexture, rt, replaceMaterial);
        }

        #endregion
    }
}
