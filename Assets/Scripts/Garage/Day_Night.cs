using UnityEngine;

public class SkyboxToggle : MonoBehaviour
{
    [Header("Skybox Materials")]
    [SerializeField] private Material daySkybox;
    [SerializeField] private Material nightSkybox;

    [Header("Fog Colors (Built-in)")]
    [SerializeField] private Color dayFogColor = new Color(0.5f, 0.6f, 0.7f);
    [SerializeField] private Color nightFogColor = new Color(0.05f, 0.05f, 0.1f);

    [Header("Optional Directional Light")]
    [SerializeField] private Light sunLight;

    private bool isNight = false;

    void Start()
    {
        // Set initial day states
        RenderSettings.skybox = daySkybox;
        RenderSettings.fogColor = dayFogColor;
        DynamicGI.UpdateEnvironment();
    }

    public void ToggleDayNight()
    {
        isNight = !isNight;

        if (isNight)
        {
            RenderSettings.skybox = nightSkybox;
            RenderSettings.fogColor = nightFogColor; // Updates standard fog color
            if (sunLight != null) sunLight.gameObject.SetActive(false);
        }
        else
        {
            RenderSettings.skybox = daySkybox;
            RenderSettings.fogColor = dayFogColor; // Updates standard fog color
            if (sunLight != null) sunLight.gameObject.SetActive(true);
        }

        DynamicGI.UpdateEnvironment();
    }
}