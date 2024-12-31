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

namespace SS.UIComponent
{
    /// <summary>
    /// 处理富文本的图标内容
    /// </summary>
    public class RichTextIconImage : RawImage
    {
        #region properties

        private List<RichText.RichInfo> _sprites;
        private List<UIVertex[]> _vertices;
        private Dictionary<RichText.RichInfo, UIVertex[]> _gifs;
        private LinkedList<int> _iconShadows;
        private static readonly int Offset = Shader.PropertyToID("_Offset");
        private static readonly int Scale = Shader.PropertyToID("_Scale");

        private int textureWidth;
        private int textureHeight;
        private RenderTexture rt;
        private List<Coroutine> gifCoroutines = new List<Coroutine>();
        private Material blitMaterial;

        #endregion
        
        #region Public Methods

        public void SetIcons(List<RichText.RichInfo> iconInfos, Dictionary<string, Sprite> icons, List<UIVertex[]> vertices, LinkedList<int> iconShadows, Dictionary<RichText.RichInfo, UIVertex[]> gifs)
        {
            _sprites = iconInfos;
            _vertices = vertices;
            _iconShadows = iconShadows;
            _gifs = gifs;
            // 先把上次的停了（如果有）
            StopAllGifCoroutines();
            if (_sprites.Count == 1 && _gifs.Count == 0)
            {
                texture = icons[_sprites[0].Content].texture;
            }
            else if (_sprites.Count == 0 && _gifs.Count == 1)
            {
                // 单个动图
                foreach (var gif in gifs)
                {
                    ApplySingleGif(gif.Key.Content, gif.Value);
                    break;
                }
            }
            else
            {
                rt = CombineSprites(icons);
                texture = rt;
            }
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

        private RenderTexture CombineSprites(Dictionary<string, Sprite> icons)
        {
            textureWidth = 0;
            textureHeight = 0;
            
            // 获取所有gif的尺寸
            foreach (var gif in _gifs)
            {
                // 这个方法会进行缓存，下面可以直接调用
                var gifSize = GifLoadManager.Instance.GetGifSize(gif.Key.Content);
                textureWidth += gifSize.x;
                textureHeight = Mathf.Max(textureHeight, gifSize.y);
            }

            // 获取所有图标的尺寸
            foreach (var kvp in icons)
            {
                var sprite = kvp.Value;
                textureWidth += (int)sprite.rect.width;
                textureHeight = Mathf.Max(textureHeight, (int)sprite.rect.height);
            }
            
            // textureWidth = totalWidth;
            // textureHeight = maxHeight;
            var totalWidth = (float)textureWidth;
            var maxHeight = (float)textureHeight;
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
            var index = 0;
            var currentX = 0;
            foreach (var kvp in icons)
            {
                var sprite = kvp.Value;
                var texture = sprite.texture;
                var spriteRect = sprite.rect;

                // 计算图标的UV坐标
                var uv = new Vector4((spriteRect.x + currentX) / totalWidth, spriteRect.y / maxHeight,
                    (spriteRect.width + currentX) / totalWidth, spriteRect.height / maxHeight);
                iconUvs.TryAddToDictionary(kvp.Key, uv);

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
                Vector2 offset = new Vector2(currentX / totalWidth, 0);
                Vector2 scale = new Vector2(spriteRect.width / totalWidth, spriteRect.height / maxHeight);
                
                blitMaterial.SetVector(Offset, offset);
                blitMaterial.SetVector(Scale, scale);
                Graphics.Blit(texture, renderTexture, blitMaterial);

                currentX += (int)spriteRect.width; // 更新x坐标
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
            
            var gifUvs = new Dictionary<string, Vector4>(_gifs.Count);
            foreach (var kvp in _gifs)
            {
                var gifName = kvp.Key.Content;
                var gifSize = GifLoadManager.Instance.GetGifSize(gifName);
                var uv = new Vector4(currentX / totalWidth, 0,
                    (gifSize.x + currentX) / totalWidth, gifSize.y / maxHeight);
                gifUvs.TryAddToDictionary(gifName, uv);
                currentX += gifSize.x; // 更新x坐标
            }

            foreach (var gif in _gifs)
            {
                var uv = gifUvs[gif.Key.Content];
                var verts = gif.Value;
                // uv是x y左下， z w右上，但这里顶点实际上是从左上开始的顺时针
                verts[0].uv0 = new Vector2(uv.x, uv.w);
                verts[1].uv0 = new Vector2(uv.z, uv.w);
                verts[2].uv0 = new Vector2(uv.z, uv.y);
                verts[3].uv0 = new Vector2(uv.x, uv.y);
                
                GifLoadManager.Instance.LoadGif(gif.Key.Content, frames =>
                {
                    gifCoroutines.Add(StartCoroutine(PlayGif(gif.Key, frames)));
                });
            }

            RenderTexture.active = prevRT;
            return renderTexture;
        }

        private void ApplySingleGif(string gifName, UIVertex[] vertices)
        {
            GifLoadManager.Instance.LoadGif(gifName, frames =>
            {
                gifCoroutines.Add(StartCoroutine(PlaySingleGif(frames)));
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
                y = 0,
                // scale
                z = uv.z - uv.x + rect.x / textureWidth,
                w = uv.w
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
                y = 0,
                // scale
                z = uv.z - uv.x,
                w = uv.w
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
                y = 0,
                // scale
                z = uvZ - uvX,
                w = uvW
            };

            return os;
        }

        private void StopAllGifCoroutines()
        {
            if (gifCoroutines.Count > 0)
            {
                foreach (var gifCoroutine in gifCoroutines)
                {
                    StopCoroutine(gifCoroutine);
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
        
        IEnumerator PlayGif(RichText.RichInfo gifInfo, List<GifData> frames)
        {
            var index = 1;
            var countDown = 0f;

            var verts = _gifs[gifInfo];
            var offsetScale = UV2OffsetScale(verts[3].uv0.x, verts[3].uv0.y, verts[1].uv0.x, verts[1].uv0.y);
            
#if UNITY_EDITOR
            Debug.Log($"Play Gif: {gifInfo.Content}, offset and scale: {offsetScale}");
#endif
            
            var offset = new Vector2(offsetScale.x, offsetScale.y);
            var scale = new Vector2(offsetScale.z, offsetScale.w);
            blitMaterial.SetVector(Offset, offset);
            blitMaterial.SetVector(Scale, scale);
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
                    blitMaterial.SetVector(Offset, offset);
                    blitMaterial.SetVector(Scale, scale);
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