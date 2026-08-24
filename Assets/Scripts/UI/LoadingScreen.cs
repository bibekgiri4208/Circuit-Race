using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    private GameObject loadingPanel;
    private TextMeshProUGUI loadingText;
    private TextMeshProUGUI progressText;
    private Slider progressBar;
    private float fakeProgress;
    private bool isLoading;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        CreateUI();
    }

    void Update()
    {
        if (!isLoading) return;

        fakeProgress = Mathf.MoveTowards(fakeProgress, 0.9f, Time.unscaledDeltaTime * 0.5f);
        progressBar.value = fakeProgress;
        progressText.text = Mathf.RoundToInt(fakeProgress * 100f) + "%";

        float dots = Mathf.Repeat(Time.unscaledTime * 2f, 4f);
        string dotStr = new string('.', Mathf.FloorToInt(dots));
        loadingText.text = "LOADING" + dotStr;
    }

    void CreateUI()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        // Dark background
        loadingPanel = new GameObject("LoadingPanel");
        loadingPanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = loadingPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelBg = loadingPanel.AddComponent<Image>();
        panelBg.color = new Color(0.05f, 0.05f, 0.05f, 1f);

        // Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(loadingPanel.transform, false);
        RectTransform titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.7f);
        titleRect.anchorMax = new Vector2(0.5f, 0.7f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(600, 80);
        TextMeshProUGUI titleTmp = titleGO.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "CIRCUIT RACE";
        titleTmp.fontSize = 60;
        titleTmp.color = new Color(0.85f, 0.15f, 0.1f, 1f);
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontStyle = FontStyles.Bold;

        // Loading text
        GameObject loadingGO = new GameObject("LoadingText");
        loadingGO.transform.SetParent(loadingPanel.transform, false);
        RectTransform loadingRect = loadingGO.AddComponent<RectTransform>();
        loadingRect.anchorMin = new Vector2(0.5f, 0.45f);
        loadingRect.anchorMax = new Vector2(0.5f, 0.45f);
        loadingRect.anchoredPosition = Vector2.zero;
        loadingRect.sizeDelta = new Vector2(400, 60);
        loadingText = loadingGO.AddComponent<TextMeshProUGUI>();
        loadingText.text = "LOADING...";
        loadingText.fontSize = 40;
        loadingText.color = Color.white;
        loadingText.alignment = TextAlignmentOptions.Center;

        // Progress bar background
        GameObject barBgGO = new GameObject("BarBackground");
        barBgGO.transform.SetParent(loadingPanel.transform, false);
        RectTransform barBgRect = barBgGO.AddComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.5f, 0.35f);
        barBgRect.anchorMax = new Vector2(0.5f, 0.35f);
        barBgRect.anchoredPosition = Vector2.zero;
        barBgRect.sizeDelta = new Vector2(500, 20);
        Image barBgImg = barBgGO.AddComponent<Image>();
        barBgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        // Progress bar fill
        GameObject barFillGO = new GameObject("BarFill");
        barFillGO.transform.SetParent(barBgGO.transform, false);
        RectTransform barFillRect = barFillGO.AddComponent<RectTransform>();
        barFillRect.anchorMin = Vector2.zero;
        barFillRect.anchorMax = Vector2.one;
        barFillRect.offsetMin = Vector2.zero;
        barFillRect.offsetMax = Vector2.zero;
        Image barFillImg = barFillGO.AddComponent<Image>();
        barFillImg.color = new Color(0.85f, 0.15f, 0.1f, 1f);

        // Progress bar slider
        progressBar = barBgGO.AddComponent<Slider>();
        progressBar.fillRect = barFillRect;
        progressBar.minValue = 0f;
        progressBar.maxValue = 1f;
        progressBar.value = 0f;
        progressBar.interactable = false;
        progressBar.transition = Selectable.Transition.None;

        // Progress percentage text
        GameObject progressGO = new GameObject("ProgressText");
        progressGO.transform.SetParent(loadingPanel.transform, false);
        RectTransform progressRect = progressGO.AddComponent<RectTransform>();
        progressRect.anchorMin = new Vector2(0.5f, 0.28f);
        progressRect.anchorMax = new Vector2(0.5f, 0.28f);
        progressRect.anchoredPosition = Vector2.zero;
        progressRect.sizeDelta = new Vector2(200, 40);
        progressText = progressGO.AddComponent<TextMeshProUGUI>();
        progressText.text = "0%";
        progressText.fontSize = 28;
        progressText.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        progressText.alignment = TextAlignmentOptions.Center;

        loadingPanel.SetActive(false);
    }

    public void LoadScene(string sceneName)
    {
        if (isLoading) return;
        isLoading = true;
        fakeProgress = 0f;
        progressBar.value = 0f;
        progressText.text = "0%";
        loadingPanel.SetActive(true);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        StartCoroutine(LoadSceneAsync(sceneName));
    }

    System.Collections.IEnumerator LoadSceneAsync(string sceneName)
    {
        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        asyncOp.allowSceneActivation = false;

        while (asyncOp.progress < 0.9f)
        {
            float realProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);
            fakeProgress = Mathf.Lerp(fakeProgress, realProgress, Time.unscaledDeltaTime * 2f);
            progressBar.value = fakeProgress;
            progressText.text = Mathf.RoundToInt(fakeProgress * 100f) + "%";
            yield return null;
        }

        fakeProgress = 1f;
        progressBar.value = 1f;
        progressText.text = "100%";
        yield return new WaitForSecondsRealtime(0.3f);

        asyncOp.allowSceneActivation = true;

        yield return new WaitUntil(() => asyncOp.isDone);

        loadingPanel.SetActive(false);
        isLoading = false;
    }
}
