using TMPro;
using UnityEngine;

/// <summary>
/// 示例业务：显示某设备的温度。接口见 DataProcessor。
/// </summary>
public class Controler : MonoBehaviour
{
    [SerializeField] private int deviceId = 1;
    [SerializeField] private TextMeshProUGUI statusText;

    void Update()
    {
        if (statusText == null) return;

        int type = (int)SensorType.Temp;
        float temp;

        if (DataProcessor.TryGetValue(deviceId, type, out temp))
        {
            statusText.text = "ID " + deviceId.ToString("D2") + "\nTMP: "
                            + temp.ToString("F" + SensorMeta.DecimalsOf(type))
                            + SensorMeta.UnitOf(type);
        }
        else
        {
            statusText.text = "ID " + deviceId.ToString("D2") + "\nTMP: --";
        }
    }
}