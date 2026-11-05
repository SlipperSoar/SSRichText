/*
 * @author SlipperSoar
 * @Created: 2024-12-07
 * @description Unity Text富文本扩展
 */

using System;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SS.UIComponent
{
    /// <summary>
    /// 对富文本支持更广泛的 UI 文本组件。
    /// 文本解析见 RichText.Parser.cs，网格构建见 RichText.Mesh.cs，数据类型见 RichText.Types.cs。
    /// </summary>
    [AddComponentMenu("UI/SS/RichText")]
    public partial class RichText : Text, IPointerClickHandler
    {

        public event Action<RichType, string> OnClick;

        // 占位字符，到时候要将alpha设置为0
        const string IconReplaceChar = "\u25a0"; //■
        readonly UIVertex[] m_TempVerts = new UIVertex[4];

        public static IIconProvider GlobalIconProvider = new IconProvider();

        private IIconProvider _iconProvider;

        private IIconProvider iconProvider
        {
            get
            {
                if (_iconProvider == null)
                {
                    _iconProvider = GlobalIconProvider;
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

        private static Sprite _whiteQuad;
        private const string UnderlineSpriteName = "<underline>";
        private Vector4[] lineUVs;

        private List<RichInfo> richInfos;

        // 解析缓存：文本没变时复用解析结果，避免重复执行正则和标签处理
        private string parsedText;
        private string parsedResultText;
        private string parsedTextWithoutTag;
        private List<RichInfo> parsedRichInfos;

        // 重建容器复用：active 里的容器仍被图标层/点击检测持有，
        // 只有被新一帧替换后才回到池中。打字机式逐帧重建不再每帧新建一套 List。
        private readonly Stack<RichTextRebuildBuffers> m_BufferPool = new Stack<RichTextRebuildBuffers>();
        private RichTextRebuildBuffers m_PendingBuffers;   // 本次重建正在填充
        private RichTextRebuildBuffers m_ActiveBuffers;    // 已交给图标层，不可回池
        private bool iconUpdatePending;


#if UNITY_EDITOR
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
#endif



        protected override void OnEnable()
        {
            base.OnEnable();
            Canvas.willRenderCanvases += FlushIconUpdate;
        }

        protected override void OnDisable()
        {
            Canvas.willRenderCanvases -= FlushIconUpdate;
            iconUpdatePending = false;
            // 图标层组件仍在，只是 GameObject 失活：必须先让它解除对容器/顶点的引用，
            // 否则回池复用时会读到被清空或被覆写的数据。
            if (_iconImage != null)
                _iconImage.Clear();
            ReleaseActiveBuffers();
            m_PendingBuffers = null;
            richInfos = null;
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            Canvas.willRenderCanvases -= FlushIconUpdate;
            if (_iconImage != null)
                _iconImage.Clear();
            ReleaseActiveBuffers();
            m_PendingBuffers = null;
            m_BufferPool.Clear();
            base.OnDestroy();
        }

        private RichTextRebuildBuffers RentBuffers()
        {
            var buffers = m_BufferPool.Count > 0 ? m_BufferPool.Pop() : new RichTextRebuildBuffers();
            buffers.Reset();
            return buffers;
        }

        private void ReleaseActiveBuffers()
        {
            if (m_ActiveBuffers == null)
                return;
            // 旧图标层仍可能引用这批 quad；放弃它们，避免复用时串改顶点。
            m_ActiveBuffers.DetachQuads();
            m_ActiveBuffers.Reset();
            m_BufferPool.Push(m_ActiveBuffers);
            m_ActiveBuffers = null;
        }

        /// <summary>早退路径：丢弃尚未 Flush 的缓冲，避免残留到下一帧。</summary>
        private void DiscardPendingBuffers()
        {
            if (m_PendingBuffers == null)
                return;
            m_PendingBuffers.DetachQuads();
            m_PendingBuffers.Reset();
            m_BufferPool.Push(m_PendingBuffers);
            m_PendingBuffers = null;
            iconUpdatePending = false;
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            if (!supportRichText)
            {
                base.OnPopulateMesh(toFill);
                DiscardPendingBuffers();
                return;
            }

            if (font == null)
            {
                DiscardPendingBuffers();
                return;
            }

            // 文本处理：内容没变时复用上次的解析结果，只在文本变化时重新跑正则
            if (parsedRichInfos == null || parsedText != text)
            {
                parsedRichInfos = ProcessRichText(text, out parsedResultText, out parsedTextWithoutTag);
                parsedText = text;
            }

            // 本次重建统一从池里取容器。上一帧已交给图标层的 active 缓冲仍被持有，
            // 要等 FlushIconUpdate 真正把它替换掉之后才回池（见 QueueIconUpdate）。
            var buf = m_PendingBuffers ?? RentBuffers();
            m_PendingBuffers = buf;
            buf.ResetForRebuild();

            // 每次重建都克隆一份模板：网格构建会写 Rects/Content，图标层也会持有这些对象
            CloneRichInfos(parsedRichInfos, buf.RichInfos);
            richInfos = buf.RichInfos;
            var effectedVerts = buf.EffectedVerts;
            var iconInfos = buf.IconInfos;
            var icons = buf.Icons;
            var iconVerts = buf.IconVerts;
            var gifs = buf.Gifs;
            var iconShadows = buf.IconShadows;
            var drawingLineInfos = buf.DrawingLineInfos;
            var resultText = parsedResultText;
            var textWithoutTag = parsedTextWithoutTag;

            // We don't care if we the font Texture changes while we are doing our Update.
            // The end result of cachedTextGenerator will be valid for this instance.
            // Otherwise, we can get issues like Case 619238.
            m_DisableFontTextureRebuiltCallback = true;

            Vector2 extents = rectTransform.rect.size;
            var settings = GetGenerationSettings(extents);
            cachedTextGenerator.PopulateWithErrors(resultText, settings, gameObject);

            // Apply the offset to the vertices
            var verts = cachedTextGenerator.verts;
            // 获取最后一个可显示出来的字符顶点，以免超出显示区域后显示错乱
            verts = ProcessRawTags(verts, resultText, textWithoutTag, buf.RawTagVerts);
            var vertCount = verts.Count;
            foreach (var richInfo in richInfos)
            {
                CalculateRects(richInfo, verts, vertCount);
                switch (richInfo.Type)
                {
                    case RichType.Icon:
                    {
                        var icon = ApplyIcon(richInfo, verts, icons, vertCount);
                        if (icon.icon != null)
                        {
                            iconInfos.Add(richInfo);
                            icons.TryAddToDictionary(richInfo.Content, icon.icon);
                            iconVerts.Add(icon.verts);
                        }
                    }
                        break;
                    case RichType.Gif:
                    {
                        var gifVerts = ApplyGif(richInfo, verts, vertCount);
                        if (gifVerts != null)
                        {
                            gifs.Add(richInfo, gifVerts);
                        }
                    }
                        break;
                    case RichType.Outline:
                        ApplyOutlineEffect(richInfo, verts, effectedVerts, vertCount);
                        break;
                    case RichType.Shadow:
                        ApplyShadowEffect(richInfo, verts, effectedVerts, vertCount, iconShadows);
                        break;
                    case RichType.Underline:
                    {
                        drawingLineInfos.Add(richInfo);
                    }
                        break;
                    case RichType.StrikeLine:
                    {
                        drawingLineInfos.Add(richInfo);
                    }
                        break;
                    case RichType.Gradient:
                        ApplyGradientEffect(richInfo, verts, vertCount);
                        break;
                }
            }

            // 尝试渲染 划线 含下划线、删除线
            // 当存在图标时，可以通过图标的占位字符拿到铺满颜色的uv区域
            // 当不存在图标时，没有占位字符可以用，就用图标的渲染方式来渲染下划线
            if (lineUVs == null)
            {
                foreach (var richInfo in drawingLineInfos)
                {
                    richInfo.Content = UnderlineSpriteName;
                    // 考虑换行，会是多个矩形
                    var underlineVerts = ApplyDrawingLineEffect(richInfo, verts, vertCount);
                    CheckAndCreateWhiteQuad();
                    foreach (var uVerts in underlineVerts)
                    {
                        iconInfos.Add(richInfo);
                        icons.TryAddToDictionary(richInfo.Content, _whiteQuad);
                        iconVerts.Add(uVerts);
                    }
                }
            }
            else
            {
                foreach (var richInfo in drawingLineInfos)
                {
                    // 考虑换行，会是多个矩形
                    var underlineVerts = ApplyDrawingLineEffect(richInfo, verts, vertCount);
                    foreach (var uVerts in underlineVerts)
                    {
                        for (int i = 0; i < uVerts.Length; i++)
                        {
                            var vert = uVerts[i];
                            vert.uv0 = lineUVs[i];
                            effectedVerts.Add(vert);
                        }
                    }
                }
            }

            float unitsPerPixel = 1 / pixelsPerUnit;
            vertCount = verts.Count;

            // We have no verts to process just return (case 1037923)
            if (vertCount <= 0)
            {
                toFill.Clear();
                // 清理icon
                // 计算完把划线用的uv清掉
                lineUVs = null;
                // 避免同时更新渲染
                QueueIconUpdate();
                return;
            }

            Vector2 roundingOffset = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
            roundingOffset = PixelAdjustPoint(roundingOffset) - roundingOffset;
            toFill.Clear();

            vertCount = effectedVerts.Count + verts.Count;
            var allVertices = buf.AllVertices;
            allVertices.Clear();
            allVertices.AddRange(verts);
            allVertices.AddRange(effectedVerts);
            if (roundingOffset != Vector2.zero)
            {
                for (int i = 0; i < vertCount; ++i)
                {
                    int tempVertsIndex = i & 3;
                    m_TempVerts[tempVertsIndex] = allVertices[i];
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

                foreach (var gif in gifs)
                {
                    var tempVerts = gif.Value;

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
                    m_TempVerts[tempVertsIndex] = allVertices[i];
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

                foreach (var gif in gifs)
                {
                    var tempVerts = gif.Value;

                    for (int j = 0; j < 4; j++)
                    {
                        tempVerts[j].position *= unitsPerPixel;
                    }
                }
            }

            // 计算完把下划线用的uv清掉
            lineUVs = null;
            // 避免同时更新渲染
            QueueIconUpdate();

            m_DisableFontTextureRebuiltCallback = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position,
                eventData.pressEventCamera, out var lp);

            // 点击读的是当前有效的解析副本；richInfos 指向 active 缓冲，
            // 而 active 只在图标层真正换掉后才回池，所以这里的数据仍有效。
            if (richInfos == null)
                return;

            RichInfo target = null;
            foreach (var richInfo in richInfos)
            {
                var rects = richInfo.Rects;
                if (rects == null)
                    continue;

                bool hit = false;
                for (int i = 0; i < rects.Count; i++)
                {
                    var rect = rects[i];
                    if (rect.x <= lp.x && rect.z >= lp.x && rect.y <= lp.y && rect.w >= lp.y)
                    {
                        hit = true;
                        break;
                    }
                }

                // 类型枚举值即优先级，取代 OrderByDescending，避免 LINQ 分配
                if (hit && (target == null || richInfo.Type > target.Type))
                    target = richInfo;
            }

            if (target != null)
            {
                string message;
                switch (target.Type)
                {
                    case RichType.Link:
                        // 不带协议头的地址补上 https://，方便直接交给 Application.OpenURL
                        message = NormalizeUrl(target.Content);
                        break;
                    case RichType.Icon:
                    case RichType.Gif:
                        message = target.Content;
                        break;
                    default:
                        message = target.EffectedStr;
                        break;
                }

                OnClick?.Invoke(target.Type, message);
            }
        }



        public void SetIconProvider(IIconProvider provider)
        {
            iconProvider = provider;
        }



        private void QueueIconUpdate()
        {
            // 容器都在 m_PendingBuffers 上；这里只登记「本帧有图标层要更新」。
            // 旧的 active 先不回池 —— 图标层此刻仍在读它，等 Flush 真正换掉再释放。
            iconUpdatePending = true;
        }

        /// <summary>
        /// 在当前帧的 Canvas 更新结束后统一应用图标层，并立即重建子 RawImage。
        /// 此处完成 pending → active 的所有权移交：旧 active 不再被读取，可以回池。
        /// </summary>
        private void FlushIconUpdate()
        {
            if (!iconUpdatePending)
                return;

            iconUpdatePending = false;

            var buffers = m_PendingBuffers;
            m_PendingBuffers = null;
            if (buffers == null)
                return;

            bool needShowIcons = buffers.IconInfos.Count > 0 || buffers.Gifs.Count > 0;

            if (!needShowIcons)
            {
                if (_iconImage != null)
                {
                    _iconImage.gameObject.SetActive(false);
                    _iconImage.Clear();
                }
            }
            else
            {
                var image = iconImage;
                image.gameObject.SetActive(true);
                image.SetIcons(buffers.IconInfos, buffers.Icons, buffers.IconVerts,
                    buffers.IconShadows, buffers.Gifs);
                // willRenderCanvases 中 CanvasUpdateRegistry 已经执行完，这里主动重建一次，
                // 让图标与文本在同一帧生效
                image.Rebuild(CanvasUpdate.PreRender);
            }

            // SetIcons/Clear 已经接手新缓冲（或明确弃用），旧 active 可以安全回池。
            ReleaseActiveBuffers();
            if (needShowIcons)
            {
                m_ActiveBuffers = buffers;
            }
            else
            {
                // 无图标：这份缓冲没人持有，直接回池复用
                buffers.DetachQuads();
                buffers.Reset();
                m_BufferPool.Push(buffers);
            }
        }

    }
}
