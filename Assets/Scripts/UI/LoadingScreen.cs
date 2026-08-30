using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private Slider progressBar;

    [Header("Load Settings")]
    [SerializeField] private float minimumLoadTime = 5f;

    private static string targetScene;
    private float fakeProgress;
    private bool isLoading;

    private const string LoadingSceneName = "Loading Screen";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(targetScene))
        {
            if (loadingPanel != null)
                loadingPanel.SetActive(true);

            StartCoroutine(LoadSceneAsync(targetScene));
        }
    }

    void Update()
    {
        if (!isLoading) return;

        float dots = Mathf.Repeat(Time.unscaledTime * 2f, 4f);
        string dotStr = new string('.', Mathf.FloorToInt(dots));
        loadingText.text = "LOADING" + dotStr;
    }

    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        targetScene = sceneName;
        SceneManager.LoadScene(LoadingSceneName);
    }

    System.Collections.IEnumerator LoadSceneAsync(string sceneName)
    {
        isLoading = true;
        fakeProgress = 0f;
        progressBar.value = 0f;
        progressText.text = "0%";
        Time.timeScale = 1f;
        AudioListener.pause = false;

        AsyncOperation asyncOp = SceneManager.LoadSceneAsync(sceneName);
        asyncOp.allowSceneActivation = false;

        while (asyncOp.progress < 0.9f)
        {
            yield return null;
        }

        float elapsedTime = 0f;

        while (elapsedTime < minimumLoadTime)
        {
            elapsedTime += Time.unscaledDeltaTime;
            fakeProgress = Mathf.Clamp01(elapsedTime / minimumLoadTime);
            progressBar.value = fakeProgress;
            progressText.text = Mathf.RoundToInt(fakeProgress * 100f) + "%";
            yield return null;
        }

        fakeProgress = 1f;
        progressBar.value = 1f;
        progressText.text = "100%";
        yield return new WaitForSecondsRealtime(0.3f);

        asyncOp.allowSceneActivation = true;
    }
}
