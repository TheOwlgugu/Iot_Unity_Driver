using TMPro;
using UnityEngine;

public class LCtr : MonoBehaviour
{
    [SerializeField] private int deviceId = 3;
    [SerializeField] private Light pointLight;
    [SerializeField] private TextMeshProUGUI statusText;

    void Update()
    {
        if (statusText == null) return;
        float light;
        float tmp;
        float humi;
        float co2;
        float pm25;

        if (DataProcessor.TryGetValue(deviceId, (int)SensorType.Temp, out tmp))
        {
            DataProcessor.TryGetValue(deviceId, (int)SensorType.Humi, out humi);
            DataProcessor.TryGetValue(deviceId, (int)SensorType.CO2, out co2);
            DataProcessor.TryGetValue(deviceId, (int)SensorType.PM25, out pm25);
            DataProcessor.TryGetValue(deviceId, (int)SensorType.Light, out light);
            pointLight.enabled = ( light >= 0.5f ? true : false );
 
            statusText.text = $"TEMP: {tmp}\n" +
                              $"HUMI: {humi}\n" +
                              $"CO2:  {co2}\n" +
                              $"PM2.5:{pm25}\n";
        }
        else
        {
            statusText.text = " --";
        }
    }
}