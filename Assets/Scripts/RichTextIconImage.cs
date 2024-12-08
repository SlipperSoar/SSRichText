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

        private List<Sprite> _sprites;
        private List<UIVertex[]> _vertices;

        #endregion
        
        #region Public Methods

        public void SetIcons(List<Sprite> sprites, List<UIVertex[]> vertices)
        {
            _sprites = sprites;
            _vertices = vertices;
            if (_sprites.Count == 1)
            {
                sprite = _sprites[0];
            }
            else
            {
                sprite = CombineSprites();
            }

            SetVerticesDirty();
        }
        
        public void SetIcons(List<(Sprite, UIVertex[])> sprites)
        {
            _sprites = sprites.Select(x => x.Item1).ToList();
            _vertices = sprites.Select(x => x.Item2).ToList();
            if (_sprites.Count == 1)
            {
                sprite = _sprites[0];
            }
            else
            {
                sprite = CombineSprites();
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
                var verts = _vertices[i];
                
                verts[0].color = color;
                verts[1].color = color;
                verts[2].color = color;
                verts[3].color = color;
                
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

        private Sprite CombineSprites()
        {
            // 获取所有图标的尺寸
            var totalWidth = 0;
            var maxHeight = 0;
            
            foreach (var sprite in _sprites)
            {
                totalWidth += (int)sprite.rect.width;
                maxHeight = Mathf.Max(maxHeight, (int)sprite.rect.height);
            }
            
            // 创建一个新的大纹理来存储所有图标
            Texture2D combinedTexture = new Texture2D(totalWidth, maxHeight);
            combinedTexture.filterMode = FilterMode.Bilinear;
            combinedTexture.wrapMode = TextureWrapMode.Repeat;
            
            var currentX = 0;
            for (int i = 0; i < _sprites.Count; i++)
            {
                var sprite = _sprites[i];
                var texture = sprite.texture;
                var spriteRect = sprite.rect;

                // 计算图标的UV坐标
                var uv = new Vector4((spriteRect.x + currentX) / totalWidth, spriteRect.y / maxHeight,
                    (spriteRect.width + currentX) / totalWidth, spriteRect.height / maxHeight);
                var verts = _vertices[i];
                // uv是x y左下， z w右上，但这里顶点实际上是从左上开始的顺时针
                verts[0].uv0 = new Vector2(uv.x, uv.w);
                verts[1].uv0 = new Vector2(uv.z, uv.w);
                verts[2].uv0 = new Vector2(uv.z, uv.y);
                verts[3].uv0 = new Vector2(uv.x, uv.y);

                // 将图标的像素数据拷贝到大纹理
                Color[] pixels = texture.GetPixels((int)spriteRect.x, (int)spriteRect.y, (int)spriteRect.width, (int)spriteRect.height);
                combinedTexture.SetPixels(currentX, 0, (int)spriteRect.width, (int)spriteRect.height, pixels);

                currentX += (int)spriteRect.width; // 更新x坐标
            }

            combinedTexture.Apply();  // 更新纹理
            
            // 使用合并后的纹理生成新的 Sprite
            var newSprite = Sprite.Create(combinedTexture, new Rect(0, 0, combinedTexture.width, combinedTexture.height), new Vector2(0.5f, 0.5f));

            return newSprite;
        }

        #endregion
    }
}