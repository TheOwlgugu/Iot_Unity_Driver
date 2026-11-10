using System;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// DataBase 自测：按键查询最新/在线状态/历史，结果全部走 Debug.Log。
/// 挂到场景里任意 GameObject 上即可。
/// </summary>
public class DataBaseTester : MonoBehaviour
{
    [Header("设备")]
    [SerializeField] private int deviceId = 3;
    [SerializeField] private int dataType = 1;

    [Header("最新条数")]
    [SerializeField] private int latestLimit = 10;

    [Header("历史时间（yyyy-MM-dd HH:mm:ss）")]
    [SerializeField] private string historyStart = "2026-11-01 00:00:00";
    [SerializeField] private string historyEnd = "2026-11-30 23:59:59";

    [Header("按键")]
    [SerializeField] private KeyCode latestKey = KeyCode.A;
    [SerializeField] private KeyCode statusKey = KeyCode.S;
    [SerializeField] private KeyCode historyKey = KeyCode.D;

    private bool busy;

    void Update()
    {
        if (busy) return;

        if (Input.GetKeyDown(latestKey))
        {
            _ = Run("GetLatest", TestLatest);
        }
        else if (Input.GetKeyDown(statusKey))
        {
            _ = Run("GetStatus", TestStatus);
        }
        else if (Input.GetKeyDown(historyKey))
        {
            _ = Run("GetHistory", TestHistory);
        }
    }

    private async Task Run(string name, Func<Task> test)
    {
        busy = true;
        try
        {
            await test();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DataBaseTester] {name} 失败: {e.Message}");
        }
        finally
        {
            busy = false;
        }
    }

    private async Task TestLatest()
    {
        Debug.Log($"[DataBaseTester] GetLatest device={deviceId} type={dataType} limit={latestLimit}");

        DBdata[] rows = await DataBase.GetLatest(deviceId, dataType, latestLimit);
        PrintRows(rows, "最新");
    }

    private async Task TestStatus()
    {
        bool online = await DataBase.GetStatus(deviceId);
        Debug.Log($"[DataBaseTester] 设备 {deviceId} 状态: {(online ? "在线" : "离线")}");
    }

    private async Task TestHistory()
    {
        Debug.Log($"[DataBaseTester] GetHistory device={deviceId} type={dataType} {historyStart} ~ {historyEnd}");

        DBdata[] rows = await DataBase.GetHistory(deviceId, dataType, historyStart, historyEnd);
        PrintRows(rows, "历史");

        if (DataBase.LastHistoryTruncated)
            Debug.LogWarning("[DataBaseTester] 历史超过 5000 行，已截断");
    }

    private void PrintRows(DBdata[] rows, string label)
    {
        if (rows == null || rows.Length == 0)
        {
            Debug.Log($"[DataBaseTester] {label}: 空");
            return;
        }

        Debug.Log($"[DataBaseTester] {label}: {rows.Length} 条");
        foreach (DBdata r in rows)
        {
            Debug.Log($"  id={r.id} dev={r.deviceId} type={r.dataType} " +
                      $"value={r.value:0.00} time={r.LocalTime:yyyy-MM-dd HH:mm:ss}");
        }
    }
}
