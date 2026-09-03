using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class PauseMenu : MonoBehaviour
{
    private GameObject pauseButtonObj;
    private GameObject pausePanel;
    private bool isPaused;
    private bool cursorLocked;

    void Start()
    {
        CreateUI();
        Time.timeScale = 1f;
        LockCursor();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isPaused)
                Resume();
            else
                Pause();
        }

        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            if (cursorLocked)
                UnlockCursor();
            else
                LockCursor();
        }
    }

    void CreateUI()
    {
        GameObject canvasGO = new GameObject("PauseCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // --- Pause Button (top-left) ---
        pauseButtonObj = CreateButton(canvas.transform, "PauseBtn", "||",
            new Vector2(0, 1), new Vector2(0, 1), new Vector2(50, -50),
            new Vector2(60, 60), 32, new Color(0f, 0f, 0f, 0.5f));
        pauseButtonObj.GetComponent<Button>().onClick.AddListener(Pause);

        // --- Pause Panel (dark overlay + menu) ---
        pausePanel = new GameObject("PausePanel");
        pausePanel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = pausePanel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image panelBg = pausePanel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.7f);
        pausePanel.SetActive(false);

        // Menu box
        GameObject menuBox = CreatePanel(pausePanel.transform, "MenuBox",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0),
            new Vector2(400, 350), new Color(0.12f, 0.12f, 0.12f, 0.95f));

        // Title
        CreateText(menuBox.transform, "Title", "PAUSED",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30),
            new Vector2(300, 60), 48, Color.white, TextAlignmentOptions.Center);

        // Resume button
        GameObject resumeBtn = CreateMenuButton(menuBox.transform, "ResumeBtn", "RESUME",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 50));
        resumeBtn.GetComponent<Button>().onClick.AddListener(Resume);

        // Restart button
        GameObject restartBtn = CreateMenuButton(menuBox.transform, "RestartBtn", "RESTART",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20));
        restartBtn.GetComponent<Button>().onClick.AddListener(Restart);

        // Quit button
        GameObject quitBtn = CreateMenuButton(menuBox.transform, "QuitBtn", "QUIT",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90));
        quitBtn.GetComponent<Button>().onClick.AddListener(Quit);
    }

    public void Pause()
    {
        isPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        pausePanel.SetActive(true);
        pauseButtonObj.SetActive(false);
        UnlockCursor();
    }

    public void Resume()
    {
        isPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        pausePanel.SetActive(false);
        pauseButtonObj.SetActive(true);
        LockCursor();
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        LoadingScreen.LoadScene(SceneManager.GetActiveScene().name);
    }

    public     void Quit()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        LoadingScreen.LoadScene("Garage");
    }

    void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorLocked = true;
    }

    void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorLocked = false;
    }

    GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, float fontSize, Color bgColor)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);
        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        Image img = btnGO.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(bgColor.r, bgColor.g, bgColor.b, 0.7f);
        cb.pressedColor = new Color(bgColor.r, bgColor.g, bgColor.b, 0.4f);
        btn.colors = cb;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return btnGO;
    }

    GameObject CreatePanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos,
        Vector2 size, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    void CreateText(Transform parent, string name, string content,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos,
        Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.fontStyle = FontStyles.Bold;
    }

    GameObject CreateMenuButton(Transform parent, string name, string label,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos)
    {
        GameObject btnGO = new GameObject(name);
        btnGO.transform.SetParent(parent, false);
        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(260, 50);

        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.85f, 0.15f, 0.1f, 1f);

        Button btn = btnGO.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.85f, 0.15f, 0.1f, 1f);
        cb.highlightedColor = new Color(1f, 0.25f, 0.2f, 1f);
        cb.pressedColor = new Color(0.65f, 0.1f, 0.08f, 1f);
        btn.colors = cb;

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 30;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return btnGO;
    }
}
