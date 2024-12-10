using System;
using System.Collections;
using System.Collections.Generic;
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
