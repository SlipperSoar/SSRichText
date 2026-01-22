#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SS.Editor
{
    public class GifImporter : AssetPostprocessor
    {
        private void OnPostprocessTexture(Texture2D texture)
        {
            // 只处理 GIF 文件
            if (!Path.GetExtension(assetPath).ToLower().Equals(".gif"))
            {
                return;
            }

            ConvertGifToTextAsset(assetPath);
        }

        // 将 GIF 文件转换为 TextAsset
        static void ConvertGifToTextAsset(string gifFilePath)
        {
            var bytesPath = Path.ChangeExtension(gifFilePath, ".bytes");

            // 读取 GIF 文件的字节数据
            byte[] gifBytes = File.ReadAllBytes(gifFilePath);

            // 将字节数据写入文件
            File.WriteAllBytes(bytesPath, gifBytes);

            // 刷新 AssetDatabase，以便 Unity 能识别并显示这个新创建的文件
            AssetDatabase.ImportAsset(bytesPath);

            // 为新的 .bytes 文件设置自定义图标
            SetCustomIconForGifAsset(bytesPath, gifFilePath);

            // 删除原始 GIF 文件的导入资源
            AssetDatabase.DeleteAsset(gifFilePath);
        }

        // 为 TextAsset 设置图标
        static void SetCustomIconForGifAsset(string bytesAssetPath, string gifFilePath)
        {
            // 获取与 GIF 文件相关的纹理（可以是 GIF 文件本身的缩略图，或者指定的图像）
            Texture2D gifThumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(gifFilePath);

            if (gifThumbnail != null)
            {
                // 为新生成的 .bytes 文件设置图标
                EditorGUIUtility.SetIconForObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(bytesAssetPath),
                    gifThumbnail);
            }
            else
            {
                Debug.LogWarning("GIF thumbnail not found, using default icon.");
            }
        }
    }
}
#endif