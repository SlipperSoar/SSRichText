/*
 * @author SlipperSoar
 * @Created: 2024-12-08
 * @description Unity Text 富文本扩展的图标用的provider
 */

using UnityEngine;

namespace SS.UIComponent
{
    public interface IIconProvider
    {
        Sprite GetIcon(string iconName);
    }
    
    /// <summary>
    /// 提供使用的Icon
    /// </summary>
    public class IconProvider : IIconProvider
    {
        #region properties

        //

        #endregion
        
        #region Public Methods

        public Sprite GetIcon(string iconName)
        {
            return Resources.Load<Sprite>(iconName);
        }

        #endregion
    }
}
