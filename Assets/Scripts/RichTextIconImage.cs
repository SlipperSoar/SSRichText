using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace SS.UIComponent
{
    /// <summary>
    /// 处理富文本的图标内容
    /// </summary>
    public class RichTextIconImage : Image
    {
        #region properties

        private List<RichText.RichInfo> _sprites;
        private List<UIVertex[]> _vertices;

        #endregion
        
        #region Public Methods

        public void SetIcons(List<RichText.RichInfo> iconInfos, Dictionary<string, Sprite> icons, List<UIVertex[]> vertices)
        {
            _sprites = iconInfos;
            _vertices = vertices;
            if (_sprites.Count == 1)
            {
                sprite = icons[_sprites[0].Content];
            }
            else
            {
                sprite = CombineSprites(icons);
            }

            SetVerticesDirty();
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
                
                toFill.AddVert(verts[0]);
                toFill.AddVert(verts[1]);
                toFill.AddVert(verts[2]);
                toFill.AddVert(verts[3]);

                var vertCount = toFill.currentVertCount;
                // 添加图标的四个顶点所组成的矩形
                toFill.AddTriangle(vertCount - 4, vertCount - 3, vertCount - 2);
                toFill.AddTriangle(vertCount - 4, vertCount - 2, vertCount - 1);
            }
        }

        #region Private Methods

        private Sprite CombineSprites(Dictionary<string, Sprite> icons)
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
            
            // 创建一个新的大纹理来存储所有图标
            Texture2D combinedTexture = new Texture2D(totalWidth, maxHeight);
            combinedTexture.filterMode = FilterMode.Bilinear;
            combinedTexture.wrapMode = TextureWrapMode.Repeat;

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
                iconUvs.TryAdd(kvp.Key, uv);
                
                // 将图标的像素数据拷贝到大纹理
                Color[] pixels = texture.GetPixels((int)spriteRect.x, (int)spriteRect.y, (int)spriteRect.width, (int)spriteRect.height);
                combinedTexture.SetPixels(currentX, 0, (int)spriteRect.width, (int)spriteRect.height, pixels);

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

            combinedTexture.Apply();  // 更新纹理
            
            // 使用合并后的纹理生成新的 Sprite
            var newSprite = Sprite.Create(combinedTexture, new Rect(0, 0, combinedTexture.width, combinedTexture.height), new Vector2(0.5f, 0.5f));

            return newSprite;
        }

        #endregion
    }
}