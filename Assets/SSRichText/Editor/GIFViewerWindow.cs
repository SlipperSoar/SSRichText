using System.Collections;
using System.Collections.Generic;
using System.IO;
using SS.UIComponent;
using UnityEditor;
using UnityEngine;

namespace SS.Editor
{
    /// <summary>
    /// GIF 预览窗口：可以拖入 .gif，或直接选择已经转换好的 .bytes，查看解码结果与每一帧
    /// </summary>
    public class GIFViewerWindow : EditorWindow
    {
        private const float PreviewSize = 128f;
        private const string WindowTitle = "GIF Viewer";

        /// <summary>每次 Editor 更新最多用于推进解码的时间（秒），避免长时间阻塞编辑器</summary>
        private const double LoadTimeBudgetSeconds = 0.01;

        /// <summary>加载完成后的预览重绘间隔（秒），约 30fps</summary>
        private const double PreviewRepaintInterval = 1.0 / 30.0;

        private List<GifData> gifFrames;
        private string gifFilePath;
        private Texture2D gifFileTexture2D;
        private TextAsset gifBytesAsset;
        private Vector2 scrollPosition;
        private int currentPreviewIndex;
        private double lastFrameTime;
        private double nextPreviewRepaintTime;
        private bool isLoading;

        private IEnumerator gifDecoder;

        [MenuItem("Window/SS/GIF Viewer", priority = 2000)]
        public static void ShowWindow()
        {
            GetWindow<GIFViewerWindow>(WindowTitle).Show();
        }

        private void OnEnable()
        {
            if (gifFrames == null)
            {
                gifFrames = new List<GifData>();
            }

            lastFrameTime = EditorApplication.timeSinceStartup;
            nextPreviewRepaintTime = lastFrameTime;
        }

        private void OnDisable()
        {
            StopPreview();
            StopLoading();
            DestroyFrames();
            gifFilePath = null;
        }

        private void OnDestroy()
        {
            StopPreview();
            DestroyFrames();
        }

        private void OnGUI()
        {
            GUILayout.Label("GIF Viewer (Texture > TextAsset)", EditorStyles.boldLabel);

            DrawFileSelector();

            if (!TryGetSelectedAssetPath(out var gifPath))
            {
                return;
            }

            if (isLoading)
            {
                GUILayout.Label("Loading...");
                if (GUILayout.Button("Stop Load"))
                {
                    StopLoading();
                }

                return;
            }

            if (GUILayout.Button("Load GIF File") && gifFilePath != gifPath)
            {
                LoadGifFile(gifPath, !gifFileTexture2D);
            }

            if (string.IsNullOrEmpty(gifFilePath) || gifFrames == null || gifFrames.Count == 0)
            {
                return;
            }

            DrawAnimatedPreview();
            DrawAllFrames();
        }

        private void DrawFileSelector()
        {
            gifFileTexture2D = (Texture2D)EditorGUILayout.ObjectField("Select GIF File", gifFileTexture2D,
                typeof(Texture2D), false);
            gifBytesAsset = (TextAsset)EditorGUILayout.ObjectField("Select GIF Bytes File", gifBytesAsset,
                typeof(TextAsset), false);
        }

        /// <summary>
        /// 取得当前选中资源的路径：优先 .gif，其次 .bytes
        /// </summary>
        private bool TryGetSelectedAssetPath(out string gifPath)
        {
            gifPath = null;

            if (gifFileTexture2D != null)
            {
                gifPath = AssetDatabase.GetAssetPath(gifFileTexture2D);
                return !string.IsNullOrEmpty(gifPath) &&
                       Path.GetExtension(gifPath).ToLowerInvariant() == GifImporter.GifExtension;
            }

            if (gifBytesAsset != null)
            {
                gifPath = AssetDatabase.GetAssetPath(gifBytesAsset);
                return !string.IsNullOrEmpty(gifPath);
            }

            return false;
        }

        /// <summary>
        /// 按帧时长播放顶部的大图预览
        /// </summary>
        private void DrawAnimatedPreview()
        {
            var currentTime = EditorApplication.timeSinceStartup;

            var previewRect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize);
            var currentFrame = gifFrames[currentPreviewIndex];
            var nextFrameIndex = (currentPreviewIndex + 1) % gifFrames.Count;
            var nextFrame = gifFrames[nextFrameIndex];

            if (currentTime - lastFrameTime >= nextFrame.DelaySecond)
            {
                GUI.DrawTexture(previewRect, nextFrame.FrameTexture, ScaleMode.ScaleToFit);
                currentPreviewIndex = nextFrameIndex;
                lastFrameTime = currentTime;
            }
            else
            {
                GUI.DrawTexture(previewRect, currentFrame.FrameTexture, ScaleMode.ScaleToFit);
            }
        }

        /// <summary>
        /// 以网格形式列出帧，只绘制滚动区域内的帧，避免大 GIF 每帧重绘全部纹理
        /// </summary>
        private void DrawAllFrames()
        {
            if (gifFrames == null || gifFrames.Count == 0)
            {
                return;
            }

            const float padding = 8f;
            const float labelHeight = 18f;
            var cellWidth = PreviewSize + padding;
            var cellHeight = PreviewSize + labelHeight + padding;

            var viewHeight = Mathf.Max(120f, position.height - 260f);
            var viewRect = GUILayoutUtility.GetRect(1f, viewHeight, GUILayout.ExpandWidth(true));
            var contentWidth = Mathf.Max(PreviewSize, viewRect.width - 20f);
            var columns = Mathf.Max(1, Mathf.FloorToInt(contentWidth / cellWidth));
            var rows = Mathf.CeilToInt(gifFrames.Count / (float)columns);
            var contentRect = new Rect(0f, 0f, contentWidth, rows * cellHeight);

            scrollPosition = GUI.BeginScrollView(viewRect, scrollPosition, contentRect);

            var firstRow = Mathf.Max(0, Mathf.FloorToInt(scrollPosition.y / cellHeight) - 1);
            var lastRow = Mathf.Min(rows - 1,
                Mathf.CeilToInt((scrollPosition.y + viewRect.height) / cellHeight) + 1);

            for (var row = firstRow; row <= lastRow; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var index = row * columns + column;
                    if (index >= gifFrames.Count)
                    {
                        break;
                    }

                    var x = column * cellWidth;
                    var y = row * cellHeight;
                    var frameData = gifFrames[index];
                    GUI.Label(new Rect(x, y, PreviewSize, labelHeight),
                        $"Frame {index}, Delay: {frameData.DelaySecond} s");
                    GUI.DrawTexture(new Rect(x, y + labelHeight, PreviewSize, PreviewSize),
                        frameData.FrameTexture, ScaleMode.ScaleToFit);
                }
            }

            GUI.EndScrollView();
        }

        /// <summary>
        /// 添加至 Editor 的 Update，模拟协程运行
        /// </summary>
        private void OnEditorUpdate()
        {
            // 解码器每帧会 yield 多次，若一次 Editor 更新只 MoveNext 一步，帧数一多加载就会非常慢；
            // 这里在时间预算内尽量多推进几步，既提速又不会长时间卡住编辑器
            var startTime = EditorApplication.timeSinceStartup;
            while (gifDecoder != null && EditorApplication.timeSinceStartup - startTime < LoadTimeBudgetSeconds)
            {
                if (!gifDecoder.MoveNext())
                {
                    EditorApplication.update -= OnEditorUpdate;
                    break;
                }
            }

            Repaint();
        }

        private void LoadGifFile(string gifPath, bool isBytesAsset)
        {
            if (gifFilePath == gifPath && gifFrames != null && gifFrames.Count > 0)
            {
                return;
            }

            var bytes = isBytesAsset ? gifBytesAsset.bytes : File.ReadAllBytes(gifPath);

            if (gifFrames == null)
            {
                gifFrames = new List<GifData>();
            }

            // 载入新文件前，先停掉旧预览并销毁旧帧纹理
            StopPreview();
            DestroyFrames();

            gifFilePath = gifPath;
            currentPreviewIndex = 0;
            scrollPosition = Vector2.zero;
            isLoading = true;

            // 先移除旧的监听，避免重复点击 Load 时重复注册
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            // 传自己的列表进去，中途 Stop/关闭窗口时可以按帧释放已经解出来的纹理
            gifDecoder = GifDecoder.Decode(bytes, gifFrames, frames =>
            {
                currentPreviewIndex = 0;
                lastFrameTime = EditorApplication.timeSinceStartup;
                isLoading = false;
                Repaint();
                StartPreview();
            });
        }

        private void StartPreview()
        {
            nextPreviewRepaintTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= OnPreviewUpdate;
            EditorApplication.update += OnPreviewUpdate;
        }

        private void StopPreview()
        {
            EditorApplication.update -= OnPreviewUpdate;
        }

        /// <summary>
        /// 预览播放：按固定间隔重绘窗口，避免鼠标不动时画面停在某一帧
        /// </summary>
        private void OnPreviewUpdate()
        {
            if (isLoading || gifFrames == null || gifFrames.Count == 0)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (now < nextPreviewRepaintTime)
            {
                return;
            }

            nextPreviewRepaintTime = now + PreviewRepaintInterval;
            Repaint();
        }

        private void DestroyFrames()
        {
            if (gifFrames == null)
            {
                return;
            }

            foreach (var frame in gifFrames)
            {
                frame?.Release();
            }

            gifFrames.Clear();
            currentPreviewIndex = 0;
        }

        private void StopLoading()
        {
            StopPreview();

            if (!isLoading)
            {
                return;
            }

            gifDecoder = null;
            isLoading = false;
            // 清空路径，保证同一文件可以重新加载
            gifFilePath = null;
            EditorApplication.update -= OnEditorUpdate;
            // 已经解码出来的帧由 gifFrames 持有，这里一并释放
            DestroyFrames();
        }
    }
}
