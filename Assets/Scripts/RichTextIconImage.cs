/*
 * @author SlipperSoar
 * @Created: 2024-12-08
 * @description Unity Text富文本扩展用的RawImage扩展
 */

using System.Collections.Generic;
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
        private LinkedList<int> _iconShadows;
        private static readonly int Offset = Shader.PropertyToID("_Offset");
        private static readonly int Scale = Shader.PropertyToID("_Scale");

        private int textureWidth;
        private int textureHeight;
        private RenderTexture rt;

        #endregion
        
        #region Public Methods

        public void SetIcons(List<RichText.RichInfo> iconInfos, Dictionary<string, Sprite> icons, List<UIVertex[]> vertices, LinkedList<int> iconShadows)
        {
            _sprites = iconInfos;
            _vertices = vertices;
            _iconShadows = iconShadows;
            if (_sprites.Count == 1)
            {
                texture = icons[_sprites[0].Content].texture;
            }
            else
            {
                rt = CombineSprites(icons);
                texture = rt;
            }
        }

        #endregion
        
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
        }

        #region Private Methods

        private RenderTexture CombineSprites(Dictionary<string, Sprite> icons)
        {
            // 获取所有图标的尺寸
            var totalWidth = 0;
            var maxHeight = 0;

            foreach (var kvp in icons)
            {
                var sprite = kvp.Value;
                totalWidth += (int)sprite.rect.width;
                maxHeight = Mathf.Max(maxHeight, (int)sprite.rect.height);
            }
            
            textureWidth = totalWidth;
            textureHeight = maxHeight;
            var renderTexture = RenderTexture.GetTemporary(totalWidth, maxHeight);
            renderTexture.format = RenderTextureFormat.ARGB32;
            var prevRT = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);
            // Material blitMaterial = new Material(Shader.Find("Custom/TextureCombineUV"));
            Material blitMaterial = new Material(Shader.Find("Custom/TextureCombineVert"));

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
                Vector2 offset = new Vector2(currentX / (float)totalWidth, 0);
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

            RenderTexture.active = prevRT;
            return renderTexture;
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
        
        #endregion
    }
}