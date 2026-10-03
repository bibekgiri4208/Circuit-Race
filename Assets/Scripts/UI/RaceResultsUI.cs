using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class RaceResultsUI : MonoBehaviour
{
    private static readonly Color Accent = new Color(0.2f, 0.8f, 1f);
    private Canvas canvas;
    private GameObject banner;
    private CanvasGroup resultsGroup;
    private MenuNavigation navigation;
    private string raceScene;
    private bool ready;
    private bool leaving;

    public void ShowFinishBanner(RaceManager race)
    {
        raceScene = SceneManager.GetActiveScene().name;
        GameObject canvasObject = new GameObject("ResultsCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        banner = CreateRect(canvas.transform, "FinishBanner", new Vector2(0f, 300f), new Vector2(900f, 100f)).gameObject;
        Image background = banner.AddComponent<Image>();
        background.color = new Color(0.02f, 0.04f, 0.08f, 0.85f);
        background.raycastTarget = false;
        CreateText(banner.transform, "Finish", "FINISH  |  " + Ordinal(race.playerPosition).ToUpperInvariant() +
            " / " + Mathf.Max(1, race.RacerCount), Vector2.zero, new Vector2(860f, 85f), 48f, Accent);
    }

    public IEnumerator ShowResults(RaceManager race)
    {
        if (canvas == null) ShowFinishBanner(race);
        if (banner != null) banner.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        GameObject overlay = new GameObject("ResultsOverlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        overlay.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)overlay.transform);
        overlay.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.035f, 0.82f);
        resultsGroup = overlay.GetComponent<CanvasGroup>();
        resultsGroup.alpha = 0f;
        resultsGroup.interactable = false;
        resultsGroup.blocksRaycasts = false;

        List<PlayerLapTracker> standings = race.GetStandings();
        float height = Mathf.Max(760f, 610f + standings.Count * 44f);
        RectTransform card = CreateRect(overlay.transform, "ResultsCard", Vector2.zero, new Vector2(1100f, height));
        if (height > 960f) card.localScale = Vector3.one * (960f / height);
        card.gameObject.AddComponent<Image>().color = new Color(0.035f, 0.06f, 0.095f, 0.98f);
        float top = height * 0.5f;
        CreateImage(card, "Accent", new Vector2(0f, top - 3f), new Vector2(1100f, 6f), Accent);
        string title = race.RacerCount > 1 && race.playerPosition == 1 ? "VICTORY" : "RACE COMPLETE";
        CreateText(card, "Title", title, new Vector2(0f, top - 64f), new Vector2(1000f, 70f), 52f, Color.white);
        CreateText(card, "Track", raceScene.ToUpperInvariant() + "  /  " + race.totalLaps + " LAPS" +
            (race.IsNewBestTime ? "  /  NEW PERSONAL BEST" : ""), new Vector2(0f, top - 108f),
            new Vector2(1000f, 36f), 20f, race.IsNewBestTime ? new Color(1f, 0.82f, 0.3f) : Accent);
        CreateText(card, "Position", Ordinal(race.playerPosition).ToUpperInvariant() + "  /  " + Mathf.Max(1, race.RacerCount),
            new Vector2(0f, top - 170f), new Vector2(1000f, 72f), 56f, Accent);

        PlayerLapTracker player = race.PlayerTracker;
        CreateStat(card, -330f, top, "TOTAL TIME", FormatTime(player != null ? player.FinishTime : race.RaceTime));
        CreateStat(card, 0f, top, "BEST LAP", FormatTime(player != null ? player.BestLapTime : 0f));
        float personalBest = race.IsNewBestTime && player != null ? player.FinishTime : race.PreviousBestTime;
        CreateStat(card, 330f, top, "TRACK BEST", FormatTime(personalBest));
        CreateImage(card, "Divider", new Vector2(0f, top - 308f), new Vector2(1000f, 2f), new Color(1f, 1f, 1f, 0.15f));
        Color headerColor = new Color(0.6f, 0.7f, 0.8f);
        CreateText(card, "PositionHeader", "POS", new Vector2(-450f, top - 332f), new Vector2(80f, 32f), 17f, headerColor);
        CreateText(card, "DriverHeader", "DRIVER", new Vector2(-100f, top - 332f), new Vector2(580f, 32f), 17f,
            headerColor, TextAlignmentOptions.MidlineLeft);
        CreateText(card, "TimeHeader", "TIME / STATUS", new Vector2(345f, top - 332f), new Vector2(280f, 32f), 17f,
            headerColor, TextAlignmentOptions.MidlineRight);

        for (int i = 0; i < standings.Count; i++)
        {
            PlayerLapTracker racer = standings[i];
            RectTransform row = CreateRect(card, "Standing_" + i, new Vector2(0f, top - 374f - i * 44f), new Vector2(1000f, 40f));
            CreateImage(row, "Background", Vector2.zero, new Vector2(1000f, 40f),
                racer.IsPlayer ? new Color(0.05f, 0.4f, 0.6f, 0.65f) : new Color(1f, 1f, 1f, 0.035f));
            CreateText(row, "Rank", (i + 1).ToString("00"), new Vector2(-450f, 0f), new Vector2(80f, 40f), 24f, Accent);
            string carName = racer.name.Replace("(Clone)", "").Trim() + (racer.IsPlayer ? "  [YOU]" : "");
            CreateText(row, "Driver", carName, new Vector2(-100f, 0f), new Vector2(580f, 40f), 22f, Color.white, TextAlignmentOptions.MidlineLeft);
            string status = racer.RaceCompleted ? FormatTime(racer.FinishTime) : "ON TRACK - LAP " + racer.currentLap;
            CreateText(row, "Status", status, new Vector2(345f, 0f), new Vector2(280f, 40f), 20f,
                racer.RaceCompleted ? Color.white : new Color(0.6f, 0.7f, 0.8f), TextAlignmentOptions.MidlineRight);
        }

        List<string> laps = new List<string>();
        if (player != null)
        {
            for (int i = 0; i < player.LapTimes.Count; i++)
                laps.Add("L" + (i + 1) + "  " + FormatTime(player.LapTimes[i]));
        }
        CreateText(card, "LapSplits", string.Join("    |    ", laps), new Vector2(0f, -top + 166f),
            new Vector2(1000f, 42f), 18f, new Color(0.65f, 0.75f, 0.85f));

        Button retry = CreateButton(card, "Retry", "RACE AGAIN", new Vector2(-235f, -top + 94f));
        Button garage = CreateButton(card, "Garage", "BACK TO GARAGE", new Vector2(235f, -top + 94f));
        retry.onClick.AddListener(() => LeaveResults(raceScene));
        garage.onClick.AddListener(() => LeaveResults("Garage"));
        LinkButtons(retry, garage);
        LinkButtons(garage, retry);
        CreateText(card, "Controls", "D-PAD / STICK: SELECT    A / ENTER: CONFIRM    B / ESC: GARAGE",
            new Vector2(0f, -top + 34f), new Vector2(1000f, 30f), 16f, new Color(0.6f, 0.7f, 0.8f));

        navigation = overlay.AddComponent<MenuNavigation>();
        navigation.Initialize(overlay, retry);
        navigation.enabled = false;
        Vector3 finalScale = card.localScale;
        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.35f);
            resultsGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            card.localScale = finalScale * Mathf.Lerp(0.94f, 1f, 1f - Mathf.Pow(1f - t, 3f));
            yield return null;
        }
        card.localScale = finalScale;
        resultsGroup.alpha = 1f;
        resultsGroup.interactable = resultsGroup.blocksRaycasts = true;
        navigation.enabled = true;
        navigation.Focus(retry);
        ready = true;
    }

    void Update()
    {
        if (!ready || leaving) return;
        if ((Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame))
            LeaveResults("Garage");
    }

    void LeaveResults(string scene)
    {
        if (!ready || leaving) return;
        leaving = true;
        navigation.enabled = false;
        resultsGroup.interactable = false;
        StartCoroutine(FadeAndLoad(scene));
    }

    IEnumerator FadeAndLoad(string scene)
    {
        RectTransform fade = CreateRect(canvas.transform, "FadeToBlack", Vector2.zero, Vector2.zero);
        Stretch(fade);
        Image image = fade.gameObject.AddComponent<Image>();
        float elapsed = 0f;
        while (elapsed < 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            image.color = new Color(0f, 0f, 0f, Mathf.Clamp01(elapsed / 0.5f));
            yield return null;
        }
        Time.timeScale = 1f;
        AudioListener.pause = false;
        LoadingScreen.LoadScene(scene);
    }

    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) return "--:--.---";
        int milliseconds = Mathf.RoundToInt(seconds * 1000f);
        return (milliseconds / 60000).ToString("00") + ":" + (milliseconds / 1000 % 60).ToString("00") +
            "." + (milliseconds % 1000).ToString("000");
    }

    public static string Ordinal(int position)
    {
        position = Mathf.Max(1, position);
        int lastTwo = position % 100;
        if (lastTwo >= 11 && lastTwo <= 13) return position + "th";
        switch (position % 10)
        {
            case 1: return position + "st";
            case 2: return position + "nd";
            case 3: return position + "rd";
            default: return position + "th";
        }
    }

    static void CreateStat(Transform parent, float x, float top, string label, string value)
    {
        CreateText(parent, label + "Label", label, new Vector2(x, top - 232f), new Vector2(300f, 30f), 17f, new Color(0.6f, 0.7f, 0.8f));
        CreateText(parent, label + "Value", value, new Vector2(x, top - 270f), new Vector2(300f, 48f), 32f, Color.white);
    }

    static Button CreateButton(Transform parent, string name, string label, Vector2 position)
    {
        RectTransform rect = CreateRect(parent, name, position, new Vector2(410f, 58f));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.07f, 0.42f, 0.64f);
        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = colors.selectedColor = new Color(0.6f, 0.95f, 1f);
        colors.pressedColor = new Color(0.35f, 0.65f, 0.8f);
        button.colors = colors;
        CreateText(rect, name + "Label", label, Vector2.zero, new Vector2(400f, 52f), 24f, Color.white);
        return button;
    }

    static void LinkButtons(Button button, Button other)
    {
        Navigation links = button.navigation;
        links.mode = Navigation.Mode.Explicit;
        links.selectOnLeft = links.selectOnRight = links.selectOnUp = links.selectOnDown = other;
        button.navigation = links;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, string content, Vector2 position, Vector2 size,
        float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        RectTransform rect = CreateRect(parent, name, position, size);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        return text;
    }

    static void CreateImage(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        Image image = CreateRect(parent, name, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
    }

    static RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
