using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public class GarageMenuController : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private RectTransform safeAreaRoot;
    [SerializeField] private Button skyboxButton;
    [SerializeField] private Button rainButton;
    [SerializeField] private Button soundButton;
    [SerializeField] private Button localRaceButton;

    [Header("Existing garage environment")]
    [SerializeField] private Material[] skyboxes;
    [SerializeField] private GameObject rain;
    [SerializeField] private AudioSource rainAudio;
    [SerializeField] private AudioSource music;

    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private int skyboxIndex;
    private bool muted;

    void Start()
    {
        ApplySafeArea();
        if (skyboxButton != null) skyboxButton.onClick.AddListener(CycleSkybox);
        if (rainButton != null) rainButton.onClick.AddListener(ToggleRain);
        if (soundButton != null) soundButton.onClick.AddListener(ToggleSound);

        // Do not silently launch single-player from a multiplayer placeholder.
        if (localRaceButton != null) localRaceButton.interactable = false;

        if (safeAreaRoot != null)
        {
            foreach (Button button in safeAreaRoot.GetComponentsInChildren<Button>(true))
            {
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f, 1f, 0.88f);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = new Color(0.76f, 0.86f, 0.65f);
                colors.disabledColor = new Color(0.72f, 0.76f, 0.64f, 0.9f);
                button.colors = colors;
            }
        }

        if (skyboxes != null)
            for (int i = 0; i < skyboxes.Length; i++)
                if (skyboxes[i] == RenderSettings.skybox) skyboxIndex = i;
    }

    void Update()
    {
        if (Screen.safeArea != lastSafeArea || lastScreenSize.x != Screen.width || lastScreenSize.y != Screen.height)
            ApplySafeArea();
    }

    private void ApplySafeArea()
    {
        if (safeAreaRoot == null || Screen.width == 0 || Screen.height == 0) return;
        lastSafeArea = Screen.safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        safeAreaRoot.anchorMin = new Vector2(lastSafeArea.xMin / Screen.width, lastSafeArea.yMin / Screen.height);
        safeAreaRoot.anchorMax = new Vector2(lastSafeArea.xMax / Screen.width, lastSafeArea.yMax / Screen.height);
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
    }

    private void CycleSkybox()
    {
        if (skyboxes == null || skyboxes.Length == 0) return;
        skyboxIndex = (skyboxIndex + 1) % skyboxes.Length;
        if (skyboxes[skyboxIndex] == null) return;
        RenderSettings.skybox = skyboxes[skyboxIndex];
        DynamicGI.UpdateEnvironment();
    }

    private void ToggleRain()
    {
        if (rain == null) return;
        bool active = !rain.activeSelf;
        rain.SetActive(active);
        if (rainAudio == null) return;
        rainAudio.gameObject.SetActive(active);
        rainAudio.mute = muted;
        if (active) rainAudio.Play();
        else rainAudio.Stop();
    }

    private void ToggleSound()
    {
        muted = !muted;
        if (music != null) music.mute = muted;
        if (rainAudio != null) rainAudio.mute = muted;
        if (soundButton != null && soundButton.targetGraphic is GarageButtonGraphic graphic)
            graphic.SetMuted(muted);
    }

    void OnDestroy()
    {
        if (skyboxButton != null) skyboxButton.onClick.RemoveListener(CycleSkybox);
        if (rainButton != null) rainButton.onClick.RemoveListener(ToggleRain);
        if (soundButton != null) soundButton.onClick.RemoveListener(ToggleSound);
    }
}
