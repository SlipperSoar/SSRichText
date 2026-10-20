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
        private const int MaxHorizontalCount = 4;
        private const string WindowTitle = "GIF Viewer";
        private const string LogPrefix = "[SSRichText]";

        /// <summary>每次 Editor 更新最多用于推进解码的时间（秒），避免长时间阻塞编辑器</summary>
        private const double LoadTimeBudgetSeconds = 0.01;

        private List<GifData> gifFrames;
        private string gifFilePath;
        private Texture2D gifFileTexture2D;
        private TextAsset gifBytesAsset;
        private Vector2 scrollPosition;
        private int currentPreviewIndex;
        private double lastFrameTime;
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
        }

        private void OnDisable()
        {
            StopLoading();
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
        /// 以网格形式列出所有帧
        /// </summary>
        private void DrawAllFrames()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);

            var currentHorizontalCount = 0;
            for (var i = 0; i < gifFrames.Count; i++)
            {
                if (currentHorizontalCount == 0)
                {
                    GUILayout.BeginHorizontal();
                }

                GUILayout.BeginVertical();
                var frameData = gifFrames[i];
                GUILayout.Label($"Frame {i}, Delay: {frameData.DelaySecond} s");
                var rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize);
                GUI.DrawTexture(rect, frameData.FrameTexture, ScaleMode.ScaleToFit);
                GUILayout.EndVertical();

                if (currentHorizontalCount == MaxHorizontalCount - 1 || i == gifFrames.Count - 1)
                {
                    GUILayout.EndHorizontal();
                }

                currentHorizontalCount++;
                currentHorizontalCount %= MaxHorizontalCount;
            }

            GUILayout.EndScrollView();
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

            gifFilePath = gifPath;
            gifFrames = new List<GifData>();
            currentPreviewIndex = 0;
            scrollPosition = Vector2.zero;
            isLoading = true;

            // 先移除旧的监听，避免重复点击 Load 时重复注册
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;

            gifDecoder = GifDecoder.Decode(bytes, frames =>
            {
                gifFrames = frames;
                currentPreviewIndex = 0;
                lastFrameTime = EditorApplication.timeSinceStartup;
                isLoading = false;
                Repaint();

                Debug.Log($"{LogPrefix} Load GIF {gifFilePath} done, {frames.Count} frame(s)");
            });
        }

        private void StopLoading()
        {
            if (!isLoading)
            {
                return;
            }

            gifDecoder = null;
            isLoading = false;
            // 清空路径，保证同一文件可以重新加载
            gifFilePath = null;
            EditorApplication.update -= OnEditorUpdate;
        }
    }
}
