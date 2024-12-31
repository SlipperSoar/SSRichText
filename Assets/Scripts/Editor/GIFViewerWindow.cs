using System.Collections;
using System.Collections.Generic;
using System.IO;
using SS.UIComponent;
using UnityEditor;
using UnityEngine;

namespace SS.Editor
{
    public class GIFViewerWindow : UnityEditor.EditorWindow
    {
        private float previewSize = 128f;
        private List<GifData> gifFrames;
        private string gifFilePath;
        private Texture2D gifFileTexture2D;
        private Vector2 scrollPosition;
        private const int maxHorizontalCount = 4;
        private int currentPreviewIndex = 0;
        private double lastFrameTime;
        private bool isLoading = false;

        private IEnumerator gifDecoder;

        [MenuItem("Window/GIF Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<GIFViewerWindow>("GIF Viewer");
            window.gifFrames = new List<GifData>();
        }

        void OnEnable()
        {
            lastFrameTime = EditorApplication.timeSinceStartup;
        }
        
        private void OnGUI()
        {
            GUILayout.Label("GIF Viewer", EditorStyles.boldLabel);

            gifFileTexture2D = (Texture2D)EditorGUILayout.ObjectField("Select GIF File", gifFileTexture2D, typeof(Texture2D), false);
            if (gifFileTexture2D == null)
            {
                return;
            }
            
            string gifPath = AssetDatabase.GetAssetPath(gifFileTexture2D);
            if (string.IsNullOrEmpty(gifPath) || !Path.GetExtension(gifPath).ToLower().Equals(".gif"))
            {
                return;
            }

            if (isLoading)
            {
                GUILayout.Label("Loading...");
                if (GUILayout.Button("Stop Load"))
                {
                    gifDecoder = null;
                    isLoading = false;
                    EditorApplication.update -= OnEditorUpdate;
                }
            }

            if (GUILayout.Button("Load GIF File") && !isLoading)
            {
                if (gifFilePath != gifPath)
                {
                    gifFilePath = gifPath;
                    isLoading = true;
                    EditorApplication.update += OnEditorUpdate;
                    LoadGifFile(gifPath);
                }
            }

            if (string.IsNullOrEmpty(gifFilePath) || isLoading || gifFrames.Count == 0)
            {
                return;
            }
            
            var currentTime = EditorApplication.timeSinceStartup;
            
            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);
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

            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            var currentHorizontalCount = 0;
            for (int i = 0; i < gifFrames.Count; i++)
            {
                if (currentHorizontalCount == 0)
                {
                    GUILayout.BeginHorizontal();
                }

                GUILayout.BeginVertical();
                var frameData = gifFrames[i];
                GUILayout.Label($"Frame {i}, Delay: {frameData.DelaySecond} s");
                Rect rect = GUILayoutUtility.GetRect(previewSize, previewSize);
                GUI.DrawTexture(rect, frameData.FrameTexture, ScaleMode.ScaleToFit);
                GUILayout.EndVertical();

                if (currentHorizontalCount == maxHorizontalCount - 1 || i == gifFrames.Count - 1)
                {
                    GUILayout.EndHorizontal();
                }
                
                currentHorizontalCount++;
                currentHorizontalCount %= maxHorizontalCount;
            }
            GUILayout.EndScrollView();
        }

        /// <summary>
        /// 添加至Editor的Update，模拟协程运行
        /// </summary>
        private void OnEditorUpdate()
        {
            if (gifDecoder != null && gifDecoder.MoveNext())
            {
                Repaint();  // Refresh the window if necessary (e.g., for progress updates).
            }
            else
            {
                EditorApplication.update -= OnEditorUpdate;  // Stop updating when the coroutine finishes
            }
        }
        
        private void LoadGifFile(string gifPath)
        {
            var bytes = File.ReadAllBytes(gifPath);
            gifDecoder = GifDecoder.Decode(bytes, frames =>
            {
                Debug.Log($"<color=yellow> Load GIF {gifFilePath} Over </color>");
                gifFrames = frames;
                currentPreviewIndex = 0;
                isLoading = false;
            });
        }
    }
}