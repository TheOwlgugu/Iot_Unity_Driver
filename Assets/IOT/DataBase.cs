using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 一条数据库读数。time 是协议里的 Unix 毫秒，LocalTime 给 UI 直接显示。
/// </summary>
[Serializable]
public class DBdata
{
    public long   id;
    public int    deviceId;
    public int    dataType;
    public double value;
    public long   time;

    public DateTime LocalTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(time).ToLocalTime().DateTime;
}

[Serializable]
public class QueryResult
{
    public string   kind;
    public string   requestId;
    public bool     ok;
    public string   error;
    public DBdata[] items;
    public bool     online;
    public bool     truncated;
}

/// <summary>
/// 数据库查询驱动：只负责通过 WebSocket 和中间层通信。
/// 查询名和参数由中间层白名单定义，客户端不能发 SQL。
///
/// 业务代码只用最下面的三个方法：
///   DataBase.GetLatest(deviceId, dataType, limit)
///   DataBase.GetStatus(deviceId)
///   DataBase.GetHistory(deviceId, dataType, start, end)
/// </summary>
public static class DataBase
{
    private static readonly Dictionary<string, TaskCompletionSource<QueryResult>> pending =
        new Dictionary<string, TaskCompletionSource<QueryResult>>();

    /* ==================== 内部实现（业务代码不需要看） ==================== */

    /// <summary>由 WebSocketClient 调用，业务代码不需要用</summary>
    public static void HandleResult(string json)
    {
        QueryResult res;
        try
        {
            res = JsonUtility.FromJson<QueryResult>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DataBase] 解析 queryResult 失败: " + e.Message);
            return;
        }

        if (res == null || string.IsNullOrEmpty(res.requestId)) return;

        if (pending.TryGetValue(res.requestId, out var tcs))
        {
            pending.Remove(res.requestId);
            tcs.TrySetResult(res);
        }
    }

    private static async Task<QueryResult> Send(string name, string paramsJson, int timeoutMs = 5000)
    {
        if (!WebSocketClient.IsOpen)
            throw new InvalidOperationException("WebSocket 未连接");

        string id = Guid.NewGuid().ToString("N");
        var tcs = new TaskCompletionSource<QueryResult>();
        pending[id] = tcs;

        string json =
            $"{{\"kind\":\"query\",\"requestId\":\"{id}\",\"name\":\"{name}\",\"params\":{paramsJson}}}";

        if (!WebSocketClient.SendText(json))
        {
            pending.Remove(id);
            throw new InvalidOperationException("查询发送失败");
        }

        Task done = await Task.WhenAny(tcs.Task, Task.Delay(timeoutMs));
        if (done != tcs.Task)
        {
            pending.Remove(id);
            throw new TimeoutException("查询超时: " + name);
        }

        QueryResult res = await tcs.Task;
        if (!res.ok)
            throw new Exception(string.IsNullOrEmpty(res.error) ? "查询失败" : res.error);

        return res;
    }

    private static async Task<DBdata[]> SendItems(string name, string paramsJson,
                                                   Action<QueryResult> onResult = null)
    {
        QueryResult res = await Send(name, paramsJson);
        onResult?.Invoke(res);
        return res.items ?? Array.Empty<DBdata>();
    }

    private static Task<DBdata[]> GetHistoryByMs(int deviceId, int dataType, long startMs, long endMs)
    {
        string p = $"{{\"deviceId\":{deviceId},\"dataType\":{dataType},\"start\":{startMs},\"end\":{endMs}}}";

        return SendItems("history", p, res => LastHistoryTruncated = res.truncated);
    }

    private static long ToMs(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Unspecified)
            dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);

        return new DateTimeOffset(dt).ToUnixTimeMilliseconds();
    }

    private static DateTime ParseTime(string s)
    {
        return DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
    }

    /* ==================== 对外接口（业务只用这三个） ==================== */

    /// <summary>最近一次 GetHistory 是否被 5000 行上限截断</summary>
    public static bool LastHistoryTruncated { get; private set; }

    /// <summary>最新 N 条（按时间倒序）</summary>
    public static Task<DBdata[]> GetLatest(int deviceId, int dataType, int limit)
    {
        string p = $"{{\"deviceId\":{deviceId},\"dataType\":{dataType},\"limit\":{limit}}}";
        return SendItems("latest", p);
    }

    /// <summary>true=在线；false=离线或没有状态记录</summary>
    public static async Task<bool> GetStatus(int deviceId)
    {
        string p = $"{{\"deviceId\":{deviceId}}}";
        QueryResult res = await Send("status", p);
        return res.ok && res.online;
    }

    /// <summary>时间范围内（升序），最多 5000 行</summary>
    public static Task<DBdata[]> GetHistory(int deviceId, int dataType, DateTime start, DateTime end)
    {
        return GetHistoryByMs(deviceId, dataType, ToMs(start), ToMs(end));
    }

    /// <summary>时间范围（字符串格式 yyyy-MM-dd HH:mm:ss）</summary>
    public static Task<DBdata[]> GetHistory(int deviceId, int dataType, string start, string end)
    {
        return GetHistoryByMs(deviceId, dataType, ToMs(ParseTime(start)), ToMs(ParseTime(end)));
    }
}
