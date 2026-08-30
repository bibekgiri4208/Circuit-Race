using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    [Header("Race State")]
    public bool raceStarted = false;
    public bool raceFinished = false;
    public int playerPosition = 0;

    [Header("Countdown UI")]
    public TextMeshProUGUI countdownText;
    public CanvasGroup countdownCanvasGroup;

    [Header("Countdown Timing")]
    public float startDelay = 0.75f;
    public float numberStayTime = 0.55f;
    public float popAnimationTime = 0.25f;
    public float fadeOutTime = 0.2f;
    public float goStayTime = 0.7f;

    [Header("Countdown Scale")]
    public float startScale = 0.4f;
    public float popScale = 1.35f;
    public float normalScale = 1f;

    [Header("Finish Sequence")]
    public float finishTextStayTime = 2f;
    public float orbitDuration = 3f;
    public float orbitHeight = 3f;
    public float orbitDistance = 8f;
    public float orbitSpeed = 100f;
    public float fadeToBlackDuration = 0.8f;

    private CarSpawner carSpawner;
    private ChaseCamera chaseCam;
    private TextMeshProUGUI finishText;
    private CanvasGroup finishTextCanvasGroup;
    private TextMeshProUGUI positionText;
    private CanvasGroup positionTextCanvasGroup;
    private GameObject fadeOverlay;
    private CanvasGroup fadeOverlayCanvasGroup;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        raceStarted = false;
        raceFinished = false;

        carSpawner = FindAnyObjectByType<CarSpawner>();

        if (countdownText == null)
            CreateCountdownUI();

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = "";
        }

        if (countdownCanvasGroup != null)
            countdownCanvasGroup.alpha = 0f;

        StartCoroutine(StartCountdown());
    }

    public void StartFinishSequence()
    {
        if (raceFinished) return;
        raceFinished = true;
        StartCoroutine(FinishRaceSequence());
    }

    void CreateCountdownUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("CountdownCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        GameObject textGO = new GameObject("CountdownText");
        textGO.transform.SetParent(canvas.transform, false);
        RectTransform rect = textGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(400, 200);
        countdownText = textGO.AddComponent<TextMeshProUGUI>();
        countdownText.fontSize = 180;
        countdownText.fontStyle = FontStyles.Bold;
        countdownText.color = new Color(1f, 0.984f, 0f, 1f);
        countdownText.alignment = TextAlignmentOptions.Center;
        countdownText.text = "";

        countdownCanvasGroup = textGO.AddComponent<CanvasGroup>();
        countdownCanvasGroup.alpha = 0f;
    }

    private IEnumerator StartCountdown()
    {
        yield return new WaitForSeconds(startDelay);

        yield return ShowCountdownText("3");
        yield return ShowCountdownText("2");
        yield return ShowCountdownText("1");

        yield return ShowGoText();

        raceStarted = true;

        yield return new WaitForSeconds(goStayTime);

        yield return FadeOutCountdown();

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }

    private IEnumerator ShowCountdownText(string value)
    {
        if (countdownText == null) yield break;
        countdownText.text = value;
        countdownText.transform.localScale = Vector3.one * startScale;

        if (countdownCanvasGroup != null)
            countdownCanvasGroup.alpha = 1f;

        float timer = 0f;

        while (timer < popAnimationTime)
        {
            timer += Time.deltaTime;
            float t = timer / popAnimationTime;

            float scale = Mathf.Lerp(startScale, popScale, t);
            if (countdownText == null) yield break;
            countdownText.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        timer = 0f;

        while (timer < popAnimationTime)
        {
            timer += Time.deltaTime;
            float t = timer / popAnimationTime;

            float scale = Mathf.Lerp(popScale, normalScale, t);
            if (countdownText == null) yield break;
            countdownText.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        yield return new WaitForSeconds(numberStayTime);

        yield return FadeOutCountdown();
    }

    private IEnumerator ShowGoText()
    {
        if (countdownText == null) yield break;
        countdownText.text = "GO!";
        countdownText.transform.localScale = Vector3.one * startScale;

        if (countdownCanvasGroup != null)
            countdownCanvasGroup.alpha = 1f;

        float timer = 0f;

        while (timer < popAnimationTime)
        {
            timer += Time.deltaTime;
            float t = timer / popAnimationTime;

            float scale = Mathf.Lerp(startScale, 1.6f, t);
            if (countdownText == null) yield break;
            countdownText.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        timer = 0f;

        while (timer < popAnimationTime)
        {
            timer += Time.deltaTime;
            float t = timer / popAnimationTime;

            float scale = Mathf.Lerp(1.6f, 1.15f, t);
            if (countdownText == null) yield break;
            countdownText.transform.localScale = Vector3.one * scale;

            yield return null;
        }
    }

    private IEnumerator FadeOutCountdown()
    {
        if (countdownCanvasGroup == null)
            yield break;

        float timer = 0f;
        float startAlpha = countdownCanvasGroup.alpha;

        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            float t = timer / fadeOutTime;

            countdownCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);

            yield return null;
        }

        countdownCanvasGroup.alpha = 0f;
    }

    private IEnumerator FinishRaceSequence()
    {
        GameObject playerCar = carSpawner != null ? carSpawner.SpawnedCar : null;

        if (playerCar == null)
        {
            Debug.LogWarning("No player car found for finish sequence.");
            yield break;
        }

        CarController carController = playerCar.GetComponent<CarController>();
        if (carController != null)
            carController.enabled = false;

        Rigidbody rb = playerCar.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        Camera mainCam = Camera.main;
        if (mainCam != null)
            chaseCam = mainCam.GetComponent<ChaseCamera>();
        if (chaseCam != null)
            chaseCam.enabled = false;

        CreateFinishUI();
        ShowFinishText();

        Vector3 carCenter = playerCar.transform.position + Vector3.up * 0.8f;
        float startAngle = Mathf.Atan2(
            mainCam.transform.position.x - carCenter.x,
            mainCam.transform.position.z - carCenter.z
        ) * Mathf.Rad2Deg;

        float elapsed = 0f;
        while (elapsed < orbitDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / orbitDuration;
            float angle = startAngle + t * orbitSpeed;

            Vector3 offset = new Vector3(
                Mathf.Sin(angle * Mathf.Deg2Rad) * orbitDistance,
                orbitHeight,
                Mathf.Cos(angle * Mathf.Deg2Rad) * orbitDistance
            );

            mainCam.transform.position = carCenter + offset;
            mainCam.transform.LookAt(carCenter + Vector3.up * 0.5f);
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(FadeToBlack());

        LoadingScreen.LoadScene("Garage");
    }

    private void CreateFinishUI()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("FinishCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        // Dark backdrop panel
        GameObject panelGO = new GameObject("Backdrop");
        panelGO.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, 25f);
        panelRect.sizeDelta = new Vector2(700, 200);
        Image panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.7f);

        // Top gold line
        CreateLine(canvas.transform, "TopLine", new Vector2(0f, 115f), new Vector2(600f, 3f), new Color(1f, 0.84f, 0f, 1f));

        // Bottom gold line
        CreateLine(canvas.transform, "BottomLine", new Vector2(0f, -55f), new Vector2(600f, 3f), new Color(1f, 0.84f, 0f, 1f));

        // "RACE FINISHED" text
        GameObject textGO = new GameObject("FinishText");
        textGO.transform.SetParent(canvas.transform, false);
        RectTransform rect = textGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 65f);
        rect.sizeDelta = new Vector2(700, 80);
        finishText = textGO.AddComponent<TextMeshProUGUI>();
        finishText.text = "RACE FINISHED!";
        finishText.fontSize = 68;
        finishText.fontStyle = FontStyles.Bold;
        finishText.color = new Color(1f, 0.84f, 0f, 1f);
        finishText.alignment = TextAlignmentOptions.Center;
        finishText.outlineWidth = 0.2f;
        finishText.outlineColor = new Color(0.6f, 0.3f, 0f, 1f);
        finishText.alpha = 0f;

        finishTextCanvasGroup = textGO.AddComponent<CanvasGroup>();
        finishTextCanvasGroup.alpha = 0f;

        // Position text
        GameObject posGO = new GameObject("PositionText");
        posGO.transform.SetParent(canvas.transform, false);
        RectTransform posRect = posGO.AddComponent<RectTransform>();
        posRect.anchorMin = new Vector2(0.5f, 0.5f);
        posRect.anchorMax = new Vector2(0.5f, 0.5f);
        posRect.anchoredPosition = new Vector2(0f, 10f);
        posRect.sizeDelta = new Vector2(600, 60);
        positionText = posGO.AddComponent<TextMeshProUGUI>();
        positionText.text = "FINISHED 1st!";
        positionText.fontSize = 46;
        positionText.fontStyle = FontStyles.Bold;
        positionText.color = new Color(1f, 1f, 1f, 1f);
        positionText.alignment = TextAlignmentOptions.Center;
        positionText.outlineWidth = 0.15f;
        positionText.outlineColor = new Color(0f, 0f, 0f, 1f);
        positionText.alpha = 0f;

        positionTextCanvasGroup = posGO.AddComponent<CanvasGroup>();
        positionTextCanvasGroup.alpha = 0f;

        // Fade to black overlay
        GameObject fadeGO = new GameObject("FadeOverlay");
        fadeGO.transform.SetParent(canvas.transform, false);
        RectTransform fadeRect = fadeGO.AddComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;
        fadeOverlay = fadeGO;

        Image fadeImg = fadeGO.AddComponent<Image>();
        fadeImg.color = Color.black;

        fadeOverlayCanvasGroup = fadeGO.AddComponent<CanvasGroup>();
        fadeOverlayCanvasGroup.alpha = 0f;
    }

    private void ShowFinishText()
    {
        if (finishText != null)
            finishText.alpha = 1f;
        if (finishTextCanvasGroup != null)
            finishTextCanvasGroup.alpha = 1f;

        if (positionText != null)
        {
            string ordinal = GetOrdinal(playerPosition);
            positionText.text = "FINISHED " + ordinal + "!";
            positionText.alpha = 1f;
        }
        if (positionTextCanvasGroup != null)
            positionTextCanvasGroup.alpha = 1f;
    }

    private string GetOrdinal(int pos)
    {
        switch (pos)
        {
            case 1: return "1st";
            case 2: return "2nd";
            case 3: return "3rd";
            default: return pos + "th";
        }
    }

    private void CreateLine(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject lineGO = new GameObject(name);
        lineGO.transform.SetParent(parent, false);
        RectTransform lineRect = lineGO.AddComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = position;
        lineRect.sizeDelta = size;
        Image lineImg = lineGO.AddComponent<Image>();
        lineImg.color = color;
    }

    private IEnumerator FadeToBlack()
    {
        if (fadeOverlayCanvasGroup == null) yield break;

        float t = 0f;
        while (t < fadeToBlackDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeOverlayCanvasGroup.alpha = Mathf.Clamp01(t / fadeToBlackDuration);
            yield return null;
        }

        fadeOverlayCanvasGroup.alpha = 1f;
    }
}