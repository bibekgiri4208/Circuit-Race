using UnityEngine;
using TMPro;

public class CarHUD : MonoBehaviour
{
    public CarController car;

    public TMP_Text speedText;
    public TMP_Text gearText;
    public TMP_Text rpmText;

    void Update()
    {
        if (car == null)
            return;

        if (speedText != null)
            speedText.text = car.SpeedKmh.ToString("0") + " km/h";

        if (gearText != null)
            gearText.text = "Gear " + car.CurrentGear;

        if (rpmText != null)
            rpmText.text = car.EngineRPM.ToString("0") + " RPM";
    }
}