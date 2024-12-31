using System;
using System.Collections.Generic;
using UnityEngine;

namespace SS.UIComponent
{
    /// <summary>
    /// Gif加载管理
    /// </summary>
    public class GifLoadManager : MonoBehaviour
    {
        #region inner class

        private class GifLoadData
        {
            public List<GifData> Data;
            public float LastUseTime;
            public Action<List<GifData>> OnComplete;
        }

        #endregion
        
        #region singleton

        public static GifLoadManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameObject("GifLoadManager").AddComponent<GifLoadManager>();
                    DontDestroyOnLoad(_instance.gameObject);
                }

                return _instance;
            }
        }
        
        private static GifLoadManager _instance;

        #endregion

        #region const

        /// <summary>Gif数据卸载倒计时（秒）</summary>
        private const int UNLOAD_COUNT_DOWN_SECOND = 120;

        #endregion
        
        #region properties

        private Dictionary<string, bool> gifLoadStatus;
        private Dictionary<string, GifLoadData> gifDatas;

        #endregion

        #region Unity

        private void Awake()
        {
            gifDatas = new Dictionary<string, GifLoadData>();
            gifLoadStatus = new Dictionary<string, bool>();
        }

        private void Update()
        {
            var removeList = new List<string>(gifDatas.Count);
            // 按时间清理不再使用的GIF数据
            foreach (var gifData in gifDatas)
            {
                if (gifData.Value.LastUseTime + UNLOAD_COUNT_DOWN_SECOND <= Time.time && gifLoadStatus[gifData.Key])
                {
                    removeList.Add(gifData.Key);
                }
            }

            foreach (var gifName in removeList)
            {
                gifLoadStatus.Remove(gifName);
                gifDatas.Remove(gifName);
            }
        }

        #endregion
        
        #region Public Methods

        public void LoadGif(string gifName, bool useIO, Action<List<GifData>> onComplete)
        {
#if UNITY_EDITOR
            Debug.Log($"Load Gif: {gifName}, useIO: {useIO}");
#endif
            if (gifLoadStatus.TryGetValue(gifName, out var isLoaded))
            {
                if (isLoaded)
                {
                    var loadData = gifDatas[gifName];
                    loadData.LastUseTime = Time.time;
                    onComplete?.Invoke(loadData.Data);
                }
                else
                {
                    var loadData = gifDatas[gifName];
                    loadData.OnComplete += onComplete;
                }
            }
            else
            {
                gifDatas[gifName] = new GifLoadData()
                {
                    OnComplete = onComplete
                };
                byte[] bytes = null;
                if (useIO)
                {
                    bytes = System.IO.File.ReadAllBytes(gifName);
                }
                else
                {
                    bytes = Resources.Load<TextAsset>(gifName).bytes;
                }
                
                StartCoroutine(GifDecoder.Decode(bytes, gifData =>
                {
                    gifLoadStatus[gifName] = true;
                    var loadData = gifDatas[gifName];
                    loadData.Data = gifData;
                    loadData.OnComplete?.Invoke(gifData);
                    loadData.OnComplete = null;
                }));
            }
        }

        #endregion
    }
}