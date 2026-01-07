using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

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
            public bool DontClear;
        }
        
        private class GifPlayer
        {
            public string GifName;
            public float CountDown;
            public int Index;
            public UnityEvent<string, GifData> Player;

            public void Update(List<GifData> gifDatas)
            {
                var gifData = gifDatas[Index];
                if (CountDown >= gifData.DelaySecond)
                {
                    CountDown -= gifData.DelaySecond;
                    Player.Invoke(GifName, gifData);
                    Index++;
                    Index %= gifDatas.Count;
                }
                else
                {
                    CountDown += Time.deltaTime;
                }
            }
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

        public IGifProvider GifProvider
        {
            get
            {
                if (gifProvider == null)
                {
                    gifProvider = new GifProvider();
                }
                
                return gifProvider;
            }
            set => gifProvider = value;
        }

        private IGifProvider gifProvider;
        
        private Dictionary<string, bool> gifLoadStatus = new Dictionary<string, bool>();
        private Dictionary<string, GifLoadData> gifDatas = new Dictionary<string, GifLoadData>();
        private Dictionary<string, Vector2Int> gifSizes = new Dictionary<string, Vector2Int>();
        private Dictionary<string, GifPlayer> gifPlayers = new Dictionary<string, GifPlayer>();

        private int loadingCount = 0;
        private Queue<(string gifName, bool forceBgColorTransparent)> waitingQueue = new Queue<(string, bool)>();
        private Coroutine queueChecker;

        #endregion

        #region Unity

        private void Update()
        {
            // 进行GIF的播放
            if (gifPlayers.Count > 0)
            {
                foreach (var gifPlayer in gifPlayers)
                {
                    var gifName = gifPlayer.Key;
                    if (gifLoadStatus.TryGetValue(gifName, out var isLoaded))
                    {
                        if (isLoaded)
                        {
                            var player = gifPlayer.Value;
                            var gifData = gifDatas[gifName];
                            gifData.LastUseTime = Time.time;
                            player.Update(gifData.Data);
                        }
                    }
                }
            }
            
            var removeList = new List<string>(gifDatas.Count);
            // 按时间清理不再使用的GIF数据
            foreach (var gifData in gifDatas)
            {
                if (gifData.Value.DontClear)
                {
                    continue;
                }

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
        /// <param name="gifName">gif资源名</param>
        /// <returns>gif的显示尺寸</returns>
        public Vector2Int GetGifSize(string gifName)
        {
            if (gifSizes.TryGetValue(gifName, out var size))
            {
                return size;
            }

            byte[] bytes = GifProvider.GetGifBytes(gifName);

            size = GifDecoder.GetGifSize(bytes);
            gifSizes.TryAddToDictionary(gifName, size);
            return size;
        }

        public void PrepareGif(string gifName, bool dontClear = false, bool forceBgColorTransparent = false)
        {
#if UNITY_EDITOR
            Debug.Log($"Load Gif: {gifName}, forceBgColorTransparent: {forceBgColorTransparent}");
#endif
            if (gifLoadStatus.TryGetValue(gifName, out var isLoaded))
            {
                if (isLoaded)
                {
                    var loadData = gifDatas[gifName];
                    loadData.LastUseTime = Time.time;
                }
            }
            else
            {
                gifDatas[gifName] = new GifLoadData()
                {
                    DontClear = dontClear
                };

                gifLoadStatus[gifName] = false;
                
                // 检查数量，超过最大同时加载数量时放入队列等待
                if (loadingCount >= LOAD_MAX_COUNT)
                {
                    // 等待
                    waitingQueue.Enqueue((gifName, forceBgColorTransparent));
                }
                else
                {
                    LoadGif(gifName, forceBgColorTransparent);
                }
            }
        }

        /// <summary>
        /// 添加Gif播放器
        /// </summary>
        /// <param name="gifName">gif名</param>
        /// <param name="onUpdate">更新监听</param>
        public void AddGifPlayer(string gifName, UnityAction<string, GifData> onUpdate)
        {
            GifPlayer gifPlayer;
            if (gifPlayers.TryGetValue(gifName, out gifPlayer))
            {
                gifPlayer.Player.AddListener(onUpdate);
            }
            else
            {
                gifPlayer = new GifPlayer()
                {
                    GifName = gifName,
                    Player = new UnityEvent<string, GifData>()
                };
                gifPlayer.Player.AddListener(onUpdate);
                gifPlayers.Add(gifName, gifPlayer);
            }
        }

        /// <summary>
        /// 移除Gif播放器
        /// </summary>
        /// <param name="gifName">gif名</param>
        /// <param name="onUpdate">更新监听</param>
        public void RemoveGifPlayer(string gifName, UnityAction<string, GifData> onUpdate)
        {
            if (gifPlayers.TryGetValue(gifName, out var gifPlayer))
            {
                gifPlayer.Player.RemoveListener(onUpdate);
                if (gifPlayer.Player.GetPersistentEventCount() == 0)
                {
                    gifPlayers.Remove(gifName);
                }
            }
            else
            {
                Debug.LogWarning($"GifPlayer not found: {gifName}, is it already be removed?");
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
        
        private void LoadGif(string gifName, bool forceBgColorTransparent)
        {
            loadingCount++;
            byte[] bytes = GifProvider.GetGifBytes(gifName);
            // 在加载的同时就对gif的size进行计算并缓存起来
            var gifSize = GifDecoder.GetGifSize(bytes);
            gifSizes.TryAddToDictionary(gifName, gifSize);
                
            StartCoroutine(GifDecoder.Decode(bytes, gifData =>
            {
                gifLoadStatus[gifName] = true;
                var loadData = gifDatas[gifName];
                loadData.Data = gifData;
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
                    var (gifName, forceBgColorTransparent) = waitingQueue.Dequeue();
                    LoadGif(gifName, forceBgColorTransparent);
                }
                yield return null;
            }
        }

        #endregion
    }
}