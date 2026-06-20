using System.Collections;
using UnityEngine;
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

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        raceStarted = false;
        raceFinished = false;

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = "";
        }

        if (countdownCanvasGroup != null)
            countdownCanvasGroup.alpha = 0f;

        StartCoroutine(StartCountdown());
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
}