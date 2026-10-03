using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RaceUIBinder : MonoBehaviour
{
    public TextMeshProUGUI lapText;
    public TextMeshProUGUI checkpointText;

    public static void EnsureRaceHUD(Transform parent)
    {
        if (FindAnyObjectByType<RaceUIBinder>() != null) return;

        GameObject canvasObject = new GameObject("RaceHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(parent, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("RaceProgress", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
        rect.anchoredPosition = new Vector2(-30f, -30f);
        rect.sizeDelta = new Vector2(550f, 120f);
        Image background = panel.GetComponent<Image>();
        background.color = new Color(0.025f, 0.05f, 0.08f, 0.85f);
        background.raycastTarget = false;

        RaceUIBinder binder = canvasObject.AddComponent<RaceUIBinder>();
        binder.lapText = CreateHUDText(panel.transform, "Lap", new Vector2(0f, 27f), 30f);
        binder.lapText.text = "GET READY";
        binder.checkpointText = CreateHUDText(panel.transform, "Time", new Vector2(0f, -25f), 22f);
        binder.checkpointText.color = new Color(0.2f, 0.8f, 1f);
        binder.checkpointText.text = "WAITING FOR GO";
    }

    private static TextMeshProUGUI CreateHUDText(Transform parent, string name, Vector2 position, float fontSize)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)textObject.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(510f, 46f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void Start()
    {
        StartCoroutine(BindUIToPlayer());
    }

    private System.Collections.IEnumerator BindUIToPlayer()
    {
        CarSpawner spawner = FindAnyObjectByType<CarSpawner>();
        PlayerLapTracker tracker = null;
        while (tracker == null)
        {
            if (RaceManager.Instance != null) tracker = RaceManager.Instance.PlayerTracker;
            if (tracker == null && spawner != null && spawner.SpawnedCar != null)
                tracker = spawner.SpawnedCar.GetComponent<PlayerLapTracker>();
            if (tracker == null) yield return null;
        }

        tracker.lapText = lapText;
        tracker.checkpointText = checkpointText;
        if (checkpointText != null)
        {
            checkpointText.fontSizeMax = checkpointText.fontSize;
            checkpointText.fontSizeMin = Mathf.Min(16f, checkpointText.fontSize);
            checkpointText.enableAutoSizing = true;
        }
        tracker.RefreshUI();
    }

    void Update()
    {
        RaceManager race = RaceManager.Instance;
        if (race == null || race.PlayerTracker == null || race.raceFinished) return;
        if (lapText != null)
            lapText.text = "LAP " + race.PlayerTracker.currentLap + " / " + race.PlayerTracker.totalLaps;
        if (checkpointText != null)
        {
            if (!string.IsNullOrEmpty(race.RaceMessage)) checkpointText.text = race.RaceMessage;
            else if (!race.raceStarted) checkpointText.text = "WAITING FOR GO";
            else checkpointText.text = "TIME " + RaceResultsUI.FormatTime(race.RaceTime) +
                (race.PlayerTracker.totalCheckpoints > 0 ? "   |   CP " + race.PlayerTracker.nextCheckpointIndex +
                    " / " + race.PlayerTracker.totalCheckpoints : "");
        }
    }
}
