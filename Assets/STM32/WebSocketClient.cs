using System;
using System.IO;
using System.Text;
using UnityEngine;
using NativeWebSocket;

/// <summary>
/// 连接层：只负责和中间层保持一条 WebSocket 连接（连不上/断线自动重连）。
/// 收到的消息按 kind 分流：读数交给 DataProcessor，下行 ack 交给 Downlink。
///
/// 注意：NativeWebSocket 的 Connect() 内部是
///     await ConnectAsync(); OnOpen(); await Receive();
/// 也就是说它的 Task 要等整条连接结束才完成，所以这里只负责"发起"，状态全部走回调。
/// </summary>
public class WebSocketClient : MonoBehaviour
{
    [Header("中间层地址")]
    [SerializeField] private string serverUrl = "ws://127.0.0.1:8081";

    [Header("访问令牌（与中间层 WS_TOKEN 一致）")]
    [SerializeField] private string token = "";

    [Header("断线重连（秒）")]
    [SerializeField] private float reconnectMin = 1f;
    [SerializeField] private float reconnectMax = 30f;

    [Header("单次连接超时（秒）")]
    [SerializeField] private float connectTimeout = 10f;

    private static WebSocketClient instance;

    private WebSocket websocket;
    private float currentDelay;
    private float nextAttemptAt;
    private float attemptStartedAt;
    private bool connecting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("WebSocketClient");
        go.AddComponent<WebSocketClient>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        instance = this;
        LoadNetworkConfig();
    }

    [Serializable]
    private class NetConfig
    {
        public string wsUrl;
        public string token;
    }

    /// <summary>
    /// 优先从 Resources/network.json 读取地址和 token（也可用 StreamingAssets/network.json），
    /// 避免把服务器地址/令牌写进代码或提交到仓库。
    /// </summary>
    private void LoadNetworkConfig()
    {
        try
        {
            string json = null;

            // 优先从任意 Resources 目录加载：Assets/STM32/Resources/network.json
            var asset = Resources.Load<TextAsset>("network");
            if (asset != null) json = asset.text;

            // 兼容旧位置：Assets/StreamingAssets/network.json
            if (string.IsNullOrEmpty(json))
            {
                string path = Path.Combine(Application.streamingAssetsPath, "network.json");
                if (File.Exists(path)) json = File.ReadAllText(path);
            }

            if (string.IsNullOrEmpty(json)) return;

            var cfg = JsonUtility.FromJson<NetConfig>(json);
            if (cfg == null) return;

            if (!string.IsNullOrEmpty(cfg.wsUrl)) serverUrl = cfg.wsUrl;
            if (!string.IsNullOrEmpty(cfg.token)) token = cfg.token;
            Debug.Log($"[WebSocketClient] 配置已加载: {serverUrl}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[WebSocketClient] 读取 network.json 失败: {e.Message}");
        }
    }

    void Start()
    {
        currentDelay = reconnectMin;
        nextAttemptAt = Time.unscaledTime;
    }

    void Update()
    {
        if (websocket != null)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            websocket.DispatchMessageQueue();
#endif
        }

        if (DataProcessor.IsConnected) return;

        // 正在连接：超时才放弃，避免卡死
        if (connecting)
        {
            if (Time.unscaledTime - attemptStartedAt > connectTimeout)
            {
                Debug.LogWarning("[WebSocketClient] 连接超时，取消本次尝试");
                try { websocket?.CancelConnection(); } catch (Exception) { }
                connecting = false;
                ScheduleNextAttempt();
            }
            return;
        }

        if (Time.unscaledTime < nextAttemptAt) return;
        BeginConnect();
    }

    /// <summary>当前是否可发送（连接已建立）</summary>
    public static bool IsOpen
    {
        get
        {
            return instance != null
                && instance.websocket != null
                && instance.websocket.State == WebSocketState.Open;
        }
    }

    /// <summary>发送一条文本消息；未连接返回 false</summary>
    public static bool SendText(string text)
    {
        if (!IsOpen)
        {
            return false;
        }

        // SendText 返回 Task，这里不需要等它完成
        _ = instance.websocket.SendText(text);
        return true;
    }

    private void ScheduleNextAttempt()
    {
        nextAttemptAt = Time.unscaledTime + currentDelay;
        currentDelay = Mathf.Min(currentDelay * 2f, reconnectMax);
    }

    private void BeginConnect()
    {
        connecting = true;
        attemptStartedAt = Time.unscaledTime;

        string url = serverUrl;
        if (!string.IsNullOrEmpty(token))
        {
            url += (url.Contains("?") ? "&" : "?") + "token=" + Uri.EscapeDataString(token);
        }

        var ws = new WebSocket(url);
        websocket = ws;
        var self = ws;   // 过滤旧连接的回调

        ws.OnOpen += () =>
        {
            if (websocket != self) return;
            Debug.Log("[WebSocketClient] 已连接 " + serverUrl);
            connecting = false;
            currentDelay = reconnectMin;
            DataProcessor.SetConnected(true);
        };

        ws.OnClose += code =>
        {
            if (websocket != self) return;
            Debug.Log("[WebSocketClient] 连接关闭: " + code);
            connecting = false;
            DataProcessor.SetConnected(false);
            ScheduleNextAttempt();
        };

        ws.OnError += err =>
        {
            if (websocket != self) return;
            Debug.LogWarning("[WebSocketClient] 错误: " + err);
            connecting = false;
            DataProcessor.SetConnected(false);
            ScheduleNextAttempt();
        };

        ws.OnMessage += bytes =>
        {
            string json = Encoding.UTF8.GetString(bytes);

            // 区分"读数"和"下行 ack"
            WsEnvelope env = JsonUtility.FromJson<WsEnvelope>(json);
            if (env.kind == "ack")
            {
                Downlink.HandleAck(json);
                return;
            }

            if (env.kind == "queryResult")
            {
                DataBase.HandleResult(json);
                return;
            }

            DataProcessor.EnqueueRawMessage(json);
        };

        // 只发起，不 await：Connect() 的 Task 会在整条连接期间挂起
        _ = ws.Connect();
    }

    async void OnApplicationQuit()
    {
        if (websocket != null)
        {
            try { await websocket.Close(); } catch (Exception) { }
        }
    }
}