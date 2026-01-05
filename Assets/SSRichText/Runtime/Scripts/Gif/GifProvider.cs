/*
 * @author SlipperSoar
 * @Created: 2026-01-05 10:11:20 
 * @description Gif数据获取
 */

using UnityEngine;

namespace SS.UIComponent
{
    public interface IGifProvider
    {
        /// <summary>
        /// 获取Gif的字节数据
        /// </summary>
        /// <param name="gifName">gif名</param>
        /// <returns>Gif的所有字节数据</returns>
        byte[] GetGifBytes(string gifName);
    }

    /// <summary>
    /// 默认的Gif数据获取：从Resources加载
    /// </summary>
    public class GifProvider : IGifProvider
    {
        public byte[] GetGifBytes(string gifName)
        {
            return Resources.Load<TextAsset>(gifName).bytes;
        }
    }
}
