using System.Collections;
using System.Collections.Generic;
using SS.UIComponent;
using UnityEngine;
using UnityEngine.UI;

public class SampleSceneController : MonoBehaviour
{
    #region properties

    [SerializeField] private RichText richText;

    [SerializeField] private RawImage rawImage;

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
        
        // 测试gif的解析
        // var gifData = GifDecoder.Decode("Assets/Resources/GIF0.gif");
        // if (gifData != null)
        // {
        //     StartCoroutine(PlayGif(gifData));
        // }
    }

    IEnumerator PlayGif(List<(float delaySecond, Texture2D texture)> frames)
    {
        var index = 1;
        var countDown = 0f;
        
        rawImage.texture = frames[0].texture;
        rawImage.SetNativeSize();
        
        while (true)
        {
            if (index >= frames.Count)
            {
                index %= frames.Count;
            }
            
            var frame = frames[index];
            if (countDown >= frame.delaySecond)
            {
                rawImage.texture = frame.texture;
                countDown -= frame.delaySecond;
                index++;
            }
            
            countDown += Time.deltaTime;
            
            yield return null;
        }
    }
}
