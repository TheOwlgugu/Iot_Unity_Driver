using UnityEngine;

/// <summary>
/// 下行自测：按按键发一条控制命令，方便验证 Unity → 中间层 → 设备。
/// 挂到场景里任意 GameObject 上即可。
/// </summary>
public class DownlinkTester : MonoBehaviour
{
    [Header("要发送的命令")]
    [SerializeField] private int deviceId = 1;
    [SerializeField] private int port = 1;
    [SerializeField] private int code = 1;

    [Header("触发按键")]
    [SerializeField] private KeyCode key = KeyCode.B;

    void Update()
    {
        if (Input.GetKeyDown(key))
        {
            Debug.Log("[DownlinkTester] 按键触发: 设备" + deviceId + " 接口" + port + " 指令" + code);
            Downlink.SendCode(deviceId, port, code);
        }
    }
}