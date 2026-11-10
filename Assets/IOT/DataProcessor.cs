using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 数据层：接收 WebSocketClient 收到的 JSON，解析成统一 Reading，按 (设备号, 类型) 保存最新值。
/// 只保留最新值，历史数据请走中间层 HTTP 接口。
///
/// 业务代码只用这三个接口：
///   DataProcessor.GetDeviceIds()                 有哪些设备
///   DataProcessor.TryGetValue(id, type, out v)   某设备某类型的最新值
///   DataProcessor.Clear()                        清缓存
/// </summary>
public class DataProcessor : MonoBehaviour
{
    /// <summary>是否在 Console 打印每条读数（调试用，上线可置 false）</summary>
    public static bool LogReadings = true;

    /// <summary>是否已连上中间层（UI 显示在线状态用）</summary>
    public static bool IsConnected { get; private set; }

    private static readonly ConcurrentQueue<string> jsonQueue = new ConcurrentQueue<string>();
    private static readonly Dictionary<long, Reading> latest = new Dictionary<long, Reading>();
    private static readonly Dictionary<long, float> lastArrival = new Dictionary<long, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        var go = new GameObject("DataProcessor");
        go.AddComponent<DataProcessor>();
        DontDestroyOnLoad(go);
    }

    /* ==================== 以下两个由 WebSocketClient 调用，业务代码不需要用 ==================== */

    public static void EnqueueRawMessage(string json)
    {
        if (!string.IsNullOrEmpty(json)) jsonQueue.Enqueue(json);
    }

    public static void SetConnected(bool connected)
    {
        if (IsConnected == connected) return;
        IsConnected = connected;
        Debug.Log("[DataProcessor] 连接状态: " + (connected ? "已连接" : "已断开"));
    }

    /* ==================== 内部：解析与缓存 ==================== */

    void Update()
    {
        while (jsonQueue.TryDequeue(out string json))
        {
            ProcessJson(json);
        }
    }

    private static long SlotKey(int deviceId, int dataType)
    {
        return ((long)deviceId << 32) | (uint)dataType;
    }

    private static void ProcessJson(string json)
    {
        Reading r;
        try
        {
            r = JsonUtility.FromJson<Reading>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("[DataProcessor] JSON 解析失败: " + e.Message + " | " + json);
            return;
        }

        if (r.deviceId <= 0 || !SensorMeta.IsKnown(r.dataType))
        {
            Debug.LogWarning("[DataProcessor] 未知设备号或类型，丢弃: " + json);
            return;
        }

        // 中间层已经带了 key/unit；缺失时用本地表补齐
        if (string.IsNullOrEmpty(r.key))  r.key  = SensorMeta.KeyOf(r.dataType);
        if (string.IsNullOrEmpty(r.unit)) r.unit = SensorMeta.UnitOf(r.dataType);

        long slot = SlotKey(r.deviceId, r.dataType);
        float now = Time.unscaledTime;

        if (LogReadings)
        {
            float gap = -1f;
            float prev;
            if (lastArrival.TryGetValue(slot, out prev)) gap = now - prev;

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            double latency = (nowMs - r.timestamp) / 1000.0;

            string decimals = "F" + SensorMeta.DecimalsOf(r.dataType);
            string gapText = gap >= 0f ? gap.ToString("F1") + "s" : "首次";
            Debug.Log("[DATA] 设备" + r.deviceId
                    + " " + SensorMeta.NameOf(r.dataType)
                    + " " + r.value.ToString(decimals) + r.unit
                    + " | 间隔 " + gapText
                    + " | 延迟 " + latency.ToString("F2") + "s"
                    + " | " + r.source);
        }

        lastArrival[slot] = now;
        latest[slot] = r;
    }

    /* ==================== 对外接口（业务只用这三个） ==================== */

    /// <summary>当前有数据的设备号列表（每次调用会分配一个新 List）</summary>
    public static List<int> GetDeviceIds()
    {
        var ids = new List<int>();
        GetDeviceIds(ids);
        return ids;
    }

    /// <summary>同上，但填进你传入的 List（高频调用用这个，不产生 GC 垃圾）</summary>
    public static void GetDeviceIds(List<int> buffer)
    {
        if (buffer == null) return;

        buffer.Clear();
        foreach (var kv in latest)
        {
            int id = (int)(kv.Key >> 32);
            if (!buffer.Contains(id)) buffer.Add(id);
        }
        buffer.Sort();
    }

    /// <summary>取某设备某传感器类型的最新值；没收到过返回 false</summary>
    public static bool TryGetValue(int deviceId, int dataType, out float value)
    {
        Reading r;
        if (latest.TryGetValue(SlotKey(deviceId, dataType), out r))
        {
            value = r.value;
            return true;
        }

        value = 0f;
        return false;
    }

    /// <summary>清空所有缓存（切场景/调试用）</summary>
    public static void Clear()
    {
        latest.Clear();
        lastArrival.Clear();
    }
}