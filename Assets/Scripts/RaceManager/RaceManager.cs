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
    public float cinematicDuration = 4f;
    public float cinematicOrbitHeight = 2.5f;
    public float cinematicOrbitDistance = 7f;
    public float cinematicOrbitSpeed = 40f;
    public float cinematicLookHeight = 1.2f;

    private CarSpawner carSpawner;
    private ChaseCamera chaseCam;
    private TextMeshProUGUI finishText;
    private CanvasGroup finishTextCanvasGroup;

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
            countdownText.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        timer = 0f;

        while (timer < popAnimationTime)
        {
            timer += Time.deltaTime;
            float t = timer / popAnimationTime;

            float scale = Mathf.Lerp(popScale, normalScale, t);
            countdownText.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        yield return new WaitForSeconds(numberStayTime);

        yield return FadeOutCountdown();
    }

    private IEnumerator ShowGoText()
    {
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
            countdownText.transform.localScale = Vector3.one * scale;

            yield return null;
        }

        timer = 0f;

        while (timer < popAnimationTime)
        {
            timer += Time.deltaTime;
            float t = timer / popAnimationTime;

            float scale = Mathf.Lerp(1.6f, 1.15f, t);
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

        // Stop the car
        CarController carController = playerCar.GetComponent<CarController>();
        if (carController != null)
            carController.enabled = false;

        Rigidbody rb = playerCar.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Disable chase camera
        Camera mainCam = Camera.main;
        if (mainCam != null)
            chaseCam = mainCam.GetComponent<ChaseCamera>();
        if (chaseCam != null)
            chaseCam.enabled = false;

        // Show "RACE FINISHED" text
        CreateFinishText();

        float textTimer = 0f;
        while (textTimer < finishTextStayTime)
        {
            textTimer += Time.deltaTime;
            float t = Mathf.Clamp01(textTimer / 0.5f);
            if (finishTextCanvasGroup != null)
                finishTextCanvasGroup.alpha = t;
            yield return null;
        }

        // Cinematic camera orbit
        if (mainCam != null && playerCar != null)
        {
            float elapsed = 0f;
            Vector3 carCenter = playerCar.transform.position + Vector3.up * 0.5f;

            while (elapsed < cinematicDuration)
            {
                elapsed += Time.deltaTime;
                float angle = (elapsed / cinematicDuration) * 360f * (cinematicOrbitSpeed / 360f);

                Vector3 offset = new Vector3(
                    Mathf.Sin(angle * Mathf.Deg2Rad) * cinematicOrbitDistance,
                    cinematicOrbitHeight,
                    Mathf.Cos(angle * Mathf.Deg2Rad) * cinematicOrbitDistance
                );

                mainCam.transform.position = carCenter + offset;
                mainCam.transform.LookAt(carCenter + Vector3.up * cinematicLookHeight);

                yield return null;
            }
        }

        // Fade out finish text
        if (finishTextCanvasGroup != null)
        {
            float fadeTimer = 0f;
            while (fadeTimer < 0.5f)
            {
                fadeTimer += Time.deltaTime;
                finishTextCanvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeTimer / 0.5f);
                yield return null;
            }
        }

        // Return to garage
        if (LoadingScreen.Instance != null)
            LoadingScreen.Instance.LoadScene("Garage");
        else
            SceneManager.LoadScene("Garage");
    }

    private void CreateFinishText()
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

        GameObject textGO = new GameObject("FinishText");
        textGO.transform.SetParent(canvas.transform, false);
        RectTransform rect = textGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(800, 150);
        finishText = textGO.AddComponent<TextMeshProUGUI>();
        finishText.text = "RACE FINISHED!";
        finishText.fontSize = 90;
        finishText.fontStyle = FontStyles.Bold;
        finishText.color = new Color(1f, 0.84f, 0f, 1f);
        finishText.alignment = TextAlignmentOptions.Center;
        finishText.enableAutoSizing = false;

        // Add outline for readability
        finishText.outlineWidth = 0.15f;
        finishText.outlineColor = new Color(0f, 0f, 0f, 1f);

        finishTextCanvasGroup = textGO.AddComponent<CanvasGroup>();
        finishTextCanvasGroup.alpha = 0f;
    }
}