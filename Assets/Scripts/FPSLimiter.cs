using UnityEngine;

public class FPSLimiter : MonoBehaviour
{
    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 180;
    }
}