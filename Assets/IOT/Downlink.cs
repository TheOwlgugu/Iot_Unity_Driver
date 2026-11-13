using System;
using UnityEngine;

/// <summary>中间层返回的下行回执</summary>
[Serializable]
public struct DownlinkAck
{
    public string kind;
    public string requestId;
    public bool   ok;
    public string route;     // "wifi" / "lora"
    public string error;     // ok=false 时才有
}

/// <summary>WebSocket 报文信封，用来区分"读数"和"下行 ack"</summary>
[Serializable]
public struct WsEnvelope
{
    public string kind;
}

/// <summary>
/// 下行控制：只负责把 (设备号, 接口, 指令) 发给中间层。
/// 中间层负责路由到 WiFi/LoRa，设备执行结果不做确认（方案 A）。
///
/// 业务代码只用最下面的两个接口：
///   Downlink.SendCode(deviceId, port, code)
///   Downlink.OnAck += a => ...
/// </summary>
public static class Downlink
{
    private static int nextRequestId = 1;

    /* ==================== 内部实现（业务代码不需要看） ==================== */

    /// <summary>由 WebSocketClient 调用，业务代码不需要用</summary>
    public static void HandleAck(string json)
    {
        DownlinkAck ack;
        try
        {
            ack = JsonUtility.FromJson<DownlinkAck>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("[Downlink] ack 解析失败: " + e.Message + " | " + json);
            return;
        }

        if (ack.ok)
            Debug.Log("[Downlink] ack ok route=" + ack.route + " id=" + ack.requestId);
        else
            Debug.LogWarning("[Downlink] ack 失败: " + ack.error + " id=" + ack.requestId);

        if (OnAck != null) OnAck(ack);
    }

    /* ==================== 对外接口（业务只用这两个） ==================== */

    /// <summary>中间层回执（已路由 / 设备离线 / 参数错误）</summary>
    public static event Action<DownlinkAck> OnAck;

    /// <summary>
    /// 发送一条控制命令
    /// </summary>
    /// <param name="deviceId">设备号 1-999</param>
    /// <param name="port">接口 1-3（1=DATA 2=I2C 3=UART2）</param>
    /// <param name="code">指令 0-99999，语义由执行器决定</param>
    /// <returns>true = 已交给中间层（不代表设备已执行）</returns>
    public static bool SendCode(int deviceId, int port, int code)
    {
        if (deviceId < 1 || deviceId > 999)
        {
            Debug.LogWarning("[Downlink] deviceId 必须是 1-999");
            return false;
        }
        if (port < 1 || port > 3)
        {
            Debug.LogWarning("[Downlink] port 必须是 1-3");
            return false;
        }
        if (code < 0 || code > 99999)
        {
            Debug.LogWarning("[Downlink] code 必须是 0-99999");
            return false;
        }

        int id = nextRequestId++;
        string json = "{\"type\":\"cmd\",\"requestId\":\"" + id + "\""
                    + ",\"deviceId\":" + deviceId
                    + ",\"port\":" + port
                    + ",\"code\":" + code + "}";

        if (!WebSocketClient.SendText(json))
        {
            Debug.LogWarning("[Downlink] 未连接中间层，命令未发送");
            return false;
        }

        Debug.Log("[Downlink] 已发送 设备" + deviceId + " 接口" + port + " 指令" + code.ToString("D5"));
        return true;
    }
}
