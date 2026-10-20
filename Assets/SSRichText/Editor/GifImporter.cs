using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SS.Editor
{
    /// <summary>
    /// 导入 .gif 时自动转换成同名的 .bytes，方便通过 Resources / TextAsset 读取。
    /// 转换完成后会删除原始 .gif 资源。
    /// </summary>
    public class GifImporter : AssetPostprocessor
    {
        internal const string GifExtension = ".gif";
        internal const string BytesExtension = ".bytes";
        private const string LogPrefix = "[SSRichText]";

        private void OnPostprocessTexture(Texture2D texture)
        {
            // 只处理 GIF 文件
            if (!string.Equals(Path.GetExtension(assetPath), GifExtension, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ConvertGifToTextAsset(assetPath);
        }

        /// <summary>
        /// 把 GIF 的字节数据写成同名 .bytes，并沿用 GIF 的缩略图作为图标
        /// </summary>
        private static void ConvertGifToTextAsset(string gifFilePath)
        {
            if (!File.Exists(gifFilePath))
            {
                Debug.LogWarning($"{LogPrefix} GIF file not found: {gifFilePath}");
                return;
            }

            var bytesPath = Path.ChangeExtension(gifFilePath, BytesExtension);

            // 读取 GIF 文件的字节数据并写成 TextAsset
            File.WriteAllBytes(bytesPath, File.ReadAllBytes(gifFilePath));
            AssetDatabase.ImportAsset(bytesPath, ImportAssetOptions.ForceUpdate);

            // 为新的 .bytes 文件设置自定义图标
            SetIconForBytesAsset(bytesPath, gifFilePath);

            // 转换完成后原始 GIF 不再需要，删除以免同一份数据在工程里出现两次
            AssetDatabase.DeleteAsset(gifFilePath);
        }

        /// <summary>
        /// 把 GIF 的缩略图设置为对应 .bytes 文件的图标
        /// </summary>
        private static void SetIconForBytesAsset(string bytesAssetPath, string gifFilePath)
        {
            var gifThumbnail = AssetDatabase.LoadAssetAtPath<Texture2D>(gifFilePath);
            var bytesAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(bytesAssetPath);

            if (gifThumbnail == null || bytesAsset == null)
            {
                Debug.LogWarning($"{LogPrefix} GIF thumbnail not found, using default icon: {gifFilePath}");
                return;
            }

            EditorGUIUtility.SetIconForObject(bytesAsset, gifThumbnail);
        }
    }
}
