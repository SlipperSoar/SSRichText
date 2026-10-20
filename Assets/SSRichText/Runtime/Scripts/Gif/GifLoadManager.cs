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

        #region settings

        /// <summary>GIF 数据闲置多久后卸载（秒），默认 120</summary>
        public static int UnloadCountDownSeconds { get; set; } = 120;

        /// <summary>同时解码的 GIF 数量上限，默认 2；设置过大会增加一次性解码造成的卡顿</summary>
        public static int MaxConcurrentLoads { get; set; } = 2;

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
        private readonly List<string> removeList = new List<string>();

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
            
            removeList.Clear();
            // 按时间清理不再使用的GIF数据
            foreach (var gifData in gifDatas)
            {
                if (gifData.Value.DontClear)
                {
                    continue;
                }

                if (gifData.Value.LastUseTime + UnloadCountDownSeconds <= Time.time && gifLoadStatus[gifData.Key])
                {
                    removeList.Add(gifData.Key);
                }
            }

            foreach (var gifName in removeList)
            {
                if (gifDatas.TryGetValue(gifName, out var gifData))
                {
                    ReleaseGifData(gifData);
                }

                gifLoadStatus.Remove(gifName);
                gifDatas.Remove(gifName);
                gifSizes.Remove(gifName);
            }
        }

        private void OnDestroy()
        {
            foreach (var gifData in gifDatas.Values)
            {
                ReleaseGifData(gifData);
            }

            gifDatas.Clear();
            gifLoadStatus.Clear();
            gifSizes.Clear();
            gifPlayers.Clear();

            if (_instance == this)
            {
                _instance = null;
            }
        }

        #endregion
        
        #region Public Methods

        /// <summary>
        /// 单独添加一个Gif的数据
        /// 该方式可以在不解析已有gif的情况下添加GIF数据，适用于动态生成Gif效果。
        /// 管理器会接管这些帧的纹理，卸载/替换时会销毁它们。
        /// </summary>
        /// <param name="gifName">gif名</param>
        /// <param name="size">gif尺寸</param>
        /// <param name="gifFrames">帧数据</param>
        /// <param name="dontClear">不进行超时自动清理</param>
        public void AddGifData(string gifName, Vector2Int size, IEnumerable<GifData> gifFrames, bool dontClear = false)
        {
            gifLoadStatus[gifName] = true;
            gifSizes[gifName] = size;
            if (gifDatas.TryGetValue(gifName, out var gifData))
            {
                // 先复制一份，兼容 gifFrames 就是 gifData.Data 自身的情况；
                // 旧帧由管理器持有，替换前先销毁纹理
                var frames = new List<GifData>(gifFrames);
                ReleaseGifData(gifData);
                gifData.Data.AddRange(frames);
                gifData.LastUseTime = Time.time;
                gifData.DontClear = dontClear;
            }
            else
            {
                gifDatas.TryAddToDictionary(gifName, new GifLoadData()
                {
                    Data = new List<GifData>(gifFrames),
                    LastUseTime = Time.time,
                    DontClear = dontClear
                });
            }
        }
        
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
                if (loadingCount >= MaxConcurrentLoads)
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

        /// <summary>
        /// 销毁一组 GIF 帧纹理并清空列表
        /// </summary>
        private static void ReleaseGifData(GifLoadData data)
        {
            if (data?.Data == null)
            {
                return;
            }

            foreach (var frame in data.Data)
            {
                frame?.Release();
            }

            data.Data.Clear();
        }

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
                if (loadingCount < MaxConcurrentLoads && waitingQueue.Count > 0)
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