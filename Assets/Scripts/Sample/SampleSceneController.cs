using System.Collections;
using System.Collections.Generic;
using SS.UIComponent;
using UnityEngine;

public class SampleSceneController : MonoBehaviour
{
    #region properties

    [SerializeField] private RichText richText;

    #endregion
    
    void Start()
    {
        richText.OnClick += (type, message) =>
        {
            Debug.Log($"Click: {type}, {message}");
            switch (type)
            {
                case RichText.RichType.Link:
                    Application.OpenURL(message);
                    break;
            }
        };
    }
}
