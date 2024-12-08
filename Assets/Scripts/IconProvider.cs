using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SS.UIComponent
{
    /// <summary>
    /// 提供使用的Icon
    /// </summary>
    [ExecuteInEditMode]
    public class IconProvider : MonoBehaviour
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
