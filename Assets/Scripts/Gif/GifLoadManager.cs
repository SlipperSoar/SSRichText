using System;
using System.Collections;
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
                    _instance = FindObjectOfType<GifLoadManager>();
                    if (_instance == null)
                    {
                        _instance = new GameObject("GifLoadManager").AddComponent<GifLoadManager>();
                        DontDestroyOnLoad(_instance.gameObject);
                    }

                    _instance.InitQueueCheck();
                }

                return _instance;
            }
        }
        
        private static GifLoadManager _instance;

        #endregion

        #region const

        /// <summary>Gif数据卸载倒计时（秒）</summary>
        private const int UNLOAD_COUNT_DOWN_SECOND = 120;

        /// <summary>最大同时加载数量，避免一次性加载太多导致卡顿（即使是协程）</summary>
        private const int LOAD_MAX_COUNT = 2;

        #endregion
        
        #region properties

        private Dictionary<string, bool> gifLoadStatus = new Dictionary<string, bool>();
        private Dictionary<string, GifLoadData> gifDatas = new Dictionary<string, GifLoadData>();
        private Dictionary<string, Vector2Int> gifSizes = new Dictionary<string, Vector2Int>();

        private int loadingCount = 0;
        private Queue<(string gifName, bool useIO, bool forceBgColorTransparent)> waitingQueue = new Queue<(string, bool, bool)>();
        private Coroutine queueChecker;

        #endregion

        #region Unity

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
                gifSizes.Remove(gifName);
            }
        }

        #endregion
        
        #region Public Methods

        /// <summary>
        /// 获取GIF的显示尺寸
        /// </summary>
        /// <param name="gifName">gif资源名或路径</param>
        /// <param name="useIO">是否使用IO（路径）</param>
        /// <returns></returns>
        public Vector2Int GetGifSize(string gifName, bool useIO = false)
        {
            if (gifSizes.TryGetValue(gifName, out var size))
            {
                return size;
            }

            byte[] bytes = null;
            if (useIO)
            {
                bytes = System.IO.File.ReadAllBytes(gifName);
            }
            else
            {
                bytes = Resources.Load<TextAsset>(gifName).bytes;
            }
            
            size = GifDecoder.GetGifSize(bytes);
            gifSizes.TryAddToDictionary(gifName, size);
            return size;
        }
        
        public void LoadGif(string gifName, Action<List<GifData>> onComplete, bool useIO = false, bool forceBgColorTransparent = false)
        {
#if UNITY_EDITOR
            Debug.Log($"Load Gif: {gifName}, useIO: {useIO}, forceBgColorTransparent: {forceBgColorTransparent}");
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
                gifLoadStatus[gifName] = false;
                
                // 检查数量，超过最大同时加载数量时放入队列等待
                if (loadingCount >= LOAD_MAX_COUNT)
                {
                    // 等待
                    waitingQueue.Enqueue((gifName, useIO, forceBgColorTransparent));
                }
                else
                {
                    LoadGif(gifName, useIO, forceBgColorTransparent);
                }
            }
        }

        #endregion

        #region Private Methods

        private void InitQueueCheck()
        {
            if (queueChecker != null)
            {
                StopCoroutine(queueChecker);
            }

            queueChecker = StartCoroutine(CheckLoadingQueue());
        }
        
        private void LoadGif(string gifName, bool useIO, bool forceBgColorTransparent)
        {
            loadingCount++;
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
                loadingCount--;
            }, forceBgColorTransparent: forceBgColorTransparent));
        }

        private IEnumerator CheckLoadingQueue()
        {
            while (true)
            {
                // 检查等待队列
                if (loadingCount < LOAD_MAX_COUNT && waitingQueue.Count > 0)
                {
                    var (gifName, useIO, forceBgColorTransparent) = waitingQueue.Dequeue();
                    LoadGif(gifName, useIO, forceBgColorTransparent);
                }
                yield return null;
            }
        }

        #endregion
    }
}