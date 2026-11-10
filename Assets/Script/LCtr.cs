using TMPro;
using UnityEngine;

/// <summary>
/// 示例业务：用光照值控制补光灯（0=暗，1=亮）。
/// 这只是演示，业务逻辑由前端开发改；接口见 DataProcessor。
/// </summary>
public class LCtr : MonoBehaviour
{
    [SerializeField] private int deviceId = 1;
    [SerializeField] private Light pointLight;
    [SerializeField] private TextMeshProUGUI statusText;

    void Update()
    {
        if (statusText == null) return;

        float light;
        if (DataProcessor.TryGetValue(deviceId, (int)SensorType.Light, out light))
        {
            if (pointLight != null) pointLight.enabled = (light == 0f);   // 暗的时候开灯
            statusText.text = "Light: " + (light == 0f ? "On" : "Off");
        }
        else
        {
            statusText.text = "Light: --";
        }
    }
}