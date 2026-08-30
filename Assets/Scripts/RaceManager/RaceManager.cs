using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[System.Serializable]
public class CountdownCameraAngle
{
    public string label;
    public Vector3 positionOffset;
    public Vector3 lookAtOffset;
    public float fov;
}

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    [Header("Race State")]
    public bool raceStarted = false;
    public bool raceFinished = false;
    public int playerPosition = 0;

    [Header("Countdown Timing")]
    public float startDelay = 0.75f;
    public float numberStayTime = 0.5f;
    public float numberAnimTime = 0.2f;
    public float fadeOutTime = 0.15f;
    public float goStayTime = 0.6f;
    public float cameraTransitionTime = 0.6f;

    [Header("Countdown Text (scene objects)")]
    public GameObject countdownParent;
    public TextMeshPro textThree;
    public TextMeshPro textTwo;
    public TextMeshPro textOne;
    public TextMeshPro textGo;

    [Header("Countdown Colors")]
    public Color colorThree = new Color(1f, 0.15f, 0.15f, 1f);
    public Color colorTwo = new Color(1f, 0.35f, 0.05f, 1f);
    public Color colorOne = new Color(1f, 0.6f, 0f, 1f);
    public Color colorGo = new Color(0f, 1f, 0.3f, 1f);

    [Header("Camera Angles (relative to car)")]
    public CountdownCameraAngle angleThree = new CountdownCameraAngle
    {
        label = "Rear Left Quarter",
        positionOffset = new Vector3(-4f, 2f, -6f),
        lookAtOffset = new Vector3(0f, 1f, 0f),
        fov = 45f
    };
    public CountdownCameraAngle angleTwo = new CountdownCameraAngle
    {
        label = "Front Low",
        positionOffset = new Vector3(0f, 1f, 6f),
        lookAtOffset = new Vector3(0f, 0.8f, 0f),
        fov = 40f
    };
    public CountdownCameraAngle angleOne = new CountdownCameraAngle
    {
        label = "Side Close-Up",
        positionOffset = new Vector3(5f, 2.5f, 0f),
        lookAtOffset = new Vector3(0f, 1f, 0f),
        fov = 35f
    };
    public CountdownCameraAngle angleGo = new CountdownCameraAngle
    {
        label = "Rear Chase",
        positionOffset = new Vector3(0f, 2.5f, -6f),
        lookAtOffset = new Vector3(0f, 1f, 2f),
        fov = 50f
    };

    [Header("Finish Sequence")]
    public float finishTextStayTime = 2f;
    public float orbitDuration = 3f;
    public float orbitHeight = 3f;
    public float orbitDistance = 8f;
    public float orbitSpeed = 100f;
    public float fadeToBlackDuration = 0.8f;

    private CarSpawner carSpawner;
    private ChaseCamera chaseCam;
    private Camera mainCam;
    private TextMeshProUGUI finishText;
    private CanvasGroup finishTextCanvasGroup;
    private TextMeshProUGUI positionText;
    private CanvasGroup positionTextCanvasGroup;
    private GameObject fadeOverlay;
    private CanvasGroup fadeOverlayCanvasGroup;

    private AnimationCurve punchCurve;
    private Vector3[] originalScales = new Vector3[4];

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        raceStarted = false;
        raceFinished = false;

        carSpawner = FindAnyObjectByType<CarSpawner>();
        mainCam = Camera.main;

        if (mainCam != null)
        {
            chaseCam = mainCam.GetComponent<ChaseCamera>();
            if (chaseCam != null)
                chaseCam.holdPosition = true;
        }

        punchCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 2.5f),
            new Keyframe(0.15f, 1.2f, 0f, 0f),
            new Keyframe(0.3f, 0.95f, 0f, 0f),
            new Keyframe(0.5f, 1.05f, 0f, 0f),
            new Keyframe(0.7f, 0.98f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f)
        );
        punchCurve.preWrapMode = WrapMode.ClampForever;
        punchCurve.postWrapMode = WrapMode.ClampForever;

        DisableCountdownObjects();
        StoreOriginalScales();

        StartCoroutine(StartCountdown());
    }

    public void StartFinishSequence()
    {
        if (raceFinished) return;
        raceFinished = true;
        StartCoroutine(FinishRaceSequence());
    }

    private Transform GetCarTransform()
    {
        if (carSpawner != null && carSpawner.SpawnedCar != null)
            return carSpawner.SpawnedCar.transform;
        return null;
    }

    private void DisableCountdownObjects()
    {
        if (countdownParent != null)
            countdownParent.SetActive(false);

        if (textThree != null) textThree.gameObject.SetActive(false);
        if (textTwo != null) textTwo.gameObject.SetActive(false);
        if (textOne != null) textOne.gameObject.SetActive(false);
        if (textGo != null) textGo.gameObject.SetActive(false);
    }

    private void StoreOriginalScales()
    {
        if (textThree != null) originalScales[0] = textThree.transform.localScale;
        if (textTwo != null) originalScales[1] = textTwo.transform.localScale;
        if (textOne != null) originalScales[2] = textOne.transform.localScale;
        if (textGo != null) originalScales[3] = textGo.transform.localScale;
    }

    private TextMeshPro GetTMPForValue(string value)
    {
        switch (value)
        {
            case "3": return textThree;
            case "2": return textTwo;
            case "1": return textOne;
            default: return null;
        }
    }

    private IEnumerator TransitionCamera(Transform carTransform, CountdownCameraAngle angle)
    {
        if (mainCam == null || carTransform == null) yield break;

        Vector3 carPos = carTransform.position;
        Quaternion carRot = carTransform.rotation;

        Vector3 targetPos = carPos + carRot * angle.positionOffset;
        Vector3 lookTarget = carPos + carRot * angle.lookAtOffset;
        Quaternion targetRot = Quaternion.LookRotation(lookTarget - targetPos, Vector3.up);
        float targetFov = angle.fov;

        Vector3 startPos = mainCam.transform.position;
        Quaternion startRot = mainCam.transform.rotation;
        float startFov = mainCam.fieldOfView;

        float timer = 0f;
        while (timer < cameraTransitionTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / cameraTransitionTime);
            float smooth = t * t * (3f - 2f * t);

            if (mainCam == null) yield break;
            mainCam.transform.position = Vector3.Lerp(startPos, targetPos, smooth);
            mainCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, smooth);
            mainCam.fieldOfView = Mathf.Lerp(startFov, targetFov, smooth);

            yield return null;
        }

        if (mainCam != null)
        {
            mainCam.transform.position = targetPos;
            mainCam.transform.rotation = targetRot;
            mainCam.fieldOfView = targetFov;
        }
    }

    private IEnumerator StartCountdown()
    {
        yield return new WaitForSeconds(startDelay);

        Transform car = GetCarTransform();
        if (car == null)
        {
            Debug.LogWarning("RaceManager: No car found for countdown.");
            yield break;
        }

        if (countdownParent != null)
            countdownParent.SetActive(true);

        yield return ShowCountdownNumber("3", colorThree, angleThree, car);
        yield return ShowCountdownNumber("2", colorTwo, angleTwo, car);
        yield return ShowCountdownNumber("1", colorOne, angleOne, car);

        yield return ShowGoText(angleGo, car);

        raceStarted = true;

        if (chaseCam != null)
            chaseCam.holdPosition = false;

        yield return new WaitForSeconds(goStayTime);
        yield return FadeOutCountdown3D();

        if (countdownParent != null)
            countdownParent.SetActive(false);
    }

    private IEnumerator ShowCountdownNumber(string value, Color color, CountdownCameraAngle angle, Transform car)
    {
        TextMeshPro tmp = GetTMPForValue(value);
        if (tmp == null || mainCam == null) yield break;

        StartCoroutine(TransitionCamera(car, angle));

        int index = value == "3" ? 0 : value == "2" ? 1 : 2;
        Vector3 origScale = originalScales[index];

        tmp.gameObject.SetActive(true);
        tmp.color = color;
        tmp.transform.localScale = origScale * 3f;

        float timer = 0f;
        while (timer < numberAnimTime)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / numberAnimTime);
            float curveValue = punchCurve.Evaluate(t);
            float scale = Mathf.Lerp(3f, 1f, curveValue);

            if (tmp == null) yield break;
            tmp.transform.localScale = origScale * scale;

            if (tmp != null)
            {
                float alpha = Mathf.Lerp(0f, 1f, Mathf.Clamp01(t * 3f));
                tmp.color = new Color(color.r, color.g, color.b, alpha);
            }

            yield return null;
        }

        if (tmp != null)
            tmp.transform.localScale = origScale;

        yield return new WaitForSeconds(numberStayTime);

        yield return FadeOutSingle(tmp, color);

        if (tmp != null)
            tmp.gameObject.SetActive(false);
    }

    private IEnumerator ShowGoText(CountdownCameraAngle angle, Transform car)
    {
        if (textGo == null || mainCam == null) yield break;

        StartCoroutine(TransitionCamera(car, angle));

        Vector3 origScale = originalScales[3];

        textGo.gameObject.SetActive(true);
        textGo.color = colorGo;
        textGo.transform.localScale = origScale * 3.5f;

        float timer = 0f;
        float animDuration = numberAnimTime * 1.5f;
        while (timer < animDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / animDuration);
            float curveValue = punchCurve.Evaluate(t);
            float scale = Mathf.Lerp(3.5f, 1f, curveValue);

            if (textGo == null) yield break;
            textGo.transform.localScale = origScale * scale;

            if (textGo != null)
            {
                float alpha = Mathf.Lerp(0f, 1f, Mathf.Clamp01(t * 4f));
                textGo.color = new Color(colorGo.r, colorGo.g, colorGo.b, alpha);
            }

            yield return null;
        }

        if (textGo != null)
            textGo.transform.localScale = origScale;

        float pulseTimer = 0f;
        while (pulseTimer < 0.25f)
        {
            pulseTimer += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(pulseTimer * Mathf.PI * 5f) * 0.06f;
            if (textGo == null) yield break;
            textGo.transform.localScale = origScale * pulse;
            yield return null;
        }

        if (textGo != null)
            textGo.transform.localScale = origScale;
    }

    private IEnumerator FadeOutSingle(TextMeshPro tmp, Color baseColor)
    {
        if (tmp == null) yield break;

        float timer = 0f;
        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            float t = timer / fadeOutTime;

            if (tmp == null) yield break;
            float alpha = Mathf.Lerp(1f, 0f, t);
            tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            yield return null;
        }

        if (tmp != null)
            tmp.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
    }

    private IEnumerator FadeOutCountdown3D()
    {
        float timer = 0f;
        TextMeshPro[] temps = { textThree, textTwo, textOne, textGo };
        Color[] colors = { colorThree, colorTwo, colorOne, colorGo };
        float[] startAlphas = new float[4];

        for (int i = 0; i < 4; i++)
        {
            if (temps[i] != null)
                startAlphas[i] = temps[i].color.a;
        }

        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            float t = timer / fadeOutTime;

            for (int i = 0; i < 4; i++)
            {
                if (temps[i] != null && temps[i].gameObject.activeSelf)
                {
                    float alpha = Mathf.Lerp(startAlphas[i], 0f, t);
                    temps[i].color = new Color(colors[i].r, colors[i].g, colors[i].b, alpha);
                }
            }

            yield return null;
        }

        for (int i = 0; i < 4; i++)
        {
            if (temps[i] != null)
            {
                temps[i].color = new Color(colors[i].r, colors[i].g, colors[i].b, 0f);
                temps[i].gameObject.SetActive(false);
            }
        }
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

        Camera cam = Camera.main;
        if (cam != null)
            chaseCam = cam.GetComponent<ChaseCamera>();
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