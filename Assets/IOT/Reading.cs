using System;

/// <summary>
/// 传感器类型序号 —— 与固件 Packet.h 的 READING_* 和中间层 core/reading.js 的 TYPES 完全一致。
/// 0 光照 / 1 温度 / 2 CO2 / 3 湿度 / 4 PM2.5；5-99 预留。
/// 新增类型必须先改这张表，再改固件和中间层，禁止各层自己另起一套编号。
/// </summary>
public enum SensorType
{
    Light = 0,
    Temp  = 1,
    CO2   = 2,
    Humi  = 3,
    PM25  = 4
}

/// <summary>
/// 中间层推送的统一读数（字段名必须和 WebSocket JSON 一致）
/// 例：{"deviceId":1,"dataType":1,"value":25.2,"key":"temp","unit":"°C","source":"WiFi","timestamp":1792909407497}
/// 注意：value 已经是工程值（中间层换算过），Unity 侧不要再做任何缩放。
/// </summary>
[Serializable]
public struct Reading
{
    public int    deviceId;
    public int    dataType;
    public string key;
    public float  value;
    public string unit;
    public string source;
    public long   timestamp;
}

/// <summary>
/// 类型元数据（key / 名称 / 单位 / 小数位）—— 与中间层 TYPES 表保持一致。
/// 前端要显示时用这里的单位和小数位，不要硬编码。
/// </summary>
public static class SensorMeta
{
    public static bool IsKnown(int type) => type >= 0 && type <= 4;

    public static string KeyOf(int type)
    {
        switch (type)
        {
            case 0: return "light";
            case 1: return "temp";
            case 2: return "co2";
            case 3: return "humi";
            case 4: return "pm25";
            default: return "unknown";
        }
    }

    public static string NameOf(int type)
    {
        switch (type)
        {
            case 0: return "光照";
            case 1: return "温度";
            case 2: return "CO2";
            case 3: return "湿度";
            case 4: return "PM2.5";
            default: return "未知";
        }
    }

    public static string UnitOf(int type)
    {
        switch (type)
        {
            case 1: return "°C";
            case 2: return "ppm";
            case 3: return "%RH";
            case 4: return "μg/m3";
            default: return "";
        }
    }

    public static int DecimalsOf(int type)
    {
        switch (type)
        {
            case 1: return 2;   // 温度
            case 3: return 2;   // 湿度
            case 4: return 0;   // PM2.5
            default: return 0;
        }
    }

    public static bool TryParseKey(string key, out int type)
    {
        switch (key)
        {
            case "light": type = 0; return true;
            case "temp":  type = 1; return true;
            case "co2":   type = 2; return true;
            case "humi":  type = 3; return true;
            case "pm25":  type = 4; return true;
            default:      type = -1; return false;
        }
    }
}