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
        private List<(float delaySecond, Texture2D texture)> gifFrames;
        private string gifFilePath;
        private Texture2D gifFileTexture2D;
        private Vector2 scrollPosition;
        private const int maxHorizontalCount = 4;
        private int currentPreviewIndex = 0;
        private double lastFrameTime;

        [MenuItem("Window/GIF Viewer")]
        public static void ShowWindow()
        {
            var window = GetWindow<GIFViewerWindow>("GIF Viewer");
            window.gifFrames = new List<(float delaySecond, Texture2D texture)>();
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

            if (GUILayout.Button("Load GIF File"))
            {
                if (gifFilePath != gifPath)
                {
                    gifFilePath = gifPath;
                    var gifData = GifDecoder.Decode(gifPath);
                    gifFrames.Clear();
                    currentPreviewIndex = 0;
                    while (gifData.MoveNext())
                    {
                        gifFrames.Add(gifData.Current);
                    }
                }
            }

            if (string.IsNullOrEmpty(gifFilePath) || gifFrames.Count == 0)
            {
                return;
            }
            
            var currentTime = EditorApplication.timeSinceStartup;
            
            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);
            var currentFrame = gifFrames[currentPreviewIndex];
            var nextFrameIndex = (currentPreviewIndex + 1) % gifFrames.Count;
            var nextFrame = gifFrames[nextFrameIndex];
            if (currentTime - lastFrameTime >= nextFrame.delaySecond)
            {
                GUI.DrawTexture(previewRect, nextFrame.texture, ScaleMode.ScaleToFit);
                currentPreviewIndex = nextFrameIndex;
                lastFrameTime = currentTime;
            }
            else
            {
                GUI.DrawTexture(previewRect, currentFrame.texture, ScaleMode.ScaleToFit);
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
                GUILayout.Label($"Frame {i}, Delay: {frameData.delaySecond} s");
                Rect rect = GUILayoutUtility.GetRect(previewSize, previewSize);
                GUI.DrawTexture(rect, frameData.texture, ScaleMode.ScaleToFit);
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
    }
}